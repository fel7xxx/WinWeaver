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
	// shared look of every popup (WDialog, CheckDialog, engine questions):
	// dark gradient body with a soft glow in the colour of the dialog type, tinted icon tile,
	// small type label above the title, the last paragraph of the text as a note card,
	// footer band with the buttons on the right, fade + slight rise on open.
	internal static class DlgChrome
	{
		public static int M => Theme.S(28f);

		public static int IconS => Theme.S(52f);

		public static int BtnH => Theme.S(38f);

		// padding of the footer band around the buttons
		public static int FooterPad => Theme.S(16f);

		public static int FooterH => BtnH + FooterPad * 2;

		public static readonly Font TitleF = Theme.F(15f, semi: true);

		public static readonly Font KickerF = Theme.F(8f, semi: true);

		public static readonly Color BodyC = Color.FromArgb(172, 172, 182);

		public static Color Tone(DlgIcon i)
		{
			switch (i)
			{
			case DlgIcon.Warn:
				return Theme.Yellow;
			case DlgIcon.Error:
				return Theme.Red;
			case DlgIcon.Ok:
				return Theme.Green;
			case DlgIcon.Question:
				return Color.FromArgb(150, 178, 235);
			default:
				return Color.FromArgb(170, 184, 214);
			}
		}

		public static string GlyphOf(DlgIcon i)
		{
			switch (i)
			{
			case DlgIcon.Warn:
				return Glyph.Warn;
			case DlgIcon.Error:
				return Glyph.Error;
			case DlgIcon.Ok:
				return Glyph.Check;
			case DlgIcon.Question:
				return "\ue9ce";
			default:
				return Glyph.Info;
			}
		}

		public static string KickerOf(DlgIcon i)
		{
			switch (i)
			{
			case DlgIcon.Warn:
				return "ВНИМАНИЕ";
			case DlgIcon.Error:
				return "ОШИБКА";
			case DlgIcon.Ok:
				return "ГОТОВО";
			case DlgIcon.Question:
				return "ПОДТВЕРЖДЕНИЕ";
			default:
				return "ИНФОРМАЦИЯ";
			}
		}

		// no close cross: Esc / the second button close the window
		public static Rectangle CloseR(int w)
		{
			return Rectangle.Empty;
		}

		// ---------- header layout shared by all dialogs ----------
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

		private static void Split(string body, out string main, out string note)
		{
			main = body ?? "";
			note = null;
			int k = main.LastIndexOf("\n\n", StringComparison.Ordinal);
			if (k > 0 && k < main.Length - 2)
			{
				note = main.Substring(k + 2).Trim();
				main = main.Substring(0, k).TrimEnd();
				if (note.Length == 0)
				{
					note = null;
				}
			}
		}

		private static int NotePadX => Theme.S(14f);

		private static int NotePadY => Theme.S(10f);

		public static Head Layout(int w, string title, string body)
		{
			Head h = new Head();
			int m = M;
			int ic = IconS;
			h.Icon = new Rectangle(m, m, ic, ic);
			int tx = m + ic + Theme.S(18f);
			int tw = w - tx - m;
			Split(body, out h.Main, out h.NoteText);
			int kh = Theme.Measure("А", KickerF).Height;
			int th = Theme.Measure(title ?? "", TitleF, tw, wrap: true).Height;
			int headH = kh + Theme.S(2f) + th;
			int ty = m + Math.Max(0, (ic - headH) / 2);
			h.Kicker = new Rectangle(tx, ty, tw, kh);
			h.Title = new Rectangle(tx, h.Kicker.Bottom + Theme.S(2f), tw, th);
			int y = Math.Max(h.Title.Bottom, h.Icon.Bottom - Theme.S(4f));
			if (!string.IsNullOrEmpty(h.Main))
			{
				int bh = Theme.Measure(h.Main, Theme.Body, tw, wrap: true).Height;
				h.Body = new Rectangle(tx, h.Title.Bottom + Theme.S(10f), tw, bh + Theme.S(2f));
				y = Math.Max(y, h.Body.Bottom);
			}
			if (h.NoteText != null)
			{
				int nw = tw - NotePadX * 2 - Theme.S(3f);
				int nh = Theme.Measure(h.NoteText, Theme.Body, nw, wrap: true).Height;
				int ny = Math.Max(h.Body.IsEmpty ? h.Title.Bottom : h.Body.Bottom, h.Title.Bottom) + Theme.S(14f);
				h.Note = new Rectangle(tx, ny, tw, nh + NotePadY * 2);
				y = Math.Max(y, h.Note.Bottom);
			}
			h.Bottom = Math.Max(y, h.Icon.Bottom);
			return h;
		}

		public static void DrawHead(Graphics g, Head h, DlgIcon icon, string title)
		{
			Badge(g, h.Icon, icon);
			Color tone = Tone(icon);
			TextRenderer.DrawText(g, KickerOf(icon), KickerF, h.Kicker, Color.FromArgb(225, tone), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, title, TitleF, h.Title, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			if (!string.IsNullOrEmpty(h.Main))
			{
				TextRenderer.DrawText(g, h.Main, Theme.Body, h.Body, BodyC, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			}
			if (h.NoteText != null)
			{
				Theme.Smooth(g);
				Rectangle r = h.Note;
				using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), Theme.S(10f)))
				{
					using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb(30, tone), Color.FromArgb(10, tone), 0f))
					{
						g.FillPath(b, p);
					}
					using (Pen pen = new Pen(Color.FromArgb(46, tone)))
					{
						g.DrawPath(pen, p);
					}
					Region old = g.Clip;
					g.SetClip(p);
					using (SolidBrush ab = new SolidBrush(Color.FromArgb(200, tone)))
					{
						g.FillRectangle(ab, r.X, r.Y, Theme.S(3f), r.Height);
					}
					g.Clip = old;
				}
				Rectangle tr = new Rectangle(r.X + NotePadX + Theme.S(3f), r.Y + NotePadY, r.Width - NotePadX * 2 - Theme.S(3f), r.Height - NotePadY * 2);
				TextRenderer.DrawText(g, h.NoteText, Theme.Body, tr, Color.FromArgb(214, 214, 222), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			}
		}

		// ---------- background ----------
		public static void Bg(Graphics g, Size sz)
		{
			Bg(g, sz, DlgIcon.Info, -1);
		}

		// everything except text: buttons ask for it as their background (ISurface), so it must be the same picture
		public static void Bg(Graphics g, Size sz, DlgIcon icon, int footerTop)
		{
			Color tone = Tone(icon);
			using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, -1, sz.Width, sz.Height + 2), Color.FromArgb(36, 36, 43), Color.FromArgb(22, 22, 26), 90f))
			{
				g.FillRectangle(b, 0, 0, sz.Width, sz.Height);
			}
			// soft glow of the dialog type behind the icon
			SmoothingMode sm = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			int gr = Theme.S(190f);
			int cx = M + IconS / 2;
			int cy = M + IconS / 2;
			using (GraphicsPath gp = new GraphicsPath())
			{
				gp.AddEllipse(cx - gr, cy - gr, gr * 2, gr * 2);
				using (PathGradientBrush pb = new PathGradientBrush(gp))
				{
					pb.CenterColor = Color.FromArgb(40, tone);
					pb.SurroundColors = new Color[1] { Color.FromArgb(0, tone) };
					pb.FocusScales = new PointF(0.05f, 0.05f);
					g.FillPath(pb, gp);
				}
			}
			g.SmoothingMode = sm;
			if (footerTop > 0 && footerTop < sz.Height)
			{
				using (SolidBrush fb = new SolidBrush(Color.FromArgb(70, 8, 8, 10)))
				{
					g.FillRectangle(fb, 0, footerTop, sz.Width, sz.Height - footerTop);
				}
				using (Pen lp = new Pen(Color.FromArgb(18, 255, 255, 255)))
				{
					g.DrawLine(lp, 0, footerTop, sz.Width, footerTop);
				}
			}
		}

		public static void Paint(Graphics g, Size sz, DlgIcon icon, int footerTop, bool closeHot)
		{
			Bg(g, sz, icon, footerTop);
			Color tone = Tone(icon);
			// top accent: tone colour fading to the edges
			using (LinearGradientBrush hl = new LinearGradientBrush(new Rectangle(0, 0, sz.Width, 2), Color.Transparent, Color.Transparent, 0f))
			{
				ColorBlend cb = new ColorBlend();
				cb.Positions = new float[4] { 0f, 0.25f, 0.6f, 1f };
				cb.Colors = new Color[4] { Color.FromArgb(0, tone), Color.FromArgb(200, tone), Color.FromArgb(90, tone), Color.FromArgb(0, tone) };
				hl.InterpolationColors = cb;
				g.FillRectangle(hl, 0, 0, sz.Width, Math.Max(1, Theme.S(1.5f)));
			}
			using (Pen pen = new Pen(Color.FromArgb(64, 64, 74)))
			{
				g.DrawRectangle(pen, 0, 0, sz.Width - 1, sz.Height - 1);
			}
		}

		// tinted tile: dark glass with the type colour, glyph in that colour, faint halo
		public static void Badge(Graphics g, Rectangle r, DlgIcon icon)
		{
			Theme.Smooth(g);
			Color tone = Tone(icon);
			for (int i = 3; i >= 1; i--)
			{
				int d = Theme.S(3f * i);
				RectangleF hr = new RectangleF(r.X - d, r.Y - d, r.Width + d * 2, r.Height + d * 2);
				using (GraphicsPath hp = Theme.Round(hr, r.Width * 0.3f + d))
				{
					using (SolidBrush hb = new SolidBrush(Color.FromArgb(7, tone)))
					{
						g.FillPath(hb, hp);
					}
				}
			}
			RectangleF rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
			using (GraphicsPath p = Theme.Round(rf, r.Width * 0.3f))
			{
				Theme.FillGradient(g, p, rf, Theme.Mix(Color.FromArgb(46, 46, 54), tone, 0.22f), Theme.Mix(Color.FromArgb(28, 28, 33), tone, 0.08f));
				using (LinearGradientBrush bb = new LinearGradientBrush(new RectangleF(rf.X, rf.Y - 1f, rf.Width, rf.Height + 2f), Color.FromArgb(150, tone), Color.FromArgb(40, tone), 90f))
				{
					using (Pen pen = new Pen(bb, 1f))
					{
						g.DrawPath(pen, p);
					}
				}
			}
			if (Theme.IconBig != null)
			{
				Theme.DrawIcon(g, GlyphOf(icon), Theme.IconBig, r, tone);
			}
			else
			{
				using (Font f = Theme.F(18f, semi: true))
				{
					string s = (icon == DlgIcon.Warn) ? "!" : ((icon == DlgIcon.Error) ? "×" : ((icon == DlgIcon.Ok) ? "✓" : ((icon == DlgIcon.Question) ? "?" : "i")));
					TextRenderer.DrawText(g, s, f, r, tone, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				}
			}
		}

		// buttons right-aligned in the footer band: [main] [middle] [cancel]
		public static void PlaceButtons(int w, int footerTop, FButton yes, FButton no, FButton mid = null)
		{
			int min = Theme.S(112f);
			int by = footerTop + FooterPad;
			int x = w - M;
			foreach (FButton b in new FButton[3] { no, mid, yes })
			{
				if (b == null)
				{
					continue;
				}
				int bw = Math.Max(min, b.Width);
				b.SetBounds(x - bw, by, bw, BtnH);
				x -= bw + Theme.S(10f);
			}
		}

		public static BtnKind YesKind(string yes, DlgIcon icon)
		{
			string y = (yes ?? "").ToLowerInvariant();
			if (icon == DlgIcon.Error || (icon == DlgIcon.Warn && (y.StartsWith("удалить") || y.StartsWith("откатить") || y.StartsWith("выключить") || y.StartsWith("расшифровать") || y.StartsWith("очистить") || y.StartsWith("сбросить"))))
			{
				return y.StartsWith("понятно") || y.StartsWith("ок") ? BtnKind.Silver : BtnKind.Danger;
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

		// fade in and rise by a few pixels
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
						float k = Anim.EaseOut(t);
						f.Opacity = Math.Min(1.0, k * 1.15);
						f.Top = top + (int)Math.Round(rise * (1f - k));
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
}
