using System;
using System.Globalization;

namespace WinWeaver;

internal class PauseDialog : Form, ISurface
{
	private readonly TextBox _num = new TextBox();

	private readonly Segment _unit = new Segment
	{
		Items = new string[4] { "Дней", "Недель", "Месяцев", "Лет" }
	};

	private readonly FButton _yes;

	private readonly FButton _no;

	private DlgChrome.Head _head;

	private Rectangle _fieldR;

	private Rectangle _previewR;

	private int _footerTop;

	public int Days = -1;

	private const string Title0 = "Своя длительность паузы";

	private const string Body0 = "Укажи срок — обновления Windows (включая исправления безопасности) не будут скачиваться до этой даты.\n\nОчень длинную паузу крупное обновление Windows иногда сбрасывает — тогда просто поставь её снова.";

	private const int MaxYear = 2077;

	private static CultureInfo Ru => L.Culture;

	private DateTime Until
	{
		get
		{
			if (!int.TryParse(_num.Text.Trim(), out var result) || result <= 0)
			{
				return DateTime.MinValue;
			}
			DateTime now = DateTime.Now;
			try
			{
				return _unit.Selected switch
				{
					0 => now.AddDays(result), 
					1 => now.AddDays((double)result * 7.0), 
					2 => now.AddMonths(result), 
					_ => now.AddYears(result), 
				};
			}
			catch
			{
				return DateTime.MinValue;
			}
		}
	}

	private PauseDialog()
	{
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.Manual;
		base.ShowInTaskbar = false;
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Theme.Card2;
		base.KeyPreview = true;
		Text = "Своя длительность паузы";
		if (WDialog.AppIcon != null)
		{
			base.Icon = WDialog.AppIcon;
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
		_yes = new FButton
		{
			Text = "Поставить паузу",
			Kind = BtnKind.Silver,
			Font = Theme.Body
		};
		_yes.AutoWidth(Theme.S(44f));
		_yes.Click += delegate
		{
			if (Days > 0)
			{
				base.DialogResult = DialogResult.Yes;
				Close();
			}
		};
		_no = new FButton
		{
			Text = "Отмена",
			Kind = BtnKind.Ghost,
			Font = Theme.Body
		};
		_no.AutoWidth(Theme.S(44f));
		_no.Click += delegate
		{
			base.DialogResult = DialogResult.No;
			Close();
		};
		base.Controls.Add(_num);
		base.Controls.Add(_unit);
		base.Controls.Add(_yes);
		base.Controls.Add(_no);
		DoLayoutDlg();
		Recalc();
	}

	private void Recalc()
	{
		DateTime dateTime = Until;
		if (dateTime != DateTime.MinValue && dateTime.Year > 2077)
		{
			dateTime = new DateTime(2077, 12, 30, 23, 0, 0);
		}
		Days = ((dateTime == DateTime.MinValue) ? (-1) : Math.Max(1, (int)Math.Round((dateTime - DateTime.Now).TotalDays)));
		_yes.Enabled = Days > 0;
		Invalidate(_previewR);
	}

	private void DoLayoutDlg()
	{
		int num = Theme.S(540f);
		_head = DlgChrome.Layout(num, "Своя длительность паузы", "Укажи срок — обновления Windows (включая исправления безопасности) не будут скачиваться до этой даты.\n\nОчень длинную паузу крупное обновление Windows иногда сбрасывает — тогда просто поставь её снова.");
		int x = _head.Title.X;
		int num2 = _head.Bottom + Theme.S(18f);
		int num3 = Theme.S(38f);
		_fieldR = new Rectangle(x, num2, Theme.S(86f), num3);
		int preferredHeight = _num.PreferredHeight;
		_num.SetBounds(_fieldR.X + Theme.S(10f), _fieldR.Y + (num3 - preferredHeight) / 2, _fieldR.Width - Theme.S(20f), preferredHeight);
		_unit.SetBounds(_fieldR.Right + Theme.S(10f), num2 + (num3 - Theme.S(34f)) / 2, num - DlgChrome.M - (_fieldR.Right + Theme.S(10f)), Theme.S(34f));
		_previewR = new Rectangle(x, _fieldR.Bottom + Theme.S(12f), num - DlgChrome.M - x, Theme.S(24f));
		_footerTop = _previewR.Bottom + Theme.S(22f);
		base.Size = new Size(num, _footerTop + DlgChrome.FooterH);
		DlgChrome.PlaceButtons(num, _footerTop, _yes, _no);
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
			base.DialogResult = DialogResult.No;
			Close();
		}
		else if (e.KeyCode == Keys.Return && Days > 0)
		{
			e.SuppressKeyPress = true;
			base.DialogResult = DialogResult.Yes;
			Close();
		}
		base.OnKeyDown(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		DlgChrome.Paint(graphics, base.Size, DlgIcon.Question, _footerTop, closeHot: false);
		DlgChrome.DrawHead(graphics, _head, DlgIcon.Question, "Своя длительность паузы");
		Theme.Smooth(graphics);
		using (GraphicsPath path = Theme.Round(new RectangleF((float)_fieldR.X + 0.5f, (float)_fieldR.Y + 0.5f, (float)_fieldR.Width - 1f, (float)_fieldR.Height - 1f), Theme.S(9f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 30, 35)))
			{
				graphics.FillPath(brush, path);
			}
			using Pen pen = new Pen(_num.Focused ? Color.FromArgb(150, 178, 235) : Theme.BorderHi);
			graphics.DrawPath(pen, path);
		}
		DateTime dateTime = DateTime.Now.AddDays(Math.Max(0, Days));
		string text;
		Color fore;
		if (Days <= 0)
		{
			text = "Введи число больше нуля";
			fore = Theme.Red;
		}
		else
		{
			bool flag = Until.Year > 2077;
			text = "Пауза до " + dateTime.ToString("d MMMM yyyy", Ru) + " (" + dateTime.ToString("dddd", Ru) + ")" + (flag ? " — дальше 2077 года нельзя" : "");
			fore = Theme.Yellow;
		}
		TextRenderer.DrawText(graphics, text, Theme.Body, _previewR, fore, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	public void PaintSurface(Graphics g)
	{
		DlgChrome.Bg(g, base.Size, DlgIcon.Question, _footerTop);
	}

	public static int Ask(Form owner)
	{
		using PauseDialog pauseDialog = new PauseDialog();
		Dimmer dimmer = null;
		if (owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized)
		{
			pauseDialog.Location = new Point(owner.Left + (owner.Width - pauseDialog.Width) / 2, owner.Top + (owner.Height - pauseDialog.Height) / 2);
			dimmer = new Dimmer(owner);
			dimmer.Show(owner);
		}
		try
		{
			IWin32Window win32Window = dimmer;
			return (pauseDialog.ShowDialog(win32Window ?? owner) == DialogResult.Yes) ? pauseDialog.Days : (-1);
		}
		finally
		{
			if (dimmer != null)
			{
				dimmer.Close();
				dimmer.Dispose();
			}
			owner?.Activate();
		}
	}
}
