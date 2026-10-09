// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace WinWeaver
{
	// Home banner: WeavOS wallpaper, greeting, system specs with animated bars and clickable status chips
	internal class Hero : Control, IBlock
	{
		private class Chip
		{
			public string Key;

			public string Text;

			public Color Color;

			public Rectangle R;
		}

		private Image _img;

		public string Line1 = Brand.Name;

		public string Line2 = "Windows 11";

		public string Line3 = "";

		public bool NeedReboot;

		private string _cpu = "";

		private string _gpu = "";

		private string _ram = "";

		private string _disk = "";

		private readonly Tween _ramBar;

		private readonly Tween _diskBar;

		private readonly Tween _appear;

		private float _shine = -1f;

		private bool _loaded;

		private readonly Chip[] _chips = new Chip[3]
		{
			new Chip { Key = "level", Text = "…", Color = Theme.Sub },
			new Chip { Key = "privacy", Text = "…", Color = Theme.Sub },
			new Chip { Key = "history", Text = "…", Color = Theme.Sub }
		};

		private string _hot;

		public event Action<string> ChipClicked;

		// optimization score: settings that match the recommendation
		private int _ok = -1;

		private int _total;

		private readonly Tween _ring;

		private Rectangle _fixR;

		private Rectangle _viewR;

		private Rectangle _scoreR;

		private Rectangle _rebootR;

		private string _greet = "";

		public bool Fixing;

		private int _bad = -1;

		public void SetScore(int ok, int total, int bad = -1)
		{
			_ok = ok;
			_total = total;
			_bad = bad;
			_ring.Target = (total > 0) ? ((float)ok / (float)total) : 0f;
			Invalidate();
		}

		public static readonly string[] LevelNames = new string[3] { "light", "optimal", "gamer" };

		public static string LevelName(string id)
		{
			switch (id)
			{
			case "light":
				return "Базовая";
			case "optimal":
				return "Оптимальная";
			case "gamer":
				return "Максимальная";
			default:
				return null;
			}
		}

		public Hero()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			_ramBar = new Tween(this, 0f, 0.12f).Timed(800);
			_diskBar = new Tween(this, 0f, 0.12f).Timed(800);
			_appear = new Tween(this, 0f, 0.14f);
			_ring = new Tween(this, 0f, 0.08f).Timed(900);
			_img = LoadWallpaper(1600);
		}

		// the WeavOS wallpaper that is set on the desktop (the variant chosen last time)
		public static Image LoadWallpaper(int maxW)
		{
			return Walls.Banner(Prefs.LastWallpaper, maxW) ?? Walls.Load(Prefs.LastWallpaper, maxW);
		}

		private static Image Shrink(Image src, int maxW)
		{
			int w = Math.Min(src.Width, maxW);
			int h = Math.Max(1, src.Height * w / src.Width);
			Bitmap bitmap = new Bitmap(w, h);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.DrawImage(src, 0, 0, w, h);
			}
			return bitmap;
		}

		public int DoLayout(int x, int y, int w)
		{
			int h = Theme.S(250f);
			SetBounds(x, y, w, h);
			return h;
		}

		public object Fetch(IEngine e)
		{
			return new object[4]
			{
				e.Eval("Get-FSysInfo"),
				e.Str(e.Eval("Get-FLevelId")),
				0,
				e.As(e.Eval("Get-FHistCount"), 0)
			};
		}

		public void Apply(object v)
		{
			object[] a = v as object[];
			if (a == null || a.Length < 4)
			{
				return;
			}
			if (a[0] is Hashtable h)
			{
				Line2 = S(h["OS"], "Windows 11") + ((h["Build"] != null) ? ("  •  сборка " + h["Build"]) : "");
				int hr = DateTime.Now.Hour;
				string hi = (hr >= 5 && hr < 12) ? "Доброе утро" : ((hr >= 12 && hr < 18) ? "Добрый день" : ((hr >= 18 && hr < 23) ? "Добрый вечер" : "Доброй ночи"));
				_greet = (hi + ((h["User"] != null) ? (", " + h["User"]) : "")).ToUpperInvariant();
				Line3 = (S(h["PC"], "") + ((h["Uptime"] != null) ? ("  •  работает " + h["Uptime"]) : "")).Trim(' ', '•');
				_cpu = S(h["CPU"], "—");
				_gpu = S(h["GPU"], "—");
				_ram = S(h["RAM"], "—");
				_disk = (S(h["Drive"], "") + " " + S(h["DiskFree"], "")).Trim();
				if (_disk == "")
				{
					_disk = "—";
				}
				_ramBar.Target = Pct(h["RAMUsed"]);
				_diskBar.Target = Pct(h["DiskUsed"]);
			}
			string lvl = LevelName(a[1] as string);
			_chips[0].Text = (lvl != null) ? lvl : "не выбран";
			_chips[0].Color = (lvl != null) ? Theme.Green : Theme.Yellow;
			int score = (a[2] is int) ? ((int)a[2]) : (-1);
			_chips[1].Text = (score >= 0) ? ("Слежка выкл.: " + score + "/4") : "Приватность";
			_chips[1].Color = (score >= 4) ? Theme.Green : ((score >= 2) ? Theme.Silver1 : Theme.Yellow);
			int n = (a[3] is int) ? ((int)a[3]) : 0;
			_chips[2].Text = (n > 0) ? ("Изменений: " + n) : "Нет изменений";
			_chips[2].Color = Theme.Silver1;
			if (!_loaded)
			{
				_loaded = true;
				_appear.Target = 1f;
				PlayShine();
			}
			Invalidate();
		}

		public void PlayShine()
		{
			if (!Anim.Enabled)
			{
				return;
			}
			Anim.Start(this, 1400, delegate(float t)
			{
				_shine = t;
				Invalidate();
			}, delegate
			{
				_shine = -1f;
				Invalidate();
			}, "hero-shine");
		}

		private static float Pct(object o)
		{
			try
			{
				return Math.Max(0f, Math.Min(1f, Convert.ToSingle(o) / 100f));
			}
			catch
			{
				return 0f;
			}
		}

		private static string S(object o, string d)
		{
			if (o != null && !(o.ToString() == ""))
			{
				return o.ToString();
			}
			return d;
		}

		private Rectangle Panel()
		{
			int pw = Math.Min(Theme.S(380f), (int)(Width * 0.42f));
			int m = Theme.S(18f);
			return new Rectangle(Width - pw - m, m, pw, Height - m * 2);
		}

		private bool Wide => Width >= Theme.S(760f);

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			Theme.Smooth(g);
			RectangleF rf = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
			using (GraphicsPath clip = Theme.Round(rf, Theme.S(18f)))
			{
				g.SetClip(clip);
				if (_img != null)
				{
					float k = Math.Max(Width / (float)_img.Width, Height / (float)_img.Height);
					float iw = _img.Width * k;
					float ih = _img.Height * k;
					g.InterpolationMode = InterpolationMode.HighQualityBilinear;
					g.DrawImage(_img, (Width - iw) / 2f, (Height - ih) * 0.5f, iw, ih);
				}
				else
				{
					using (LinearGradientBrush b = new LinearGradientBrush(rf, Color.FromArgb(58, 58, 66), Color.FromArgb(22, 22, 26), 20f))
					{
						g.FillPath(b, clip);
					}
				}
				using (LinearGradientBrush b2 = new LinearGradientBrush(new RectangleF(-1f, 0f, Width + 2, Height), Color.FromArgb(235, 14, 14, 17), Color.FromArgb(70, 14, 14, 17), 0f))
				{
					g.FillRectangle(b2, 0, 0, Width, Height);
				}
				using (LinearGradientBrush b3 = new LinearGradientBrush(new RectangleF(0f, Height * 0.45f, Width, Height * 0.55f + 1f), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(150, 10, 10, 12), 90f))
				{
					g.FillRectangle(b3, 0f, Height * 0.45f, Width, Height * 0.55f);
				}
				g.ResetClip();
				using (Pen pen = new Pen(Color.FromArgb(70, 255, 255, 255)))
				{
					g.DrawPath(pen, clip);
				}
			}
			float ap = Anim.EaseOut(_appear.Value);
			int lift = (int)((1f - ap) * Theme.S(12f));
			int x = Theme.S(32f);
			int textR = Wide ? (Panel().Left - Theme.S(20f)) : (Width - Theme.S(32f));
			Color subC = Theme.Mix(Color.FromArgb(30, 30, 34), Theme.Silver2, ap);
			TextRenderer.DrawText(g, (_greet == "") ? "ДОБРО ПОЖАЛОВАТЬ" : _greet, Theme.Tiny, new Rectangle(x + Theme.S(2f), Theme.S(28f) + lift, textR - x, Theme.S(18f)), subC, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			using (Font font = Theme.F(30f, semi: true, FontStyle.Bold))
			{
				Theme.GradientText(g, Line1, font, new PointF(x, Theme.S(42f) + lift), _shine);
			}
			Rectangle l2 = new Rectangle(x, Theme.S(98f) + lift, textR - x, Theme.S(22f));
			TextRenderer.DrawText(g, Line2, Theme.Body, l2, Theme.Mix(Color.FromArgb(30, 30, 34), Theme.Text, ap), TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
			Rectangle l3 = new Rectangle(x, Theme.S(121f) + lift, textR - x, Theme.S(20f));
			TextRenderer.DrawText(g, Line3, Theme.Small, l3, Theme.Mix(Color.FromArgb(30, 30, 34), Theme.Sub, ap), TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
			PaintScore(g, ap, x, textR);
			_chips[0].R = Rectangle.Empty;
			_chips[1].R = Rectangle.Empty;
			_chips[2].R = Rectangle.Empty;
			if (Wide)
			{
				PaintSpecs(g, ap);
			}
			_rebootR = Rectangle.Empty;
			if (NeedReboot)
			{
				string text = "Перезагрузить, чтобы применить";
				int w = Theme.Measure(text, Theme.Small).Width + Theme.S(44f);
				int rx = Wide ? (Panel().Left - w - Theme.S(14f)) : (Width - w - Theme.S(24f));
				_rebootR = new Rectangle(rx, Theme.S(22f), w, Theme.S(32f));
				bool hot = _hot == "reboot";
				using (GraphicsPath rp = Theme.Round(_rebootR, _rebootR.Height / 2f))
				{
					using (SolidBrush rb = new SolidBrush(Color.FromArgb(hot ? 90 : 60, Theme.Yellow)))
					{
						g.FillPath(rb, rp);
					}
					using (Pen rpen = new Pen(Color.FromArgb(hot ? 220 : 150, Theme.Yellow)))
					{
						g.DrawPath(rpen, rp);
					}
				}
				RectangleF ar = new RectangleF(rx + Theme.S(13f), _rebootR.Y + Theme.S(10f), Theme.S(12f), Theme.S(12f));
				using (Pen rap = new Pen(Theme.Mix(Theme.Yellow, Color.White, 0.3f), Math.Max(1.5f, Theme.S(1.7f))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				{
					g.DrawArc(rap, ar, -60f, 300f);
					g.DrawLine(rap, ar.Right - Theme.S(2f), ar.Y - Theme.S(1f), ar.Right - Theme.S(1.5f), ar.Y + Theme.S(3.5f));
				}
				TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(rx + Theme.S(32f), _rebootR.Y, w - Theme.S(40f), _rebootR.Height), Theme.Mix(Theme.Yellow, Color.White, 0.55f), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		}

		private void PaintScore(Graphics g, float ap, int x, int textR)
		{
			int bh = Theme.S(76f);
			Rectangle box = new Rectangle(x - Theme.S(4f), Height - Theme.S(18f) - bh + (int)((1f - ap) * Theme.S(10f)), Math.Min(textR - x + Theme.S(4f), Theme.S(560f)), bh);
			_scoreR = box;
			using (GraphicsPath gp = Theme.Round(box, Theme.S(14f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * ap), 16, 16, 19)))
				{
					g.FillPath(b, gp);
				}
				bool bhot = _hot == "box" || _hot == "view" || _hot == "fix";
				using (Pen pen = new Pen(Color.FromArgb((int)((bhot ? 95 : 60) * ap), 255, 255, 255)))
				{
					g.DrawPath(pen, gp);
				}
			}
			// ring
			int rs = Theme.S(52f);
			RectangleF rr = new RectangleF(box.X + Theme.S(12f), box.Y + (bh - rs) / 2f, rs, rs);
			float th = Theme.S(5f);
			RectangleF ar = new RectangleF(rr.X + th / 2f, rr.Y + th / 2f, rr.Width - th, rr.Height - th);
			using (Pen bg = new Pen(Color.FromArgb(50, 255, 255, 255), th))
			{
				g.DrawEllipse(bg, ar);
			}
			float v = _ring.Value;
			bool known = _ok >= 0 && _total > 0;
			float rt = _ring.Target;
			Color rc = !known ? Theme.Silver2 : ((rt >= 0.85f) ? Theme.Green : ((rt >= 0.6f) ? Theme.Silver1 : Theme.Yellow));
			if (known && v > 0.005f)
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(rr, Theme.Silver1, rc, 45f))
				{
					using (Pen fg = new Pen(lb, th))
					{
						fg.StartCap = LineCap.Round;
						fg.EndCap = LineCap.Round;
						g.DrawArc(fg, ar, -90f, 360f * v);
					}
				}
			}
			string pct = known ? ((int)Math.Round(v * 100f)).ToString() : "…";
			using (Font pf = Theme.F(12f, semi: true))
			{
				TextRenderer.DrawText(g, pct, pf, Rectangle.Round(rr), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
			int tx = (int)rr.Right + Theme.S(14f);
			int bad = known ? ((_bad >= 0) ? _bad : (_total - _ok)) : 0;
			// one split pill: [ Исправить N | › ] — the left part fixes, the chevron (and the whole box) opens the list
			// no bulk apply from home: the pill only opens the performance settings
			bool canFix = false;
			_fixR = Rectangle.Empty;
			_viewR = Rectangle.Empty;
			int left = box.Right - Theme.S(16f);
			if (known || Fixing)
			{
				string bt = Fixing ? "Применяю…" : ((bad > 0) ? "Что мешает FPS" : "Максимум FPS");
				int ph = Theme.S(34f);
				int cw = Theme.S(38f);
				int lw = Theme.Measure(bt, Theme.Small).Width + Theme.S(30f);
				Rectangle pill = new Rectangle(box.Right - Theme.S(16f) - lw - cw, box.Y + (bh - ph) / 2, lw + cw, ph);
				Rectangle lr = new Rectangle(pill.X, pill.Y, lw, ph);
				Rectangle cr = new Rectangle(pill.X + lw, pill.Y, cw, ph);
				bool fh = canFix && _hot == "fix";
				bool vh = _hot == "view" || _hot == "box";
				using (GraphicsPath bp = Theme.Round(pill, ph / 2f))
				{
					if (canFix)
					{
						Theme.FillGradient(g, bp, pill, Theme.Silver1, Theme.Silver2);
						Region old = g.Clip;
						g.SetClip(bp, CombineMode.Intersect);
						if (fh)
						{
							using (SolidBrush hb = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
							{
								g.FillRectangle(hb, lr);
							}
						}
						if (vh)
						{
							using (SolidBrush hb2 = new SolidBrush(Color.FromArgb(_hot == "view" ? 70 : 35, 255, 255, 255)))
							{
								g.FillRectangle(hb2, cr);
							}
						}
						g.Clip = old;
						using (Pen dp = new Pen(Color.FromArgb(70, 22, 22, 26)))
						{
							g.DrawLine(dp, cr.X, cr.Y + Theme.S(8f), cr.X, cr.Bottom - Theme.S(8f));
						}
					}
					else
					{
						Color oc = Fixing ? Theme.Silver2 : ((bad > 0) ? Theme.Silver1 : Theme.Green);
						using (SolidBrush fb = new SolidBrush(Color.FromArgb(vh ? 48 : 30, oc)))
						{
							g.FillPath(fb, bp);
						}
						using (Pen fp = new Pen(Color.FromArgb(vh ? 150 : 100, oc)))
						{
							g.DrawPath(fp, bp);
						}
					}
				}
				Color tc = canFix ? Theme.OnSilver : (Fixing ? Theme.Sub : ((bad > 0) ? Theme.Text : Theme.Green));
				TextRenderer.DrawText(g, bt, Theme.Small, new Rectangle(lr.X + Theme.S(4f), lr.Y, lr.Width - Theme.S(2f), lr.Height), tc, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				// chevron
				using (Pen chp = new Pen(tc, Math.Max(1.4f, Theme.S(1.7f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
				{
					float ccx = cr.X + cr.Width / 2f - Theme.S(2f) + ((_hot == "view") ? Theme.S(2f) : 0f);
					float ccy = cr.Y + cr.Height / 2f;
					float s4 = Theme.S(4f);
					g.DrawLines(chp, new PointF[3] { new PointF(ccx - s4 / 2f, ccy - s4), new PointF(ccx + s4 / 2f, ccy), new PointF(ccx - s4 / 2f, ccy + s4) });
				}
				if (canFix)
				{
					_fixR = lr;
					_viewR = cr;
				}
				else if (!Fixing)
				{
					_viewR = pill;
				}
				left = pill.Left;
			}
			int tw = left - Theme.S(12f) - tx;
			string lvl = _chips[0].Text;
			string head = !known ? "Считаю FPS-потенциал…" : ((bad == 0) ? "Windows настроена на максимум FPS" : ((v >= 0.8f) ? "Почти максимум FPS" : ((v >= 0.4f) ? "FPS можно поднять" : "FPS не оптимизирован")));
			TextRenderer.DrawText(g, head, Theme.Title, new Rectangle(tx, box.Y + Theme.S(16f), Math.Max(10, tw), Theme.S(22f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			string sub = known ? ("FPS-потенциал " + _ok + " из 100") : "проверяю настройки…";
			TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, box.Y + Theme.S(40f), Math.Max(10, tw), Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		private void PaintSpecs(Graphics g, float ap)
		{
			Rectangle p = Panel();
			p.Offset(0, (int)((1f - ap) * Theme.S(10f)));
			using (GraphicsPath gp = Theme.Round(p, Theme.S(14f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * ap), 16, 16, 19)))
				{
					g.FillPath(b, gp);
				}
				using (Pen pen = new Pen(Color.FromArgb((int)(60 * ap), 255, 255, 255)))
				{
					g.DrawPath(pen, gp);
				}
			}
			string[] lab = new string[4] { "Процессор", "Видеокарта", "Память", "Диск" };
			string[] val = new string[4] { _cpu, _gpu, _ram, _disk };
			string[] gl = new string[4] { Glyph.Cpu, Glyph.Monitor, Glyph.Ram, Glyph.Disk };
			int pad = Theme.S(16f);
			int rowH = (p.Height - pad * 2) / 4;
			for (int i = 0; i < 4; i++)
			{
				int y = p.Y + pad + i * rowH;
				int ib = Theme.S(30f);
				Rectangle icon = new Rectangle(p.X + pad, y + (rowH - ib) / 2, ib, ib);
				Theme.IconBox(g, icon, gl[i], bright: false, 0f, Theme.IconSmall);
				int tx = icon.Right + Theme.S(12f);
				int tw = p.Right - pad - tx;
				Color lc = Theme.Mix(Color.FromArgb(20, 20, 24), Theme.Dim, ap);
				Color vc = Theme.Mix(Color.FromArgb(20, 20, 24), Theme.Text, ap);
				TextRenderer.DrawText(g, lab[i], Theme.Tiny, new Rectangle(tx, y + Theme.S(4f), tw, Theme.S(16f)), lc, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				bool bar = i >= 2;
				int vw = bar ? (tw - Theme.S(96f)) : tw;
				TextRenderer.DrawText(g, (val[i] == "") ? "…" : val[i], Theme.Small, new Rectangle(tx, y + Theme.S(19f), vw, Theme.S(20f)), vc, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				if (bar)
				{
					float v = (i == 2) ? _ramBar.Value : _diskBar.Value;
					RectangleF br = new RectangleF(p.Right - pad - Theme.S(86f), y + Theme.S(26f), Theme.S(86f), Theme.S(5f));
					Theme.Progress(g, br, v, -1f);
					string pc = (int)Math.Round(v * 100f) + "%";
					TextRenderer.DrawText(g, (i == 2 ? "занято " : "занято ") + pc, Theme.Tiny, new Rectangle((int)br.X - Theme.S(20f), y + Theme.S(4f), (int)br.Width + Theme.S(20f), Theme.S(16f)), lc, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right);
				}
			}
		}

		private string HitChip(Point pt)
		{
			if (!_rebootR.IsEmpty && _rebootR.Contains(pt))
			{
				return "reboot";
			}
			if (!_fixR.IsEmpty && _fixR.Contains(pt))
			{
				return "fix";
			}
			if (!_viewR.IsEmpty && _viewR.Contains(pt))
			{
				return "view";
			}
			foreach (Chip c in _chips)
			{
				if (c.R.Contains(pt))
				{
					return c.Key;
				}
			}
			if (_ok >= 0 && _total > 0 && !Fixing && _scoreR.Contains(pt))
			{
				return "box";
			}
			return null;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			string h = HitChip(e.Location);
			if (h != _hot)
			{
				_hot = h;
				Cursor = (h != null) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = null;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			string h = HitChip(e.Location);
			if (h != null && e.Button == MouseButtons.Left)
			{
				ChipClicked?.Invoke((h == "box") ? "view" : h);
			}
			base.OnMouseClick(e);
		}
	}
}
