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
	internal class Segment : Control
	{
		public string[] Items = new string[0];

		private int _sel = -1;

		private int _hot = -1;

		private readonly Tween _pos;

		public int Selected
		{
			get
			{
				return _sel;
			}
			set
			{
				if (_sel < 0 || value < 0)
				{
					_pos.Snap(Math.Max(0, value));
				}
				else
				{
					_pos.Target = value;
				}
				_sel = value;
				Invalidate();
			}
		}

		public bool Busy { get; set; }

		private int SegW
		{
			get
			{
				if (Items.Length == 0)
				{
					return base.Width;
				}
				return base.Width / Items.Length;
			}
		}

		public event EventHandler<int> Changed;

		public Segment()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			base.Height = Theme.S(34f);
			Cursor = Cursors.Hand;
			_pos = new Tween(this, 0f, 0.24f);
		}

		public void SimSelect(int i)
		{
			if (!Busy && i != _sel)
			{
				Selected = i;
				Invalidate();
				this.Changed?.Invoke(this, i);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int num = ((SegW > 0) ? (e.X / SegW) : (-1));
			if (num != _hot)
			{
				_hot = num;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = -1;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (!Busy)
			{
				int num = ((SegW > 0) ? (e.X / SegW) : (-1));
				if (num >= 0 && num < Items.Length && num != _sel)
				{
					Selected = num;
					Invalidate();
					this.Changed?.Invoke(this, num);
				}
			}
			base.OnMouseDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
			using (GraphicsPath path = Theme.Round(r, Theme.S(8f)))
			{
				Theme.FillGradient(graphics, path, r, Theme.Control2, Color.FromArgb(34, 34, 40));
				using (Pen pen = new Pen(Theme.Border))
				{
					graphics.DrawPath(pen, path);
				}
			}
			int segW = SegW;
			if (_sel >= 0 && Items.Length > 0)
			{
				RectangleF r2 = new RectangleF(_pos.Value * (float)segW + (float)Theme.S(3f), Theme.S(3f), segW - Theme.S(6f), base.Height - Theme.S(6f));
				using (GraphicsPath path2 = Theme.Round(r2, Theme.S(6f)))
				{
					Theme.FillGradient(graphics, path2, r2, Theme.Silver1, Theme.Silver2);
				}
			}
			for (int i = 0; i < Items.Length; i++)
			{
				Rectangle bounds = new Rectangle(i * segW, 0, (i == Items.Length - 1) ? (base.Width - i * segW) : segW, base.Height);
				float near = Math.Max(0f, 1f - Math.Abs(_pos.Value - (float)i));
				Color foreColor = ((_sel < 0) ? ((i == _hot) ? Theme.Text : Theme.Sub) : Theme.Mix((i == _hot) ? Theme.Text : Theme.Sub, Theme.OnSilver, near));
				TextRenderer.DrawText(graphics, Items[i], Theme.Small, bounds, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			}
		}
	}
}
