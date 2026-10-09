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
	internal class SectionLabel : Control, IBlock
	{
		// optional muted note on the right side of the line
		public string Hint;

		public SectionLabel(string t)
		{
			Text = t;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
		}

		public int DoLayout(int x, int y, int w)
		{
			int num = Theme.S(34f);
			SetBounds(x, y + Theme.S(6f), w, num - Theme.S(6f));
			return num;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			Size size = Theme.Measure(Text.ToUpperInvariant(), Theme.H2);
			TextRenderer.DrawText(graphics, Text.ToUpperInvariant(), Theme.H2, new Point(Theme.S(4f), (base.Height - size.Height) / 2), Theme.Sub, TextFormatFlags.NoPrefix);
			int num = size.Width + Theme.S(16f);
			int y = base.Height / 2;
			int right = base.Width;
			if (!string.IsNullOrEmpty(Hint))
			{
				Size hs = Theme.Measure(Hint, Theme.Tiny);
				if (num + hs.Width + Theme.S(40f) < base.Width)
				{
					TextRenderer.DrawText(graphics, Hint, Theme.Tiny, new Point(base.Width - hs.Width - Theme.S(4f), (base.Height - hs.Height) / 2), Theme.Dim, TextFormatFlags.NoPrefix);
					right = base.Width - hs.Width - Theme.S(16f);
				}
			}
			if (num < right)
			{
				using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(num, y, Math.Max(2, right - num), 2), Theme.Border, Color.FromArgb(0, Theme.Border), 0f))
				{
					graphics.FillRectangle(brush, num, y, right - num, 1);
				}
			}
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
