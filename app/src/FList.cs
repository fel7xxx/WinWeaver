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
	internal class FList : Surface
	{
		public List<Row> Rows = new List<Row>();

		public int[] Cols = new int[0];

		public string[] Headers;

		public bool SingleSelect = true;

		public int Selected = -1;

		private int _hot = -1;

		private int _scroll;

		private const int RH = 40;

		private const int HEAD = 30;

		private int RowH => Theme.S(40f);

		private int HeadH
		{
			get
			{
				if (Headers == null)
				{
					return 0;
				}
				return Theme.S(30f);
			}
		}

		private int ViewTop => HeadH;

		private int TotalH => Rows.Count * RowH;

		private int MaxScroll => Math.Max(0, TotalH - (base.Height - ViewTop));

		public int ContentHeight => HeadH + Math.Max(1, Rows.Count) * RowH + Theme.S(6f);

		public Row SelectedRow
		{
			get
			{
				if (Selected < 0 || Selected >= Rows.Count)
				{
					return null;
				}
				return Rows[Selected];
			}
		}

		public event EventHandler SelectionChanged;

		public event EventHandler<Row> RowToggled;

		public event EventHandler DataChanged;

		public FList()
		{
			BackColor = Theme.Card2;
		}

		public void SetData(List<Row> rows)
		{
			Rows = rows ?? new List<Row>();
			Selected = -1;
			_scroll = 0;
			Invalidate();
			this.DataChanged?.Invoke(this, EventArgs.Empty);
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (MaxScroll <= 0)
			{
				base.OnMouseWheel(e);
				return;
			}
			_scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - e.Delta));
			Invalidate();
			if (e is HandledMouseEventArgs)
			{
				((HandledMouseEventArgs)e).Handled = true;
			}
		}

		private int HitRow(int y)
		{
			if (y < ViewTop)
			{
				return -1;
			}
			int num = (y - ViewTop + _scroll) / RowH;
			if (num < 0 || num >= Rows.Count)
			{
				return -1;
			}
			return num;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int num = HitRow(e.Y);
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
			int num = HitRow(e.Y);
			if (num < 0)
			{
				return;
			}
			Row row = Rows[num];
			if (row.Toggleable)
			{
				int y = ViewTop + num * RowH - _scroll;
				if (new Rectangle(base.Width - Theme.S(60f), y, Theme.S(52f), RowH).Contains(e.Location))
				{
					row.On = !row.On;
					Invalidate();
					this.RowToggled?.Invoke(this, row);
					return;
				}
			}
			if (row.Checkable)
			{
				row.Checked = !row.Checked;
			}
			Selected = num;
			Invalidate();
			this.SelectionChanged?.Invoke(this, EventArgs.Empty);
		}

		private int[] ColX(int w)
		{
			int num = Cols.Length;
			int[] array = new int[num + 1];
			int num2 = 0;
			int[] cols = Cols;
			foreach (int num3 in cols)
			{
				num2 += num3;
			}
			if (num2 == 0)
			{
				num2 = 1;
			}
			int num4 = Theme.S(14f);
			bool flag = Rows.Exists((Row r) => r.Toggleable);
			int num5 = w - num4 - (flag ? Theme.S(64f) : 0);
			int num6 = num5 - num4;
			int num7 = num4;
			for (int num8 = 0; num8 < num; num8++)
			{
				array[num8] = num7;
				num7 += num6 * Cols[num8] / num2;
			}
			array[num] = num5;
			return array;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
			using (GraphicsPath graphicsPath = Theme.Round(r, Theme.S(10f)))
			{
				Theme.FillGradient(graphics, graphicsPath, r, Theme.Card1, Theme.Card2);
				using (Pen pen = new Pen(Theme.Border))
				{
					graphics.DrawPath(pen, graphicsPath);
				}
				graphics.SetClip(graphicsPath);
			}
			int[] array = ((Cols.Length != 0) ? ColX(base.Width) : null);
			if (Headers != null && array != null)
			{
				Rectangle rectangle = new Rectangle(0, 0, base.Width, HeadH);
				for (int i = 0; i < Headers.Length && i < Cols.Length; i++)
				{
					TextRenderer.DrawText(graphics, Headers[i], Theme.H2, new Rectangle(array[i], rectangle.Y, array[i + 1] - array[i] - Theme.S(8f), HeadH), Theme.Sub, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
				}
				using (Pen pen2 = new Pen(Theme.Border))
				{
					graphics.DrawLine(pen2, Theme.S(10f), HeadH, base.Width - Theme.S(10f), HeadH);
				}
			}
			if (Rows.Count == 0)
			{
				TextRenderer.DrawText(graphics, "Список пуст", Theme.Body, new Rectangle(0, ViewTop, base.Width, RowH), Theme.Dim, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				return;
			}
			for (int j = 0; j < Rows.Count; j++)
			{
				int num = ViewTop + j * RowH - _scroll;
				if (num + RowH < ViewTop || num > base.Height)
				{
					continue;
				}
				Row row = Rows[j];
				Rectangle rectangle2 = new Rectangle(Theme.S(4f), num, base.Width - Theme.S(8f), RowH - Theme.S(4f));
				if (j == Selected)
				{
					using (GraphicsPath path = Theme.Round(new RectangleF(rectangle2.X, rectangle2.Y + 1, rectangle2.Width, rectangle2.Height - 1), Theme.S(7f)))
					{
						Theme.FillGradient(graphics, path, rectangle2, Color.FromArgb(58, 58, 68), Color.FromArgb(44, 44, 52));
						using (Pen pen3 = new Pen(Theme.BorderHi))
						{
							graphics.DrawPath(pen3, path);
						}
					}
				}
				else if (j == _hot)
				{
					using (GraphicsPath path2 = Theme.Round(new RectangleF(rectangle2.X, rectangle2.Y + 1, rectangle2.Width, rectangle2.Height - 1), Theme.S(7f)))
					{
						using (SolidBrush brush = new SolidBrush(Color.FromArgb(36, 255, 255, 255)))
						{
							graphics.FillPath(brush, path2);
						}
					}
				}
				int num2 = ((array != null) ? array[0] : Theme.S(14f));
				if (row.Checkable)
				{
					Rectangle rectangle3 = new Rectangle(Theme.S(14f), num + (RowH - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
					using (GraphicsPath path3 = Theme.Round(rectangle3, Theme.S(4f)))
					{
						using (SolidBrush brush2 = new SolidBrush(row.Checked ? Theme.Silver1 : Theme.Control2))
						{
							graphics.FillPath(brush2, path3);
						}
						using (Pen pen4 = new Pen(row.Checked ? Theme.Silver1 : Theme.BorderHi))
						{
							graphics.DrawPath(pen4, path3);
						}
					}
					if (row.Checked && Theme.IconSmall != null)
					{
						TextRenderer.DrawText(graphics, "\ue73e", Theme.IconSmall, rectangle3, Theme.OnSilver, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
					}
					num2 = rectangle3.Right + Theme.S(10f);
				}
				for (int k = 0; k < row.Cells.Length; k++)
				{
					int num3 = ((array != null && k < Cols.Length) ? ((k == 0) ? num2 : array[k]) : (num2 + k * Theme.S(160f)));
					int val = ((array != null && k < Cols.Length) ? (array[k + 1] - num3 - Theme.S(8f)) : Theme.S(150f));
					Color foreColor = ((k == 0) ? Theme.Text : (row.Accent ?? Theme.Sub));
					TextRenderer.DrawText(graphics, row.Cells[k], (k == 0) ? Theme.Body : Theme.Small, new Rectangle(num3, num, Math.Max(0, val), RowH - Theme.S(4f)), foreColor, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
				}
				if (!row.Toggleable)
				{
					continue;
				}
				RectangleF r2 = new RectangleF(base.Width - Theme.S(58f), (float)num + (float)(RowH - Theme.S(24f)) / 2f, Theme.S(46f), Theme.S(24f));
				using (GraphicsPath path4 = Theme.Round(r2, r2.Height / 2f))
				{
					Theme.FillGradient(graphics, path4, r2, row.On ? Color.FromArgb(230, 230, 236) : Theme.Control1, row.On ? Color.FromArgb(176, 176, 188) : Theme.Control2);
				}
				float num4 = r2.Height - (float)Theme.S(6f);
				using (GraphicsPath path5 = Theme.Round(new RectangleF(row.On ? (r2.Right - num4 - (float)Theme.S(3f)) : (r2.X + (float)Theme.S(3f)), r2.Y + (float)Theme.S(3f), num4, num4), num4 / 2f))
				{
					using (SolidBrush brush3 = new SolidBrush(row.On ? Theme.OnSilver : Color.FromArgb(200, 200, 208)))
					{
						graphics.FillPath(brush3, path5);
					}
				}
			}
			if (MaxScroll <= 0)
			{
				return;
			}
			int num5 = base.Height - ViewTop;
			int num6 = Math.Max(Theme.S(30f), num5 * num5 / TotalH);
			int num7 = ViewTop + (num5 - num6) * _scroll / MaxScroll;
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(90, 150, 150, 160)))
			{
				using (GraphicsPath path6 = Theme.Round(new RectangleF(base.Width - Theme.S(6f), num7, Theme.S(4f), num6), Theme.S(2f)))
				{
					graphics.FillPath(brush4, path6);
				}
			}
		}
	}
}
