// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class Canvas : Control
	{
		public Page Page;

		public int HeaderH => Theme.S(24f) + Theme.Measure(Page.Title, Theme.H1).Height + Theme.S(2f) + Theme.Measure("Ag", Theme.Body).Height + Theme.S(22f);

		public Canvas(Page p)
		{
			Page = p;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Theme.Bg;
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			Page.SmoothBy(-Page.WheelPx(e.Delta));
			if (e is HandledMouseEventArgs)
			{
				((HandledMouseEventArgs)e).Handled = true;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			int num = Theme.S(280f);
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, -1, base.Width, num + 2), Color.FromArgb(30, 30, 35), Theme.Bg, 90f))
			{
				graphics.FillRectangle(brush, 0, 0, base.Width, num);
			}
			int margin = Page.Margin;
			TextRenderer.DrawText(graphics, Page.Title, Theme.H1, new Point(margin - Theme.S(3f), Theme.S(24f)), Theme.Text, TextFormatFlags.NoPrefix);
			int height = Theme.Measure(Page.Title, Theme.H1).Height;
			TextRenderer.DrawText(graphics, Page.Subtitle, Theme.Body, new Point(margin - Theme.S(1f), Theme.S(24f) + height + Theme.S(2f)), Theme.Sub, TextFormatFlags.NoPrefix);
		}
	}
}
