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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace WinWeaver
{
	// Disk cleanup: system drive bar, categories with real sizes and check boxes, one "clean" button
	internal class CleanView : Card
	{
		public class Cat
		{
			public string Id;
			public string Name;
			public string Desc;
			public double Size = -1;
			public bool On;
			public Rectangle R;
		}

		private static readonly string[][] Known = new string[][]
		{
			new string[] { "temp", "Временные файлы", "Папка TEMP твоего профиля", "1" },
			new string[] { "wintemp", "Временные файлы Windows", "Windows\\Temp и остатки установщиков", "1" },
			new string[] { "wucache", "Кэш обновлений", "Уже установленные пакеты Центра обновления", "1" },
			new string[] { "docache", "Оптимизация доставки", "Файлы, которые Windows раздаёт другим ПК", "1" },
			new string[] { "dumps", "Дампы и отчёты об ошибках", "Снимки памяти после сбоев и отчёты Windows", "1" },
			new string[] { "logs", "Журналы установки", "Старые журналы CBS, DISM и обновлений", "1" },
			new string[] { "thumbs", "Эскизы картинок", "Пересоздадутся сами при открытии папок", "0" },
			new string[] { "shader", "Кэш шейдеров", "DirectX, NVIDIA и AMD — игры соберут его заново", "0" },
			new string[] { "browser", "Кэш браузеров", "Пароли, вкладки и история не трогаются", "0" },
			new string[] { "recycle", "Корзина", "Удалённые файлы во всех корзинах", "0" }
		};

		public readonly List<Cat> Cats = new List<Cat>();

		private readonly FButton _clean;

		private readonly FButton _rescan;

		private double _free = -1;

		private double _size;

		private string _drive = "C:";

		private bool _scanned;

		private DateTime _scanAt = DateTime.MinValue;

		public bool Dirty = true;

		public string LastResult = "";

		private int _hot = -1;

		private Rectangle _barR;

		private Rectangle _headR;

		private int _listTop;

		private readonly Tween _barT;

		public event Action CleanClicked;

		public event Action RescanClicked;

		private int RowH => Theme.S(52f);

		public CleanView()
		{
			foreach (string[] k in Known)
			{
				Cats.Add(new Cat { Id = k[0], Name = k[1], Desc = k[2], On = k[3] == "1" });
			}
			_barT = new Tween(this, 0f, 0.12f).Timed(800);
			_clean = new FButton { Text = "Очистить выбранное", Kind = BtnKind.Silver, Font = Theme.Body };
			_clean.AutoWidth(Theme.S(36f));
			_clean.Click += delegate
			{
				CleanClicked?.Invoke();
			};
			_rescan = new FButton { Text = "Пересканировать", Kind = BtnKind.Ghost, Font = Theme.Body, Glyph = Glyph.Refresh };
			_rescan.AutoWidth(Theme.S(30f));
			_rescan.Click += delegate
			{
				RescanClicked?.Invoke();
			};
			Controls.Add(_clean);
			Controls.Add(_rescan);
		}

		public double Selected
		{
			get
			{
				double s = 0;
				foreach (Cat c in Cats)
				{
					if (c.On && c.Size > 0)
					{
						s += c.Size;
					}
				}
				return s;
			}
		}

		public static string Fmt(double b)
		{
			CultureInfo ru = L.Culture;
			if (b < 0)
			{
				return "…";
			}
			if (b >= 1073741824.0)
			{
				return (b / 1073741824.0).ToString("0.0", ru) + " ГБ";
			}
			if (b >= 1048576.0)
			{
				return Math.Round(b / 1048576.0).ToString(ru) + " МБ";
			}
			if (b > 0)
			{
				return Math.Max(1.0, Math.Round(b / 1024.0)).ToString(ru) + " КБ";
			}
			return "пусто";
		}

		private static double D(object o)
		{
			try
			{
				return (o == null) ? 0.0 : Convert.ToDouble(o, CultureInfo.InvariantCulture);
			}
			catch
			{
				return 0.0;
			}
		}

		public void SetScanning()
		{
			Dirty = true;
			_scanned = false;
			foreach (Cat c in Cats)
			{
				c.Size = -1;
			}
			_clean.Enabled = false;
			Skeleton.Track(this, true);
			Invalidate();
		}

		public override object Fetch(IEngine e)
		{
			object disk = e.Eval("Get-FDiskInfo");
			object scan = null;
			if (Dirty || (DateTime.Now - _scanAt).TotalSeconds > 90.0)
			{
				scan = e.Eval("Get-FCleanScan");
			}
			return new object[2] { disk, scan };
		}

		public override void Apply(object v)
		{
			object[] a = v as object[];
			if (a == null)
			{
				return;
			}
			if (a[0] is Hashtable d)
			{
				_free = D(d["Free"]);
				_size = D(d["Size"]);
				_drive = (d["Drive"] ?? "C:").ToString();
				_barT.Target = (_size > 0) ? ((float)(1.0 - _free / _size)) : 0f;
			}
			object[] arr = a[1] as object[];
			if (arr == null && a[1] is Hashtable)
			{
				arr = new object[1] { a[1] };
			}
			if (arr != null)
			{
				foreach (object o in arr)
				{
					Hashtable h = o as Hashtable;
					if (h == null)
					{
						continue;
					}
					string id = (h["Id"] ?? "").ToString();
					foreach (Cat c in Cats)
					{
						if (c.Id == id)
						{
							c.Size = D(h["Size"]);
						}
					}
				}
				_scanned = true;
				Skeleton.Track(this, false);
				Dirty = false;
				_scanAt = DateTime.Now;
			}
			_clean.Enabled = _scanned;
			Invalidate();
		}

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Pad + Theme.S(2f);
			_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(56f));
			_barR = new Rectangle(pad, _headR.Bottom + Theme.S(12f), w - pad * 2, Theme.S(10f));
			int by = _barR.Bottom + Theme.S(18f);
			_clean.SetBounds(pad, by, _clean.Width, Theme.S(36f));
			_rescan.SetBounds(_clean.Right + Theme.S(8f), by, _rescan.Width, Theme.S(36f));
			_listTop = by + Theme.S(36f) + Theme.S(18f);
			int cols = (w - pad * 2 >= Theme.S(760f)) ? 2 : 1;
			int gap = Theme.S(12f);
			int cw = (w - pad * 2 - gap * (cols - 1)) / cols;
			for (int i = 0; i < Cats.Count; i++)
			{
				int c = i % cols;
				int r = i / cols;
				Cats[i].R = new Rectangle(pad + c * (cw + gap), _listTop + r * (RowH + Theme.S(6f)), cw, RowH);
			}
			int rows = (Cats.Count + cols - 1) / cols;
			int h = _listTop + rows * (RowH + Theme.S(6f)) - Theme.S(6f) + pad;
			SetBounds(x, y, w, h);
			Invalidate();
			return h;
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			Rectangle ib = new Rectangle(_headR.X, _headR.Y, Theme.S(52f), Theme.S(52f));
			Theme.IconBox(g, ib, Glyph.Disk, bright: true, 0f, Theme.Icon);
			int tx = ib.Right + Theme.S(16f);
			string sel = Fmt(Selected);
			string right = _scanned ? sel : "считаю…";
			using (Font big = Theme.F(20f, semi: true))
			{
				Size bs = Theme.Measure(right, big);
				int rx = _headR.Right - bs.Width;
				TextRenderer.DrawText(g, "МОЖНО ОСВОБОДИТЬ", Theme.Tiny, new Rectangle(rx - Theme.S(60f), _headR.Y + Theme.S(2f), bs.Width + Theme.S(60f), Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
				TextRenderer.DrawText(g, right, big, new Point(rx, _headR.Y + Theme.S(18f)), _scanned ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				int tw = rx - Theme.S(60f) - tx;
				using (Font tf = Theme.F(15f, semi: true))
				{
					TextRenderer.DrawText(g, "Диск " + _drive, tf, new Rectangle(tx, _headR.Y + Theme.S(4f), tw, Theme.S(26f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}
				string sub = (_free < 0) ? "проверяю…" : (Fmt(_free) + " свободно из " + Fmt(_size) + ((LastResult != "") ? ("  •  " + LastResult) : ""));
				TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, _headR.Y + Theme.S(32f), tw, Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			// usage bar: used part + the part that will be freed
			using (GraphicsPath bp = Theme.Round(_barR, _barR.Height / 2f))
			{
				using (SolidBrush bb = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
				{
					g.FillPath(bb, bp);
				}
				float used = Anim.Clamp(_barT.Value);
				float fw = Math.Max(_barR.Height, _barR.Width * used);
				RectangleF ur = new RectangleF(_barR.X, _barR.Y, fw, _barR.Height);
				Color uc = (used > 0.9f) ? Theme.Red : ((used > 0.8f) ? Theme.Yellow : Theme.Silver2);
				using (GraphicsPath up = Theme.Round(ur, _barR.Height / 2f))
				{
					Theme.FillGradient(g, up, ur, Theme.Silver1, uc, 0f);
				}
				if (_scanned && _size > 0 && Selected > 0)
				{
					float sw = (float)(_barR.Width * Math.Min(used, Selected / _size));
					sw = Math.Max(Theme.S(4f), sw);
					RectangleF sr = new RectangleF(_barR.X + fw - sw, _barR.Y, sw, _barR.Height);
					Region old = g.Clip;
					g.SetClip(bp, CombineMode.Intersect);
					using (SolidBrush gb = new SolidBrush(Theme.Green))
					{
						g.FillRectangle(gb, sr);
					}
					g.Clip = old;
				}
			}
			for (int i = 0; i < Cats.Count; i++)
			{
				PaintCat(g, Cats[i], i == _hot);
			}
		}

		private void PaintCat(Graphics g, Cat c, bool hot)
		{
			Rectangle r = c.R;
			using (GraphicsPath p = Theme.Round(r, Theme.S(10f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(hot ? 24 : (c.On ? 16 : 8), 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(c.On ? 46 : 26, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			Rectangle cb = new Rectangle(r.X + Theme.S(14f), r.Y + (r.Height - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
			using (GraphicsPath cp = Theme.Round(cb, Theme.S(4f)))
			{
				using (SolidBrush cbb = new SolidBrush(c.On ? Theme.Silver1 : Theme.Control2))
				{
					g.FillPath(cbb, cp);
				}
				using (Pen cpen = new Pen(c.On ? Theme.Silver1 : Theme.BorderHi))
				{
					g.DrawPath(cpen, cp);
				}
			}
			if (c.On)
			{
				using (Pen ck = new Pen(Theme.OnSilver, Math.Max(1.6f, Theme.S(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
				{
					g.DrawLines(ck, new PointF[3]
					{
						new PointF(cb.X + cb.Width * 0.24f, cb.Y + cb.Height * 0.52f),
						new PointF(cb.X + cb.Width * 0.43f, cb.Y + cb.Height * 0.7f),
						new PointF(cb.X + cb.Width * 0.77f, cb.Y + cb.Height * 0.32f)
					});
				}
			}
			string sz = (c.Size < 0) ? "" : Fmt(c.Size);
			int sw = (c.Size < 0) ? Theme.S(56f) : (Theme.Measure(sz, Theme.Body).Width + Theme.S(4f));
			if (c.Size < 0)
			{
				Skeleton.Bar(g, new RectangleF(r.Right - Theme.S(14f) - sw, r.Y + r.Height / 2 - Theme.S(6f), sw, Theme.S(12f)), this);
			}
			int tx = cb.Right + Theme.S(12f);
			int tw = r.Right - Theme.S(14f) - sw - tx - Theme.S(8f);
			TextRenderer.DrawText(g, c.Name, Theme.Body, new Rectangle(tx, r.Y + Theme.S(7f), tw, Theme.S(20f)), c.On ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, c.Desc, Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(27f), tw, Theme.S(17f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			Color szc = (c.Size >= 1073741824.0) ? Theme.Yellow : ((c.Size > 0) ? Theme.Text : Theme.Dim);
			TextRenderer.DrawText(g, sz, Theme.Body, new Rectangle(r.Right - Theme.S(14f) - sw, r.Y, sw, r.Height), szc, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
		}

		private int Hit(Point p)
		{
			for (int i = 0; i < Cats.Count; i++)
			{
				if (Cats[i].R.Contains(p))
				{
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = Hit(e.Location);
			if (h != _hot)
			{
				_hot = h;
				Cursor = (h >= 0) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = -1;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			int h = Hit(e.Location);
			if (h >= 0 && e.Button == MouseButtons.Left)
			{
				Cats[h].On = !Cats[h].On;
				Invalidate();
			}
			base.OnMouseClick(e);
		}
	
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Skeleton.Track(this, !_scanned);
		}
	}
}