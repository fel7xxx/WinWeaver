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
using System.Globalization;
using System.Windows.Forms;

namespace WinWeaver
{
	// custom update pause: number + unit (days / weeks / months / years) with a live "until" preview
	internal class PauseDialog : Form, ISurface
	{
		private static CultureInfo Ru => L.Culture;

		private readonly TextBox _num = new TextBox();

		private readonly Segment _unit = new Segment { Items = new string[4] { "Дней", "Недель", "Месяцев", "Лет" } };

		private readonly FButton _yes;

		private readonly FButton _no;

		private DlgChrome.Head _head;

		private Rectangle _fieldR;

		private Rectangle _previewR;

		private int _footerTop;

		public int Days = -1;

		private const string Title0 = "Своя длительность паузы";

		private const string Body0 = "Укажи срок — обновления Windows (включая исправления безопасности) не будут скачиваться до этой даты.\n\nОчень длинную паузу крупное обновление Windows иногда сбрасывает — тогда просто поставь её снова.";

		private PauseDialog()
		{
			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.Manual;
			ShowInTaskbar = false;
			AutoScaleMode = AutoScaleMode.None;
			BackColor = Theme.Card2;
			KeyPreview = true;
			Text = Title0;
			if (WDialog.AppIcon != null)
			{
				Icon = WDialog.AppIcon;
			}
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			_num.BorderStyle = BorderStyle.None;
			_num.BackColor = Color.FromArgb(30, 30, 35);
			_num.ForeColor = Theme.Text;
			_num.Font = Theme.F(13f, semi: true);
			_num.MaxLength = 5;
			_num.Text = "2";
			_num.TextAlign = HorizontalAlignment.Center;
			_num.KeyPress += delegate(object s, KeyPressEventArgs e)
			{
				if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
				{
					e.Handled = true;
				}
			};
			_num.TextChanged += delegate
			{
				Recalc();
			};
			_num.GotFocus += delegate
			{
				Invalidate();
			};
			_num.LostFocus += delegate
			{
				Invalidate();
			};
			_unit.Selected = 2;
			_unit.Changed += delegate
			{
				Recalc();
			};
			_yes = new FButton { Text = "Поставить паузу", Kind = BtnKind.Silver, Font = Theme.Body };
			_yes.AutoWidth(Theme.S(44f));
			_yes.Click += delegate
			{
				if (Days > 0)
				{
					DialogResult = DialogResult.Yes;
					Close();
				}
			};
			_no = new FButton { Text = "Отмена", Kind = BtnKind.Ghost, Font = Theme.Body };
			_no.AutoWidth(Theme.S(44f));
			_no.Click += delegate
			{
				DialogResult = DialogResult.No;
				Close();
			};
			Controls.Add(_num);
			Controls.Add(_unit);
			Controls.Add(_yes);
			Controls.Add(_no);
			DoLayoutDlg();
			Recalc();
		}

		private DateTime Until
		{
			get
			{
				int n;
				if (!int.TryParse(_num.Text.Trim(), out n) || n <= 0)
				{
					return DateTime.MinValue;
				}
				DateTime now = DateTime.Now;
				try
				{
					switch (_unit.Selected)
					{
					case 0:
						return now.AddDays(n);
					case 1:
						return now.AddDays(n * 7.0);
					case 2:
						return now.AddMonths(n);
					default:
						return now.AddYears(n);
					}
				}
				catch
				{
					return DateTime.MinValue;
				}
			}
		}

		private const int MaxYear = 2077;

		private void Recalc()
		{
			DateTime u = Until;
			if (u != DateTime.MinValue && u.Year > MaxYear)
			{
				u = new DateTime(MaxYear, 12, 30, 23, 0, 0);
			}
			Days = (u == DateTime.MinValue) ? -1 : Math.Max(1, (int)Math.Round((u - DateTime.Now).TotalDays));
			_yes.Enabled = Days > 0;
			Invalidate(_previewR);
		}

		private void DoLayoutDlg()
		{
			int w = Theme.S(540f);
			_head = DlgChrome.Layout(w, Title0, Body0);
			int x = _head.Title.X;
			int y = _head.Bottom + Theme.S(18f);
			int fh = Theme.S(38f);
			_fieldR = new Rectangle(x, y, Theme.S(86f), fh);
			int ih = _num.PreferredHeight;
			_num.SetBounds(_fieldR.X + Theme.S(10f), _fieldR.Y + (fh - ih) / 2, _fieldR.Width - Theme.S(20f), ih);
			_unit.SetBounds(_fieldR.Right + Theme.S(10f), y + (fh - Theme.S(34f)) / 2, w - DlgChrome.M - (_fieldR.Right + Theme.S(10f)), Theme.S(34f));
			_previewR = new Rectangle(x, _fieldR.Bottom + Theme.S(12f), w - DlgChrome.M - x, Theme.S(24f));
			_footerTop = _previewR.Bottom + Theme.S(22f);
			Size = new Size(w, _footerTop + DlgChrome.FooterH);
			DlgChrome.PlaceButtons(w, _footerTop, _yes, _no);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			DlgChrome.Round(this);
			DlgChrome.FadeIn(this);
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			_num.Focus();
			_num.SelectAll();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				DialogResult = DialogResult.No;
				Close();
			}
			else if (e.KeyCode == Keys.Return && Days > 0)
			{
				e.SuppressKeyPress = true;
				DialogResult = DialogResult.Yes;
				Close();
			}
			base.OnKeyDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			DlgChrome.Paint(g, Size, DlgIcon.Question, _footerTop, false);
			DlgChrome.DrawHead(g, _head, DlgIcon.Question, Title0);
			Theme.Smooth(g);
			using (GraphicsPath p = Theme.Round(new RectangleF(_fieldR.X + 0.5f, _fieldR.Y + 0.5f, _fieldR.Width - 1f, _fieldR.Height - 1f), Theme.S(9f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 30, 35)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(_num.Focused ? Color.FromArgb(150, 178, 235) : Theme.BorderHi))
				{
					g.DrawPath(pen, p);
				}
			}
			DateTime u = DateTime.Now.AddDays(Math.Max(0, Days));
			string t;
			Color c;
			if (Days <= 0)
			{
				t = "Введи число больше нуля";
				c = Theme.Red;
			}
			else
			{
				bool cap = Until.Year > MaxYear;
				t = "Пауза до " + u.ToString("d MMMM yyyy", Ru) + " (" + u.ToString("dddd", Ru) + ")" + (cap ? " — дальше 2077 года нельзя" : "");
				c = Theme.Yellow;
			}
			TextRenderer.DrawText(g, t, Theme.Body, _previewR, c, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}

		public void PaintSurface(Graphics g)
		{
			DlgChrome.Bg(g, Size, DlgIcon.Question, _footerTop);
		}

		// returns the number of days or -1
		public static int Ask(Form owner)
		{
			using (PauseDialog d = new PauseDialog())
			{
				Dimmer dim = null;
				if (owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized)
				{
					d.Location = new Point(owner.Left + (owner.Width - d.Width) / 2, owner.Top + (owner.Height - d.Height) / 2);
					dim = new Dimmer(owner);
					dim.Show(owner);
				}
				try
				{
					return (d.ShowDialog((IWin32Window)dim ?? owner) == DialogResult.Yes) ? d.Days : -1;
				}
				finally
				{
					if (dim != null)
					{
						dim.Close();
						dim.Dispose();
					}
					owner?.Activate();
				}
			}
		}
	}
}
