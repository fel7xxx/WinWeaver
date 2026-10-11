using System;

namespace WinWeaver;

internal class PanelDialog : Form, ISurface
{
	private class SurfacePanel : Panel, ISurface
	{
		private readonly PanelDialog _d;

		public SurfacePanel(PanelDialog d)
		{
			_d = d;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		public void PaintSurface(Graphics g)
		{
			GraphicsState gstate = g.Save();
			g.TranslateTransform(-base.Left, -base.Top);
			_d.PaintSurface(g);
			g.Restore(gstate);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			PaintSurface(e.Graphics);
			_d.PaintContent(e.Graphics);
		}
	}

	protected readonly string TitleText;

	protected readonly string BodyText;

	protected readonly DlgIcon IconKind;

	protected readonly Panel Content;

	protected readonly FButton Yes;

	protected readonly FButton No;

	protected FButton Extra;

	private DlgChrome.Head _head;

	private int _footerTop;

	protected int DlgW;

	protected int ContentTop => _head.Bottom + Theme.S(20f);

	protected int M => DlgChrome.M;

	protected int InnerW => DlgW - M * 2;

	protected PanelDialog(string title, string body, string yes, string no, DlgIcon icon, float width = 560f)
	{
		TitleText = title;
		BodyText = body;
		IconKind = icon;
		DlgW = Theme.S(width);
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.Manual;
		base.ShowInTaskbar = false;
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Theme.Card2;
		base.KeyPreview = true;
		Text = title;
		if (WDialog.AppIcon != null)
		{
			base.Icon = WDialog.AppIcon;
		}
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		_head = DlgChrome.Layout(DlgW, title, body);
		Content = new SurfacePanel(this)
		{
			Location = new Point(0, ContentTop),
			Width = DlgW
		};
		base.Controls.Add(Content);
		Yes = new FButton
		{
			Text = yes,
			Kind = DlgChrome.YesKind(yes, icon),
			Font = Theme.Body
		};
		Yes.AutoWidth(Theme.S(44f));
		Yes.Click += delegate
		{
			if (Validate())
			{
				base.DialogResult = DialogResult.Yes;
				Close();
			}
		};
		base.Controls.Add(Yes);
		if (!string.IsNullOrEmpty(no))
		{
			No = new FButton
			{
				Text = no,
				Kind = BtnKind.Ghost,
				Font = Theme.Body
			};
			No.AutoWidth(Theme.S(44f));
			No.Click += delegate
			{
				base.DialogResult = DialogResult.No;
				Close();
			};
			base.Controls.Add(No);
		}
	}

	protected FButton AddExtra(string text, EventHandler click)
	{
		Extra = new FButton
		{
			Text = text,
			Kind = BtnKind.Ghost,
			Font = Theme.Body
		};
		Extra.AutoWidth(Theme.S(30f));
		Extra.Click += click;
		base.Controls.Add(Extra);
		return Extra;
	}

	protected virtual bool Validate()
	{
		return true;
	}

	protected void Finish(int contentH)
	{
		Content.Height = contentH;
		_footerTop = Content.Bottom + Theme.S(22f);
		base.Size = new Size(DlgW, _footerTop + DlgChrome.FooterH);
		DlgChrome.PlaceButtons(DlgW, _footerTop, Yes, No);
		if (Extra != null)
		{
			Extra.SetBounds(M, _footerTop + DlgChrome.FooterPad, Math.Max(Theme.S(112f), Extra.Width), DlgChrome.BtnH);
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		DlgChrome.Round(this);
		DlgChrome.FadeIn(this);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape && No != null)
		{
			base.DialogResult = DialogResult.No;
			Close();
		}
		base.OnKeyDown(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		DlgChrome.Paint(e.Graphics, base.Size, IconKind, _footerTop, closeHot: false);
		DlgChrome.DrawHead(e.Graphics, _head, IconKind, TitleText);
	}

	public void PaintSurface(Graphics g)
	{
		DlgChrome.Bg(g, base.Size, IconKind, _footerTop);
	}

	protected virtual void PaintContent(Graphics g)
	{
	}

	protected void Label(Graphics g, string text, int y, string right = null)
	{
		TextRenderer.DrawText(g, text.ToUpperInvariant(), Theme.FC(8f, semi: true), new Point(M, y), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		if (right != null)
		{
			TextRenderer.DrawText(g, right, Theme.Tiny, new Rectangle(M, y, InnerW, Theme.S(16f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.NoPadding);
		}
	}

	public DialogResult ShowFor(IWin32Window owner)
	{
		Form form = owner as Form;
		Dimmer dimmer = null;
		if (form != null && form.Visible && form.WindowState != FormWindowState.Minimized)
		{
			base.Location = new Point(form.Left + (form.Width - base.Width) / 2, form.Top + Math.Max(0, (form.Height - base.Height) / 2));
			dimmer = new Dimmer(form);
			dimmer.Show(form);
		}
		else
		{
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			base.Location = new Point(workingArea.Left + (workingArea.Width - base.Width) / 2, workingArea.Top + Math.Max(0, (workingArea.Height - base.Height) / 2));
			base.TopMost = true;
			base.ShowInTaskbar = true;
		}
		try
		{
			IWin32Window win32Window = dimmer;
			return ShowDialog(win32Window ?? owner);
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
