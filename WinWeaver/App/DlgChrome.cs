using System;

namespace WinWeaver;

internal static class DlgChrome
{
	internal class Head
	{
		public Rectangle Icon;

		public Rectangle Kicker;

		public Rectangle Title;

		public Rectangle Body;

		public Rectangle Note;

		public string Main;

		public string NoteText;

		public int Bottom;
	}

	public static readonly Font TitleF = Theme.F(15f, semi: true);

	public static readonly Font KickerF = Theme.F(8f, semi: true);

	public static readonly Color BodyC = Color.FromArgb(172, 172, 182);

	public static int M => Theme.S(28f);

	public static int IconS => Theme.S(52f);

	public static int BtnH => Theme.S(38f);

	public static int FooterPad => Theme.S(16f);

	public static int FooterH => BtnH + FooterPad * 2;

	private static int NotePadX => Theme.S(14f);

	private static int NotePadY => Theme.S(10f);

	public static Color Tone(DlgIcon i)
	{
		return i switch
		{
			DlgIcon.Warn => Theme.Yellow, 
			DlgIcon.Error => Theme.Red, 
			DlgIcon.Ok => Theme.Green, 
			DlgIcon.Question => Color.FromArgb(150, 178, 235), 
			_ => Color.FromArgb(170, 184, 214), 
		};
	}

	public static string GlyphOf(DlgIcon i)
	{
		return i switch
		{
			DlgIcon.Warn => "\ue7ba", 
			DlgIcon.Error => "\uea39", 
			DlgIcon.Ok => "\ue73e", 
			DlgIcon.Question => "\ue9ce", 
			_ => "\ue946", 
		};
	}

	public static string KickerOf(DlgIcon i)
	{
		return i switch
		{
			DlgIcon.Warn => "ВНИМАНИЕ", 
			DlgIcon.Error => "ОШИБКА", 
			DlgIcon.Ok => "ГОТОВО", 
			DlgIcon.Question => "ПОДТВЕРЖДЕНИЕ", 
			_ => "ИНФОРМАЦИЯ", 
		};
	}

	public static Rectangle CloseR(int w)
	{
		return Rectangle.Empty;
	}

	private static void Split(string body, out string main, out string note)
	{
		main = body ?? "";
		note = null;
		int num = main.LastIndexOf("\n\n", StringComparison.Ordinal);
		if (num > 0 && num < main.Length - 2)
		{
			note = main.Substring(num + 2).Trim();
			main = main.Substring(0, num).TrimEnd();
			if (note.Length == 0)
			{
				note = null;
			}
		}
	}

	public static Head Layout(int w, string title, string body)
	{
		Head head = new Head();
		int m = M;
		int iconS = IconS;
		head.Icon = new Rectangle(m, m, iconS, iconS);
		int num = m + iconS + Theme.S(18f);
		int num2 = w - num - m;
		Split(body, out head.Main, out head.NoteText);
		int height = Theme.Measure("А", KickerF).Height;
		int height2 = Theme.Measure(title ?? "", TitleF, num2, wrap: true).Height;
		int num3 = height + Theme.S(2f) + height2;
		int y = m + Math.Max(0, (iconS - num3) / 2);
		head.Kicker = new Rectangle(num, y, num2, height);
		head.Title = new Rectangle(num, head.Kicker.Bottom + Theme.S(2f), num2, height2);
		int val = Math.Max(head.Title.Bottom, head.Icon.Bottom - Theme.S(4f));
		if (!string.IsNullOrEmpty(head.Main))
		{
			int height3 = Theme.Measure(head.Main, Theme.Body, num2, wrap: true).Height;
			head.Body = new Rectangle(num, head.Title.Bottom + Theme.S(10f), num2, height3 + Theme.S(2f));
			val = Math.Max(val, head.Body.Bottom);
		}
		if (head.NoteText != null)
		{
			int width = num2 - NotePadX * 2 - Theme.S(3f);
			int height4 = Theme.Measure(head.NoteText, Theme.Body, width, wrap: true).Height;
			int y2 = Math.Max(head.Body.IsEmpty ? head.Title.Bottom : head.Body.Bottom, head.Title.Bottom) + Theme.S(14f);
			head.Note = new Rectangle(num, y2, num2, height4 + NotePadY * 2);
			val = Math.Max(val, head.Note.Bottom);
		}
		head.Bottom = Math.Max(val, head.Icon.Bottom);
		return head;
	}

	public static void DrawHead(Graphics g, Head h, DlgIcon icon, string title)
	{
		Badge(g, h.Icon, icon);
		Color baseColor = Tone(icon);
		TextRenderer.DrawText(g, KickerOf(icon), KickerF, h.Kicker, Color.FromArgb(225, baseColor), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, title, TitleF, h.Title, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		if (!string.IsNullOrEmpty(h.Main))
		{
			TextRenderer.DrawText(g, h.Main, Theme.Body, h.Body, BodyC, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		}
		if (h.NoteText == null)
		{
			return;
		}
		Theme.Smooth(g);
		Rectangle note = h.Note;
		using (GraphicsPath graphicsPath = Theme.Round(new RectangleF((float)note.X + 0.5f, (float)note.Y + 0.5f, (float)note.Width - 1f, (float)note.Height - 1f), Theme.S(10f)))
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(note.X - 1, note.Y, note.Width + 2, note.Height), Color.FromArgb(30, baseColor), Color.FromArgb(10, baseColor), 0f))
			{
				g.FillPath(brush, graphicsPath);
			}
			using (Pen pen = new Pen(Color.FromArgb(46, baseColor)))
			{
				g.DrawPath(pen, graphicsPath);
			}
			Region clip = g.Clip;
			g.SetClip(graphicsPath);
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(200, baseColor)))
			{
				g.FillRectangle(brush2, note.X, note.Y, Theme.S(3f), note.Height);
			}
			g.Clip = clip;
			clip.Dispose();
		}
		TextRenderer.DrawText(r: new Rectangle(note.X + NotePadX + Theme.S(3f), note.Y + NotePadY, note.Width - NotePadX * 2 - Theme.S(3f), note.Height - NotePadY * 2), dc: g, text: h.NoteText, font: Theme.Body, fore: Color.FromArgb(214, 214, 222), flags: TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
	}

	public static void Bg(Graphics g, Size sz)
	{
		Bg(g, sz, DlgIcon.Info, -1);
	}

	public static void Bg(Graphics g, Size sz, DlgIcon icon, int footerTop)
	{
		Color baseColor = Tone(icon);
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, -1, sz.Width, sz.Height + 2), Color.FromArgb(36, 36, 43), Color.FromArgb(22, 22, 26), 90f))
		{
			g.FillRectangle(brush, 0, 0, sz.Width, sz.Height);
		}
		SmoothingMode smoothingMode = g.SmoothingMode;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		int num = Theme.S(190f);
		int num2 = M + IconS / 2;
		int num3 = M + IconS / 2;
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(num2 - num, num3 - num, num * 2, num * 2);
			using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(40, baseColor);
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, baseColor) };
			pathGradientBrush.FocusScales = new PointF(0.05f, 0.05f);
			g.FillPath(pathGradientBrush, graphicsPath);
		}
		g.SmoothingMode = smoothingMode;
		if (footerTop > 0 && footerTop < sz.Height)
		{
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(70, 8, 8, 10)))
			{
				g.FillRectangle(brush2, 0, footerTop, sz.Width, sz.Height - footerTop);
			}
			using Pen pen = new Pen(Color.FromArgb(18, 255, 255, 255));
			g.DrawLine(pen, 0, footerTop, sz.Width, footerTop);
		}
	}

	public static void Paint(Graphics g, Size sz, DlgIcon icon, int footerTop, bool closeHot)
	{
		Bg(g, sz, icon, footerTop);
		Color baseColor = Tone(icon);
		using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(0, 0, sz.Width, 2), Color.Transparent, Color.Transparent, 0f))
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[4] { 0f, 0.25f, 0.6f, 1f };
			colorBlend.Colors = new Color[4]
			{
				Color.FromArgb(0, baseColor),
				Color.FromArgb(200, baseColor),
				Color.FromArgb(90, baseColor),
				Color.FromArgb(0, baseColor)
			};
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, 0, 0, sz.Width, Math.Max(1, Theme.S(1.5f)));
		}
		using Pen pen = new Pen(Color.FromArgb(64, 64, 74));
		g.DrawRectangle(pen, 0, 0, sz.Width - 1, sz.Height - 1);
	}

	public static void Badge(Graphics g, Rectangle r, DlgIcon icon)
	{
		Theme.Smooth(g);
		Color color = Tone(icon);
		for (int num = 3; num >= 1; num--)
		{
			int num2 = Theme.S(3f * (float)num);
			using GraphicsPath path = Theme.Round(new RectangleF(r.X - num2, r.Y - num2, r.Width + num2 * 2, r.Height + num2 * 2), (float)r.Width * 0.3f + (float)num2);
			using SolidBrush brush = new SolidBrush(Color.FromArgb(7, color));
			g.FillPath(brush, path);
		}
		RectangleF r2 = new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, (float)r.Width - 1f, (float)r.Height - 1f);
		using (GraphicsPath path2 = Theme.Round(r2, (float)r.Width * 0.3f))
		{
			Theme.FillGradient(g, path2, r2, Theme.Mix(Color.FromArgb(46, 46, 54), color, 0.22f), Theme.Mix(Color.FromArgb(28, 28, 33), color, 0.08f));
			using LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(r2.X, r2.Y - 1f, r2.Width, r2.Height + 2f), Color.FromArgb(150, color), Color.FromArgb(40, color), 90f);
			using Pen pen = new Pen(brush2, 1f);
			g.DrawPath(pen, path2);
		}
		if (Theme.IconBig != null)
		{
			Theme.DrawIcon(g, GlyphOf(icon), Theme.IconBig, r, color);
			return;
		}
		using Font font = Theme.F(18f, semi: true);
		TextRenderer.DrawText(g, icon switch
		{
			DlgIcon.Question => "?", 
			DlgIcon.Ok => "✓", 
			DlgIcon.Error => "×", 
			DlgIcon.Warn => "!", 
			_ => "i", 
		}, font, r, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	public static void PlaceButtons(int w, int footerTop, FButton yes, FButton no, FButton mid = null)
	{
		int val = Theme.S(112f);
		int y = footerTop + FooterPad;
		int num = w - M;
		FButton[] array = new FButton[3] { no, mid, yes };
		foreach (FButton fButton in array)
		{
			if (fButton != null)
			{
				int num2 = Math.Max(val, fButton.Width);
				fButton.SetBounds(num - num2, y, num2, BtnH);
				num -= num2 + Theme.S(10f);
			}
		}
	}

	public static BtnKind YesKind(string yes, DlgIcon icon)
	{
		string text = (yes ?? "").ToLowerInvariant();
		if (icon == DlgIcon.Error || (icon == DlgIcon.Warn && (text.StartsWith("удалить") || text.StartsWith("откатить") || text.StartsWith("выключить") || text.StartsWith("расшифровать") || text.StartsWith("очистить") || text.StartsWith("сбросить"))))
		{
			if (!text.StartsWith("понятно") && !text.StartsWith("ок"))
			{
				return BtnKind.Danger;
			}
			return BtnKind.Silver;
		}
		return BtnKind.Silver;
	}

	public static void Round(Form f)
	{
		if (!Win.RoundCorners(f.Handle))
		{
			using (GraphicsPath path = Theme.Round(new RectangleF(0f, 0f, f.Width, f.Height), Theme.S(14f)))
			{
				f.Region = new Region(path);
			}
		}
	}

	public static void FadeIn(Form f)
	{
		if (!Anim.Enabled)
		{
			return;
		}
		try
		{
			int top = f.Top;
			int rise = Theme.S(10f);
			f.Opacity = 0.0;
			f.Top = top + rise;
			Anim.Start(f, 190, delegate(float t)
			{
				try
				{
					float num = Anim.EaseOut(t);
					f.Opacity = Math.Min(1.0, (double)num * 1.15);
					f.Top = top + (int)Math.Round((float)rise * (1f - num));
				}
				catch
				{
				}
			}, delegate
			{
				try
				{
					f.Opacity = 1.0;
					f.Top = top;
				}
				catch
				{
				}
			}, "dlg-fade-" + f.GetHashCode());
		}
		catch
		{
		}
	}
}
