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
	internal class FButton : Control
	{
		public BtnKind Kind = BtnKind.Ghost;

		public string Glyph;

		private bool _hot;

		private bool _down;

		private readonly Tween _hotT;

		private readonly Tween _downT;

		public FButton()
		{
			_hotT = new Tween(this, 0f, 0.25f);
			_downT = new Tween(this, 0f, 0.35f);
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick | ControlStyles.Selectable, value: false);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			TabStop = false;
			base.Height = Theme.S(36f);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			_hot = true;
			_hotT.Target = 1f;
			Invalidate();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = false;
			_down = false;
			_hotT.Target = 0f;
			_downT.Target = 0f;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_down = true;
				_downT.Target = 1f;
				Invalidate();
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			bool click = _down && e.Button == MouseButtons.Left && base.ClientRectangle.Contains(e.Location);
			_down = false;
			_downT.Target = 0f;
			Invalidate();
			base.OnMouseUp(e);
			if (click && base.Enabled)
			{
				OnClick(EventArgs.Empty);
			}
		}

		public void PerformClick()
		{
			if (base.Enabled && base.Visible)
			{
				OnClick(EventArgs.Empty);
			}
		}

		protected override void OnTextChanged(EventArgs e)
		{
			Invalidate();
			base.OnTextChanged(e);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			PaintParent(this, e.Graphics);
		}

		internal static void PaintParent(Control c, Graphics g)
		{
			Control parent = c.Parent;
			ISurface surface = parent as ISurface;
			if (surface != null)
			{
				GraphicsState state = g.Save();
				try
				{
					g.TranslateTransform(-c.Left, -c.Top);
					surface.PaintSurface(g);
				}
				finally
				{
					g.Restore(state);
				}
				return;
			}
			Color color = Theme.Bg;
			for (Control p = parent; p != null; p = p.Parent)
			{
				if (p.BackColor.A == 255)
				{
					color = p.BackColor;
					break;
				}
			}
			g.Clear(color);
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			_hot = false;
			_down = false;
			_hotT.Snap(0f);
			_downT.Snap(0f);
			Invalidate();
			base.OnEnabledChanged(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			float inset = _downT.Value * 1.2f;
			RectangleF r = new RectangleF(0.5f + inset, 0.5f + inset, (float)base.Width - 1f - inset * 2f, (float)base.Height - 1f - inset * 2f);
			float t = _hotT.Value * 0.08f + _downT.Value * 0.08f;
			Color c;
			Color c2;
			Color color;
			Color foreColor;
			if (!base.Enabled)
			{
				c = Theme.Control2;
				c2 = Theme.Control2;
				color = Theme.Border;
				foreColor = Theme.Dim;
			}
			else if (Kind == BtnKind.Silver)
			{
				c = Theme.Mix(Theme.Silver1, Color.White, t);
				c2 = Theme.Mix(Theme.Silver2, Color.FromArgb(120, 120, 132), t);
				color = Color.FromArgb(210, 210, 220);
				foreColor = Theme.OnSilver;
			}
			else if (Kind == BtnKind.Danger)
			{
				c = Theme.Mix(Color.FromArgb(70, 44, 46), Color.FromArgb(90, 54, 56), t);
				c2 = Theme.Mix(Color.FromArgb(52, 34, 36), Color.FromArgb(70, 42, 44), t);
				color = Color.FromArgb(120, 70, 72);
				foreColor = Theme.Red;
			}
			else
			{
				c = Theme.Mix(Theme.Control1, Theme.BorderHi, t);
				c2 = Theme.Mix(Theme.Control2, Theme.Control1, t);
				color = Theme.Mix(Theme.Border, Theme.BorderHi, _hotT.Value);
				foreColor = Theme.Text;
			}
			using (GraphicsPath path = Theme.Round(r, Theme.S(8f)))
			{
				Theme.FillGradient(graphics, path, r, c, c2);
				using (Pen pen = new Pen(color))
				{
					graphics.DrawPath(pen, path);
				}
			}
			string glyph = Glyph;
			Font iconSmall = Theme.IconSmall;
			TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
			int padX = Theme.S(10f);
			if (glyph != null && iconSmall != null)
			{
				int gw = Theme.Measure(glyph, iconSmall).Width;
				int gap = Theme.S(7f);
				int tw = Theme.Measure(Text, Font).Width;
				int x = Math.Max(padX, (base.Width - (gw + gap + tw)) / 2);
				TextRenderer.DrawText(graphics, glyph, iconSmall, new Rectangle(x, 0, gw + 2, base.Height), foreColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
				x += gw + gap;
				TextRenderer.DrawText(graphics, Text, Font, new Rectangle(x, 0, Math.Max(1, base.Width - x - padX), base.Height), foreColor, flags);
			}
			else
			{
				TextRenderer.DrawText(graphics, Text, Font, new Rectangle(padX, 0, Math.Max(1, base.Width - padX * 2), base.Height), foreColor, flags | TextFormatFlags.HorizontalCenter);
			}
		}

		public void AutoWidth(int pad)
		{
			int num = ((Glyph != null && Theme.IconSmall != null) ? (Theme.Measure(Glyph, Theme.IconSmall).Width + Theme.S(7f)) : 0);
			base.Width = Theme.Measure(Text, Font).Width + num + pad + Theme.S(4f);
		}
	}
}
