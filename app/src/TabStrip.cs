// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	// Sub-tabs of a page: capsule with a sliding silver pill under the current tab
	internal class TabStrip : Control, IBlock
	{
		public readonly string[] Items;

		public int Selected;

		private int _hover = -1;

		private Rectangle[] _r = new Rectangle[0];

		private readonly Tween _pos;

		private readonly Tween _hov;

		public event Action<int> Changed;

		public TabStrip(string[] items)
		{
			Items = items ?? new string[0];
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			_pos = new Tween(this, 0f, 0.24f);
			_hov = new Tween(this, 0f, 0.25f);
		}

		public void Select(int i)
		{
			Selected = i;
			_pos.Target = i;
			Invalidate();
		}

		public int DoLayout(int x, int y, int w)
		{
			int h = Theme.S(40f);
			int pad = Theme.S(4f);
			_r = new Rectangle[Items.Length];
			int cx = pad;
			for (int i = 0; i < Items.Length; i++)
			{
				int tw = Theme.Measure(Items[i], Theme.Body).Width + Theme.S(36f);
				_r[i] = new Rectangle(cx, pad, tw, h - pad * 2);
				cx += tw + Theme.S(2f);
			}
			SetBounds(x, y, Math.Min(w, cx + pad - Theme.S(2f)), h);
			Invalidate();
			return h + Theme.S(4f);
		}

		private RectangleF Pill()
		{
			if (_r.Length == 0)
			{
				return RectangleF.Empty;
			}
			float p = Math.Max(0f, Math.Min(_r.Length - 1, _pos.Value));
			int a = (int)Math.Floor(p);
			int b = Math.Min(_r.Length - 1, a + 1);
			float t = p - a;
			Rectangle ra = _r[a];
			Rectangle rb = _r[b];
			return new RectangleF(ra.X + (rb.X - ra.X) * t, ra.Y, ra.Width + (rb.Width - ra.Width) * t, ra.Height);
		}

		// paint the page header gradient ourselves instead of the default "transparent" background:
		// the default re-runs the whole parent paint (page title text included) under the strip on every frame
		protected override void OnPaintBackground(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			if (!(Parent is Canvas))
			{
				FButton.PaintParent(this, g);
				return;
			}
			g.Clear(Theme.Bg);
			int gh = Theme.S(280f);
			int pw = Math.Max(1, Parent.Width);
			if (Top < gh)
			{
				using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(-Left, -1 - Top, pw, gh + 2), Color.FromArgb(30, 30, 35), Theme.Bg, 90f))
				{
					g.FillRectangle(brush, new Rectangle(-Left, -Top, pw, gh));
				}
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			Theme.Smooth(g);
			RectangleF bg = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
			using (GraphicsPath path = Theme.Round(bg, bg.Height / 2f))
			{
				Theme.FillGradient(g, path, bg, Color.FromArgb(32, 32, 38), Color.FromArgb(25, 25, 30));
				using (Pen pen = new Pen(Theme.Border))
				{
					g.DrawPath(pen, path);
				}
			}
			if (_hover >= 0 && _hover < _r.Length && _hover != Selected)
			{
				using (GraphicsPath hp = Theme.Round(_r[_hover], _r[_hover].Height / 2f))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
					{
						g.FillPath(b, hp);
					}
				}
			}
			RectangleF pr = Pill();
			if (pr.Width > 0f)
			{
				using (GraphicsPath pp = Theme.Round(pr, pr.Height / 2f))
				{
					Theme.FillGradient(g, pp, pr, Theme.Silver1, Theme.Silver2);
				}
			}
			for (int i = 0; i < _r.Length; i++)
			{
				float near = 1f - Math.Min(1f, Math.Abs(_pos.Value - i));
				Color c = Theme.Mix((i == _hover) ? Theme.Text : Theme.Sub, Theme.OnSilver, near);
				TextRenderer.DrawText(g, Items[i], (near > 0.5f) ? Theme.Title : Theme.Body, _r[i], c, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
			}
		}

		private int Hit(Point p)
		{
			for (int i = 0; i < _r.Length; i++)
			{
				if (_r[i].Contains(p))
				{
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = Hit(e.Location);
			if (h != _hover)
			{
				_hover = h;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hover = -1;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			int h = Hit(e.Location);
			if (e.Button == MouseButtons.Left && h >= 0 && h != Selected)
			{
				Changed?.Invoke(h);
			}
			base.OnMouseDown(e);
		}

		public object Fetch(IEngine e)
		{
			return null;
		}

		public void Apply(object v)
		{
		}
	}
}
