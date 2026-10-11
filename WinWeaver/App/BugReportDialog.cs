using System;
using System.Runtime.InteropServices;

namespace WinWeaver;

internal class BugReportDialog : PanelDialog
{
	private readonly TextBox _box;

	private readonly string _diag;

	private bool _copied;

	private readonly int _boxY;

	private readonly int _noteY;

	public BugReportDialog()
		: base("Сообщить об ошибке", "Откроется форма на GitHub (нужен бесплатный аккаунт). Сведения ниже скопируются в буфер — вставь их в поле «Журнал». Имя пользователя и компьютера в них заменены.", "Открыть форму", "Закрыть", DlgIcon.Info, 760f)
	{
		_diag = Diag.Text();
		int y = (_boxY = 0);
		Panel panel = new Panel
		{
			BackColor = Color.FromArgb(22, 23, 27)
		};
		panel.SetBounds(base.M, y, base.InnerW, Theme.S(220f));
		_box = new TextBox
		{
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Vertical,
			BorderStyle = BorderStyle.None,
			BackColor = Color.FromArgb(22, 23, 27),
			ForeColor = Theme.Sub,
			Font = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
			Text = _diag.Replace("\r\n", "\n").Replace("\n", "\r\n"),
			WordWrap = false,
			TabStop = false
		};
		_box.HandleCreated += delegate
		{
			if (Win.IsWindows)
			{
				try
				{
					// Avalonia: тёмная тема поля задаётся стилями Fluent Dark.
				}
				catch
				{
				}
			}
		};
		_box.SetBounds(Theme.S(10f), Theme.S(8f), base.InnerW - Theme.S(20f), Theme.S(204f));
		panel.Controls.Add(_box);
		Content.Controls.Add(panel);
		y = panel.Bottom + Theme.S(12f);
		int num = base.M;
		string[][] array = new string[3][]
		{
			new string[2] { "Копировать сведения", "copy" },
			new string[2] { "Предложить идею", "idea" },
			new string[2] { "Обсуждения", "talk" }
		};
		foreach (string[] array2 in array)
		{
			string i = array2[1];
			FButton fButton = new FButton
			{
				Text = array2[0],
				Kind = BtnKind.Ghost,
				Font = Theme.Small
			};
			fButton.AutoWidth(Theme.S(24f));
			fButton.SetBounds(num, y, fButton.Width, Theme.S(32f));
			fButton.Click += delegate
			{
				if (i == "copy")
				{
					Copy();
				}
				else
				{
					Shell.Open((i == "idea") ? "https://github.com/fel7xxx/WinWeaver/issues/new?template=feature_request.yml" : "https://github.com/fel7xxx/WinWeaver/discussions");
				}
			};
			Content.Controls.Add(fButton);
			num += fButton.Width + Theme.S(8f);
		}
		Finish((_noteY = y + Theme.S(40f)) + Theme.S(20f));
	}


	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		_box.SelectionStart = 0;
		_box.SelectionLength = 0;
		Yes.Focus();
	}

	private void Copy()
	{
		try
		{
			Clipboard.SetText(_diag);
			_copied = true;
		}
		catch
		{
		}
		Content.Invalidate();
	}

	protected override bool Validate()
	{
		Copy();
		Shell.Open("https://github.com/fel7xxx/WinWeaver/issues/new?template=bug_report.yml&version=" + Uri.EscapeDataString("1.0.2") + "&windows=" + Uri.EscapeDataString(Diag.WindowsLine()));
		return true;
	}

	protected override void PaintContent(Graphics g)
	{
		TextRenderer.DrawText(g, _copied ? "Сведения скопированы — вставь их в форму на GitHub (Ctrl + V)" : "Пароли и личные файлы в журнал не попадают.", Theme.Tiny, new Rectangle(base.M, _noteY, base.InnerW, Theme.S(18f)), _copied ? Theme.Green : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}
}
