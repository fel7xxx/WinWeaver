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
using System.Drawing.Text;
using System.Windows.Forms;

namespace WinWeaver
{
	internal static class Theme
	{
		public static readonly Color Bg = Color.FromArgb(15, 16, 19);

		public static readonly Color BgTop = Color.FromArgb(21, 22, 26);

		public static readonly Color Side1 = Color.FromArgb(27, 28, 33);

		public static readonly Color Side2 = Color.FromArgb(16, 17, 20);

		public static readonly Color Card1 = Color.FromArgb(35, 37, 43);

		public static readonly Color Card2 = Color.FromArgb(27, 28, 33);

		public static readonly Color CardHot1 = Color.FromArgb(42, 44, 52);

		public static readonly Color CardHot2 = Color.FromArgb(32, 34, 40);

		public static readonly Color Border = Color.FromArgb(46, 48, 57);

		public static readonly Color BorderHi = Color.FromArgb(68, 72, 84);

		public static readonly Color Text = Color.FromArgb(236, 236, 240);

		public static readonly Color Sub = Color.FromArgb(150, 150, 160);

		public static readonly Color Dim = Color.FromArgb(104, 104, 114);

		public static readonly Color Silver1 = Color.FromArgb(236, 236, 241);

		public static readonly Color Silver2 = Color.FromArgb(160, 160, 171);

		public static readonly Color Green = Color.FromArgb(111, 207, 151);

		public static readonly Color Yellow = Color.FromArgb(226, 194, 107);

		public static readonly Color Red = Color.FromArgb(235, 122, 122);

		public static readonly Color Control1 = Color.FromArgb(50, 52, 61);

		public static readonly Color Control2 = Color.FromArgb(39, 41, 48);

		public static readonly Color OnSilver = Color.FromArgb(22, 22, 26);

		// ice-blue accent: active navigation, switches, progress, focus and "new" highlights
		public static readonly Color Ice = Color.FromArgb(132, 204, 242);

		public static readonly Color IceHi = Color.FromArgb(214, 241, 255);

		public static readonly Color IceDim = Color.FromArgb(64, 108, 138);

		public static float Scale = 1f;

		public static string Family = "Segoe UI";

		public static string FamilySemi = "Segoe UI Semibold";

		public static string IconFamily = null;

		public static Font Body;

		public static Font Small;

		public static Font Tiny;

		public static Font Title;

		public static Font H1;

		public static Font H2;

		public static Font Logo;

		public static Font Mono;

		public static Font Icon;

		public static Font IconSmall;

		public static Font IconBig;

		public static int S(float v)
		{
			return (int)Math.Round(v * Scale);
		}

		public static Font F(float size, bool semi = false, FontStyle st = FontStyle.Regular)
		{
			return new Font(semi ? FamilySemi : Family, size, st, GraphicsUnit.Point);
		}

		public static void Init(float dpiScale)
		{
			Scale = ((dpiScale <= 0f) ? 1f : dpiScale);
			using (InstalledFontCollection installedFontCollection = new InstalledFontCollection())
			{
				bool flag = false;
				bool flag2 = false;
				bool flag3 = false;
				bool flag4 = false;
				string text = null;
				FontFamily[] families = installedFontCollection.Families;
				for (int i = 0; i < families.Length; i++)
				{
					string name = families[i].Name;
					switch (name)
					{
					case "Segoe UI":
						flag = true;
						continue;
					case "Segoe UI Semibold":
						flag2 = true;
						continue;
					case "Segoe Fluent Icons":
						flag3 = true;
						continue;
					case "Segoe MDL2 Assets":
						flag4 = true;
						continue;
					}
					if (text == null)
					{
						switch (name)
						{
						case "Noto Sans":
						case "Liberation Sans":
						case "Arial":
							text = name;
							break;
						}
					}
				}
				if (!flag)
				{
					Family = text ?? FontFamily.GenericSansSerif.Name;
					FamilySemi = Family;
				}
				else if (!flag2)
				{
					FamilySemi = Family;
				}
				IconFamily = (flag3 ? "Segoe Fluent Icons" : (flag4 ? "Segoe MDL2 Assets" : null));
			}
			Body = F(10f);
			Small = F(9f);
			Tiny = F(8f);
			Title = F(10.5f, semi: true);
			H1 = F(19f, semi: true);
			H2 = F(9f, semi: true);
			Logo = F(22f, semi: true, FontStyle.Bold);
			Mono = new Font(HasFamily("Cascadia Mono") ? "Cascadia Mono" : (HasFamily("Consolas") ? "Consolas" : FontFamily.GenericMonospace.Name), 9f);
			if (IconFamily != null)
			{
				Icon = new Font(IconFamily, 12f);
				IconSmall = new Font(IconFamily, 10f);
				IconBig = new Font(IconFamily, 18f);
			}
		}

		private static bool HasFamily(string n)
		{
			using (InstalledFontCollection installedFontCollection = new InstalledFontCollection())
			{
				FontFamily[] families = installedFontCollection.Families;
				for (int i = 0; i < families.Length; i++)
				{
					if (families[i].Name == n)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static GraphicsPath Round(RectangleF r, float radius)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			float num = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
			if (num <= 1f)
			{
				graphicsPath.AddRectangle(r);
				return graphicsPath;
			}
			graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		public static void Smooth(Graphics g)
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		}

		public static void FillGradient(Graphics g, GraphicsPath path, RectangleF r, Color c1, Color c2, float angle = 90f)
		{
			if (r.Width < 1f || r.Height < 1f)
			{
				return;
			}
			using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(r.X - 1f, r.Y - 1f, r.Width + 2f, r.Height + 2f), c1, c2, angle))
			{
				g.FillPath(brush, path);
			}
		}

		public static void Card(Graphics g, Rectangle bounds, bool hot = false, float radius = 12f)
		{
			Card(g, bounds, hot ? 1f : 0f, radius);
		}

		public static void Card(Graphics g, Rectangle bounds, float hot, float radius = 12f)
		{
			Smooth(g);
			RectangleF r = new RectangleF((float)bounds.X + 0.5f, (float)bounds.Y + 0.5f, (float)bounds.Width - 1.5f, (float)bounds.Height - 1.5f);
			using (GraphicsPath path = Round(r, S(radius)))
			{
				FillGradient(g, path, r, Mix(Card1, CardHot1, hot), Mix(Card2, CardHot2, hot));
				using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1f, r.Width, r.Height + 2f), Mix(Color.FromArgb(60, 63, 73), Mix(Color.FromArgb(96, 100, 112), Ice, 0.25f), hot), Mix(Border, BorderHi, hot * 0.6f), 90f))
				{
					using (Pen pen = new Pen(brush, 1f))
					{
						g.DrawPath(pen, path);
					}
				}
			}
		}

		// small rounded "pill" label: translucent fill + outline in color c
		public static void Pill(Graphics g, Rectangle r, string text, Font f, Color c, int fillAlpha = 34)
		{
			Smooth(g);
			using (GraphicsPath p = Round(r, (float)r.Height / 2f))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(fillAlpha, c)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(110, c)))
				{
					g.DrawPath(pen, p);
				}
			}
			TextRenderer.DrawText(g, text, f, r, c, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		public static int PillW(string text, Font f)
		{
			return Measure(text, f).Width + S(20f);
		}

		// rounded square with an icon (used on cards, rows and dialogs)
		public static void IconBox(Graphics g, Rectangle r, string glyph, bool bright = false, float glow = 0f, Font font = null)
		{
			Smooth(g);
			using (GraphicsPath path = Round(r, (float)r.Width * 0.27f))
			{
				if (bright)
				{
					FillGradient(g, path, r, Silver1, Silver2);
				}
				else
				{
					FillGradient(g, path, r, Mix(Color.FromArgb(58, 58, 67), Color.FromArgb(78, 78, 90), glow), Mix(Color.FromArgb(40, 40, 47), Color.FromArgb(52, 52, 61), glow));
				}
				using (Pen pen = new Pen(bright ? Color.FromArgb(210, 210, 220) : Mix(BorderHi, Color.FromArgb(120, 120, 134), glow)))
				{
					g.DrawPath(pen, path);
				}
			}
			DrawIcon(g, glyph, font ?? ((r.Width >= S(36f)) ? Icon : IconSmall), r, bright ? OnSilver : Silver1);
		}

		// title text filled with the silver gradient (logo, hero)
		public static void GradientText(Graphics g, string text, Font font, PointF at, float shine = -1f)
		{
			using (GraphicsPath gp = new GraphicsPath())
			{
				float em = font.SizeInPoints * g.DpiY / 72f;
				gp.AddString(L.T(text), font.FontFamily, (int)font.Style, em, at, StringFormat.GenericTypographic);
				RectangleF bounds = gp.GetBounds();
				if (bounds.Width <= 0f)
				{
					return;
				}
				Smooth(g);
				using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(bounds.X, bounds.Y - 1f, bounds.Width, bounds.Height + 2f), Color.White, Color.FromArgb(150, 162, 178), 90f))
				{
					g.FillPath(brush, gp);
				}
				if (shine >= 0f && shine <= 1f)
				{
					float w = bounds.Width * 0.35f;
					float x = bounds.X - w + (bounds.Width + w * 2f) * shine;
					RectangleF sr = new RectangleF(x - w / 2f, bounds.Y, w, bounds.Height);
					using (LinearGradientBrush sb = new LinearGradientBrush(new RectangleF(sr.X - 1f, sr.Y, sr.Width + 2f, sr.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
					{
						ColorBlend cb = new ColorBlend();
						cb.Positions = new float[3] { 0f, 0.5f, 1f };
						cb.Colors = new Color[3] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(200, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
						sb.InterpolationColors = cb;
						Region old = g.Clip;
						g.SetClip(gp, CombineMode.Intersect);
						g.FillRectangle(sb, sr);
						g.Clip = old;
					}
				}
			}
		}

		// thin progress bar with a moving highlight; p in 0..1, phase in 0..1 for the shimmer
		public static void Progress(Graphics g, RectangleF r, float p, float phase)
		{
			Smooth(g);
			using (GraphicsPath bg = Round(r, r.Height / 2f))
			{
				using (SolidBrush b = new SolidBrush(Control1))
				{
					g.FillPath(b, bg);
				}
			}
			float w = Math.Max(r.Height, r.Width * Anim.Clamp(p));
			RectangleF fr = new RectangleF(r.X, r.Y, w, r.Height);
			using (GraphicsPath fp = Round(fr, r.Height / 2f))
			{
				FillGradient(g, fp, fr, Ice, IceHi, 0f);
				float sw = Math.Max(S(40f), fr.Width * 0.3f);
				float sx = fr.X - sw + (fr.Width + sw) * phase;
				using (LinearGradientBrush sb = new LinearGradientBrush(new RectangleF(sx - 1f, fr.Y, sw + 2f, fr.Height), Color.Transparent, Color.Transparent, 0f))
				{
					ColorBlend cb = new ColorBlend();
					cb.Positions = new float[3] { 0f, 0.5f, 1f };
					cb.Colors = new Color[3] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(170, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
					sb.InterpolationColors = cb;
					Region old = g.Clip;
					g.SetClip(fp, CombineMode.Intersect);
					g.FillRectangle(sb, sx, fr.Y, sw, fr.Height);
					g.Clip = old;
				}
			}
		}

		public static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra = TextFormatFlags.Default)
		{
			TextRenderer.DrawText(g, s ?? "", f, r, c, TextFormatFlags.NoPrefix | extra);
		}

		public static Size Measure(string s, Font f, int width = int.MaxValue, bool wrap = false)
		{
			TextFormatFlags textFormatFlags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
			if (wrap)
			{
				textFormatFlags |= TextFormatFlags.WordBreak;
			}
			return TextRenderer.MeasureText(string.IsNullOrEmpty(s) ? " " : s, f, new Size(width, int.MaxValue), textFormatFlags);
		}

		// small filled heart (support button)
		public static void Heart(Graphics g, RectangleF r, Color c)
		{
			Smooth(g);
			using (GraphicsPath p = new GraphicsPath())
			{
				float w = r.Width, h = r.Height, x = r.X, y = r.Y;
				p.AddBezier(x + w / 2f, y + h, x + w * 0.1f, y + h * 0.62f, x - w * 0.05f, y + h * 0.18f, x + w * 0.27f, y + h * 0.04f);
				p.AddBezier(x + w * 0.27f, y + h * 0.04f, x + w * 0.42f, y - h * 0.02f, x + w / 2f, y + h * 0.12f, x + w / 2f, y + h * 0.22f);
				p.AddBezier(x + w / 2f, y + h * 0.22f, x + w / 2f, y + h * 0.12f, x + w * 0.58f, y - h * 0.02f, x + w * 0.73f, y + h * 0.04f);
				p.AddBezier(x + w * 0.73f, y + h * 0.04f, x + w * 1.05f, y + h * 0.18f, x + w * 0.9f, y + h * 0.62f, x + w / 2f, y + h);
				p.CloseFigure();
				using (SolidBrush b = new SolidBrush(c))
				{
					g.FillPath(b, p);
				}
			}
		}

		public static Color Mix(Color a, Color b, float t)
		{
			if (t < 0f)
			{
				t = 0f;
			}
			if (t > 1f)
			{
				t = 1f;
			}
			return Color.FromArgb((int)((float)(int)a.A + (float)(b.A - a.A) * t), (int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
		}

		public static Color Kind(string kind)
		{
			switch (kind)
			{
			case "ok":
				return Green;
			case "warn":
				return Yellow;
			case "err":
				return Red;
			default:
				return Silver2;
			}
		}

		public static void DrawIcon(Graphics g, string glyph, Font f, Rectangle r, Color c)
		{
			if (f != null && !string.IsNullOrEmpty(glyph))
			{
				TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				return;
			}
			Smooth(g);
			int num = Math.Min(r.Width, r.Height) / 3;
			using (Pen pen = new Pen(c, Math.Max(1.5f, S(1.6f))))
			{
				g.DrawEllipse(pen, (float)r.X + (float)(r.Width - num) / 2f, (float)r.Y + (float)(r.Height - num) / 2f, num, num);
			}
		}
	}
}
