using System;
using System.Collections.Generic;

namespace WinWeaver;

internal static class Skeleton
{
	private static readonly HashSet<Control> _live = new HashSet<Control>();

	private static Timer _t;

	public static float Phase => (float)(Anim.Now % 1600.0 / 1600.0);

	public static void Track(Control c, bool loading)
	{
		if (!Anim.Enabled)
		{
			return;
		}
		if (loading)
		{
			_live.Add(c);
			if (_t == null)
			{
				_t = new Timer
				{
					Interval = 33
				};
				_t.Tick += delegate
				{
					List<Control> list = new List<Control>();
					foreach (Control item in _live)
					{
						if (item.IsDisposed)
						{
							list.Add(item);
						}
						else if (item.Visible)
						{
							item.Invalidate();
						}
					}
					foreach (Control item2 in list)
					{
						_live.Remove(item2);
					}
					if (_live.Count == 0)
					{
						_t.Stop();
					}
				};
			}
			if (!_t.Enabled)
			{
				_t.Start();
			}
		}
		else
		{
			_live.Remove(c);
		}
	}

	public static void Bar(Graphics g, RectangleF r, Control owner = null)
	{
		if (r.Width < 2f || r.Height < 2f)
		{
			return;
		}
		Theme.Smooth(g);
		using GraphicsPath path = Theme.Round(r, Math.Min(r.Height / 2f, Theme.S(7f)));
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
		{
			g.FillPath(brush, path);
		}
		if (!Anim.Enabled)
		{
			return;
		}
		int num = 0;
		if (owner != null && owner.IsHandleCreated)
		{
			try
			{
				num = owner.PointToScreen(Point.Empty).X;
			}
			catch
			{
			}
		}
		float num2 = Theme.S(1400f);
		float num3 = Theme.S(260f);
		float num4 = 0f - num3 + (num2 + num3 * 2f) * Phase - (float)(num % (int)num2);
		RectangleF rect = new RectangleF(num4 - num3 / 2f, r.Y, num3, r.Height);
		if (rect.Right < r.X || rect.X > r.Right)
		{
			return;
		}
		using LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(rect.X - 1f, rect.Y, rect.Width + 2f, Math.Max(1f, rect.Height)), Color.Transparent, Color.Transparent, 0f);
		ColorBlend colorBlend = new ColorBlend();
		colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
		colorBlend.Colors = new Color[3]
		{
			Color.FromArgb(0, 255, 255, 255),
			Color.FromArgb(22, 255, 255, 255),
			Color.FromArgb(0, 255, 255, 255)
		};
		linearGradientBrush.InterpolationColors = colorBlend;
		using Region clip = g.Clip;
		g.SetClip(path, CombineMode.Intersect);
		g.FillRectangle(linearGradientBrush, rect);
		g.Clip = clip;
	}

	public static void Row(Graphics g, Rectangle r, Control owner = null, int icon = 38)
	{
		int num = Theme.S(icon);
		Bar(g, new RectangleF(r.X + Theme.S(8f), r.Y + (r.Height - num) / 2, num, num), owner);
		float x = r.X + Theme.S(8f) + num + Theme.S(14f);
		Bar(g, new RectangleF(x, r.Y + r.Height / 2 - Theme.S(16f), Math.Min(Theme.S(240f), r.Width / 3), Theme.S(13f)), owner);
		Bar(g, new RectangleF(x, r.Y + r.Height / 2 + Theme.S(5f), Math.Min(Theme.S(170f), r.Width / 4), Theme.S(10f)), owner);
	}
}
