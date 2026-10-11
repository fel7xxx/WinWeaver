using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WinWeaver;

internal static class Theme
{
	private struct MKey : IEquatable<MKey>
	{
		public string S;

		public Font F;

		public int W;

		public bool Wrap;

		public bool Equals(MKey o)
		{
			if (W == o.W && Wrap == o.Wrap && F == o.F)
			{
				return string.Equals(S, o.S, StringComparison.Ordinal);
			}
			return false;
		}

		public override bool Equals(object o)
		{
			if (o is MKey)
			{
				return Equals((MKey)o);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return ((S.GetHashCode() * 397) ^ RuntimeHelpers.GetHashCode(F)) * 31 + W * 2 + (Wrap ? 1 : 0);
		}
	}

	public static readonly Color Bg = Color.FromArgb(12, 12, 14);

	public static readonly Color BgTop = Color.FromArgb(12, 12, 14);

	public static readonly Color Side1 = Color.FromArgb(16, 16, 19);

	public static readonly Color Side2 = Color.FromArgb(16, 16, 19);

	public static readonly Color Card1 = Color.FromArgb(20, 20, 23);

	public static readonly Color Card2 = Color.FromArgb(20, 20, 23);

	public static readonly Color CardHot1 = Color.FromArgb(26, 26, 31);

	public static readonly Color CardHot2 = Color.FromArgb(24, 24, 28);

	public static readonly Color Border = Color.FromArgb(49, 49, 57);

	public static readonly Color BorderHi = Color.FromArgb(87, 87, 99);

	public static readonly Color Line = Color.FromArgb(35, 35, 40);

	public static readonly Color Text = Color.FromArgb(237, 237, 240);

	public static readonly Color Sub = Color.FromArgb(178, 178, 188);

	public static readonly Color Dim = Color.FromArgb(156, 156, 169);

	public static readonly Color Silver1 = Color.FromArgb(255, 255, 255);

	public static readonly Color Silver2 = Color.FromArgb(255, 255, 255);

	public static readonly Color Green = Color.FromArgb(63, 207, 142);

	public static readonly Color Yellow = Color.FromArgb(245, 195, 75);

	public static readonly Color Red = Color.FromArgb(255, 107, 116);

	public static readonly Color Control1 = Color.FromArgb(42, 42, 49);

	public static readonly Color Control2 = Color.FromArgb(18, 18, 21);

	public static readonly Color OnSilver = Color.FromArgb(255, 255, 255);

	public static readonly Color Accent = Color.FromArgb(255, 59, 74);

	public static readonly Color Ice = Color.FromArgb(255, 59, 74);

	public static readonly Color IceHi = Color.FromArgb(255, 107, 116);

	public static readonly Color Blue = Color.FromArgb(107, 120, 255);
	public static readonly Color Cyan = Color.FromArgb(47, 184, 214);
	public static readonly Color Orange = Color.FromArgb(255, 154, 82);
	public static readonly Color Panel2 = Color.FromArgb(24, 24, 28);
	public static readonly Color Hover = Color.FromArgb(28, 28, 33);
	public const float BW = 1f;
	public static readonly Color IceDim = Color.FromArgb(110, 26, 34);

	public static float Scale = 1f;

	public static string Family = "UI";

	public static string FamilySemi = "UI SemiBold";

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

	private static readonly Dictionary<string, Font> _fc = new Dictionary<string, Font>();

	private static readonly Dictionary<long, Bitmap> _glows = new Dictionary<long, Bitmap>();

	private static readonly Dictionary<char, string[]> _dm = new Dictionary<char, string[]>
	{
		{
			'0',
			new string[7] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." }
		},
		{
			'1',
			new string[7] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." }
		},
		{
			'2',
			new string[7] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" }
		},
		{
			'3',
			new string[7] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." }
		},
		{
			'4',
			new string[7] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." }
		},
		{
			'5',
			new string[7] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." }
		},
		{
			'6',
			new string[7] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." }
		},
		{
			'7',
			new string[7] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." }
		},
		{
			'8',
			new string[7] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." }
		},
		{
			'9',
			new string[7] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." }
		},
		{
			'%',
			new string[7] { "##...", "##..#", "...#.", "..#..", ".#...", "#..##", "...##" }
		},
		{
			'+',
			new string[7] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." }
		},
		{
			'-',
			new string[7] { ".....", ".....", ".....", "#####", ".....", ".....", "....." }
		},
		{
			'.',
			new string[7] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." }
		},
		{
			',',
			new string[7] { ".....", ".....", ".....", ".....", ".##..", "..#..", ".#..." }
		},
		{
			':',
			new string[7] { ".....", ".##..", ".##..", ".....", ".##..", ".##..", "....." }
		},
		{
			'/',
			new string[7] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." }
		},
		{
			' ',
			new string[7] { ".....", ".....", ".....", ".....", ".....", ".....", "....." }
		}
	};

	private static readonly Dictionary<MKey, Size> _measured = new Dictionary<MKey, Size>();

	public static int S(float v)
	{
		return (int)Math.Round(v * Scale);
	}

	public static Font F(float size, bool semi = false, FontStyle st = FontStyle.Regular)
	{
		return new Font(semi ? FamilySemi : Family, size * 1.06f, st, GraphicsUnit.Point);
	}

	public static Font FC(float size, bool semi = false, FontStyle st = FontStyle.Regular)
	{
		string key = size.ToString(CultureInfo.InvariantCulture) + (semi ? "s" : "r") + (int)st;
		if (!_fc.TryGetValue(key, out var value))
		{
			value = F(size, semi, st);
			_fc[key] = value;
		}
		return value;
	}

	/// <summary>
	/// Шрифты: JetBrains Mono встроен в сборку как ресурс Avalonia (avares://),
	/// значки — системный Segoe Fluent Icons / Segoe MDL2 Assets.
	/// Масштабирование под DPI выполняет Avalonia, поэтому Scale по умолчанию = 1.
	/// </summary>
	public static void Init(float dpiScale)
	{
		Scale = ((dpiScale <= 0f) ? 1f : dpiScale);
		Family = "UI";
		FamilySemi = "UI SemiBold";
		IconFamily = HasFamily("Segoe Fluent Icons") ? "Segoe Fluent Icons" : (HasFamily("Segoe MDL2 Assets") ? "Segoe MDL2 Assets" : null);
		Body = F(Prefs.LargerText ? 10.5f : 9.5f);
		Small = F(Prefs.LargerText ? 9.5f : 8.5f);
		Tiny = F(Prefs.LargerText ? 8.5f : 7.5f);
		Title = F(10f, semi: true);
		H1 = F(17f, semi: true);
		H2 = F(Prefs.LargerText ? 10f : 9f, semi: true);
		Logo = F(18f, semi: true);
		Mono = new Font("JetBrains Mono", 8f);
		if (IconFamily != null)
		{
			Icon = new Font(IconFamily, 12f);
			IconSmall = new Font(IconFamily, 10f);
			IconBig = new Font(IconFamily, 18f);
		}
	}

	private static bool HasFamily(string n)
	{
		try
		{
			foreach (var f in Avalonia.Media.FontManager.Current.SystemFonts)
			{
				if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static GraphicsPath Round(RectangleF r, float radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		if (!(Math.Min(r.Width, r.Height) <= S(28f) && radius * 2f >= Math.Min(r.Width, r.Height) - 1f)) radius = Math.Min(radius, S(11f));
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
		using LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(r.X - 1f, r.Y - 1f, r.Width + 2f, r.Height + 2f), c1, c2, angle);
		g.FillPath(brush, path);
	}

	/// <summary>Переключатель в стиле превью: серый трек → акцентный, ручка едет с пружиной.</summary>
	public static void Toggle(Graphics g, RectangleF r, float knob, float on, bool busy = false, float hot = 0f)
	{
		Smooth(g);
		using (GraphicsPath path = Round(r, r.Height / 2f))
		{
			using SolidBrush brush = new SolidBrush(Mix(Mix(Color.FromArgb(42, 42, 49), Color.FromArgb(54, 54, 62), hot), Accent, Math.Max(0f, Math.Min(1f, on))));
			g.FillPath(brush, path);
		}
		float d = r.Height - S(6f);
		float k = Math.Max(-0.08f, Math.Min(1.08f, knob));
		float x = r.X + S(3f) + k * (r.Width - d - S(6f));
		using SolidBrush b2 = new SolidBrush(busy ? Dim : Mix(Color.FromArgb(141, 141, 150), Color.White, Math.Max(0f, Math.Min(1f, on))));
		g.FillEllipse(b2, new RectangleF(x, r.Y + S(3f), d, d));
	}

	/// <summary>Кружок статуса: 0 — недоступно, 1 — по совету (синий с галочкой), 2 — стоит изменить (пустой), 3 — по желанию (зелёный), 4 — в очереди.</summary>
	public static void StatusCircle(Graphics g, float cx, float cy, float r, int kind)
	{
		Smooth(g);
		RectangleF rc = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
		switch (kind)
		{
		case 1:
		{
			using (SolidBrush b = new SolidBrush(Blue)) g.FillEllipse(b, rc);
			using Pen p = new Pen(Color.White, S(1.6f));
			p.StartCap = LineCap.Round;
			p.EndCap = LineCap.Round;
			p.LineJoin = LineJoin.Round;
			g.DrawLines(p, new PointF[3]
			{
				new PointF(cx - r * 0.42f, cy + r * 0.02f),
				new PointF(cx - r * 0.1f, cy + r * 0.34f),
				new PointF(cx + r * 0.45f, cy - r * 0.3f)
			});
			break;
		}
		case 3:
		{
			using SolidBrush b = new SolidBrush(Green);
			g.FillEllipse(b, rc);
			break;
		}
		case 4:
		{
			using Pen p = new Pen(Yellow, S(1.5f));
			g.DrawEllipse(p, rc.X + 0.75f, rc.Y + 0.75f, rc.Width - 1.5f, rc.Height - 1.5f);
			using SolidBrush b = new SolidBrush(Yellow);
			g.FillEllipse(b, cx - r * 0.35f, cy - r * 0.35f, r * 0.7f, r * 0.7f);
			break;
		}
		default:
		{
			using Pen p = new Pen(kind == 0 ? Color.FromArgb(60, 60, 66) : Color.FromArgb(85, 85, 94), S(1.5f));
			g.DrawEllipse(p, rc.X + 0.75f, rc.Y + 0.75f, rc.Width - 1.5f, rc.Height - 1.5f);
			break;
		}
		}
	}

	public static void Card(Graphics g, Rectangle bounds, bool hot = false, float radius = 12f)
	{
		Card(g, bounds, hot ? 1f : 0f, radius);
	}

	public static void Card(Graphics g, Rectangle bounds, float hot, float radius = 12f)
	{
		Smooth(g);
		using GraphicsPath path = Round(new RectangleF((float)bounds.X + 1f, (float)bounds.Y + 1f, (float)bounds.Width - 2.5f, (float)bounds.Height - 2.5f), S(12f));
		using (SolidBrush brush = new SolidBrush(Mix(Card1, CardHot1, hot)))
		{
			g.FillPath(brush, path);
		}
		using Pen pen = new Pen(Mix(Border, BorderHi, hot), Theme.BW);
		g.DrawPath(pen, path);
	}

	public static void Pill(Graphics g, Rectangle r, string text, Font f, Color c, int fillAlpha = 34)
	{
		Smooth(g);
		RectangleF r2 = new RectangleF((float)r.X + 0.75f, (float)r.Y + 0.75f, (float)r.Width - 1.5f, (float)r.Height - 1.5f);
		bool flag = c.R == c.G && c.G == c.B;
		using (GraphicsPath path = Round(r2, r2.Height / 2f))
		{
			using Pen pen = new Pen(flag ? Mix(Border, c, 0.35f) : Color.FromArgb(170, c), Theme.BW);
			g.DrawPath(pen, path);
		}
		TextRenderer.DrawText(g, Up(text), f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	public static int PillW(string text, Font f)
	{
		return Measure(Up(text), f).Width + S(20f);
	}

	public static void IconBox(Graphics g, Rectangle r, string glyph, bool bright = false, float glow = 0f, Font font = null)
	{
		Smooth(g);
		RectangleF rect = new RectangleF((float)r.X + 1f, (float)r.Y + 1f, (float)r.Width - 2.5f, (float)r.Height - 2.5f);
		if (bright)
		{
			using SolidBrush brush = new SolidBrush(Accent);
			g.FillEllipse(brush, rect);
		}
		using (Pen pen = new Pen(bright ? Accent : Mix(Border, BorderHi, glow), Theme.BW))
		{
			g.DrawEllipse(pen, rect);
		}
		DrawIcon(g, glyph, font ?? ((r.Width >= S(36f)) ? Icon : IconSmall), r, bright ? OnSilver : Text);
	}

	public static void GradientText(Graphics g, string text, Font font, PointF at, float shine = -1f)
	{
		using GraphicsPath graphicsPath = new GraphicsPath();
		float emSize = font.SizeInPoints * g.DpiY / 72f;
		graphicsPath.AddString(Up(text), font.FontFamily, (int)font.Style, emSize, at, StringFormat.GenericTypographic);
		RectangleF bounds = graphicsPath.GetBounds();
		if (bounds.Width <= 0f)
		{
			return;
		}
		Smooth(g);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(bounds.X, bounds.Y - 1f, bounds.Width, bounds.Height + 2f), Color.White, Color.FromArgb(242, 242, 242), 90f))
		{
			g.FillPath(brush, graphicsPath);
		}
		if (!(shine >= 0f) || !(shine <= 1f))
		{
			return;
		}
		float num = bounds.Width * 0.35f;
		float num2 = bounds.X - num + (bounds.Width + num * 2f) * shine;
		RectangleF rect = new RectangleF(num2 - num / 2f, bounds.Y, num, bounds.Height);
		using LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(rect.X - 1f, rect.Y, rect.Width + 2f, rect.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f);
		ColorBlend colorBlend = new ColorBlend();
		colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
		colorBlend.Colors = new Color[3]
		{
			Color.FromArgb(0, 255, 255, 255),
			Color.FromArgb(200, 255, 255, 255),
			Color.FromArgb(0, 255, 255, 255)
		};
		linearGradientBrush.InterpolationColors = colorBlend;
		Region clip = g.Clip;
		g.SetClip(graphicsPath, CombineMode.Intersect);
		g.FillRectangle(linearGradientBrush, rect);
		g.Clip = clip;
		clip.Dispose();
	}

	public static void Progress(Graphics g, RectangleF r, float p, float phase)
	{
		Smooth(g);
		float num = Math.Max(3f, Math.Min(r.Height + (float)S(2f), S(6f)));
		float num2 = num * 0.75f;
		int num3 = Math.Max(1, (int)((r.Width + num2) / (num + num2)));
		float num4 = r.Y + r.Height / 2f;
		int num5 = (int)Math.Round((float)num3 * Anim.Clamp(p));
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(44, 44, 44));
		using SolidBrush brush = new SolidBrush(Color.White);
		for (int i = 0; i < num3; i++)
		{
			RectangleF rect = new RectangleF(r.X + (float)i * (num + num2), num4 - num / 2f, num, num);
			if (i < num5)
			{
				g.FillEllipse(brush, rect);
			}
			else if (i == num5 && p < 1f && phase % 0.5f < 0.3f)
			{
				Dot(g, rect.X + num / 2f, num4, num / 2f, Accent);
			}
			else
			{
				g.FillEllipse(brush2, rect);
			}
		}
	}

	public static string Up(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return L.T(s);
		}
		return s ?? "";
	}

	private static Bitmap Glow(Color c, float rad, float glow, out float half)
	{
		int num = Math.Max(1, (int)Math.Round(rad * 4f));
		int num2 = Math.Max(1, Math.Min(40, (int)Math.Round(glow * 20f)));
		long key = (long)((ulong)(uint)c.ToArgb() << 24) ^ ((long)num << 8) ^ num2;
		float num3 = (float)num / 4f * 3.2f;
		half = (float)Math.Ceiling(num3) + 1f;
		if (_glows.TryGetValue(key, out var value))
		{
			return value;
		}
		int num4 = (int)(half * 2f);
		value = new Bitmap(num4, num4, PixelFormat.Format32bppPArgb);
		using (Graphics graphics = Graphics.FromImage(value))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.Clear(Color.Transparent);
			float num5 = (float)num2 / 20f;
			using GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(half - num3, half - num3, num3 * 2f, num3 * 2f);
			using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb((int)(150f * num5), c);
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, c) };
			ColorBlend colorBlend = new ColorBlend(3);
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(0, c),
				Color.FromArgb((int)(60f * num5 * (float)(int)c.A / 255f), c.R, c.G, c.B),
				Color.FromArgb((int)(170f * num5 * (float)(int)c.A / 255f), c.R, c.G, c.B)
			};
			colorBlend.Positions = new float[3] { 0f, 0.55f, 1f };
			pathGradientBrush.InterpolationColors = colorBlend;
			graphics.FillPath(pathGradientBrush, graphicsPath);
		}
		if (_glows.Count > 400)
		{
			foreach (Bitmap value2 in _glows.Values)
			{
				value2.Dispose();
			}
			_glows.Clear();
		}
		_glows[key] = value;
		return value;
	}

	public static void Dot(Graphics g, float cx, float cy, float rad, Color c, float glow = 1f)
	{
		Smooth(g);
		if (false && glow > 0f && rad > 0.3f)
		{
			float half;
			Bitmap bitmap = Glow(c, rad, glow, out half);
			g.DrawImage(bitmap, (float)Math.Round(cx - half), (float)Math.Round(cy - half), bitmap.Width, bitmap.Height);
		}
		using SolidBrush brush = new SolidBrush(c);
		g.FillEllipse(brush, cx - rad, cy - rad, rad * 2f, rad * 2f);
	}

	public static void DotBar(Graphics g, float x, float cy, int n, int filled, float d, float gap, bool glow = true)
	{
		Smooth(g);
		float w = (float)n * d + (float)Math.Max(0, n - 1) * gap;
		float h = Math.Max(3f, d * 0.75f);
		using (GraphicsPath tp = Round(new RectangleF(x, cy - h / 2f, w, h), h / 2f))
		{
			using SolidBrush tb = new SolidBrush(Color.FromArgb(32, 32, 38));
			g.FillPath(tb, tp);
		}
		if (filled > 0 && n > 0)
		{
			float fw = Math.Max(h, w * Math.Min(1f, (float)filled / (float)n));
			using GraphicsPath fp = Round(new RectangleF(x, cy - h / 2f, fw, h), h / 2f);
			using SolidBrush fb = new SolidBrush(Color.FromArgb(201, 201, 207));
			g.FillPath(fb, fp);
		}
	}

	public static void DotLogo(Graphics g, float x, float y, float cell, Color c, bool accent = true)
	{
		string[] array = new string[7] { "#.....#", "#.....#", "#..#..#", "#..#..#", "#.#.#.#", "##...##", "#.....#" };
		Smooth(g);
		float num = cell * 0.8f;
		using SolidBrush brush = new SolidBrush(c);
		for (int i = 0; i < 7; i++)
		{
			for (int j = 0; j < 7; j++)
			{
				if (array[i][j] == '#')
				{
					if (accent && i == 2 && j == 3)
					{
						Dot(g, x + (float)j * cell + num / 2f, y + (float)i * cell + num / 2f, num / 2f, Accent, 0.8f);
					}
					else
					{
						g.FillEllipse(brush, x + (float)j * cell, y + (float)i * cell, num, num);
					}
				}
			}
		}
	}

	private static Font DotFont(float cell)
	{
		// Высота цифр ≈ 7 клеток точечной матрицы → кегль Inter SemiBold.
		float px = cell * 7f / 0.727f;
		return FC((float)Math.Round(px * 0.75f / 1.06f * 2f) / 2f, semi: true);
	}

	public static float DotTextW(string s, float cell)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0f;
		}
		return Measure(s, DotFont(cell)).Width;
	}

	public static float DotText(Graphics g, string s, float x, float y, float cell, Color c)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0f;
		}
		Font f = DotFont(cell);
		Size sz = Measure(s, f);
		float capTop = (sz.Height - cell * 7f) / 2f;
		TextRenderer.DrawText(g, s, f, new Point((int)Math.Round(x), (int)Math.Round(y - capTop)), c, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		return sz.Width;
	}

	/// <summary>Дуга-индикатор 270° как в превью: серый трек и акцентная дуга со скруглёнными концами.</summary>
	public static void Gauge(Graphics g, float cx, float cy, float r, float v, float thick)
	{
		Smooth(g);
		v = Math.Max(0f, Math.Min(1f, v));
		RectangleF rc = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
		using (Pen tp = new Pen(Color.FromArgb(32, 32, 38), thick))
		{
			tp.StartCap = LineCap.Round;
			tp.EndCap = LineCap.Round;
			g.DrawArc(tp, rc, 135f, 270f);
		}
		if (v > 0.002f)
		{
			using Pen vp = new Pen(Mix(IceHi, Accent, v), thick);
			vp.StartCap = LineCap.Round;
			vp.EndCap = LineCap.Round;
			g.DrawArc(vp, rc, 135f, 270f * v);
		}
	}

	public static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra = TextFormatFlags.Default)
	{
		TextRenderer.DrawText(g, s ?? "", f, r, c, TextFormatFlags.NoPrefix | extra);
	}

	/// <summary>Подпись, которая помещается в ширину (с учётом лёгкого ужатия шрифта до 80%):
	/// при нехватке места отбрасывает хвостовые части «•», а если и так не влезает — null.</summary>
	public static string FitOrShort(string text, Font f, int w)
	{
		if (string.IsNullOrEmpty(text) || w <= 0)
		{
			return null;
		}
		string t = text;
		while (true)
		{
			if (Measure(t, f).Width * 0.8f <= w)
			{
				return t;
			}
			int i = t.LastIndexOf(" • ", StringComparison.Ordinal);
			int j = t.LastIndexOf("  •  ", StringComparison.Ordinal);
			int k = Math.Max(i, j);
			if (k <= 0)
			{
				return null;
			}
			t = t.Substring(0, k).TrimEnd();
		}
	}

	public static Size Measure(string s, Font f, int width = int.MaxValue, bool wrap = false)
	{
		string text = (string.IsNullOrEmpty(s) ? " " : s);
		if (!wrap)
		{
			width = int.MaxValue;
		}
		MKey mKey = default(MKey);
		mKey.S = text;
		mKey.F = f;
		mKey.W = width;
		mKey.Wrap = wrap;
		MKey key = mKey;
		if (_measured.TryGetValue(key, out var value))
		{
			return value;
		}
		TextFormatFlags textFormatFlags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
		if (wrap)
		{
			textFormatFlags |= TextFormatFlags.WordBreak;
		}
		value = TextRenderer.MeasureText(text, f, new Size(width, int.MaxValue), textFormatFlags);
		if (_measured.Count > 30000)
		{
			_measured.Clear();
		}
		_measured[key] = value;
		return value;
	}

	public static void Heart(Graphics g, RectangleF r, Color c)
	{
		Smooth(g);
		using GraphicsPath graphicsPath = new GraphicsPath();
		float width = r.Width;
		float height = r.Height;
		float x = r.X;
		float y = r.Y;
		graphicsPath.AddBezier(x + width / 2f, y + height, x + width * 0.1f, y + height * 0.62f, x - width * 0.05f, y + height * 0.18f, x + width * 0.27f, y + height * 0.04f);
		graphicsPath.AddBezier(x + width * 0.27f, y + height * 0.04f, x + width * 0.42f, y - height * 0.02f, x + width / 2f, y + height * 0.12f, x + width / 2f, y + height * 0.22f);
		graphicsPath.AddBezier(x + width / 2f, y + height * 0.22f, x + width / 2f, y + height * 0.12f, x + width * 0.58f, y - height * 0.02f, x + width * 0.73f, y + height * 0.04f);
		graphicsPath.AddBezier(x + width * 0.73f, y + height * 0.04f, x + width * 1.05f, y + height * 0.18f, x + width * 0.9f, y + height * 0.62f, x + width / 2f, y + height);
		graphicsPath.CloseFigure();
		using SolidBrush brush = new SolidBrush(c);
		g.FillPath(brush, graphicsPath);
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
		return kind switch
		{
			"ok" => Green, 
			"warn" => Yellow, 
			"err" => Red, 
			_ => Silver2, 
		};
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
		using Pen pen = new Pen(c, Math.Max(1.5f, S(1.6f)));
		g.DrawEllipse(pen, (float)r.X + (float)(r.Width - num) / 2f, (float)r.Y + (float)(r.Height - num) / 2f, num, num);
	}
}
