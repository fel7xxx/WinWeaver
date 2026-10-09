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
	// Shown over a page that has not been read yet: nothing is read in advance, the user reads a page
	// when it is needed. Card in the middle with a short explanation and the "read" button; a spinner while reading.
	internal class ReadGate : Control
	{
		private readonly FButton _btn;

		private readonly Timer _t = new Timer { Interval = 16 };

		private float _ang;

		private bool _busy;

		public event Action ReadClicked;

		public string PageTitle = "";

		public ReadGate()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
			BackColor = Theme.Bg;
			_btn = new FButton { Text = "Прочитать раздел", Kind = BtnKind.Silver, Font = Theme.Body, Glyph = Glyph.Refresh };
			_btn.AutoWidth(Theme.S(26f));
			_btn.Click += delegate
			{
				if (!_busy)
				{
					ReadClicked?.Invoke();
				}
			};
			Controls.Add(_btn);
			_t.Tick += delegate
			{
				_ang = (_ang + 7f) % 360f;
				Invalidate(SpinRect);
			};
		}

		public bool Busy
		{
			get
			{
				return _busy;
			}
			set
			{
				_busy = value;
				_btn.Visible = !value;
				if (value)
				{
					_t.Start();
				}
				else
				{
					_t.Stop();
				}
				Invalidate();
			}
		}

		private int CardW => Math.Min(Theme.S(480f), Width - Theme.S(40f));

		private int CardH => Theme.S(220f);

		private Rectangle CardRect => new Rectangle((Width - CardW) / 2, Math.Max(Theme.S(24f), Height / 2 - CardH / 2 - Theme.S(60f)), CardW, CardH);

		private Rectangle SpinRect
		{
			get
			{
				Rectangle c = CardRect;
				int s = Theme.S(28f);
				return new Rectangle(c.X + (c.Width - s) / 2 - 2, c.Bottom - Theme.S(58f) - 2, s + 4, s + 4);
			}
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			base.OnLayout(levent);
			Rectangle c = CardRect;
			_btn.Location = new Point(c.X + (c.Width - _btn.Width) / 2, c.Bottom - Theme.S(62f));
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			PerformLayout();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			Theme.Smooth(g);
			g.Clear(Theme.Bg);
			Rectangle c = CardRect;
			using (GraphicsPath p = Theme.Round(c, Theme.S(14f)))
			{
				Theme.FillGradient(g, p, c, Theme.Card1, Theme.Card2);
				using (Pen pen = new Pen(Theme.Border, 1f))
				{
					g.DrawPath(pen, p);
				}
			}
			int y = c.Y + Theme.S(24f);
			if (Theme.Icon != null)
			{
				Theme.DrawIcon(g, Glyph.Info, Theme.Icon, new Rectangle(c.X, y, c.Width, Theme.S(26f)), Theme.Sub);
			}
			y += Theme.S(34f);
			TextFormatFlags cf = TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak;
			TextRenderer.DrawText(g, _busy ? "Читаю раздел…" : "Раздел ещё не прочитан", Theme.Title, new Rectangle(c.X + Theme.S(20f), y, c.Width - Theme.S(40f), Theme.S(26f)), Theme.Text, cf | TextFormatFlags.SingleLine);
			y += Theme.S(30f);
			string sub = _busy ? "Смотрю текущие настройки в системе — обычно несколько секунд" : "Чтобы показать текущие настройки, программа прочитает их из системы. Обычно это занимает несколько секунд.";
			TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(c.X + Theme.S(28f), y, c.Width - Theme.S(56f), Theme.S(40f)), Theme.Sub, cf);
			if (_busy)
			{
				Rectangle s = SpinRect;
				s.Inflate(-2, -2);
				using (Pen tp = new Pen(Color.FromArgb(30, 255, 255, 255), Theme.S(2.5f)))
				{
					g.DrawEllipse(tp, s);
				}
				using (Pen ap = new Pen(Theme.Silver1, Theme.S(2.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				{
					g.DrawArc(ap, s, _ang, 100f);
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_t.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
