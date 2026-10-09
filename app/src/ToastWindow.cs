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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinWeaver
{
	// Layered (per-pixel alpha) host for the task toast: soft dark glow around the card + real fade.
	internal class ToastWindow : Form
	{
		private readonly StatusBar _bar;

		private int Glow => Theme.S(26f);

		[StructLayout(LayoutKind.Sequential)]
		private struct PT
		{
			public int x;

			public int y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SZ
		{
			public int cx;

			public int cy;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct BLEND
		{
			public byte Op;

			public byte Flags;

			public byte Alpha;

			public byte Format;
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pprSrc, int crKey, ref BLEND pblend, int dwFlags);

		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr h);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr h, IntPtr dc);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateCompatibleDC(IntPtr dc);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteDC(IntPtr dc);

		[DllImport("gdi32.dll")]
		private static extern IntPtr SelectObject(IntPtr dc, IntPtr o);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr o);

		public ToastWindow(StatusBar bar)
		{
			_bar = bar;
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			AutoScaleMode = AutoScaleMode.None;
		}

		protected override bool ShowWithoutActivation => true;

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				cp.ExStyle |= 0x80000 | 0x80 | 0x8000000; // WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
				return cp;
			}
		}

		public void ShowOn(Form owner)
		{
			if (owner != null)
			{
				Owner = owner;
			}
			Show();
			Render();
		}

		public void Render()
		{
			if (IsDisposed || _bar.Width <= 0 || _bar.Height <= 0)
			{
				return;
			}
			int gl = Glow;
			Rectangle sr = _bar.ScreenRect;
			int w = sr.Width + gl * 2;
			int h = sr.Height + gl * 2;
			using (Bitmap card = new Bitmap(sr.Width, sr.Height, PixelFormat.Format32bppRgb))
			using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
			{
				using (Graphics cg = Graphics.FromImage(card))
				{
					_bar.PaintTo(cg);
				}
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.Transparent);
					g.SmoothingMode = SmoothingMode.AntiAlias;
					// dark glow: stacked translucent rounded rects, denser towards the card, a bit lower than it
					int steps = 13;
					for (int i = 0; i < steps; i++)
					{
						float k = (float)i / (steps - 1);
						float grow = gl * (1f - k);
						RectangleF r = new RectangleF(gl - grow, gl - grow + Theme.S(5f) * (1f - k * 0.6f), sr.Width + grow * 2f, sr.Height + grow * 2f);
						using (GraphicsPath gp = Theme.Round(r, Theme.S(12f) + grow))
						using (SolidBrush sb = new SolidBrush(Color.FromArgb((int)(6 + 16 * k * k), 0, 0, 0)))
						{
							g.FillPath(sb, gp);
						}
					}
					using (GraphicsPath cp = Theme.Round(new RectangleF(gl, gl, sr.Width, sr.Height), Theme.S(12f)))
					using (TextureBrush tb = new TextureBrush(card))
					{
						tb.TranslateTransform(gl, gl);
						g.FillPath(tb, cp);
					}
				}
				Push(bmp, new Point(sr.X - gl, sr.Y - gl), (byte)Math.Max(0, Math.Min(255, (int)(255f * _bar.Fade))));
			}
		}

		private void Push(Bitmap bmp, Point pos, byte alpha)
		{
			IntPtr screen = GetDC(IntPtr.Zero);
			IntPtr mem = CreateCompatibleDC(screen);
			IntPtr hb = IntPtr.Zero;
			IntPtr old = IntPtr.Zero;
			try
			{
				hb = bmp.GetHbitmap(Color.FromArgb(0));
				old = SelectObject(mem, hb);
				SZ size = new SZ { cx = bmp.Width, cy = bmp.Height };
				PT src = new PT { x = 0, y = 0 };
				PT dst = new PT { x = pos.X, y = pos.Y };
				BLEND bl = new BLEND { Op = 0, Flags = 0, Alpha = alpha, Format = 1 };
				UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bl, 2);
			}
			finally
			{
				ReleaseDC(IntPtr.Zero, screen);
				if (hb != IntPtr.Zero)
				{
					SelectObject(mem, old);
					DeleteObject(hb);
				}
				DeleteDC(mem);
			}
		}

		private Point Local(Point p)
		{
			return new Point(p.X - Glow, p.Y - Glow);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			_bar.WinMove(Local(e.Location));
			Cursor = _bar.Cursor;
			base.OnMouseMove(e);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			_bar.WinEnter();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_bar.WinLeave();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			Point p = Local(e.Location);
			if (p.X >= 0 && p.Y >= 0 && p.X < _bar.Width && p.Y < _bar.Height)
			{
				_bar.WinClick(p, e.Button);
			}
			base.OnMouseClick(e);
		}
	}
}
