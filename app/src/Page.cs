// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class Page : Control
	{
		public string Key;

		public string Title;

		public string Subtitle;

		public string GlyphText;

		public readonly Canvas Body;

		public readonly List<IBlock> Blocks = new List<IBlock>();

		public Action OnShown;

		private int _scroll;

		private int _contentH;

		private bool _dragThumb;

		private int _dragY0;

		private int _dragS0;

		private readonly Timer _relayout;

		public new int Margin
		{
			get
			{
				if (base.Width <= Theme.S(1000f))
				{
					return Theme.S(28f);
				}
				return Theme.S(40f);
			}
		}

		private int BarW => Theme.S(12f);

		private int MaxScroll => Math.Max(0, _contentH - base.Height);

		public Page(string key, string glyph, string title, string subtitle)
		{
			Key = key;
			GlyphText = glyph;
			Title = title;
			Subtitle = subtitle;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Theme.Bg;
			Body = new Canvas(this);
			base.Controls.Add(Body);
			_relayout = new Timer
			{
				Interval = 10
			};
			_relayout.Tick += delegate
			{
				_relayout.Stop();
				DoLayoutBlocks();
			};
		}

		// ---- sub-tabs: blocks added after AddTabs/Section(i) belong to tab i; others are always shown ----
		private readonly Dictionary<IBlock, int> _section = new Dictionary<IBlock, int>();

		private int _addSection = -1;

		public TabStrip Tabs;

		public int CurrentTab;

		public T Add<T>(T b) where T : IBlock
		{
			Blocks.Add(b);
			_section[b] = _addSection;
			AddControls(b);
			return b;
		}

		public TabStrip AddTabs(params string[] names)
		{
			_addSection = -1;
			Tabs = Add(new TabStrip(names));
			Tabs.Changed += delegate(int i)
			{
				SelectTab(i);
			};
			_addSection = 0;
			return Tabs;
		}

		public void Section(int i)
		{
			_addSection = i;
		}

		public void SelectTab(int i, bool animate = true)
		{
			if (Tabs == null || i < 0 || i >= Tabs.Items.Length)
			{
				return;
			}
			int old = CurrentTab;
			// keep the tab strip where it is: scroll back only when it is hidden above the window
			int keep = Math.Max(0, Tabs.Top - Theme.S(12f));
			int newScroll = Math.Min(_scroll, keep);
			bool stripStays = newScroll == _scroll;
			Action change = delegate
			{
				StopSmooth();
				CurrentTab = i;
				Tabs.Select(i);
				_scroll = newScroll;
				DoLayoutBlocks();
			};
			if (animate && old != i && Visible)
			{
				// only the content under the tab strip slides; the header and the tab pill stay in place
				int top = stripStays ? Math.Max(0, Body.Top + Tabs.Bottom + Theme.S(6f)) : 0;
				Transition.Play(this, change, new Rectangle(0, top, base.Width - BarW, base.Height - top), (i > old) ? 1 : (-1), 220);
			}
			else
			{
				change();
			}
			if (old != i && AfterTab != null)
			{
				AfterTab();
			}
		}

		private bool InTab(IBlock b)
		{
			if (b is SettingCard sc && sc.IsExpert && !Prefs.Expert)
			{
				return false;
			}
			int s;
			return !_section.TryGetValue(b, out s) || s < 0 || s == CurrentTab;
		}

		private static void SetShown(IBlock b, bool v)
		{
			if (b is Control c)
			{
				if (c.Visible != v)
				{
					c.Visible = v;
				}
			}
			else if (b is Grid g)
			{
				foreach (IBlock item in g.Items)
				{
					SetShown(item, v);
				}
			}
		}

		private void AddControls(IBlock b)
		{
			if (b is Control value)
			{
				Body.Controls.Add(value);
			}
			else
			{
				if (!(b is Grid grid))
				{
					return;
				}
				foreach (IBlock item in grid.Items)
				{
					AddControls(item);
				}
			}
		}

		public void RelayoutSoon()
		{
			_relayout.Stop();
			_relayout.Start();
		}

		// ---- "not read yet" cover (see ReadGate) ----
		public ReadGate Gate;

		public bool Gated => Gate != null && Gate.Visible;

		public void ShowGate(Action read)
		{
			if (Gate == null)
			{
				Gate = new ReadGate { PageTitle = Title };
				Gate.ReadClicked += delegate
				{
					_gateRead?.Invoke();
				};
				base.Controls.Add(Gate);
			}
			_gateRead = read;
			Gate.Busy = false;
			_scroll = 0;
			ClampScroll();
			Gate.Visible = true;
			PlaceGate();
			Gate.BringToFront();
		}

		public void HideGate()
		{
			if (Gate != null && Gate.Visible)
			{
				Gate.Busy = false;
				Gate.Visible = false;
				Invalidate();
			}
		}

		private Action _gateRead;

		public void GateRead()
		{
			if (Gated)
			{
				_gateRead?.Invoke();
			}
		}

		private void PlaceGate()
		{
			if (Gated)
			{
				int top = Math.Max(0, Body.Top + Body.HeaderH);
				Gate.Bounds = new Rectangle(0, top, Math.Max(1, base.Width - BarW), Math.Max(1, base.Height - top));
			}
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			DoLayoutBlocks();
		}

		public void DoLayoutBlocks()
		{
			long lt0 = Lag.T0();
			try
			{
				DoLayoutBlocksCore();
				PlaceGate();
			}
			finally
			{
				Lag.Done("DoLayoutBlocks " + Key + (Visible ? "" : " (скрыт)"), lt0);
			}
		}

		private void DoLayoutBlocksCore()
		{
			if (base.Width <= 0 || base.Height <= 0)
			{
				return;
			}
			int num = base.Width - BarW;
			int margin = Margin;
			int num2 = Math.Min(num - margin * 2, Theme.S(1200f));
			margin = Math.Max(margin, (num - num2) / 2);
			Body.SuspendLayout();
			int num3 = Body.HeaderH;
			int num4 = Theme.S(12f);
			foreach (IBlock block in Blocks)
			{
				bool shown = InTab(block);
				SetShown(block, shown);
				if (!shown)
				{
					continue;
				}
				int num5 = block.DoLayout(margin, num3, num2);
				num3 += num5 + num4;
			}
			Body.ResumeLayout(performLayout: false);
			_contentH = num3 + Theme.S(24f);
			Body.SetBounds(0, -_scroll, num, Math.Max(base.Height, _contentH));
			ClampScroll();
			Invalidate();
			Body.Invalidate(invalidateChildren: true);
		}

		private float _smoothTarget = -1f;

		private float _smoothPos;

		private bool _smoothOn;

		private double _smoothLast;

		private Func<bool> _smoothFrame;

		// wheel / page clicks: glide to the target instead of jumping (time based, ~120 ms to settle)
		public void SmoothBy(int d)
		{
			if (Gated)
			{
				return;
			}
			if (!Anim.Enabled)
			{
				ScrollBy(d);
				return;
			}
			if (_smoothTarget < 0f)
			{
				_smoothTarget = _scroll;
				_smoothPos = _scroll;
				_smoothLast = Anim.Now;
			}
			else if (Math.Abs(_smoothPos - _scroll) > 1.5f)
			{
				// scrolled by something else meanwhile (thumb drag, relayout): continue from where the page really is
				_smoothPos = _scroll;
			}
			float pending = _smoothTarget - _smoothPos;
			if (d != 0 && pending != 0f && Math.Sign(d) != Math.Sign(pending))
			{
				// direction reversed while still gliding: start from the current position, so quick
				// up/down wheel flicks move the page each time instead of cancelling out and leaving it in place
				_smoothTarget = _smoothPos;
			}
			_smoothTarget = Math.Max(0, Math.Min(MaxScroll, _smoothTarget + d));
			if (_smoothFrame == null)
			{
				_smoothFrame = SmoothStep;
			}
			if (!_smoothOn)
			{
				_smoothOn = true;
				// first step right now, in the same wheel message: the page reacts instantly,
				// the shared animation clock then finishes the glide
				_smoothLast = Anim.Now - 16.0;
				if (SmoothStep())
				{
					Anim.Frame(_smoothFrame);
				}
			}
		}

		private void StopSmooth()
		{
			_smoothTarget = -1f;
			_smoothOn = false;
		}

		private bool SmoothStep()
		{
			if (_smoothTarget < 0f || IsDisposed)
			{
				_smoothOn = false;
				return false;
			}
			if (Math.Abs(_smoothPos - _scroll) > 1.5f)
			{
				_smoothPos = _scroll;
			}
			double now = Anim.Now;
			float dt = Math.Min(50f, (float)(now - _smoothLast));
			_smoothLast = now;
			_smoothTarget = Math.Max(0, Math.Min(MaxScroll, _smoothTarget));
			float k = 1f - (float)Math.Exp(-dt / 45f);
			_smoothPos += (_smoothTarget - _smoothPos) * k;
			bool done = Math.Abs(_smoothTarget - _smoothPos) < 0.6f;
			if (done)
			{
				_smoothPos = _smoothTarget;
			}
			int ns = (int)Math.Round(_smoothPos);
			if (ns != _scroll)
			{
				_scroll = ns;
				ClampScroll();
				Invalidate(new Rectangle(base.Width - BarW, 0, BarW, base.Height));
				PaintNow();
			}
			if (done)
			{
				StopSmooth();
				return false;
			}
			return true;
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr rgn, uint flags);

		// set while the page is moving; cards don't light up under a pointer that only "meets" them because
		// the content slid under it (that hover flicker during scrolling looked like lag)
		private static double _scrollingUntil;

		public static bool Scrolling => Anim.Now < _scrollingUntil;

		// paint the strip that the scroll uncovered, *including* the child controls, in the same frame.
		// Body.Update() painted only the canvas itself: cards and toggles in the new strip popped in a moment later.
		private void PaintNow()
		{
			_scrollingUntil = Anim.Now + 120.0;
			if (Win.IsWindows && Body.IsHandleCreated)
			{
				try
				{
					RedrawWindow(Body.Handle, IntPtr.Zero, IntPtr.Zero, 0x0100 | 0x0080); // RDW_UPDATENOW | RDW_ALLCHILDREN
					Update();
					return;
				}
				catch
				{
				}
			}
			Body.Update();
			Update();
		}

		private void ClampScroll()
		{
			int num = Math.Max(0, Math.Min(MaxScroll, _scroll));
			if (num != _scroll || Body.Top != -num)
			{
				_scroll = num;
				Body.Top = -num;
			}
		}

		public void ScrollBy(int d)
		{
			if (Gated)
			{
				return;
			}
			StopSmooth();
			int scroll = _scroll;
			_scroll += d;
			ClampScroll();
			if (_scroll != scroll)
			{
				Invalidate(new Rectangle(base.Width - BarW, 0, BarW, base.Height));
				PaintNow();
			}
		}

		public void ScrollTop()
		{
			StopSmooth();
			_scroll = 0;
			ClampScroll();
			Invalidate();
		}

		// one wheel notch = Windows "lines per notch" (3 by default) × 40 px, scaled with DPI
		// (it was a fixed 120 physical px, which felt slow on 125–200 % screens)
		internal static int WheelPx(int delta)
		{
			int lines = SystemInformation.MouseWheelScrollLines;
			if (lines <= 0 || lines > 20)
			{
				lines = 3;
			}
			return (int)Math.Round(delta / 120.0 * lines * Theme.S(40f));
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			SmoothBy(-WheelPx(e.Delta));
			if (e is HandledMouseEventArgs)
			{
				((HandledMouseEventArgs)e).Handled = true;
			}
		}

		private Rectangle Thumb()
		{
			if (MaxScroll <= 0)
			{
				return Rectangle.Empty;
			}
			int num = base.Height - Theme.S(16f);
			int num2 = Math.Max(Theme.S(40f), num * base.Height / Math.Max(1, _contentH));
			int y = Theme.S(8f) + (num - num2) * _scroll / MaxScroll;
			return new Rectangle(base.Width - BarW + Theme.S(3f), y, Theme.S(6f), num2);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			Rectangle rectangle = Thumb();
			if (rectangle != Rectangle.Empty && e.X >= base.Width - BarW)
			{
				if (rectangle.Contains(e.Location))
				{
					StopSmooth();
					_dragThumb = true;
					_dragY0 = e.Y;
					_dragS0 = _scroll;
					base.Capture = true;
				}
				else
				{
					SmoothBy((e.Y < rectangle.Y) ? (-base.Height + Theme.S(60f)) : (base.Height - Theme.S(60f)));
				}
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (_dragThumb)
			{
				Rectangle rectangle = Thumb();
				int num = base.Height - Theme.S(16f) - rectangle.Height;
				if (num > 0)
				{
					_scroll = _dragS0 + (e.Y - _dragY0) * MaxScroll / num;
					ClampScroll();
					Invalidate(new Rectangle(base.Width - BarW, 0, BarW, base.Height));
					PaintNow();
				}
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			_dragThumb = false;
			base.Capture = false;
			base.OnMouseUp(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			using (SolidBrush brush = new SolidBrush(Theme.Bg))
			{
				graphics.FillRectangle(brush, base.Width - BarW, 0, BarW, base.Height);
			}
			int gh = Theme.S(280f);
			using (LinearGradientBrush brush3 = new LinearGradientBrush(new Rectangle(base.Width - BarW, -_scroll - 1, BarW, gh + 2), Color.FromArgb(30, 30, 35), Theme.Bg, 90f))
			{
				graphics.FillRectangle(brush3, base.Width - BarW, -_scroll, BarW, gh);
			}
			Rectangle rectangle = Thumb();
			if (rectangle == Rectangle.Empty)
			{
				return;
			}
			using (GraphicsPath path = Theme.Round(rectangle, (float)rectangle.Width / 2f))
			{
				using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(rectangle.X, rectangle.Y - 1, rectangle.Width, rectangle.Height + 2), Color.FromArgb(110, 110, 122), Color.FromArgb(70, 70, 80), 90f))
				{
					graphics.FillPath(brush2, path);
				}
			}
		}

		public Action AfterTab;

		// called on the UI thread after Refresh applied fresh values (before the relayout)
		public Action AfterRefresh;

		// freshness of a full read (all tabs): set by the main form, compared with its action counter
		public int FreshStamp = -1;

		public DateTime FreshAt = DateTime.MinValue;

		public void Refresh(Worker w)
		{
			Refresh(w, all: false, done: null);
		}

		public void Refresh(Worker w, bool all, Action done)
		{
			IBlock[] blocks = (all ? Blocks : Blocks.FindAll(InTab)).ToArray();
			w.Run(delegate(IEngine e)
			{
				object[] array = new object[blocks.Length];
				for (int i = 0; i < blocks.Length; i++)
				{
					try
					{
						array[i] = blocks[i].Fetch(e);
					}
					catch
					{
					}
				}
				return array;
			}, delegate(object o)
			{
				ApplyWhenCalm(o, blocks, done, all);
			});
		}

		// applying values relayouts the page: never in the middle of a page/tab transition
		public void ForgetData()
		{
			_sigs.Clear();
		}

		private readonly Dictionary<IBlock, string> _sigs = new Dictionary<IBlock, string>();

		// a stable text form of fetched data; null = unknown kind of object (always applied)
		private static string Sig(object v, int depth)
		{
			if (depth > 8)
			{
				return null;
			}
			if (v == null)
			{
				return "~";
			}
			if (v is string str)
			{
				return "s" + str.Length + ":" + str;
			}
			if (v is bool || v is int || v is long || v is double || v is float || v is decimal || v is short || v is byte || v is uint || v is ulong || v is DateTime || v.GetType().IsEnum)
			{
				return v.GetType().Name[0] + Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
			}
			StringBuilder sb = new StringBuilder();
			if (v is System.Collections.IDictionary d)
			{
				List<string> keys = new List<string>();
				Dictionary<string, object> map = new Dictionary<string, object>();
				foreach (System.Collections.DictionaryEntry de in d)
				{
					string k = Convert.ToString(de.Key, System.Globalization.CultureInfo.InvariantCulture) ?? "";
					keys.Add(k);
					map[k] = de.Value;
				}
				keys.Sort(StringComparer.Ordinal);
				sb.Append('{');
				foreach (string k in keys)
				{
					string sv = Sig(map[k], depth + 1);
					if (sv == null)
					{
						return null;
					}
					sb.Append(k.Length).Append(':').Append(k).Append('=').Append(sv).Append(';');
				}
				sb.Append('}');
				return sb.ToString();
			}
			if (v is System.Collections.IEnumerable en)
			{
				sb.Append('[');
				foreach (object x in en)
				{
					string sv = Sig(x, depth + 1);
					if (sv == null)
					{
						return null;
					}
					sb.Append(sv).Append(',');
				}
				sb.Append(']');
				return sb.ToString();
			}
			return null;
		}

		private void ApplyWhenCalm(object o, IBlock[] blocks, Action done, bool skipSame = false)
		{
			int wait = (int)(Transition.BusyUntil - DateTime.Now).TotalMilliseconds;
			if (wait > 0 && Visible)
			{
				Timer t = new Timer { Interval = Math.Max(15, wait) };
				t.Tick += delegate
				{
					t.Stop();
					t.Dispose();
					ApplyWhenCalm(o, blocks, done, skipSame);
				};
				t.Start();
				return;
			}
			{
				{
				if (o is object[] array)
				{
					// the same values as last time: nothing to apply, no relayout (this was the freeze after a switch)
					bool any = false;
					SuspendLayout();
					for (int i = 0; i < blocks.Length && i < array.Length; i++)
					{
						string sig = Sig(array[i], 0);
						if (skipSame && sig != null && _sigs.TryGetValue(blocks[i], out string old) && old == sig)
						{
							continue;
						}
						any = true;
						try
						{
							long t0 = Lag.T0();
							blocks[i].Apply(array[i]);
							Lag.Done("Применение " + Key + "/" + blocks[i].GetType().Name, t0);
							if (sig != null)
							{
								_sigs[blocks[i]] = sig;
							}
							else
							{
								_sigs.Remove(blocks[i]);
							}
						}
						catch
						{
							_sigs.Remove(blocks[i]);
						}
					}
					long t1 = Lag.T0();
					ResumeLayout(any);
					Lag.Done("ResumeLayout " + Key, t1);
					if (any)
					{
						t1 = Lag.T0();
						AfterRefresh?.Invoke();
						Lag.Done("AfterRefresh " + Key, t1);
						t1 = Lag.T0();
						DoLayoutBlocks();
						Lag.Done("Раскладка " + Key, t1);
					}
				}
				done?.Invoke();
				}
			}
		}
	}
}
