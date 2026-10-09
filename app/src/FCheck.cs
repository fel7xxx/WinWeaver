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
	internal class FCheck : Control
	{
		public bool Checked = true;

		public FCheck(string text)
		{
			Text = text;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			Font = Theme.Small;
			base.Height = Theme.S(26f);
			base.Width = Theme.Measure(text, Theme.Small).Width + Theme.S(34f);
		}

		protected override void OnClick(EventArgs e)
		{
			Checked = !Checked;
			Invalidate();
			base.OnClick(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			Rectangle rectangle = new Rectangle(1, (base.Height - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
			using (GraphicsPath path = Theme.Round(rectangle, Theme.S(4f)))
			{
				using (SolidBrush brush = new SolidBrush(Checked ? Theme.Ice : Theme.Control2))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(Checked ? Theme.Ice : Theme.BorderHi))
				{
					graphics.DrawPath(pen, path);
				}
			}
			if (Checked)
			{
				if (Theme.IconSmall != null)
				{
					TextRenderer.DrawText(graphics, "\ue73e", Theme.IconSmall, rectangle, Theme.OnSilver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				}
				else
				{
					using (Pen pen2 = new Pen(Theme.OnSilver, Theme.S(2f)))
					{
						graphics.DrawLines(pen2, new Point[3]
						{
							new Point(rectangle.X + Theme.S(4f), rectangle.Y + Theme.S(9f)),
							new Point(rectangle.X + Theme.S(8f), rectangle.Y + Theme.S(13f)),
							new Point(rectangle.X + Theme.S(14f), rectangle.Y + Theme.S(5f))
						});
					}
				}
			}
			TextRenderer.DrawText(graphics, Text, Font, new Rectangle(rectangle.Right + Theme.S(10f), 0, base.Width - rectangle.Right - Theme.S(10f), base.Height), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}
}
