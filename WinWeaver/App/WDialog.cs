using System;

namespace WinWeaver;

internal class WDialog : Form, ISurface
{
	private readonly string _title;

	private readonly string _body;

	private readonly DlgIcon _icon;

	private readonly FButton _yes;

	private readonly FButton _no;

	private readonly FButton _mid;

	public static Icon AppIcon;

	private int _footerTop;

	private bool _closeHot;

	private Rectangle _iconR;

	private DlgChrome.Head _head;

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

	private void DoLayoutDlg()
	{
		int num = Theme.S((_mid != null) ? 580f : 520f);
		_head = DlgChrome.Layout(num, _title, _body);
		_iconR = _head.Icon;
		_footerTop = _head.Bottom + Theme.S(26f);
		base.Size = new Size(num, _footerTop + DlgChrome.FooterH);
		DlgChrome.PlaceButtons(num, _footerTop, _yes, _no, _mid);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		DlgChrome.Round(this);
		DlgChrome.FadeIn(this);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		bool flag = DlgChrome.CloseR(base.Width).Contains(e.Location);
		if (flag != _closeHot)
		{
			_closeHot = flag;
			Cursor = (flag ? Cursors.Hand : Cursors.Default);
			Invalidate(DlgChrome.CloseR(base.Width));
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
		if (DlgChrome.CloseR(base.Width).Contains(e.Location))
		{
			base.DialogResult = ((_no != null) ? DialogResult.No : DialogResult.Yes);
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
		Graphics graphics = e.Graphics;
		DlgChrome.Paint(graphics, base.Size, _icon, _footerTop, _closeHot);
		DlgChrome.DrawHead(graphics, _head, _icon, _title);
	}

	public void PaintSurface(Graphics g)
	{
		DlgChrome.Bg(g, base.Size, _icon, _footerTop);
	}

	private static DialogResult Run(IWin32Window owner, string title, string body, string yes, string no, DlgIcon icon, string mid)
	{
		using WDialog wDialog = new WDialog(title, body, yes, no, icon, mid);
		Form form = owner as Form;
		Dimmer dimmer = null;
		if (form != null && form.Visible && form.WindowState != FormWindowState.Minimized)
		{
			wDialog.Location = new Point(form.Left + (form.Width - wDialog.Width) / 2, form.Top + (form.Height - wDialog.Height) / 2);
			dimmer = new Dimmer(form);
			dimmer.Show(form);
		}
		else
		{
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			wDialog.StartPosition = FormStartPosition.Manual;
			wDialog.Location = new Point(workingArea.Left + (workingArea.Width - wDialog.Width) / 2, workingArea.Top + (workingArea.Height - wDialog.Height) / 2);
			wDialog.TopMost = true;
			wDialog.ShowInTaskbar = true;
		}
		try
		{
			IWin32Window win32Window = dimmer;
			return wDialog.ShowDialog(win32Window ?? owner);
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

	public static bool Ask(IWin32Window owner, string title, string body, string yes, string no, DlgIcon icon = DlgIcon.Question)
	{
		return Run(owner, title, body, yes, no, icon, null) == DialogResult.Yes;
	}

	public static int Choose(IWin32Window owner, string title, string body, string yes, string mid, string no, DlgIcon icon = DlgIcon.Question)
	{
		return Run(owner, title, body, yes, no, icon, mid) switch
		{
			DialogResult.Retry => 1, 
			DialogResult.Yes => 0, 
			_ => 2, 
		};
	}

	public static void Info(IWin32Window owner, string title, string body, DlgIcon icon = DlgIcon.Info)
	{
		Ask(owner, title, body, "Понятно", null, icon);
	}
}
