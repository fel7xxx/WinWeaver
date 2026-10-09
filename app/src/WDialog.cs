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
	internal class WDialog : Form, ISurface
	{
		private readonly string _title;

		private readonly string _body;

		private readonly DlgIcon _icon;

		private readonly FButton _yes;

		private readonly FButton _no;

		private readonly FButton _mid;

				public static Icon AppIcon;

		private WDialog(string title, string body, string yes, string no, DlgIcon icon, string mid = null)
		{
			_title = title;
			_body = body;
			_icon = icon;
			base.FormBorderStyle = FormBorderStyle.None;
			base.StartPosition = FormStartPosition.Manual;
			base.ShowInTaskbar = false;
			base.AutoScaleMode = AutoScaleMode.None;
			BackColor = Theme.Card2;
			base.KeyPreview = true;
			Text = title;
			if (AppIcon != null)
			{
				base.Icon = AppIcon;
			}
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			_yes = new FButton
			{
				Text = yes,
				Kind = DlgChrome.YesKind(yes, icon),
				Font = Theme.Body
			};
			_yes.AutoWidth(Theme.S(44f));
			_yes.Click += delegate
			{
				base.DialogResult = DialogResult.Yes;
				Close();
			};
			base.Controls.Add(_yes);
			if (!string.IsNullOrEmpty(no))
			{
				_no = new FButton
				{
					Text = no,
					Kind = BtnKind.Ghost,
					Font = Theme.Body
				};
				_no.AutoWidth(Theme.S(44f));
				_no.Click += delegate
				{
					base.DialogResult = DialogResult.No;
					Close();
				};
				base.Controls.Add(_no);
			}
			if (!string.IsNullOrEmpty(mid))
			{
				_mid = new FButton
				{
					Text = mid,
					Kind = BtnKind.Ghost,
					Font = Theme.Body
				};
				_mid.AutoWidth(Theme.S(44f));
				_mid.Click += delegate
				{
					base.DialogResult = DialogResult.Retry;
					Close();
				};
				base.Controls.Add(_mid);
			}
			DoLayoutDlg();
		}

		private int _footerTop;

		private bool _closeHot;

		private Rectangle _iconR;

		private DlgChrome.Head _head;

		private void DoLayoutDlg()
		{
			int w = Theme.S(_mid != null ? 580f : 520f);
			_head = DlgChrome.Layout(w, _title, _body);
			_iconR = _head.Icon;
			_footerTop = _head.Bottom + Theme.S(26f);
			Size = new Size(w, _footerTop + DlgChrome.FooterH);
			DlgChrome.PlaceButtons(w, _footerTop, _yes, _no, _mid);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			DlgChrome.Round(this);
			DlgChrome.FadeIn(this);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			bool h = DlgChrome.CloseR(Width).Contains(e.Location);
			if (h != _closeHot)
			{
				_closeHot = h;
				Cursor = h ? Cursors.Hand : Cursors.Default;
				Invalidate(DlgChrome.CloseR(Width));
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (_closeHot)
			{
				_closeHot = false;
				Invalidate();
			}
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (DlgChrome.CloseR(Width).Contains(e.Location))
			{
				DialogResult = (_no != null) ? DialogResult.No : DialogResult.Yes;
				Close();
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				base.DialogResult = ((_no != null) ? DialogResult.No : DialogResult.Yes);
				Close();
			}
			else if (e.KeyCode == Keys.Return)
			{
				base.DialogResult = DialogResult.Yes;
				Close();
			}
			base.OnKeyDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			DlgChrome.Paint(g, Size, _icon, _footerTop, _closeHot);
			DlgChrome.DrawHead(g, _head, _icon, _title);
		}

		// buttons sit on the footer band: they ask their parent for the background under them
		public void PaintSurface(Graphics g)
		{
			DlgChrome.Bg(g, Size, _icon, _footerTop);
		}

		private static DialogResult Run(IWin32Window owner, string title, string body, string yes, string no, DlgIcon icon, string mid)
		{
			using (WDialog felDialog = new WDialog(title, body, yes, no, icon, mid))
			{
				Form form = owner as Form;
				Dimmer dimmer = null;
				if (form != null && form.Visible && form.WindowState != FormWindowState.Minimized)
				{
					felDialog.Location = new Point(form.Left + (form.Width - felDialog.Width) / 2, form.Top + (form.Height - felDialog.Height) / 2);
					dimmer = new Dimmer(form);
					dimmer.Show(form);
				}
				else
				{
					Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
					felDialog.StartPosition = FormStartPosition.Manual;
					felDialog.Location = new Point(workingArea.Left + (workingArea.Width - felDialog.Width) / 2, workingArea.Top + (workingArea.Height - felDialog.Height) / 2);
					felDialog.TopMost = true;
					felDialog.ShowInTaskbar = true;
				}
				try
				{
					IWin32Window win32Window = dimmer;
					return felDialog.ShowDialog(win32Window ?? owner);
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

		public static bool Ask(IWin32Window owner, string title, string body, string yes, string no, DlgIcon icon = DlgIcon.Question)
		{
			return Run(owner, title, body, yes, no, icon, null) == DialogResult.Yes;
		}

		// three buttons: returns 0 = yes, 1 = middle, 2 = no / Esc
		public static int Choose(IWin32Window owner, string title, string body, string yes, string mid, string no, DlgIcon icon = DlgIcon.Question)
		{
			DialogResult r = Run(owner, title, body, yes, no, icon, mid);
			return (r == DialogResult.Yes) ? 0 : ((r == DialogResult.Retry) ? 1 : 2);
		}

		public static void Info(IWin32Window owner, string title, string body, DlgIcon icon = DlgIcon.Info)
		{
			Ask(owner, title, body, "Понятно", null, icon);
		}
	}
}
