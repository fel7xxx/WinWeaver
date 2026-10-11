using System;
using System.IO;

namespace WinWeaver;

internal static class Walls
{
	public static readonly string[] Names = new string[4] { "Ледяной шёлк", "Графит", "Сияние", "Серебро" };

	public static int Count => Names.Length;

	public static int Clamp(int i)
	{
		if (i >= 0 && i < Count)
		{
			return i;
		}
		return 0;
	}

	public static byte[] Bytes(int i, bool thumb = false)
	{
		return Res("wall" + (Clamp(i) + 1) + (thumb ? "_t" : "") + ".jpg");
	}

	public static Image Banner(int i, int maxW)
	{
		return Decode(Res("wall" + (Clamp(i) + 1) + "_h.jpg"), maxW);
	}

	private static byte[] Res(string name)
	{
		try
		{
			using Stream stream = typeof(Walls).Assembly.GetManifestResourceStream(name);
			if (stream == null)
			{
				return null;
			}
			MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			return null;
		}
	}

	public static Image Load(int i, int maxW)
	{
		return Decode(Bytes(i, maxW <= 480), maxW);
	}

	private static Image Decode(byte[] b, int maxW)
	{
		if (b == null)
		{
			return null;
		}
		try
		{
			using MemoryStream stream = new MemoryStream(b);
			using Image image = Image.FromStream(stream);
			int num = Math.Min(image.Width, maxW);
			int height = Math.Max(1, image.Height * num / image.Width);
			Bitmap bitmap = new Bitmap(num, height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.DrawImage(image, 0, 0, num, height);
			}
			return bitmap;
		}
		catch
		{
			return null;
		}
	}
}
