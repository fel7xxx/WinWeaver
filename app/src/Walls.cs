// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

// WinWeaver — Windows 11 setup tool
// Copyright © 2026 fel7xxx. Licensed under GPL-3.0-or-later, see LICENSE.
using System;
using System.Drawing;
using System.IO;

namespace WinWeaver
{
	// built-in WeavOS wallpapers (3840×2160 JPEG) and their small previews
	internal static class Walls
	{
		public static readonly string[] Names = { "Ледяной шёлк", "Графит", "Сияние", "Серебро" };

		public static int Count => Names.Length;

		public static int Clamp(int i)
		{
			return (i < 0 || i >= Count) ? 0 : i;
		}

		public static byte[] Bytes(int i, bool thumb = false)
		{
			return Res("wall" + (Clamp(i) + 1) + (thumb ? "_t" : "") + ".jpg");
		}

		// the same picture without the "WeavOS" lettering, 1600 px — background of the home banner
		public static Image Banner(int i, int maxW)
		{
			return Decode(Res("wall" + (Clamp(i) + 1) + "_h.jpg"), maxW);
		}

		private static byte[] Res(string name)
		{
			try
			{
				using (Stream s = typeof(Walls).Assembly.GetManifestResourceStream(name))
				{
					if (s == null)
					{
						return null;
					}
					MemoryStream m = new MemoryStream();
					s.CopyTo(m);
					return m.ToArray();
				}
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
				using (MemoryStream m = new MemoryStream(b))
				using (Image src = Image.FromStream(m))
				{
					int w = Math.Min(src.Width, maxW);
					int h = Math.Max(1, src.Height * w / src.Width);
					Bitmap bmp = new Bitmap(w, h);
					using (Graphics g = Graphics.FromImage(bmp))
					{
						g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
						g.DrawImage(src, 0, 0, w, h);
					}
					return bmp;
				}
			}
			catch
			{
				return null;
			}
		}
	}
}
