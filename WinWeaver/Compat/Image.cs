using System;
using System.IO;
using AM = Avalonia.Media;
using AMI = Avalonia.Media.Imaging;
using AV = Avalonia;

namespace WinWeaver;

/// <summary>Растровое изображение (аналог System.Drawing.Image) поверх Avalonia Bitmap / RenderTargetBitmap.</summary>
internal abstract class Image : IDisposable
{
	public abstract int Width { get; }

	public abstract int Height { get; }

	public Size Size => new Size(Width, Height);

	internal abstract AM.IImage AvSource { get; }

	public static Image FromStream(Stream s) => new Bitmap(s);

	public static Image FromFile(string path)
	{
		using var fs = File.OpenRead(path);
		return new Bitmap(fs);
	}

	public virtual void Dispose()
	{
	}
}

internal sealed class Bitmap : Image
{
	private AMI.Bitmap _src;

	private AMI.RenderTargetBitmap _rt;

	private readonly int _w;

	private readonly int _h;

	internal int OpenContexts;

	public override int Width => _w;

	public override int Height => _h;

	internal override AM.IImage AvSource => (AM.IImage)_rt ?? _src;

	/// <summary>Масштаб экрана (RenderScaling) для новых «пустых» битмапов-кэшей,
	/// чтобы на HiDPI они не были размытыми. Логический размер (Width/Height) не меняется.</summary>
	internal static double RenderScale = 1;

	/// <summary>Пикселей на логическую единицу у этого битмапа.</summary>
	internal double Scale { get; private set; } = 1;

	internal int PixelWidth => (int)Math.Ceiling(_w * Scale);

	internal int PixelHeight => (int)Math.Ceiling(_h * Scale);

	public Bitmap(int width, int height)
	{
		_w = Math.Max(1, width);
		_h = Math.Max(1, height);
		Scale = RenderScale;
	}

	public Bitmap(int width, int height, PixelFormat format)
		: this(width, height)
	{
	}

	public Bitmap(Stream s)
	{
		_src = new AMI.Bitmap(s);
		_w = _src.PixelSize.Width;
		_h = _src.PixelSize.Height;
	}

	public Bitmap(string path)
	{
		using var fs = File.OpenRead(path);
		_src = new AMI.Bitmap(fs);
		_w = _src.PixelSize.Width;
		_h = _src.PixelSize.Height;
	}

	internal Bitmap(AMI.Bitmap src)
	{
		_src = src;
		_w = src.PixelSize.Width;
		_h = src.PixelSize.Height;
	}

	public Bitmap(Image original)
		: this(original, original.Width, original.Height)
	{
	}

	public Bitmap(Image original, int width, int height)
		: this(width, height)
	{
		if (original is Bitmap b && b._src != null && b._rt == null)
		{
			Scale = 1;
			_src = b._src.CreateScaledBitmap(new AV.PixelSize(_w, _h), AM.Imaging.BitmapInterpolationMode.HighQuality);
			return;
		}
		using var g = Graphics.FromImage(this);
		g.DrawImage(original, 0, 0, _w, _h);
	}

	public Bitmap(Image original, Size size)
		: this(original, size.Width, size.Height)
	{
	}

	/// <summary>Возвращает поверхность для рисования (создаётся при первом обращении).</summary>
	internal AMI.RenderTargetBitmap EnsureTarget()
	{
		if (_rt == null)
		{
			_rt = new AMI.RenderTargetBitmap(new AV.PixelSize(PixelWidth, PixelHeight), new AV.Vector(96, 96));
			if (_src != null)
			{
				using (var dc = _rt.CreateDrawingContext(false))
				{
					dc.DrawImage(_src, new AV.Rect(0, 0, _w, _h));
				}
				_src = null;
			}
		}
		return _rt;
	}

	public void Save(Stream s)
	{
		if (_rt != null)
		{
			_rt.Save(s);
		}
		else
		{
			_src?.Save(s);
		}
	}

	public void Save(string path)
	{
		using var fs = File.Create(path);
		Save(fs);
	}

	public override void Dispose()
	{
		// Битмапы Avalonia освобождаются сборщиком мусора: явный Dispose
		// может прийтись на кадр, где картинка ещё используется в отрисовке.
	}
}

/// <summary>Значок окна/приложения.</summary>
internal sealed class Icon : IDisposable
{
	private readonly byte[] _bytes;

	private Bitmap _bmp;

	public int Width => ToBitmap().Width;

	public int Height => ToBitmap().Height;

	public Icon(Stream s)
	{
		using var ms = new MemoryStream();
		s.CopyTo(ms);
		_bytes = ms.ToArray();
	}

	public Icon(string path, int w, int h)
	{
		_bytes = NativeIcons.ExtractPng(path, Math.Max(w, h)) ?? throw new FileNotFoundException(path);
	}

	internal Icon(byte[] bytes)
	{
		_bytes = bytes;
	}

	public static Icon ExtractAssociatedIcon(string path)
	{
		var png = NativeIcons.ExtractPng(path, 32);
		return png == null ? null : new Icon(png);
	}

	internal Avalonia.Controls.WindowIcon ToWindowIcon()
	{
		using var ms = new MemoryStream(_bytes);
		return new Avalonia.Controls.WindowIcon(ms);
	}

	public Bitmap ToBitmap()
	{
		if (_bmp == null)
		{
			try
			{
				using var ms = new MemoryStream(_bytes);
				_bmp = new Bitmap(ms);
			}
			catch
			{
				_bmp = new Bitmap(32, 32);
			}
		}
		return _bmp;
	}

	public void Dispose()
	{
	}
}

internal sealed class ColorMatrix
{
	public float Matrix00 { get; set; } = 1f;

	public float Matrix11 { get; set; } = 1f;

	public float Matrix22 { get; set; } = 1f;

	public float Matrix33 { get; set; } = 1f;

	public float Matrix44 { get; set; } = 1f;
}

internal sealed class ImageAttributes : IDisposable
{
	internal float Opacity = 1f;

	public void SetColorMatrix(ColorMatrix m, ColorMatrixFlag flags = ColorMatrixFlag.Default, ColorAdjustType type = ColorAdjustType.Default)
	{
		Opacity = m?.Matrix33 ?? 1f;
	}

	public void SetWrapMode(WrapMode mode)
	{
	}

	public void Dispose()
	{
	}
}

/// <summary>Извлечение значков из exe (только Windows, через GDI+ из .NET Framework).</summary>
internal static class NativeIcons
{
	public static byte[] ExtractPng(string path, int size)
	{
#if NETFRAMEWORK
		try
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return null;
			}
			System.Drawing.Icon ico;
			try
			{
				ico = new System.Drawing.Icon(path, size, size);
			}
			catch
			{
				ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
			}
			if (ico == null)
			{
				return null;
			}
			using (ico)
			using (var bmp = ico.ToBitmap())
			using (var ms = new MemoryStream())
			{
				bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
				return ms.ToArray();
			}
		}
		catch
		{
			return null;
		}
#else
		return null;
#endif
	}
}
