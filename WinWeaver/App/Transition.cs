using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WinWeaver;

internal static class Transition
{
	private class Overlay : Control
	{
		public Bitmap A;

		public Bitmap B;

		public float T;

		public int Dx;

		public int Dy;

		public float BAt = 0.3f;

		public const float Cut = 0.3f;

		public Action Pending;

		public Overlay()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.Clear(Theme.Bg);
			if (T < 0.3f || B == null)
			{
				float num = 1f - Anim.EaseOut(Math.Min(1f, T / 0.3f));
				if (A == null || !(num > 0.01f))
				{
					return;
				}
				if (num >= 0.999f)
				{
					graphics.DrawImageUnscaled(A, 0, 0);
					return;
				}
				using ImageAttributes imageAttributes = new ImageAttributes();
				imageAttributes.SetColorMatrix(new ColorMatrix
				{
					Matrix33 = num
				});
				graphics.DrawImage(A, new Rectangle(0, 0, A.Width, A.Height), 0, 0, A.Width, A.Height, GraphicsUnit.Pixel, imageAttributes);
				return;
			}
			float num2 = Anim.EaseOut(Math.Max(0f, (T - BAt) / Math.Max(0.05f, 1f - BAt)));
			if (num2 >= 0.999f)
			{
				graphics.DrawImageUnscaled(B, 0, 0);
				return;
			}
			int x = (int)((1f - num2) * (float)Dx);
			int y = (int)((1f - num2) * (float)Dy);
			using ImageAttributes imageAttributes2 = new ImageAttributes();
			imageAttributes2.SetColorMatrix(new ColorMatrix
			{
				Matrix33 = num2
			});
			graphics.DrawImage(B, new Rectangle(x, y, B.Width, B.Height), 0, 0, B.Width, B.Height, GraphicsUnit.Pixel, imageAttributes2);
		}
	}

	private static readonly Dictionary<Control, Overlay> _active = new Dictionary<Control, Overlay>();


	public static DateTime BusyUntil = DateTime.MinValue;

	private static DateTime _lastStart = DateTime.MinValue;

	private static readonly Stopwatch _sw = new Stopwatch();

	private static readonly bool _trace = Environment.GetEnvironmentVariable("WW_TRACE") == "1";

	private static void Drop(Control parent)
	{
		if (_active.TryGetValue(parent, out var value))
		{
			_active.Remove(parent);
			Anim.Cancel("transition-" + parent.GetHashCode());
			Action pending = value.Pending;
			value.Pending = null;
			try
			{
				pending?.Invoke();
			}
			catch
			{
			}
			parent.Controls.Remove(value);
			parent.Invalidate(invalidateChildren: true);
			value.A?.Dispose();
			value.B?.Dispose();
			value.Dispose();
		}
	}

	private static Bitmap Snap(Control c, Rectangle area)
	{
		Rectangle rectangle = new Rectangle(0, 0, Math.Max(1, c.Width), Math.Max(1, c.Height));
		Bitmap bitmap = new Bitmap(rectangle.Width, rectangle.Height);
		c.DrawToBitmap(bitmap, rectangle);
		if (area.IsEmpty || area == rectangle)
		{
			return bitmap;
		}
		Bitmap bitmap3 = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height));
		using (Graphics graphics2 = Graphics.FromImage(bitmap3))
		{
			graphics2.DrawImage(bitmap, new Rectangle(0, 0, bitmap3.Width, bitmap3.Height), area, GraphicsUnit.Pixel);
		}
		bitmap.Dispose();
		return bitmap3;
	}

	internal static void DebugSnap(Control c, string path)
	{
		using Bitmap bitmap = Snap(c, Rectangle.Empty);
		bitmap.Save(path);
	}

	public static void Play(Control host, Action change, int dir = 0, int ms = 240)
	{
		Play(host, change, Rectangle.Empty, dir, ms);
	}

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
		if (_active.ContainsKey(parent) || (DateTime.Now - _lastStart).TotalMilliseconds < (double)ms)
		{
			Drop(parent);
			change();
			BusyUntil = DateTime.Now.AddMilliseconds(60.0);
			_lastStart = DateTime.Now;
			return;
		}
		_lastStart = DateTime.Now;
		Drop(parent);
		Overlay overlay = null;
		try
		{
			Rectangle rectangle2 = new Rectangle(0, 0, host.Width, host.Height);
			area = (area.IsEmpty ? rectangle2 : Rectangle.Intersect(area, rectangle2));
			if (area.Width < 8 || area.Height < 8)
			{
				area = rectangle2;
			}
			_sw.Restart();
			overlay = new Overlay
			{
				Bounds = new Rectangle(host.Left + area.X, host.Top + area.Y, area.Width, area.Height),
				A = Snap(host, area)
			};
			Trace("snapA");
			parent.Controls.Add(overlay);
			overlay.BringToFront();
			_active[parent] = overlay;
			overlay.Update();
		}
		catch
		{
			Drop(parent);
			change();
			return;
		}
		overlay.Dx = ((dir != 0) ? (dir * Theme.S(28f)) : 0);
		overlay.Dy = ((dir == 0) ? Theme.S(16f) : 0);
		overlay.Pending = change;
		Overlay o = overlay;
		Timer later = new Timer
		{
			Interval = Math.Max(15, (int)((float)ms * 0.3f * 0.5f))
		};
		later.Tick += delegate
		{
			later.Stop();
			later.Dispose();
			if (!o.IsDisposed && o.Pending != null)
			{
				Action pending = o.Pending;
				o.Pending = null;
				long t2 = Lag.T0();
				try
				{
					_sw.Restart();
					pending();
					Trace("change");
					host.PerformLayout();
					o.B = Snap(host, area);
					Trace("snapB");
					o.BAt = Math.Max(0.3f, o.T);
					BusyUntil = DateTime.Now.AddMilliseconds((1f - o.BAt) * (float)ms + 40f);
				}
				catch
				{
					Drop(parent);
				}
				Lag.Done("Переход: новая страница", t2);
			}
		};
		later.Start();
		Anim.Start(overlay, ms, delegate(float t)
		{
			o.T = t;
			o.Invalidate();
		}, delegate
		{
			Drop(parent);
		}, "transition-" + parent.GetHashCode());
	}
}
