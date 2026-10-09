// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace WinWeaver
{
	// One shared UI clock (vsync-paced on Windows, 10 ms timer elsewhere) drives every animation in the program (time-based runs and hover tweens).
	internal static class Anim
	{
		private class Run
		{
			public Control Owner;
			public object Key;
			public double Start;
			public int Ms;
			public Action<float> Step;
			public Action Done;
		}

		public static bool Enabled = Environment.GetEnvironmentVariable("WW_NOANIM") != "1" && !Prefs.NoAnim;

		private static readonly List<Run> _runs = new List<Run>();

		private static readonly List<Tween> _tweens = new List<Tween>();

		private static Timer _timer;

		// Monotonic high-resolution clock (QPC) for all animation timing. DateTime.UtcNow only advances in
		// ~15.6 ms steps on many PCs, so with a 10 ms timer some frames saw dt = 0 and the next one a double
		// step — that uneven motion was the visible micro-stutter in scrolling, hovers and progress bars.
		private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

		private static readonly double _tickMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

		public static double Now => _clock.ElapsedTicks * _tickMs;

		// per-frame callbacks (smooth scrolling); return false to unsubscribe
		private static readonly List<Func<bool>> _frames = new List<Func<bool>>();

		public static void Frame(Func<bool> f)
		{
			if (!_frames.Contains(f))
			{
				_frames.Add(f);
			}
			Ensure();
		}

		// --- vsync pacing (Windows with DWM): a background thread waits for the next composition (DwmFlush)
		// and posts one frame to the UI thread, so animations step exactly once per monitor refresh
		// (60/120/144/240 Hz) instead of drifting against it with a 10 ms timer.
		[System.Runtime.InteropServices.DllImport("dwmapi.dll")]
		private static extern int DwmFlush();

		[System.Runtime.InteropServices.DllImport("dwmapi.dll")]
		private static extern int DwmIsCompositionEnabled(out bool enabled);

		private static System.Threading.SynchronizationContext _ui;

		private static System.Threading.Thread _vsync;

		private static readonly System.Threading.AutoResetEvent _wake = new System.Threading.AutoResetEvent(false);

		private static volatile bool _vsActive;

		private static int _vsPending;

		private static bool _vsBroken;

		private static bool VsyncAvailable()
		{
			// opt-in only (WW_VSYNC=1): on some PCs DwmFlush returned early or late and frames came at ~30 fps,
			// which made scrolling feel sluggish. The default is the 10 ms high-resolution timer.
			if (_vsBroken || !Win.IsWindows || Environment.GetEnvironmentVariable("WW_VSYNC") != "1")
			{
				return false;
			}
			try
			{
				bool on;
				return DwmIsCompositionEnabled(out on) == 0 && on;
			}
			catch
			{
				_vsBroken = true;
				return false;
			}
		}

		private static bool StartVsync()
		{
			System.Threading.SynchronizationContext ctx = System.Threading.SynchronizationContext.Current;
			if (!(ctx is WindowsFormsSynchronizationContext) || !VsyncAvailable())
			{
				return false;
			}
			_ui = ctx;
			if (_vsync == null)
			{
				_vsync = new System.Threading.Thread(VsyncLoop) { IsBackground = true, Name = "WW vsync", Priority = System.Threading.ThreadPriority.AboveNormal };
				_vsync.Start();
			}
			_vsActive = true;
			_wake.Set();
			return true;
		}

		private static void VsyncLoop()
		{
			double last = Now;
			while (true)
			{
				if (!_vsActive)
				{
					_wake.WaitOne();
					last = Now;
					continue;
				}
				try
				{
					if (DwmFlush() != 0)
					{
						System.Threading.Thread.Sleep(8);
					}
				}
				catch
				{
					_vsBroken = true;
					_vsActive = false;
					PostFallback();
					continue;
				}
				// DwmFlush returns at once when the window is minimized / composition paused: don't spin
				double now = Now;
				if (now - last < 3.0)
				{
					System.Threading.Thread.Sleep(6);
					now = Now;
				}
				last = now;
				// at most one frame in flight: a slow frame is skipped instead of queueing up behind input
				if (System.Threading.Interlocked.CompareExchange(ref _vsPending, 1, 0) == 0)
				{
					try
					{
						_ui.Post(VsyncFrame, null);
					}
					catch
					{
						_vsPending = 0;
					}
				}
			}
		}

		private static void PostFallback()
		{
			try
			{
				_ui?.Post(delegate
				{
					Ensure();
				}, null);
			}
			catch
			{
			}
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr rgn, uint flags);

		private static void VsyncFrame(object state)
		{
			_vsPending = 0;
			if (!_vsActive)
			{
				return;
			}
			Tick(null, EventArgs.Empty);
			// paint what this frame invalidated right away (RDW_UPDATENOW | RDW_ALLCHILDREN): posted frames
			// outrank WM_PAINT in the message queue, so on a slow frame the repaint could otherwise lag a frame behind
			try
			{
				foreach (Form f in Application.OpenForms)
				{
					if (f.IsHandleCreated && f.Visible && f.WindowState != FormWindowState.Minimized)
					{
						RedrawWindow(f.Handle, IntPtr.Zero, IntPtr.Zero, 0x0100 | 0x0080);
					}
				}
			}
			catch
			{
			}
		}

		private static bool Busy => _runs.Count > 0 || _tweens.Count > 0 || _frames.Count > 0;

		private static void Stop()
		{
			_vsActive = false;
			_timer?.Stop();
			HiRes(false);
		}

		[System.Runtime.InteropServices.DllImport("winmm.dll")]
		private static extern uint timeBeginPeriod(uint p);

		[System.Runtime.InteropServices.DllImport("winmm.dll")]
		private static extern uint timeEndPeriod(uint p);

		private static bool _hiRes;

		private static void HiRes(bool on)
		{
			if (on == _hiRes)
			{
				return;
			}
			_hiRes = on;
			try
			{
				if (on)
				{
					timeBeginPeriod(1);
				}
				else
				{
					timeEndPeriod(1);
				}
			}
			catch
			{
			}
		}

		private static void Ensure()
		{
			if (_vsActive)
			{
				return;
			}
			if (StartVsync())
			{
				return;
			}
			if (_timer == null)
			{
				_timer = new Timer { Interval = 10 };
				_timer.Tick += Tick;
			}
			if (!_timer.Enabled)
			{
				HiRes(true);
				_timer.Start();
			}
		}

		public static float EaseOut(float t)
		{
			t = Clamp(t);
			float u = 1f - t;
			return 1f - u * u * u;
		}

		public static float EaseInOut(float t)
		{
			t = Clamp(t);
			return (t < 0.5f) ? (4f * t * t * t) : (1f - (float)Math.Pow(-2f * t + 2f, 3.0) / 2f);
		}

		public static float Clamp(float t)
		{
			return (t < 0f) ? 0f : ((t > 1f) ? 1f : t);
		}

		// Runs step(t) with t going 0..1 over ms; an older run with the same key is cancelled.
		public static void Start(Control owner, int ms, Action<float> step, Action done = null, object key = null)
		{
			if (key != null)
			{
				_runs.RemoveAll((Run r) => r.Key != null && r.Key.Equals(key));
			}
			if (!Enabled || ms <= 0)
			{
				step(1f);
				done?.Invoke();
				return;
			}
			_runs.Add(new Run { Owner = owner, Key = key, Start = Now, Ms = ms, Step = step, Done = done });
			step(0f);
			Ensure();
		}

		public static void Cancel(object key)
		{
			_runs.RemoveAll((Run r) => r.Key != null && r.Key.Equals(key));
		}

		internal static void Watch(Tween t)
		{
			if (!_tweens.Contains(t))
			{
				_tweens.Add(t);
			}
			Ensure();
		}

		private static void Tick(object sender, EventArgs e)
		{
			long lt0 = Lag.T0();
			try
			{
				TickCore();
			}
			finally
			{
				Lag.Done("Анимации (кадр)", lt0);
			}
		}

		private static void TickCore()
		{
			double now = Now;
			Run[] runs = _runs.ToArray();
			foreach (Run r in runs)
			{
				if (r.Owner != null && r.Owner.IsDisposed)
				{
					_runs.Remove(r);
					continue;
				}
				float t = (float)((now - r.Start) / (double)r.Ms);
				if (t >= 1f)
				{
					_runs.Remove(r);
					try
					{
						r.Step(1f);
						r.Done?.Invoke();
					}
					catch
					{
					}
				}
				else
				{
					try
					{
						r.Step(t);
					}
					catch
					{
						_runs.Remove(r);
					}
				}
			}
			Tween[] tweens = _tweens.ToArray();
			foreach (Tween tw in tweens)
			{
				if (!tw.Advance())
				{
					_tweens.Remove(tw);
				}
			}
			if (_frames.Count > 0)
			{
				Func<bool>[] fr = _frames.ToArray();
				foreach (Func<bool> f in fr)
				{
					bool more;
					try
					{
						more = f();
					}
					catch
					{
						more = false;
					}
					if (!more)
					{
						_frames.Remove(f);
					}
				}
			}
			if (!Busy)
			{
				Stop();
			}
		}
	}

	// A value that eases toward its target (hover glow, sliding indicators) and repaints its control.
	// Time-based: the same speed at any frame rate. Timed(ms) switches to a fixed-duration ease-out (counters, bars, rings).
	internal class Tween
	{
		private readonly Control _c;

		private readonly float _speed;

		private readonly Action _repaint;

		private float _target;

		private float _from;

		private double _t0;

		private double _last;

		private bool _running;

		private int _ms;

		public float Value;

		public Tween(Control c, float start = 0f, float speed = 0.2f, Action repaint = null)
		{
			_c = c;
			Value = start;
			_target = start;
			_speed = speed;
			_repaint = repaint;
		}

		public Tween Timed(int ms)
		{
			_ms = ms;
			return this;
		}

		public bool Moving => _running;

		private void Repaint()
		{
			if (_repaint != null)
			{
				_repaint();
			}
			else
			{
				_c?.Invalidate();
			}
		}

		public float Target
		{
			get
			{
				return _target;
			}
			set
			{
				if (Math.Abs(value - _target) < 0.0001f && Math.Abs(Value - value) < 0.0001f)
				{
					return;
				}
				if (Math.Abs(value - _target) < 0.0001f && _running)
				{
					return;
				}
				_target = value;
				if (!Anim.Enabled)
				{
					Value = value;
					Repaint();
					return;
				}
				double now = Anim.Now;
				_from = Value;
				_t0 = now;
				if (!_running)
				{
					_last = now;
				}
				_running = true;
				Anim.Watch(this);
			}
		}

		public void Snap(float v)
		{
			_target = v;
			Value = v;
			_from = v;
			_running = false;
		}

		internal bool Advance()
		{
			if (_c != null && _c.IsDisposed)
			{
				_running = false;
				return false;
			}
			// snapped (or finished) while still in the shared timer list: a timed tween would otherwise keep
			// easing from its old start value and overwrite the snapped one
			if (!_running)
			{
				return false;
			}
			double now = Anim.Now;
			if (_ms > 0)
			{
				float t = (float)((now - _t0) / _ms);
				if (t >= 1f)
				{
					Value = _target;
					_running = false;
					Repaint();
					return false;
				}
				Value = _from + (_target - _from) * Anim.EaseOut(t);
				Repaint();
				return true;
			}
			double dt = Math.Min(100.0, Math.Max(0.5, now - _last));
			_last = now;
			float d = _target - Value;
			float eps = Math.Max(0.002f, Math.Abs(_target) * 0.001f);
			if (Math.Abs(d) <= eps)
			{
				Value = _target;
				_running = false;
				Repaint();
				return false;
			}
			float k = 1f - (float)Math.Pow(1.0 - _speed, dt / 16.667);
			Value += d * k;
			Repaint();
			return true;
		}
	}

	// Cross-fade + slide between two states of a container: snapshot before, apply the change, snapshot after, animate.
	internal static class Transition
	{
		private class Overlay : Control
		{
			public Bitmap A;

			public Bitmap B;

			public float T;

			public int Dx;

			public int Dy;

			public Overlay()
			{
				SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, value: true);
			}

			// moment (0..1) the new picture starts to appear: the cut, or later when its snapshot came late
			public float BAt = Cut;

			public const float Cut = 0.3f;

			// the page switch itself, done a few frames into the fade (or at once when the overlay is dropped)
			public Action Pending;

			protected override void OnPaint(PaintEventArgs e)
			{
				Graphics g = e.Graphics;
				g.Clear(Theme.Bg);
				// strictly one picture at a time: the old one is gone before the new one starts to appear,
				// so the same labels of two pages/tabs are never seen doubled on top of each other
				if (T < Cut || B == null)
				{
					float ka = 1f - Anim.EaseOut(Math.Min(1f, T / Cut));
					if (A != null && ka > 0.01f)
					{
						if (ka >= 0.999f)
						{
							g.DrawImageUnscaled(A, 0, 0);
							return;
						}
						using (ImageAttributes iaA = new ImageAttributes())
						{
							iaA.SetColorMatrix(new ColorMatrix { Matrix33 = ka });
							g.DrawImage(A, new Rectangle(0, 0, A.Width, A.Height), 0, 0, A.Width, A.Height, GraphicsUnit.Pixel, iaA);
						}
					}
					return;
				}
				float k = Anim.EaseOut(Math.Max(0f, (T - BAt) / Math.Max(0.05f, 1f - BAt)));
				if (k >= 0.999f)
				{
					g.DrawImageUnscaled(B, 0, 0);
					return;
				}
				int ox = (int)((1f - k) * (float)Dx);
				int oy = (int)((1f - k) * (float)Dy);
				using (ImageAttributes ia = new ImageAttributes())
				{
					ia.SetColorMatrix(new ColorMatrix { Matrix33 = k });
					g.DrawImage(B, new Rectangle(ox, oy, B.Width, B.Height), 0, 0, B.Width, B.Height, GraphicsUnit.Pixel, ia);
				}
			}
		}

		// Windows: a separate click-through layered window over the switching area. It is composited by the
		// system, so it stays smooth even while the UI thread is busy building the new page under it,
		// and the new page is revealed by fading the window out — no second snapshot is needed.
		private class Veil : Form
		{
			public Bitmap A;

			public float Ka = 1f;

			public Action Pending;

			public Veil()
			{
				FormBorderStyle = FormBorderStyle.None;
				ShowInTaskbar = false;
				StartPosition = FormStartPosition.Manual;
				BackColor = Theme.Bg;
				SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, value: true);
			}

			protected override bool ShowWithoutActivation => true;

			protected override CreateParams CreateParams
			{
				get
				{
					CreateParams cp = base.CreateParams;
					// transparent for the mouse + no activation + not in Alt-Tab (layered comes from Opacity < 1)
					cp.ExStyle |= 0x20 | 0x8000000 | 0x80;
					return cp;
				}
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				Graphics g = e.Graphics;
				g.Clear(Theme.Bg);
				if (A == null || Ka <= 0.01f)
				{
					return;
				}
				if (Ka >= 0.999f)
				{
					g.DrawImageUnscaled(A, 0, 0);
					return;
				}
				using (ImageAttributes ia = new ImageAttributes())
				{
					ia.SetColorMatrix(new ColorMatrix { Matrix33 = Ka });
					g.DrawImage(A, new Rectangle(0, 0, A.Width, A.Height), 0, 0, A.Width, A.Height, GraphicsUnit.Pixel, ia);
				}
			}
		}

		private static readonly Dictionary<Control, Veil> _veils = new Dictionary<Control, Veil>();

		private static void DropVeil(Control parent)
		{
			if (_veils.TryGetValue(parent, out Veil v))
			{
				_veils.Remove(parent);
				Anim.Cancel("veil-" + parent.GetHashCode());
				Action pend = v.Pending;
				v.Pending = null;
				try
				{
					pend?.Invoke();
				}
				catch
				{
				}
				try
				{
					v.Hide();
					v.A?.Dispose();
					v.Dispose();
				}
				catch
				{
				}
			}
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

		private static bool PlayVeil(Control host, Control parent, Action change, Rectangle area, int ms)
		{
			_sw.Restart();
			long st = Lag.T0();
			Bitmap a = ScreenSnap(host, area);
			Lag.Done("Переход: снимок окна" + ((a == null) ? " (не удался)" : ""), st);
			if (a == null)
			{
				return false;
			}
			Veil v = null;
			try
			{
				Rectangle sr = host.RectangleToScreen(area);
				v = new Veil { A = a, Bounds = sr, Pending = change };
				v.Opacity = 0.99;
				v.Show(host.FindForm());
				v.Bounds = sr;
				v.Update();
				// the task toast and dialogs stay above the veil
				foreach (Form of in Application.OpenForms)
				{
					if (of != v && of != host.FindForm() && of.Visible && of.IsHandleCreated)
					{
						SetWindowPos(of.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x1u | 0x2u | 0x10u);
					}
				}
			}
			catch
			{
				try
				{
					v?.Dispose();
				}
				catch
				{
				}
				a.Dispose();
				return false;
			}
			_veils[parent] = v;
			const float cut = 0.3f;
			Veil vv = v;
			Anim.Start(v, ms, delegate(float t)
			{
				if (vv.IsDisposed)
				{
					return;
				}
				if (t < cut)
				{
					vv.Ka = 1f - Anim.EaseOut(t / cut);
					vv.Invalidate();
				}
				if (t >= cut * 0.5f && vv.Pending != null)
				{
					Action ch = vv.Pending;
					vv.Pending = null;
					long lt = Lag.T0();
					_sw.Restart();
					try
					{
						ch();
						host.PerformLayout();
						host.Update();
					}
					catch
					{
					}
					Trace("change(veil)");
					Lag.Done("Переход: новая страница", lt);
					BusyUntil = DateTime.Now.AddMilliseconds((1f - t) * ms + 60);
				}
				if (t >= cut && vv.Pending == null)
				{
					if (vv.Ka > 0f)
					{
						vv.Ka = 0f;
						vv.Invalidate();
						vv.Update();
					}
					vv.Opacity = Math.Max(0.0, 0.99 * (1.0 - Anim.EaseOut((t - cut) / (1f - cut))));
				}
			}, delegate
			{
				DropVeil(parent);
			}, "veil-" + parent.GetHashCode());
			return true;
		}

		private static readonly Dictionary<Control, Overlay> _active = new Dictionary<Control, Overlay>();

		private static void Drop(Control parent)
		{
			if (_active.TryGetValue(parent, out Overlay old))
			{
				_active.Remove(parent);
				Anim.Cancel("transition-" + parent.GetHashCode());
				Action pend = old.Pending;
				old.Pending = null;
				try
				{
					pend?.Invoke();
				}
				catch
				{
				}
				parent.Controls.Remove(old);
				old.A?.Dispose();
				old.B?.Dispose();
				old.Dispose();
			}
		}

		// paints a control tree ourselves: DrawToBitmap misplaces nested children whose handles are not created yet.
		// Every control gets its own bitmap and an untransformed Graphics: GDI text (TextRenderer) ignores
		// Graphics.TranslateTransform on Windows, so translating one shared Graphics stacks all text at the origin.
		private class Painter : Control
		{
			// under: already rendered pixels of the parent; the child bitmap starts as a copy of them, so
			// transparent controls get the right background and GDI text (TextRenderer) is drawn onto opaque
			// pixels. On a 32bpp ARGB bitmap GDI text loses its alpha and becomes black aliased blobs.
			public Bitmap Render(Control c, Rectangle vis, bool root, Bitmap under = null, Point at = default(Point))
			{
				if (c is Overlay || (!root && !c.Visible) || c.Width <= 0 || c.Height <= 0)
				{
					return null;
				}
				Bitmap bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppRgb);
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Theme.Bg);
					if (under != null)
					{
						g.DrawImageUnscaled(under, -at.X, -at.Y);
					}
					try
					{
						if (c is TextBoxBase)
						{
							c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
						}
						else
						{
							using (PaintEventArgs pe = new PaintEventArgs(g, new Rectangle(0, 0, c.Width, c.Height)))
							{
								// transparent controls already have the parent pixels under them; the default
								// transparent background would repaint the whole parent with misplaced text
								if (c.BackColor.A == 255 || c is FButton || under == null)
								{
									InvokePaintBackground(c, pe);
								}
								InvokePaint(c, pe);
							}
						}
					}
					catch
					{
					}
					for (int i = c.Controls.Count - 1; i >= 0; i--)
					{
						Control ch = c.Controls[i];
						if (!ch.Visible || !ch.Bounds.IntersectsWith(vis))
						{
							continue;
						}
						Rectangle cv = Rectangle.Intersect(vis, ch.Bounds);
						cv.Offset(-ch.Left, -ch.Top);
						using (Bitmap cb = Render(ch, cv, false, bmp, ch.Location))
						{
							if (cb != null)
							{
								g.DrawImageUnscaled(cb, ch.Left, ch.Top);
							}
						}
					}
				}
				return bmp;
			}
		}

		private static Painter _painter;

		private static Bitmap Snap(Control c, Rectangle area)
		{
			if (_painter == null)
			{
				_painter = new Painter();
			}
			Rectangle full = new Rectangle(0, 0, Math.Max(1, c.Width), Math.Max(1, c.Height));
			Bitmap b = new Bitmap(full.Width, full.Height);
			using (Graphics g = Graphics.FromImage(b))
			{
				g.Clear(Theme.Bg);
				using (Bitmap r = _painter.Render(c, area.IsEmpty ? full : area, true))
				{
					if (r != null)
					{
						g.DrawImageUnscaled(r, 0, 0);
					}
				}
			}
			if (area.IsEmpty || area == full)
			{
				return b;
			}
			Bitmap part = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height));
			using (Graphics g2 = Graphics.FromImage(part))
			{
				g2.DrawImage(b, new Rectangle(0, 0, part.Width, part.Height), area, GraphicsUnit.Pixel);
			}
			b.Dispose();
			return part;
		}

		internal static void DebugSnap(Control c, string path)
		{
			using (Bitmap b = Snap(c, Rectangle.Empty))
			{
				b.Save(path);
			}
		}

		// dir: 0 = rise from below, 1 = come from the right, -1 = come from the left
		public static void Play(Control host, Action change, int dir = 0, int ms = 240)
		{
			Play(host, change, Rectangle.Empty, dir, ms);
		}

		// area: part of host (host coordinates) to animate; empty = whole control
		// UI work that would stutter the animation (big relayouts) waits until this moment
		public static DateTime BusyUntil = DateTime.MinValue;

		private static DateTime _lastStart = DateTime.MinValue;

		private static readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();

		private static readonly bool _trace = Environment.GetEnvironmentVariable("WW_TRACE") == "1";

		private static void Trace(string what)
		{
			if (_trace)
			{
				Console.Error.WriteLine("T " + what + " " + _sw.ElapsedMilliseconds + "ms");
			}
			Lag.Part("переход: " + what, _sw.ElapsedMilliseconds);
			_sw.Restart();
		}

		public static void Play(Control host, Action change, Rectangle area, int dir, int ms)
		{
			BusyUntil = DateTime.Now.AddMilliseconds(ms + 40);
			Control parent = host?.Parent;
			if (!Anim.Enabled || host == null || parent == null || !host.IsHandleCreated || !host.Visible || host.Width < 8 || host.Height < 8)
			{
				change();
				return;
			}
			// clicking again while a transition is still playing: no new snapshots (they are the expensive part),
			// just finish the switch at once — rapid clicking through pages/tabs stays instant
			if (_active.ContainsKey(parent) || _veils.ContainsKey(parent) || (DateTime.Now - _lastStart).TotalMilliseconds < ms)
			{
				Drop(parent);
				DropVeil(parent);
				change();
				BusyUntil = DateTime.Now.AddMilliseconds(60);
				_lastStart = DateTime.Now;
				return;
			}
			_lastStart = DateTime.Now;
			Drop(parent);
			{
				Rectangle fullV = new Rectangle(0, 0, host.Width, host.Height);
				Rectangle av = area.IsEmpty ? fullV : Rectangle.Intersect(area, fullV);
				if (av.Width >= 8 && av.Height >= 8 && PlayVeil(host, parent, change, av, ms))
				{
					return;
				}
				// painting a big page control by control took up to 0.8 s on a real PC: better no animation
				if (Type.GetType("Mono.Runtime") == null)
				{
					change();
					BusyUntil = DateTime.Now.AddMilliseconds(40);
					return;
				}
			}
			Overlay ov = null;
			try
			{
				Rectangle full = new Rectangle(0, 0, host.Width, host.Height);
				area = area.IsEmpty ? full : Rectangle.Intersect(area, full);
				if (area.Width < 8 || area.Height < 8)
				{
					area = full;
				}
				_sw.Restart();
				ov = new Overlay { Bounds = new Rectangle(host.Left + area.X, host.Top + area.Y, area.Width, area.Height), A = Snap(host, area) };
				Trace("snapA");
				parent.Controls.Add(ov);
				ov.BringToFront();
				_active[parent] = ov;
				ov.Update();
			}
			catch
			{
				Drop(parent);
				change();
				return;
			}
			ov.Dx = ((dir == 0) ? 0 : (dir * Theme.S(28f)));
			ov.Dy = ((dir == 0) ? Theme.S(16f) : 0);
			// the fade starts at once (instant feedback on the click); the heavy part — switching and
			// painting the new page — runs a few frames later, while the old picture is already fading
			ov.Pending = change;
			Overlay o = ov;
			Timer later = new Timer { Interval = Math.Max(15, (int)(ms * Overlay.Cut * 0.5f)) };
			later.Tick += delegate
			{
				later.Stop();
				later.Dispose();
				if (o.IsDisposed || o.Pending == null)
				{
					return;
				}
				Action ch = o.Pending;
				o.Pending = null;
				long lt = Lag.T0();
				try
				{
					_sw.Restart();
					ch();
					Trace("change");
					host.PerformLayout();
					o.B = Snap(host, area);
					Trace("snapB");
					o.BAt = Math.Max(Overlay.Cut, o.T);
					BusyUntil = DateTime.Now.AddMilliseconds((1f - o.BAt) * ms + 40);
				}
				catch
				{
					Drop(parent);
				}
				Lag.Done("Переход: новая страница", lt);
			};
			later.Start();
			Anim.Start(ov, ms, delegate(float t)
			{
				o.T = t;
				o.Invalidate();
			}, delegate
			{
				Drop(parent);
			}, "transition-" + parent.GetHashCode());
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		private struct RECT
		{
			public int L, T, R, B;
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hwnd, out RECT r);

		// The window's own picture from the system compositor (PrintWindow, full content): a few ms, and
		// unlike painting every control into a bitmap it does not depend on how many controls the page has.
		// Toasts and other windows on top are not included. null = not available (then no animation).
		private static Bitmap ScreenSnap(Control host, Rectangle area)
		{
			if (Type.GetType("Mono.Runtime") != null)
			{
				return null;
			}
			try
			{
				Form f = host.FindForm();
				if (f == null || !f.IsHandleCreated || f.WindowState == FormWindowState.Minimized)
				{
					return null;
				}
				if (!GetWindowRect(f.Handle, out RECT wr) || wr.R - wr.L < 8 || wr.B - wr.T < 8)
				{
					return null;
				}
				Rectangle sr = host.RectangleToScreen(area);
				Bitmap part = new Bitmap(sr.Width, sr.Height, PixelFormat.Format32bppRgb);
				using (Bitmap whole = new Bitmap(wr.R - wr.L, wr.B - wr.T, PixelFormat.Format32bppRgb))
				{
					bool ok;
					using (Graphics g = Graphics.FromImage(whole))
					{
						IntPtr hdc = g.GetHdc();
						try
						{
							ok = PrintWindow(f.Handle, hdc, 2u);
						}
						finally
						{
							g.ReleaseHdc(hdc);
						}
					}
					if (!ok)
					{
						part.Dispose();
						return null;
					}
					using (Graphics g2 = Graphics.FromImage(part))
					{
						g2.DrawImageUnscaled(whole, wr.L - sr.X, wr.T - sr.Y);
					}
				}
				return part;
			}
			catch
			{
				return null;
			}
		}
	}
}
