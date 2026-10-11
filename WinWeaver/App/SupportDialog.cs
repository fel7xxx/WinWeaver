
namespace WinWeaver;

internal class SupportDialog : PanelDialog
{
	private readonly int[] _rowY = new int[3];

	private static readonly string[] Titles = new string[3] { "Boosty", "TON", "Bitcoin (BTC)" };

	private static readonly string[] Values = new string[3] { "boosty.to/fel7xxx/donate", "UQB_35tg7SFtMrJVeU5W6MoB2shypmmpBcJUJKG5WfvCuOMc", "bc1qpn0wasg3mkgdhepkpy5qjg9rmw793f85jq8f5m" };

	private string _copied;

	public SupportDialog()
		: base("Поддержать WinWeaver", "WinWeaver бесплатный и с открытым кодом. Если программа пригодилась, можно оставить чаевые разработчику — это помогает выпускать обновления. Спасибо!", "Закрыть", null, DlgIcon.Info, 640f)
	{
		int num = 0;
		int num2 = Theme.S(62f);
		for (int j = 0; j < 3; j++)
		{
			_rowY[j] = num;
			int i = j;
			FButton fButton = new FButton
			{
				Text = ((j == 0) ? "Открыть" : "Копировать"),
				Kind = ((j != 0) ? BtnKind.Ghost : BtnKind.Silver),
				Font = Theme.Body
			};
			fButton.AutoWidth(Theme.S(30f));
			fButton.SetBounds(base.M + base.InnerW - fButton.Width - Theme.S(12f), num + (num2 - DlgChrome.BtnH) / 2, fButton.Width, DlgChrome.BtnH);
			fButton.Click += delegate
			{
				if (i == 0)
				{
					Shell.Open("https://boosty.to/fel7xxx/donate");
				}
				else
				{
					try
					{
						Clipboard.SetText(Values[i]);
						_copied = Titles[i];
					}
					catch
					{
					}
					Content.Invalidate();
				}
			};
			Content.Controls.Add(fButton);
			num += num2 + Theme.S(10f);
		}
		num += Theme.S(18f);
		Finish(num);
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		int num = Theme.S(62f);
		for (int i = 0; i < 3; i++)
		{
			Rectangle rectangle = new Rectangle(base.M, _rowY[i], base.InnerW, num);
			using (GraphicsPath path = Theme.Round(new RectangleF((float)rectangle.X + 0.5f, (float)rectangle.Y + 0.5f, rectangle.Width - 1, rectangle.Height - 1), Theme.S(12f)))
			{
				Theme.FillGradient(g, path, rectangle, Theme.Card1, Theme.Card2);
				using Pen pen = new Pen((i == 0) ? Theme.Mix(Theme.Border, Theme.Ice, 0.45f) : Theme.Border);
				g.DrawPath(pen, path);
			}
			Rectangle rectangle2 = new Rectangle(rectangle.X + Theme.S(14f), rectangle.Y + (num - Theme.S(26f)) / 2, Theme.S(26f), Theme.S(26f));
			if (i == 0)
			{
				Theme.Heart(g, rectangle2, Theme.Ice);
			}
			else
			{
				using (SolidBrush brush = new SolidBrush(Theme.Mix(Theme.Card1, Theme.Ice, 0.25f)))
				{
					g.FillEllipse(brush, rectangle2);
				}
				TextRenderer.DrawText(g, (i == 1) ? "T" : "₿", Theme.Title, rectangle2, Theme.IceHi, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			}
			int num2 = rectangle2.Right + Theme.S(14f);
			int width = rectangle.Width - (num2 - rectangle.X) - Theme.S(150f);
			TextRenderer.DrawText(g, Titles[i], Theme.Title, new Rectangle(num2, rectangle.Y + Theme.S(11f), width, Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, Values[i], Theme.Tiny, new Rectangle(num2, rectangle.Y + Theme.S(34f), width, Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine);
		}
		string text = ((_copied != null) ? L.T("Адрес {0} скопирован в буфер обмена").Replace("{0}", _copied) : "Проверь адрес после вставки: переводы в криптовалюте нельзя отменить.");
		TextRenderer.DrawText(g, text, Theme.Tiny, new Rectangle(base.M, _rowY[2] + num + Theme.S(8f), base.InnerW, Theme.S(18f)), (_copied != null) ? Theme.Green : Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}
}
