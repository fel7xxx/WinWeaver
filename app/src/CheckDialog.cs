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
	// WDialog with a list of check boxes (uninstall options, leftovers, cleanup categories)
	internal class CheckDialog : Form, ISurface
	{
		public class Row
		{
			public string Text;
			public string Hint;
			public bool Checked;
			public Rectangle R;
			// non-clickable section caption inside the list
			public bool Header;
			// small chip on the right (e.g. what the change does: "включить", "→ Значок")
			public string Tag;
		}

		private readonly string _title;

		private readonly string _body;

		private readonly DlgIcon _icon;

		private readonly Row[] _rows;

		private readonly FButton _yes;

		private readonly FButton _no;

		private Rectangle _titleR;

		private Rectangle _bodyR;

		private Rectangle _listR;

		private int _scroll;

		private int _hot = -1;

		private int RowH => Theme.S(_rows.Length > 0 && HasHints ? 44f : 34f);

		private int HeadH => Theme.S(30f);

		private int H(Row r) => r.Header ? HeadH : RowH;

		private int TotalH
		{
			get
			{
				int t = 0;
				foreach (Row r in _rows)
				{
					t += H(r);
				}
				return t;
			}
		}

		private bool HasHints
		{
			get
			{
				foreach (Row r in _rows)
				{
					if (!string.IsNullOrEmpty(r.Hint) && r.Hint.StartsWith("\n"))
					{
						return true;
					}
				}
				return false;
			}
		}

		private CheckDialog(string title, string body, Row[] rows, string yes, string no, DlgIcon icon)
		{
			_title = title;
			_body = body;
			_rows = rows ?? new Row[0];
			_icon = icon;
			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.Manual;
			ShowInTaskbar = false;
			AutoScaleMode = AutoScaleMode.None;
			BackColor = Theme.Card2;
			KeyPreview = true;
			Text = title;
			if (WDialog.AppIcon != null)
			{
				Icon = WDialog.AppIcon;
			}
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			_yes = new FButton { Text = yes, Kind = DlgChrome.YesKind(yes, icon), Font = Theme.Body };
			_yes.AutoWidth(Theme.S(44f));
			_yes.Click += delegate
			{
				DialogResult = DialogResult.Yes;
				Close();
			};
			Controls.Add(_yes);
			_no = new FButton { Text = no ?? "Отмена", Kind = BtnKind.Ghost, Font = Theme.Body };
			_no.AutoWidth(Theme.S(44f));
			_no.Click += delegate
			{
				DialogResult = DialogResult.No;
				Close();
			};
			Controls.Add(_no);
			DoLayoutDlg();
		}

		private int _footerTop;

		private bool _closeHot;

		private Rectangle _iconR;

		private DlgChrome.Head _head;

		private void DoLayoutDlg()
		{
			int w = Theme.S(_rows.Length > 12 ? 620f : (_rows.Length > 3 ? 560f : 500f));
			int m = DlgChrome.M;
			_head = DlgChrome.Layout(w, _title, _body);
			_iconR = _head.Icon;
			int ly = _head.Bottom + Theme.S(18f);
			int lh = Math.Min(TotalH, RowH * 8);
			_listR = new Rectangle(m, ly, w - m * 2, lh + ((_rows.Length > 0) ? Theme.S(8f) : 0));
			_footerTop = _listR.Bottom + Theme.S(22f);
			Size = new Size(w, _footerTop + DlgChrome.FooterH);
			DlgChrome.PlaceButtons(w, _footerTop, _yes, _no);
		}

		public void PaintSurface(Graphics g)
		{
			DlgChrome.Bg(g, Size, _icon, _footerTop);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (_closeHot || _hot >= 0)
			{
				_closeHot = false;
				_hot = -1;
				Invalidate();
			}
			base.OnMouseLeave(e);
		}

		private int MaxScroll => Math.Max(0, TotalH + Theme.S(8f) - _listR.Height);

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			DlgChrome.Round(this);
			DlgChrome.FadeIn(this);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				DialogResult = DialogResult.No;
				Close();
			}
			else if (e.KeyCode == Keys.Return)
			{
				DialogResult = DialogResult.Yes;
				Close();
			}
			base.OnKeyDown(e);
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			int old = _scroll;
			_scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - Math.Sign(e.Delta) * RowH));
			if (old != _scroll)
			{
				Invalidate();
			}
			base.OnMouseWheel(e);
		}

		private int HitRow(Point p)
		{
			if (!_listR.Contains(p))
			{
				return -1;
			}
			for (int i = 0; i < _rows.Length; i++)
			{
				if (!_rows[i].Header && _rows[i].R.Contains(p))
				{
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = HitRow(e.Location);
			bool ch = DlgChrome.CloseR(Width).Contains(e.Location);
			if (h != _hot || ch != _closeHot)
			{
				_hot = h;
				_closeHot = ch;
				Cursor = (h >= 0 || ch) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && DlgChrome.CloseR(Width).Contains(e.Location))
			{
				DialogResult = DialogResult.No;
				Close();
				return;
			}
			int h = HitRow(e.Location);
			if (h >= 0 && e.Button == MouseButtons.Left)
			{
				_rows[h].Checked = !_rows[h].Checked;
				Invalidate();
			}
			base.OnMouseClick(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			DlgChrome.Paint(g, Size, _icon, _footerTop, _closeHot);
			DlgChrome.DrawHead(g, _head, _icon, _title);
			Theme.Smooth(g);
			if (_rows.Length == 0)
			{
				return;
			}
			using (GraphicsPath lp = Theme.Round(_listR, Theme.S(10f)))
			{
				using (SolidBrush lb = new SolidBrush(Color.FromArgb(70, 12, 12, 15)))
				{
					g.FillPath(lb, lp);
				}
				using (Pen lpen = new Pen(Color.FromArgb(34, 255, 255, 255)))
				{
					g.DrawPath(lpen, lp);
				}
			}
			Region old = g.Clip;
			g.SetClip(new Rectangle(_listR.X, _listR.Y + Theme.S(4f), _listR.Width, _listR.Height - Theme.S(8f)));
			int y = _listR.Y + Theme.S(4f) - _scroll;
			for (int i = 0; i < _rows.Length; i++)
			{
				Row r = _rows[i];
				r.R = new Rectangle(_listR.X + Theme.S(4f), y, _listR.Width - Theme.S(8f), H(r));
				if (r.Header)
				{
					int cnt = 0;
					int on = 0;
					for (int j = i + 1; j < _rows.Length && !_rows[j].Header; j++)
					{
						cnt++;
						if (_rows[j].Checked)
						{
							on++;
						}
					}
					TextRenderer.DrawText(g, r.Text.ToUpperInvariant(), Theme.Tiny, new Rectangle(r.R.X + Theme.S(10f), r.R.Y + Theme.S(8f), r.R.Width, r.R.Height), Theme.Dim, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
					TextRenderer.DrawText(g, on + " из " + cnt, Theme.Tiny, new Rectangle(r.R.X, r.R.Y + Theme.S(8f), r.R.Width - Theme.S(10f), r.R.Height), Theme.Dim, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right);
					if (i > 0)
					{
						using (Pen sp = new Pen(Color.FromArgb(24, 255, 255, 255)))
						{
							g.DrawLine(sp, r.R.X + Theme.S(10f), r.R.Y + Theme.S(2f), r.R.Right - Theme.S(10f), r.R.Y + Theme.S(2f));
						}
					}
					y += H(r);
					continue;
				}
				if (i == _hot)
				{
					using (GraphicsPath hp = Theme.Round(r.R, Theme.S(8f)))
					{
						using (SolidBrush hb = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
						{
							g.FillPath(hb, hp);
						}
					}
				}
				Rectangle cb = new Rectangle(r.R.X + Theme.S(10f), r.R.Y + (RowH - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
				using (GraphicsPath cp = Theme.Round(cb, Theme.S(4f)))
				{
					using (SolidBrush cbb = new SolidBrush(r.Checked ? Theme.Silver1 : Theme.Control2))
					{
						g.FillPath(cbb, cp);
					}
					using (Pen cpen = new Pen(r.Checked ? Theme.Silver1 : Theme.BorderHi))
					{
						g.DrawPath(cpen, cp);
					}
				}
				if (r.Checked)
				{
					using (Pen ck = new Pen(Theme.OnSilver, Math.Max(1.6f, Theme.S(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
					{
						g.DrawLines(ck, new PointF[3]
						{
							new PointF(cb.X + cb.Width * 0.24f, cb.Y + cb.Height * 0.52f),
							new PointF(cb.X + cb.Width * 0.43f, cb.Y + cb.Height * 0.7f),
							new PointF(cb.X + cb.Width * 0.77f, cb.Y + cb.Height * 0.32f)
						});
					}
				}
				int tx = cb.Right + Theme.S(12f);
				string hint = r.Hint ?? "";
				bool below = hint.StartsWith("\n");
				string right = below ? "" : hint;
				int rw = (right != "") ? (Theme.Measure(right, Theme.Small).Width + Theme.S(8f)) : 0;
				if (!string.IsNullOrEmpty(r.Tag))
				{
					int pw = Theme.PillW(r.Tag, Theme.Tiny);
					Theme.Pill(g, new Rectangle(r.R.Right - Theme.S(10f) - pw, r.R.Y + (r.R.Height - Theme.S(20f)) / 2, pw, Theme.S(20f)), r.Tag, Theme.Tiny, r.Checked ? Theme.Silver1 : Theme.Dim);
					rw = Math.Max(rw, pw + Theme.S(10f));
				}
				int tw = r.R.Right - Theme.S(10f) - rw - tx;
				if (below)
				{
					TextRenderer.DrawText(g, r.Text, Theme.Body, new Rectangle(tx, r.R.Y + Theme.S(5f), tw, Theme.S(20f)), Theme.Text, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
					TextRenderer.DrawText(g, hint.Substring(1), Theme.Tiny, new Rectangle(tx, r.R.Y + Theme.S(24f), tw, Theme.S(16f)), Theme.Dim, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}
				else
				{
					TextRenderer.DrawText(g, r.Text, Theme.Body, new Rectangle(tx, r.R.Y, tw, RowH), r.Checked ? Theme.Text : Theme.Sub, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
				}
				if (right != "")
				{
					TextRenderer.DrawText(g, right, Theme.Small, new Rectangle(r.R.Right - Theme.S(10f) - rw, r.R.Y, rw, RowH), Theme.Sub, TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
				}
				y += H(r);
			}
			g.Clip = old;
			old.Dispose();
			if (MaxScroll > 0)
			{
				int trackH = _listR.Height - Theme.S(16f);
				int thumbH = Math.Max(Theme.S(30f), trackH * _listR.Height / (TotalH + Theme.S(8f)));
				int ty = _listR.Y + Theme.S(8f) + (trackH - thumbH) * _scroll / MaxScroll;
				using (GraphicsPath sp = Theme.Round(new Rectangle(_listR.Right - Theme.S(7f), ty, Theme.S(4f), thumbH), Theme.S(2f)))
				{
					using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
					{
						g.FillPath(sb, sp);
					}
				}
			}
		}

		// returns the new check states, or null when cancelled
		public static bool[] Ask(IWin32Window owner, string title, string body, Row[] rows, string yes, string no = "Отмена", DlgIcon icon = DlgIcon.Question)
		{
			using (CheckDialog d = new CheckDialog(title, body, rows, yes, no, icon))
			{
				Form form = owner as Form;
				Dimmer dimmer = null;
				if (form != null && form.Visible && form.WindowState != FormWindowState.Minimized)
				{
					d.Location = new Point(form.Left + (form.Width - d.Width) / 2, form.Top + (form.Height - d.Height) / 2);
					dimmer = new Dimmer(form);
					dimmer.Show(form);
				}
				else
				{
					Rectangle wa = Screen.PrimaryScreen.WorkingArea;
					d.Location = new Point(wa.Left + (wa.Width - d.Width) / 2, wa.Top + (wa.Height - d.Height) / 2);
					d.TopMost = true;
				}
				try
				{
					if (d.ShowDialog((IWin32Window)dimmer ?? owner) != DialogResult.Yes)
					{
						return null;
					}
					bool[] r = new bool[d._rows.Length];
					for (int i = 0; i < r.Length; i++)
					{
						r[i] = d._rows[i].Checked;
					}
					return r;
				}
				finally
				{
					if (dimmer != null)
					{
						dimmer.Close();
						dimmer.Dispose();
					}
					form?.Activate();
				}
			}
		}
	}
}
