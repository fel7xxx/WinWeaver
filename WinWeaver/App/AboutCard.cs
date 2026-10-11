using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class AboutCard : Card
{
	private readonly Image _logo;

	private readonly List<FButton> _btns = new List<FButton>();

	private readonly Font _big = Theme.F(24f, semi: true, FontStyle.Bold);

	public AboutCard(Image logo, MainForm owner)
	{
		_logo = logo;
		Add("GitHub", BtnKind.Silver, delegate
		{
			Shell.Open("https://github.com/fel7xxx/WinWeaver");
		});
		Add("Сообщить об ошибке", BtnKind.Ghost, delegate
		{
			owner.ShowBugReport();
		});
		Add("Поддержать", BtnKind.Ghost, delegate
		{
			owner.ShowSupport();
		});
	}

	private void Add(string text, BtnKind k, EventHandler click)
	{
		FButton fButton = new FButton
		{
			Text = text,
			Kind = k,
			Font = Theme.Body
		};
		fButton.AutoWidth(Theme.S(30f));
		fButton.Click += click;
		_btns.Add(fButton);
		base.Controls.Add(fButton);
	}

	public override int DoLayout(int x, int y, int w)
	{
		int num = Theme.S(26f);
		int num2 = Theme.S(150f);
		int num3 = num + Theme.S(96f);
		int y2 = num2 - num - _btns[0].Height;
		bool flag = w < Theme.S(720f);
		if (flag)
		{
			y2 = num2 - Theme.S(8f);
			num2 += _btns[0].Height + Theme.S(8f);
		}
		else
		{
			num3 = w - num;
			for (int num4 = _btns.Count - 1; num4 >= 0; num4--)
			{
				num3 -= _btns[num4].Width;
				_btns[num4].Location = new Point(num3, (num2 - _btns[num4].Height) / 2);
				num3 -= Theme.S(8f);
			}
		}
		if (flag)
		{
			foreach (FButton btn in _btns)
			{
				btn.Location = new Point(num3, y2);
				num3 += btn.Width + Theme.S(8f);
			}
		}
		SetBounds(x, y, w, num2);
		return num2;
	}

	public override void PaintSurface(Graphics g)
	{
		base.PaintSurface(g);
		Theme.Smooth(g);
		Rectangle clientRectangle = base.ClientRectangle;
		using GraphicsPath path = Theme.Round(new Rectangle(0, 0, clientRectangle.Width - 1, clientRectangle.Height - 1), Theme.S(12f));
		using LinearGradientBrush brush = new LinearGradientBrush(clientRectangle, Color.FromArgb(22, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f);
		g.FillPath(brush, path);
	}

	protected override void PaintContent(Graphics g)
	{
		int x = Theme.S(26f);
		int num = Theme.S(72f);
		int num2 = Theme.S(150f);
		Rectangle rectangle = new Rectangle(x, (num2 - num) / 2, num, num);
		if (_logo != null)
		{
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.DrawImage(_logo, rectangle);
		}
		else
		{
			Theme.IconBox(g, rectangle, "\ue713", bright: true, 0f, Theme.IconBig);
		}
		int num3 = rectangle.Right + Theme.S(22f);
		Size size = Theme.Measure("WinWeaver", _big);
		int num4 = num2 / 2 - size.Height / 2 - Theme.S(14f);
		Theme.GradientText(g, "WinWeaver", _big, new PointF(num3, num4));
		string text = "v1.0.2";
		int width = Theme.PillW(text, Theme.Tiny);
		Theme.Pill(g, new Rectangle(num3 + size.Width + Theme.S(10f), num4 + (size.Height - Theme.S(20f)) / 2 + Theme.S(2f), width, Theme.S(20f)), text, Theme.Tiny, Theme.Silver2, 22);
		TextRenderer.DrawText(g, "Твикер Windows 11 (22H2 – 26H2) без окон PowerShell и батников", Theme.Body, new Point(num3, num4 + size.Height + Theme.S(4f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, L.T("Разработчик: {0}").Replace("{0}", "fel7xxx") + "  •  © 2026  •  GPL-3.0", Theme.Small, new Point(num3, num4 + size.Height + Theme.S(28f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
	}
}
