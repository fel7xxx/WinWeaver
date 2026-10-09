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
using System.Windows.Forms;

namespace WinWeaver
{
	// loading placeholders: soft bars with one shared shimmer wave (the same phase everywhere, so nothing flickers)
	internal static class Skeleton
	{
		private static readonly HashSet<Control> _live = new HashSet<Control>();

		private static Timer _t;

		public static float Phase => (float)(Environment.TickCount % 1600) / 1600f;

		// repaint c at ~30 fps while it shows placeholders
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
					_t = new Timer { Interval = 33 };
					_t.Tick += delegate
					{
						List<Control> dead = new List<Control>();
						foreach (Control x in _live)
						{
							if (x.IsDisposed)
							{
								dead.Add(x);
							}
							else if (x.Visible)
							{
								x.Invalidate();
							}
						}
						foreach (Control d in dead)
						{
							_live.Remove(d);
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
			using (GraphicsPath p = Theme.Round(r, Math.Min(r.Height / 2f, Theme.S(7f))))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				if (!Anim.Enabled)
				{
					return;
				}
				// the wave moves across the whole window: x offset in screen space keeps neighbouring bars in sync
				int sx0 = 0;
				if (owner != null && owner.IsHandleCreated)
				{
					try
					{
						sx0 = owner.PointToScreen(Point.Empty).X;
					}
					catch
					{
					}
				}
				float span = Theme.S(1400f);
				float wave = Theme.S(260f);
				float cx = -wave + (span + wave * 2f) * Phase - sx0 % (int)span;
				RectangleF wr = new RectangleF(cx - wave / 2f, r.Y, wave, r.Height);
				if (wr.Right < r.X || wr.X > r.Right)
				{
					return;
				}
				using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(wr.X - 1f, wr.Y, wr.Width + 2f, Math.Max(1f, wr.Height)), Color.Transparent, Color.Transparent, 0f))
				{
					ColorBlend cb = new ColorBlend();
					cb.Positions = new float[3] { 0f, 0.5f, 1f };
					cb.Colors = new Color[3] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(22, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
					lb.InterpolationColors = cb;
					Region old = g.Clip;
					g.SetClip(p, CombineMode.Intersect);
					g.FillRectangle(lb, wr);
					g.Clip = old;
				}
			}
		}

		// one list row: round icon + title + subtitle
		public static void Row(Graphics g, Rectangle r, Control owner = null, int icon = 38)
		{
			int ic = Theme.S(icon);
			Bar(g, new RectangleF(r.X + Theme.S(8f), r.Y + (r.Height - ic) / 2, ic, ic), owner);
			float x = r.X + Theme.S(8f) + ic + Theme.S(14f);
			Bar(g, new RectangleF(x, r.Y + r.Height / 2 - Theme.S(16f), Math.Min(Theme.S(240f), r.Width / 3), Theme.S(13f)), owner);
			Bar(g, new RectangleF(x, r.Y + r.Height / 2 + Theme.S(5f), Math.Min(Theme.S(170f), r.Width / 4), Theme.S(10f)), owner);
		}
	}
}
