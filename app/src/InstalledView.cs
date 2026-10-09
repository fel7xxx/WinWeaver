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
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinWeaver
{
	// Installed programs (Win32 + Store): search, filter, sort, uninstall with leftovers cleanup
	internal class InstalledView : Card
	{
		public class Item
		{
			public string Id;
			public string Name;
			public string Pub;
			public string Ver;
			public double Size;
			public string Date;
			public string Loc;
			public string IconPath;
			public bool Store;
			public Image Icon;
			public Rectangle R;
			public Rectangle DelR;
			public Rectangle DirR;
			public bool Busy;
		}

		private static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

		public readonly List<Item> Items = new List<Item>();

		private readonly List<Item> _shown = new List<Item>();

		private readonly TextBox _search = new TextBox();

		private readonly Segment _filter = new Segment { Items = new string[3] { "Все", "Программы", "Microsoft Store" } };

		private readonly string[] _sorts = new string[3] { "по имени", "по размеру", "по дате" };

		private int _sort;

		private int _limit = 80;

		private bool _loaded;

		private int _hot = -1;

		private int _hotBtn; // 1 = delete, 2 = folder

		private Rectangle _headR;

		private Rectangle _statsR;

		private Rectangle _searchR;

		private Rectangle _sortR;

		private Rectangle _moreR;

		private int _listTop;

		private readonly FButton _refresh;

		public event Action<Item> UninstallClicked;

		public event Action RefreshClicked;

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

		private int RowH => Theme.S(64f);

		public InstalledView()
		{
			_search.BorderStyle = BorderStyle.None;
			_search.BackColor = Color.FromArgb(30, 30, 35);
			_search.ForeColor = Theme.Text;
			_search.Font = Theme.Body;
			_search.TextChanged += delegate
			{
				_limit = 80;
				_hot = -1;
				LayoutAgain();
			};
			_search.GotFocus += delegate { Invalidate(); };
			_search.LostFocus += delegate { Invalidate(); };
			_search.HandleCreated += delegate
			{
				try
				{
					SendMessage(_search.Handle, 0x1501, (IntPtr)1, "Найти программу или издателя");
				}
				catch
				{
				}
			};
			_filter.Selected = 0;
			_filter.Changed += delegate
			{
				_limit = 80;
				_hot = -1;
				LayoutAgain();
			};
			_refresh = new FButton { Text = "Обновить", Kind = BtnKind.Ghost, Font = Theme.Body, Glyph = Glyph.Refresh };
			_refresh.AutoWidth(Theme.S(30f));
			_refresh.Click += delegate
			{
				RefreshClicked?.Invoke();
			};
			Controls.Add(_search);
			Controls.Add(_filter);
			Controls.Add(_refresh);
		}

		private static string Str(object o)
		{
			return (o == null) ? "" : o.ToString();
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

		private static Image LoadIcon(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}
			lock (IconCache)
			{
				Image img;
				if (IconCache.TryGetValue(path, out img))
				{
					return img;
				}
				img = null;
				try
				{
					if (File.Exists(path))
					{
						string ext = Path.GetExtension(path).ToLowerInvariant();
						if (ext == ".ico")
						{
							using (Icon ic = new Icon(path, 32, 32))
							{
								img = ic.ToBitmap();
							}
						}
						else if (ext == ".png" || ext == ".jpg" || ext == ".bmp")
						{
							using (Image src = Image.FromFile(path))
							{
								img = new Bitmap(src, 32, 32);
							}
						}
						else
						{
							using (Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(path))
							{
								if (ic != null)
								{
									img = ic.ToBitmap();
								}
							}
						}
					}
				}
				catch
				{
					img = null;
				}
				IconCache[path] = img;
				return img;
			}
		}

		public override object Fetch(IEngine e)
		{
			object v = e.Eval("Get-FInstalled");
			List<Item> list = new List<Item>();
			object[] arr = v as object[];
			if (arr == null && v is Hashtable)
			{
				arr = new object[1] { v };
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
					Item it = new Item
					{
						Id = Str(h["Id"]),
						Name = Str(h["Name"]),
						Pub = Str(h["Pub"]),
						Ver = Str(h["Ver"]),
						Size = D(h["Size"]),
						Date = Str(h["Date"]),
						Loc = Str(h["Loc"]),
						IconPath = Str(h["Icon"]),
						Store = Str(h["Kind"]) == "store"
					};
					if (it.Name == "")
					{
						continue;
					}
					it.Icon = LoadIcon(it.IconPath);
					list.Add(it);
				}
			}
			return list;
		}

		public override void Apply(object v)
		{
			List<Item> list = v as List<Item>;
			if (list == null)
			{
				return;
			}
			Items.Clear();
			Items.AddRange(list);
			_loaded = true;
			Skeleton.Track(this, false);
			_hot = -1;
			LayoutAgain();
		}

		public void SetBusy(Item it, bool busy)
		{
			it.Busy = busy;
			Invalidate();
		}

		private void LayoutAgain()
		{
			if (base.Parent is Canvas)
			{
				((Canvas)base.Parent).Page.RelayoutSoon();
			}
			Invalidate();
		}

		private void Rebuild()
		{
			_shown.Clear();
			string q = _search.Text.Trim();
			foreach (Item it in Items)
			{
				if (_filter.Selected == 1 && it.Store)
				{
					continue;
				}
				if (_filter.Selected == 2 && !it.Store)
				{
					continue;
				}
				if (q != "" && it.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0 && it.Pub.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0)
				{
					continue;
				}
				_shown.Add(it);
			}
			_shown.Sort(delegate(Item a, Item b)
			{
				int r = 0;
				if (_sort == 1)
				{
					r = b.Size.CompareTo(a.Size);
				}
				else if (_sort == 2)
				{
					r = string.CompareOrdinal(b.Date, a.Date);
				}
				return (r != 0) ? r : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			});
		}

		public override int DoLayout(int x, int y, int w)
		{
			Rebuild();
			int pad = base.Pad;
			int titleH = Theme.Measure("Ag", Theme.Title).Height + Theme.S(4f) + Theme.Measure("Ag", Theme.Small).Height;
			_headR = new Rectangle(pad, pad, w - pad * 2, Math.Max(Theme.S(44f), titleH));
			_refresh.Location = new Point(w - pad - _refresh.Width, _headR.Y + (_headR.Height - _refresh.Height) / 2);
			int cy = _headR.Bottom + Theme.S(16f);
			_statsR = new Rectangle(pad, cy, w - pad * 2, Theme.S(74f));
			cy = _statsR.Bottom + Theme.S(16f);
			int fh = _filter.Height;
			int fw = Math.Min(Theme.S(420f), (w - pad * 2) / 2);
			int sw = w - pad * 2 - fw - Theme.S(12f);
			bool stack = sw < Theme.S(220f);
			if (stack)
			{
				sw = w - pad * 2;
			}
			_searchR = new Rectangle(pad, cy, sw, fh);
			int ih = _search.PreferredHeight;
			_search.SetBounds(_searchR.X + Theme.S(38f), _searchR.Y + (fh - ih) / 2, _searchR.Width - Theme.S(52f), ih);
			if (stack)
			{
				cy += fh + Theme.S(10f);
				_filter.SetBounds(pad, cy, w - pad * 2, fh);
			}
			else
			{
				_filter.SetBounds(w - pad - fw, cy, fw, fh);
			}
			cy += fh + Theme.S(14f);
			int lh = Theme.Measure("Ag", Theme.Small).Height;
			_sortR = new Rectangle(pad, cy, w - pad * 2, lh + Theme.S(8f));
			cy = _sortR.Bottom + Theme.S(6f);
			_listTop = cy;
			int ry = cy;
			foreach (Item it in Items)
			{
				it.R = Rectangle.Empty;
			}
			int n = Math.Min(_limit, _shown.Count);
			int bh = Theme.S(30f);
			int delW = Theme.Measure("Удалить", Theme.Small).Width + Theme.S(34f);
			int dirW = bh + Theme.S(4f);
			for (int i = 0; i < n; i++)
			{
				Item it = _shown[i];
				it.R = new Rectangle(pad - Theme.S(8f), ry, w - pad * 2 + Theme.S(16f), RowH);
				int by = ry + (RowH - bh) / 2;
				it.DelR = new Rectangle(w - pad - delW, by, delW, bh);
				it.DirR = (it.Loc != "" && !it.Store) ? new Rectangle(it.DelR.X - Theme.S(8f) - dirW, by, dirW, bh) : Rectangle.Empty;
				ry += RowH;
			}
			_moreR = Rectangle.Empty;
			if (_shown.Count > n)
			{
				_moreR = new Rectangle(pad, ry + Theme.S(8f), w - pad * 2, Theme.S(40f));
				ry = _moreR.Bottom;
			}
			if (!_loaded)
			{
				ry += RowH * 6;
			}
			else if (_shown.Count == 0)
			{
				ry += Theme.S(70f);
			}
			int h = ry + pad - Theme.S(4f);
			SetBounds(x, y, w, h);
			return h;
		}

		private static Color Tint(string name)
		{
			Color[] pal = new Color[6]
			{
				Color.FromArgb(96, 112, 150),
				Color.FromArgb(120, 98, 150),
				Color.FromArgb(80, 130, 128),
				Color.FromArgb(150, 110, 90),
				Color.FromArgb(110, 130, 90),
				Color.FromArgb(140, 92, 110)
			};
			int hsh = 0;
			foreach (char c in name ?? "")
			{
				hsh = hsh * 31 + c;
			}
			return pal[Math.Abs(hsh) % pal.Length];
		}

		private void DrawAppIcon(Graphics g, Item it, Rectangle r)
		{
			Theme.Smooth(g);
			using (GraphicsPath p = Theme.Round(r, r.Width * 0.28f))
			{
				Color t = Tint(it.Name);
				Theme.FillGradient(g, p, r, Theme.Mix(t, Color.FromArgb(70, 70, 80), 0.35f), Theme.Mix(t, Color.FromArgb(30, 30, 36), 0.6f));
				using (Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			if (it.Icon != null)
			{
				int s = r.Width - Theme.S(10f);
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(it.Icon, new Rectangle(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s));
				return;
			}
			string letter = it.Name.Substring(0, 1).ToUpperInvariant();
			using (Font f = Theme.F(13f, semi: true))
			{
				TextRenderer.DrawText(g, letter, f, r, Color.FromArgb(236, 236, 240), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
		}

		internal static void Stat(Graphics g, Rectangle r, string value, string label, Color c, string hint)
		{
			Theme.Smooth(g);
			using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(34, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			int bar = Theme.S(3f);
			using (GraphicsPath bp = Theme.Round(new RectangleF(r.X + Theme.S(14f), r.Y + Theme.S(16f), bar, r.Height - Theme.S(32f)), bar / 2f))
			{
				using (SolidBrush b2 = new SolidBrush(c))
				{
					g.FillPath(b2, bp);
				}
			}
			int tx = r.X + Theme.S(28f);
			using (Font big = Theme.F(19f, semi: true))
			{
				Size vs = TextRenderer.MeasureText(g, value, big, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
				TextRenderer.DrawText(g, value, big, new Point(tx, r.Y + Theme.S(12f)), Theme.Text, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
				TextRenderer.DrawText(g, label, Theme.Small, new Rectangle(tx + vs.Width + Theme.S(8f), r.Y + Theme.S(12f), r.Right - tx - vs.Width - Theme.S(12f), vs.Height), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.Bottom | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(tx, r.Bottom - Theme.S(26f), r.Right - tx - Theme.S(10f), Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		internal static void Btn(Graphics g, Rectangle r, string text, string glyph, bool hot, bool danger)
		{
			Theme.Smooth(g);
			Color c = danger ? Theme.Red : Theme.Silver2;
			using (GraphicsPath p = Theme.Round(r, r.Height / 2f))
			{
				using (SolidBrush b = new SolidBrush(hot ? Color.FromArgb(danger ? 46 : 30, danger ? c : Color.White) : Color.FromArgb(12, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(hot ? Color.FromArgb(140, c) : Color.FromArgb(40, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			Color tc = hot ? (danger ? Theme.Mix(c, Color.White, 0.25f) : Theme.Text) : Theme.Sub;
			if (text == null)
			{
				Theme.DrawIcon(g, glyph, Theme.Icon, r, tc);
				return;
			}
			TextRenderer.DrawText(g, text, Theme.Small, r, tc, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		internal static string Plural(int n, string one, string few, string many)
		{
			if (!L.IsRu)
			{
				return (n == 1) ? one : many;
			}
			int m10 = n % 10;
			int m100 = n % 100;
			if (m10 == 1 && m100 != 11)
			{
				return one;
			}
			if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14))
			{
				return few;
			}
			return many;
		}

		private static string FmtDate(string d)
		{
			DateTime t;
			if (DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
			{
				return t.ToString("d MMM yyyy", L.Culture);
			}
			return d;
		}

		protected override void PaintContent(Graphics g)
		{
			int pad = base.Pad;
			Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - Theme.S(44f)) / 2, Theme.S(44f), Theme.S(44f)), Glyph.Apps);
			int tx = _headR.X + Theme.S(58f);
			int tr = _refresh.Left - Theme.S(12f);
			int th = Theme.Measure("Ag", Theme.Title).Height;
			int ty = _headR.Y + (_headR.Height - th - Theme.S(4f) - Theme.Measure("Ag", Theme.Small).Height) / 2;
			TextRenderer.DrawText(g, "Установленные программы", Theme.Title, new Rectangle(tx, ty, tr - tx, th + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, "Удаление вместе с остатками — папками, файлами и записями реестра", Theme.Small, new Rectangle(tx, ty + th + Theme.S(4f), tr - tx, Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			int apps = 0;
			int store = 0;
			double total = 0;
			Item big = null;
			foreach (Item it in Items)
			{
				if (it.Store)
				{
					store++;
				}
				else
				{
					apps++;
				}
				total += it.Size;
				if (big == null || it.Size > big.Size)
				{
					big = it;
				}
			}
			int gap = Theme.S(10f);
			int sw = (_statsR.Width - gap * 2) / 3;
			Stat(g, new Rectangle(_statsR.X, _statsR.Y, sw, _statsR.Height), _loaded ? apps.ToString() : "…", Plural(apps, "программа", "программы", "программ"), Theme.Silver2, "установлены обычным установщиком");
			Stat(g, new Rectangle(_statsR.X + sw + gap, _statsR.Y, sw, _statsR.Height), _loaded ? store.ToString() : "…", "из Store", Theme.Silver1, "приложения Microsoft Store");
			Stat(g, new Rectangle(_statsR.X + (sw + gap) * 2, _statsR.Y, _statsR.Width - (sw + gap) * 2, _statsR.Height), _loaded ? CleanView.Fmt(total) : "…", "занято", (total > 107374182400.0) ? Theme.Yellow : Theme.Green, (big != null && big.Size > 0) ? ("больше всех: " + big.Name) : "по данным установщиков");
			Theme.Smooth(g);
			using (GraphicsPath sp = Theme.Round(_searchR, _searchR.Height / 2f))
			{
				using (SolidBrush sb = new SolidBrush(Color.FromArgb(30, 30, 35)))
				{
					g.FillPath(sb, sp);
				}
				using (Pen pen = new Pen(_search.Focused ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(44, 255, 255, 255)))
				{
					g.DrawPath(pen, sp);
				}
			}
			Theme.DrawIcon(g, Glyph.Search, Theme.Icon, new Rectangle(_searchR.X + Theme.S(10f), _searchR.Y, Theme.S(24f), _searchR.Height), Theme.Sub);
			string cnt = _loaded ? ("Показано: " + _shown.Count) : "Загружаю список программ…";
			TextRenderer.DrawText(g, cnt, Theme.Small, _sortR, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
			int sx = _sortR.Right;
			for (int i = _sorts.Length - 1; i >= 0; i--)
			{
				int w = Theme.Measure(_sorts[i], Theme.Small).Width + Theme.S(16f);
				sx -= w;
				Rectangle r = new Rectangle(sx, _sortR.Y, w, _sortR.Height);
				if (i == _sort)
				{
					using (GraphicsPath p = Theme.Round(r, r.Height / 2f))
					{
						using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
						{
							g.FillPath(b, p);
						}
					}
				}
				TextRenderer.DrawText(g, _sorts[i], Theme.Small, r, (i == _sort) ? Theme.Text : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				sx -= Theme.S(2f);
			}
			TextRenderer.DrawText(g, "Сортировка:", Theme.Small, new Rectangle(sx - Theme.S(110f), _sortR.Y, Theme.S(104f), _sortR.Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
			int n = Math.Min(_limit, _shown.Count);
			for (int i = 0; i < n; i++)
			{
				Item it = _shown[i];
				Rectangle r = it.R;
				bool hot = i == _hot;
				if (hot)
				{
					using (GraphicsPath hp = Theme.Round(r, Theme.S(10f)))
					{
						using (SolidBrush hb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
						{
							g.FillPath(hb, hp);
						}
					}
				}
				else if (i > 0 && _hot != i - 1)
				{
					using (SolidBrush lb = new SolidBrush(Color.FromArgb(36, 255, 255, 255)))
					{
						g.FillRectangle(lb, r.X + Theme.S(60f), r.Y, r.Width - Theme.S(68f), 1);
					}
				}
				int ic = Theme.S(38f);
				Rectangle ir = new Rectangle(r.X + Theme.S(8f), r.Y + (r.Height - ic) / 2, ic, ic);
				DrawAppIcon(g, it, ir);
				int x0 = ir.Right + Theme.S(14f);
				int sizeW = Theme.S(80f);
				int left = (it.DirR.IsEmpty ? it.DelR.X : it.DirR.X) - Theme.S(14f);
				int right = left - sizeW;
				int ly = r.Y + Theme.S(12f);
				int nameW = Math.Min(Theme.Measure(it.Name, Theme.Title).Width + 2, Math.Max(Theme.S(60f), right - x0 - (it.Store ? Theme.S(70f) : 0)));
				TextRenderer.DrawText(g, it.Name, Theme.Title, new Rectangle(x0, ly, nameW, Theme.S(22f)), it.Busy ? Theme.Sub : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				if (it.Store)
				{
					int cw = Theme.PillW("Store", Theme.Tiny);
					if (x0 + nameW + Theme.S(10f) + cw <= right)
					{
						Theme.Pill(g, new Rectangle(x0 + nameW + Theme.S(10f), ly, cw, Theme.S(20f)), "Store", Theme.Tiny, Theme.Silver2, 20);
					}
				}
				List<string> parts = new List<string>();
				parts.Add((it.Pub != "") ? it.Pub : "Издатель неизвестен");
				if (it.Ver != "")
				{
					parts.Add("v" + it.Ver);
				}
				if (it.Date != "")
				{
					parts.Add(FmtDate(it.Date));
				}
				string line = it.Busy ? "Удаляю…" : string.Join("   •   ", parts.ToArray());
				TextRenderer.DrawText(g, line, Theme.Small, new Rectangle(x0, ly + Theme.S(25f), right - x0, Theme.S(20f)), it.Busy ? Theme.Yellow : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				string sz = (it.Size > 0) ? CleanView.Fmt(it.Size) : "—";
				TextRenderer.DrawText(g, sz, Theme.Body, new Rectangle(right, r.Y, sizeW, r.Height), (it.Size >= 1073741824.0) ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				if (!it.DirR.IsEmpty)
				{
					Btn(g, it.DirR, null, Glyph.Folder, hot && _hotBtn == 2, danger: false);
				}
				Btn(g, it.DelR, it.Busy ? "…" : "Удалить", null, hot && _hotBtn == 1 && !it.Busy, danger: true);
			}
			if (!_moreR.IsEmpty)
			{
				string more = "Показать ещё " + Math.Min(80, _shown.Count - n) + "  (осталось " + (_shown.Count - n) + ")";
				Btn(g, _moreR, more, null, _hot == -2, danger: false);
			}
			if (!_loaded)
			{
				for (int i = 0; i < 6; i++)
				{
					Skeleton.Row(g, new Rectangle(pad - Theme.S(8f), _listTop + i * RowH, base.Width - pad * 2, RowH), this);
				}
			}
			if (_shown.Count == 0 && _loaded)
			{
				Rectangle er = new Rectangle(pad, _listTop + Theme.S(10f), base.Width - pad * 2, Theme.S(50f));
				string msg = (_search.Text.Trim() != "") ? "Ничего не нашлось — попробуй другое название" : "Здесь пусто";
				TextRenderer.DrawText(g, msg, Theme.Body, er, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
		}

		private int SortHit(Point p)
		{
			if (!_sortR.Contains(p))
			{
				return -1;
			}
			int sx = _sortR.Right;
			for (int i = _sorts.Length - 1; i >= 0; i--)
			{
				int w = Theme.Measure(_sorts[i], Theme.Small).Width + Theme.S(16f);
				sx -= w;
				if (p.X >= sx && p.X < sx + w)
				{
					return i;
				}
				sx -= Theme.S(2f);
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			int hb = 0;
			int n = Math.Min(_limit, _shown.Count);
			for (int i = 0; i < n; i++)
			{
				Item it = _shown[i];
				if (it.R.Contains(e.Location))
				{
					h = i;
					hb = it.DelR.Contains(e.Location) ? 1 : (it.DirR.Contains(e.Location) ? 2 : 0);
					break;
				}
			}
			if (h == -1 && _moreR.Contains(e.Location))
			{
				h = -2;
			}
			Cursor = (hb != 0 || h == -2 || SortHit(e.Location) >= 0) ? Cursors.Hand : Cursors.Default;
			if (h != _hot || hb != _hotBtn)
			{
				_hot = h;
				_hotBtn = hb;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (_hot != -1)
			{
				_hot = -1;
				Invalidate();
			}
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (e.Button != MouseButtons.Left)
			{
				return;
			}
			int s = SortHit(e.Location);
			if (s >= 0)
			{
				if (s != _sort)
				{
					_sort = s;
					LayoutAgain();
				}
				return;
			}
			if (_moreR.Contains(e.Location))
			{
				_limit += 80;
				LayoutAgain();
				return;
			}
			int n = Math.Min(_limit, _shown.Count);
			for (int i = 0; i < n; i++)
			{
				Item it = _shown[i];
				if (it.Busy)
				{
					continue;
				}
				if (it.DelR.Contains(e.Location))
				{
					UninstallClicked?.Invoke(it);
					return;
				}
				if (it.DirR.Contains(e.Location))
				{
					Shell.Open("explorer.exe", "\"" + it.Loc + "\"");
					return;
				}
			}
		}
	
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Skeleton.Track(this, !_loaded);
		}
	}
}
