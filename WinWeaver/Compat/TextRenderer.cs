using System;
using System.Collections.Generic;
using System.Globalization;
using AM = Avalonia.Media;
using AMT = Avalonia.Media.TextFormatting;
using AV = Avalonia;

namespace WinWeaver;

/// <summary>
/// Замена System.Windows.Forms.TextRenderer: отрисовка и измерение текста через Avalonia TextLayout
/// с поддержкой флагов TextFormatFlags (выравнивание, перенос, многоточие, отступы).
/// Все строки проходят через локализацию L.T, как и в исходной программе.
/// </summary>
internal static class TextRenderer
{
	private readonly struct Key : IEquatable<Key>
	{
		public readonly string S;
		public readonly AM.Typeface F;
		public readonly double P;
		public readonly int C;
		public readonly double W;
		public readonly double H;
		public readonly int Mode;

		public Key(string s, Font f, int c, double w, double h, int mode)
		{
			S = s;
			F = f.Typeface;
			P = f.PixelSize;
			C = c;
			W = w;
			H = h;
			Mode = mode;
		}

		public bool Equals(Key o) => C == o.C && W == o.W && H == o.H && Mode == o.Mode && P == o.P && F.Equals(o.F) && string.Equals(S, o.S, StringComparison.Ordinal);

		public override bool Equals(object o) => o is Key k && Equals(k);

		public override int GetHashCode()
		{
			unchecked
			{
				int h = S.GetHashCode();
				h = h * 31 + F.GetHashCode();
				h = h * 31 + P.GetHashCode();
				h = h * 31 + C;
				h = h * 31 + W.GetHashCode();
				h = h * 31 + H.GetHashCode();
				return h * 31 + Mode;
			}
		}
	}

	private static readonly Dictionary<Key, AMT.TextLayout> _cache = new Dictionary<Key, AMT.TextLayout>();

	private static readonly Dictionary<Font, double> _lineH = new Dictionary<Font, double>();

	internal static double LineHeight(Font f)
	{
		lock (_lineH)
		{
			if (!_lineH.TryGetValue(f, out var h))
			{
				var l = new AMT.TextLayout("Ag", f.Typeface, f.PixelSize, AM.Brushes.White);
				h = l.Height;
				_lineH[f] = h;
			}
			return h;
		}
	}

	private static int Pad(Font f, TextFormatFlags flags)
	{
		if ((flags & TextFormatFlags.NoPadding) != 0)
		{
			return 0;
		}
		int p = (int)Math.Ceiling(f.Height / 6.0);
		if ((flags & TextFormatFlags.LeftAndRightPadding) != 0)
		{
			p *= 2;
		}
		return p;
	}

	private static string Prep(string text, TextFormatFlags flags)
	{
		string s = L.T(text ?? "");
		if ((flags & TextFormatFlags.NoPrefix) == 0 && s.IndexOf('&') >= 0)
		{
			var sb = new System.Text.StringBuilder(s.Length);
			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] == '&')
				{
					if (i + 1 < s.Length && s[i + 1] == '&')
					{
						sb.Append('&');
						i++;
					}
					continue;
				}
				sb.Append(s[i]);
			}
			s = sb.ToString();
		}
		if ((flags & TextFormatFlags.SingleLine) != 0)
		{
			s = s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
		}
		else
		{
			s = s.Replace("\r\n", "\n");
		}
		if ((flags & TextFormatFlags.ExpandTabs) != 0)
		{
			s = s.Replace("\t", "        ");
		}
		return s;
	}

	private static AMT.TextLayout Layout(string s, Font f, Color c, double maxW, double maxH, TextFormatFlags flags)
	{
		bool wrap = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0;
		AM.TextTrimming trim = AM.TextTrimming.None;
		if ((flags & TextFormatFlags.WordEllipsis) != 0)
		{
			trim = AM.TextTrimming.WordEllipsis;
		}
		else if ((flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.PathEllipsis)) != 0)
		{
			trim = AM.TextTrimming.CharacterEllipsis;
		}
		AM.TextAlignment align = (flags & TextFormatFlags.HorizontalCenter) != 0 ? AM.TextAlignment.Center
			: (flags & TextFormatFlags.Right) != 0 ? AM.TextAlignment.Right : AM.TextAlignment.Left;
		bool needW = wrap || trim != AM.TextTrimming.None || align != AM.TextAlignment.Left;
		double w = needW && maxW > 0 && maxW < 1e6 ? Math.Max(1, maxW) : double.PositiveInfinity;
		double h = wrap && trim != AM.TextTrimming.None && maxH > 0 && maxH < 1e6 ? Math.Max(f.Height, maxH) : double.PositiveInfinity;
		if (double.IsInfinity(w) && align != AM.TextAlignment.Left)
		{
			align = AM.TextAlignment.Left;
		}
		int mode = (wrap ? 1 : 0) | ((int)trim.GetHashCode() & 0xff) << 1 | (int)align << 12;
		var key = new Key(s, f, c.ToArgb(), w, h, trim == AM.TextTrimming.None ? mode : mode | (trim == AM.TextTrimming.WordEllipsis ? 1 << 20 : 1 << 21));
		lock (_cache)
		{
			if (_cache.TryGetValue(key, out var l))
			{
				return l;
			}
			if (_cache.Count > 6000)
			{
				_cache.Clear();
			}
			l = new AMT.TextLayout(s.Length == 0 ? " " : s, f.Typeface, f.PixelSize, Conv.Solid(c), align,
				wrap ? AM.TextWrapping.Wrap : AM.TextWrapping.NoWrap,
				trim == AM.TextTrimming.None ? null : trim,
				null, AM.FlowDirection.LeftToRight, w, h);
			_cache[key] = l;
			return l;
		}
	}

	// Однострочный текст с многоточием: если чуть-чуть не влезает, сначала слегка уменьшаем шрифт
	// (до 80%), и только потом обрезаем. Так подписи остаются целиком.
	private static readonly Dictionary<long, Font> _fit = new Dictionary<long, Font>();

	internal static Font FitFont(string s, Font font, double availW, TextFormatFlags flags)
	{
		bool wrap = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0;
		if (wrap || (flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.PathEllipsis | TextFormatFlags.WordEllipsis)) == 0)
		{
			return font;
		}
		var full = Layout(s, font, Color.White, double.PositiveInfinity, double.PositiveInfinity, TextFormatFlags.SingleLine);
		double need = LayoutWidth(full);
		if (need <= availW + 0.5)
		{
			return font;
		}
		double k = availW / need;
		if (k < 0.8)
		{
			k = 0.8;
		}
		int step = (int)Math.Floor(k * 40.0);
		long key = ((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(font) << 8) | (long)step;
		lock (_fit)
		{
			if (!_fit.TryGetValue(key, out var f))
			{
				f = new Font(font.FontFamily, font.Size * step / 40f, font.Style, font.Unit);
				if (_fit.Count > 2000) _fit.Clear();
				_fit[key] = f;
			}
			return f;
		}
	}

	internal static readonly bool Audit = Environment.GetEnvironmentVariable("WW_TRUNC") == "1";

	private static readonly HashSet<string> _audited = new HashSet<string>();

	private static void AuditTrunc(string s, Font font, double availW, double availH, TextFormatFlags flags)
	{
		bool wrap = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0;
		bool trim = (flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.PathEllipsis | TextFormatFlags.WordEllipsis)) != 0;
		bool bad;
		if (wrap)
		{
			var full = Layout(s, font, Color.White, availW, double.PositiveInfinity, flags & ~(TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis));
			bad = full.Height > availH + 1 && availH > 0;
		}
		else
		{
			if (!trim) return;
			font = FitFont(s, font, availW, flags);
			var full = Layout(s, font, Color.White, double.PositiveInfinity, double.PositiveInfinity, TextFormatFlags.SingleLine);
			bad = LayoutWidth(full) > availW + 0.5;
		}
		if (!bad) return;
		string who = "?";
		var st = new System.Diagnostics.StackTrace(2, false);
		for (int i = 0; i < st.FrameCount; i++)
		{
			var m = st.GetFrame(i)?.GetMethod();
			var t = m?.DeclaringType;
			if (t != null && t != typeof(TextRenderer) && t.Name != "Theme")
			{
				who = t.Name + "." + m.Name;
				break;
			}
		}
		string k = who + "|" + s;
		lock (_audited)
		{
			if (_audited.Add(k))
			{
				Console.Error.WriteLine("TRUNC " + who + " w=" + (int)availW + " h=" + (int)availH + " :: " + (s.Length > 70 ? s.Substring(0, 70) : s));
			}
		}
	}

	private static double LayoutWidth(AMT.TextLayout l) => l.WidthIncludingTrailingWhitespace;

	// ---------- Ядро ----------

	private static void DrawCore(IDeviceContext dc, string text, Font font, Rectangle r, Color fore, Color back, TextFormatFlags flags, bool bounded)
	{
		if (!(dc is Graphics g) || font == null)
		{
			return;
		}
		string s = Prep(text, flags);
		if (back.A > 0 && bounded)
		{
			using var b = new SolidBrush(back);
			g.FillRectangle(b, r);
		}
		if (s.Length == 0 || fore.A == 0)
		{
			return;
		}
		int pad = Pad(font, flags);
		bool wrapped = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0;
		// Перенос строк — по той же ширине, что и Theme.Measure (без отступов), иначе последняя строка
		// может «уехать» за рассчитанную высоту и обрезаться.
		double availW = bounded ? Math.Max(0, r.Width - pad * 2) : double.PositiveInfinity;
		double availH = bounded ? r.Height : double.PositiveInfinity;
		// Прозрачность текста — через PushOpacity, чтобы анимации затухания не плодили раскладки.
		double alpha = fore.A / 255.0;
		if (bounded && availW > 0 && availW < 1e6)
		{
			font = FitFont(s, font, availW, flags);
		}
		var l = Layout(s, font, Color.FromArgb(255, fore), availW, availH, flags);
		bool nopad = false;
		if (wrapped && bounded && pad > 0 && l.Height > availH + 1 && availH > 0)
		{
			var l2 = Layout(s, font, Color.FromArgb(255, fore), Math.Max(0, r.Width), availH, flags);
			if (l2.Height < l.Height)
			{
				l = l2;
				availW = r.Width;
				nopad = true;
			}
		}
		if (Audit && bounded)
		{
			AuditTrunc(s, font, availW, availH, flags);
		}
		double x = r.X + (nopad ? 0 : pad);
		double y = r.Y;
		if (bounded)
		{
			if ((flags & TextFormatFlags.Bottom) != 0)
			{
				y = r.Bottom - l.Height;
			}
			else if ((flags & TextFormatFlags.VerticalCenter) != 0)
			{
				y = r.Y + Math.Round((r.Height - l.Height) / 2.0);
			}
			if (double.IsInfinity(l.MaxWidth) || l.MaxWidth <= 0)
			{
				double lw = LayoutWidth(l);
				if ((flags & TextFormatFlags.HorizontalCenter) != 0)
				{
					x = r.X + pad + Math.Round((availW - lw) / 2.0);
				}
				else if ((flags & TextFormatFlags.Right) != 0)
				{
					x = r.Right - pad - lw;
				}
			}
		}
		if (bounded && (flags & TextFormatFlags.NoClipping) == 0)
		{
			g.PushRawClip(new AV.Rect(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height)), () => g.DrawLayout(l, x, y, alpha));
		}
		else
		{
			g.DrawLayout(l, x, y, alpha);
		}
	}

	private static Size MeasureCore(string text, Font font, Size proposed, TextFormatFlags flags)
	{
		if (font == null)
		{
			return Size.Empty;
		}
		string s = Prep(text, flags);
		int pad = Pad(font, flags);
		if (s.Length == 0)
		{
			return new Size(0, font.Height);
		}
		double maxW = (proposed.Width <= 0 || proposed.Width >= int.MaxValue / 2) ? double.PositiveInfinity : Math.Max(1, proposed.Width - pad * 2);
		var mflags = flags & ~(TextFormatFlags.HorizontalCenter | TextFormatFlags.Right);
		if ((flags & TextFormatFlags.WordBreak) == 0)
		{
			maxW = double.PositiveInfinity;
			mflags &= ~(TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis);
		}
		var l = Layout(s, font, Color.White, maxW, double.PositiveInfinity, mflags & ~(TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis));
		double w = (mflags & TextFormatFlags.WordBreak) != 0 ? l.Width : LayoutWidth(l);
		return new Size((int)Math.Ceiling(w) + pad * 2, (int)Math.Ceiling(l.Height));
	}

	// ---------- API как у WinForms ----------

	private static readonly Rectangle Unbounded = new Rectangle(0, 0, int.MaxValue / 4, int.MaxValue / 4);

	public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color fore)
		=> DrawCore(dc, text, font, new Rectangle(pt, Unbounded.Size), fore, Color.Empty, TextFormatFlags.Default, bounded: false);

	public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color fore, TextFormatFlags flags)
		=> DrawCore(dc, text, font, new Rectangle(pt, Unbounded.Size), fore, Color.Empty, flags, bounded: false);

	public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color fore, Color back)
		=> DrawCore(dc, text, font, new Rectangle(pt, Unbounded.Size), fore, back, TextFormatFlags.Default, bounded: false);

	public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color fore, Color back, TextFormatFlags flags)
		=> DrawCore(dc, text, font, new Rectangle(pt, Unbounded.Size), fore, back, flags, bounded: false);

	public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle r, Color fore)
		=> DrawCore(dc, text, font, r, fore, Color.Empty, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter, bounded: true);

	public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle r, Color fore, TextFormatFlags flags)
		=> DrawCore(dc, text, font, r, fore, Color.Empty, flags, bounded: true);

	public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle r, Color fore, Color back)
		=> DrawCore(dc, text, font, r, fore, back, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter, bounded: true);

	public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle r, Color fore, Color back, TextFormatFlags flags)
		=> DrawCore(dc, text, font, r, fore, back, flags, bounded: true);

	public static Size MeasureText(string text, Font font) => MeasureCore(text, font, Size.Empty, TextFormatFlags.Default);

	public static Size MeasureText(string text, Font font, Size proposed) => MeasureCore(text, font, proposed, TextFormatFlags.Default);

	public static Size MeasureText(string text, Font font, Size proposed, TextFormatFlags flags) => MeasureCore(text, font, proposed, flags);

	public static Size MeasureText(IDeviceContext dc, string text, Font font) => MeasureCore(text, font, Size.Empty, TextFormatFlags.Default);

	public static Size MeasureText(IDeviceContext dc, string text, Font font, Size proposed) => MeasureCore(text, font, proposed, TextFormatFlags.Default);

	public static Size MeasureText(IDeviceContext dc, string text, Font font, Size proposed, TextFormatFlags flags) => MeasureCore(text, font, proposed, flags);
}
