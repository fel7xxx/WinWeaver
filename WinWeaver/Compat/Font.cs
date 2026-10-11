using System;
using System.Collections.Generic;
using AM = Avalonia.Media;

namespace WinWeaver;

/// <summary>Семейство шрифтов (аналог System.Drawing.FontFamily) поверх Avalonia.</summary>
internal sealed class FontFamily
{
	public const string UiUri = "avares://WinWeaver/Assets/Fonts#Inter";
	public const string EmbeddedUri = "avares://WinWeaver/Assets/Fonts#JetBrains Mono";

	public string Name { get; }

	internal FontFamily(string name)
	{
		Name = name;
	}

	public static FontFamily GenericMonospace => new FontFamily("Consolas");

	public static FontFamily GenericSansSerif => new FontFamily("Segoe UI");

	private static readonly Dictionary<string, AM.FontFamily> _cache = new Dictionary<string, AM.FontFamily>();

	/// <summary>Возвращает Avalonia-семейство и вес для имени из старого кода.</summary>
	internal static (AM.FontFamily family, AM.FontWeight weight) Resolve(string name)
	{
		AM.FontWeight w = AM.FontWeight.Normal;
		string key = name ?? "";
		if (key == "JetBrains Mono SemiBold")
		{
			key = "JetBrains Mono";
			w = AM.FontWeight.SemiBold;
		}
		else if (key == "UI SemiBold")
		{
			key = "UI";
			w = AM.FontWeight.SemiBold;
		}
		else if (key == "UI Medium")
		{
			key = "UI";
			w = AM.FontWeight.Medium;
		}
		lock (_cache)
		{
			if (!_cache.TryGetValue(key, out var fam))
			{
				fam = key == "UI" ? new AM.FontFamily(UiUri) : key == "JetBrains Mono" ? new AM.FontFamily(EmbeddedUri) : new AM.FontFamily(key.Length == 0 ? "Segoe UI" : key);
				_cache[key] = fam;
			}
			return (fam, w);
		}
	}
}

/// <summary>Шрифт (аналог System.Drawing.Font). Размер хранится в исходных единицах, отрисовка — в DIP Avalonia.</summary>
internal sealed class Font : IDisposable
{
	public string Name { get; }

	public float Size { get; }

	public FontStyle Style { get; }

	public GraphicsUnit Unit { get; }

	public FontFamily FontFamily { get; }

	public bool Bold => (Style & FontStyle.Bold) != 0;

	public bool Italic => (Style & FontStyle.Italic) != 0;

	public float SizeInPoints => Unit switch
	{
		GraphicsUnit.Pixel => Size * 72f / 96f,
		GraphicsUnit.World => Size * 72f / 96f,
		GraphicsUnit.Inch => Size * 72f,
		GraphicsUnit.Millimeter => Size * 72f / 25.4f,
		GraphicsUnit.Document => Size * 72f / 300f,
		_ => Size,
	};

	/// <summary>Размер в пикселях (DIP) — как GDI при 96 DPI.</summary>
	public double PixelSize => SizeInPoints * 96.0 / 72.0;

	internal readonly AM.Typeface Typeface;

	private int _height = -1;

	public Font(string family, float emSize, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
	{
		Name = family;
		Size = emSize;
		Style = style;
		Unit = unit;
		FontFamily = new FontFamily(family);
		var (fam, w) = FontFamily.Resolve(family);
		if (Bold)
		{
			w = AM.FontWeight.Bold;
		}
		Typeface = new AM.Typeface(fam, Italic ? AM.FontStyle.Italic : AM.FontStyle.Normal, w);
	}

	public Font(FontFamily family, float emSize, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
		: this(family?.Name, emSize, style, unit)
	{
	}

	public Font(Font prototype, FontStyle style)
		: this(prototype.Name, prototype.Size, style, prototype.Unit)
	{
	}

	/// <summary>Высота строки в пикселях (как Font.Height в GDI).</summary>
	public int Height
	{
		get
		{
			if (_height < 0)
			{
				_height = Math.Max(1, (int)Math.Ceiling(TextRenderer.LineHeight(this)));
			}
			return _height;
		}
	}

	public float GetHeight() => Height;

	public void Dispose()
	{
	}
}
