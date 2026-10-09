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
using System.Windows.Forms;

namespace WinWeaver
{
	// Built-in apps: Edge / OneDrive and the list of preinstalled Microsoft apps, each with its state and its own button
	internal class AppsView : Card
	{
		public class Item
		{
			public string Id;
			public string Name;
			public string Desc;
			public bool Installed;
			public int Rec; // 1 = can be removed, 0 = up to the user
			public bool System; // Edge / OneDrive
			public FButton Btn;
			public Rectangle R;
		}

		private readonly List<Item> _items = new List<Item>();

		public readonly List<FButton> Buttons = new List<FButton>();

		public event Action<Item> RemoveClicked;

		private bool _loaded;

		private int _hot = -1;

		private Rectangle _headR;

		private Rectangle _statsR;

		private int _sysTop;

		private int _appsTop;

		private int RowH => Theme.S(62f);

		public IEnumerable<Item> Items => _items;

		public FButton AddButton(string text, BtnKind kind, EventHandler click, string glyph = null)
		{
			FButton b = new FButton { Text = text, Kind = kind, Font = Theme.Body, Glyph = glyph };
			b.AutoWidth(Theme.S(30f));
			b.Click += click;
			Buttons.Add(b);
			base.Controls.Add(b);
			return b;
		}

		private static string Str(object o)
		{
			return (o == null) ? "" : o.ToString();
		}

		public override object Fetch(IEngine e)
		{
			List<Item> list = new List<Item>();
			list.Add(new Item { Id = "edge", Name = "Microsoft Edge", Desc = "Браузер Microsoft. WebView2 остаётся — он нужен приложениям", Installed = e.Str(e.Eval("Get-Edge")) != "удалён", Rec = 0, System = true });
			list.Add(new Item { Id = "onedrive", Name = "OneDrive", Desc = "Облако Microsoft. Файлы в папке OneDrive на диске не удаляются", Installed = e.Str(e.Eval("Get-OneDrive")) != "удалён", Rec = 1, System = true });
			object v = e.Eval("Get-FApps");
			object[] arr = v as object[];
			if (arr != null)
			{
				foreach (object o in arr)
				{
					Hashtable h = o as Hashtable;
					if (h == null)
					{
						continue;
					}
					list.Add(new Item
					{
						Id = Str(h["Id"]),
						Name = Str(h["Name"]),
						Desc = Str(h["Desc"]),
						Installed = h["Installed"] is bool && (bool)h["Installed"],
						Rec = (h["Rec"] is int) ? (int)h["Rec"] : 0
					});
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
			foreach (Item old in _items)
			{
				if (old.Btn != null)
				{
					base.Controls.Remove(old.Btn);
					old.Btn.Dispose();
				}
			}
			_items.Clear();
			// installed first inside each group, recommended removals on top
			List<Item> sys = list.FindAll((Item i) => i.System);
			List<Item> apps = list.FindAll((Item i) => !i.System);
			apps.Sort(delegate(Item a, Item b)
			{
				if (a.Installed != b.Installed)
				{
					return a.Installed ? -1 : 1;
				}
				if (a.Rec != b.Rec)
				{
					return b.Rec - a.Rec;
				}
				return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			});
			foreach (Item it in sys)
			{
				_items.Add(it);
			}
			foreach (Item it in apps)
			{
				_items.Add(it);
			}
			foreach (Item it in _items)
			{
				Item item = it;
				if (!item.Installed)
				{
					continue;
				}
				item.Btn = new FButton { Text = "Удалить", Kind = BtnKind.Ghost, Font = Theme.Small };
				item.Btn.AutoWidth(Theme.S(22f));
				item.Btn.Height = Theme.S(32f);
				item.Btn.Click += delegate
				{
					RemoveClicked?.Invoke(item);
				};
				base.Controls.Add(item.Btn);
			}
			_loaded = true;
			if (base.Parent is Canvas)
			{
				((Canvas)base.Parent).Page.RelayoutSoon();
			}
			Invalidate();
		}

		public int RecLeft => _items.FindAll((Item i) => i.Installed && i.Rec == 1 && !i.System).Count;

		public override int DoLayout(int x, int y, int w)
		{
			int pad = base.Pad;
			int titleH = Theme.Measure("Ag", Theme.Title).Height + Theme.S(4f) + Theme.Measure("Ag", Theme.Small).Height;
			_headR = new Rectangle(pad, pad, w - pad * 2, Math.Max(Theme.S(44f), titleH));
			int cy = _headR.Bottom + Theme.S(16f);
			_statsR = new Rectangle(pad, cy, w - pad * 2, Theme.S(74f));
			cy = _statsR.Bottom + Theme.S(14f);
			int lx = pad;
			foreach (FButton b in Buttons)
			{
				if (lx + b.Width > w - pad && lx > pad)
				{
					lx = pad;
					cy += b.Height + Theme.S(8f);
				}
				b.Location = new Point(lx, cy);
				lx += b.Width + Theme.S(8f);
			}
			if (Buttons.Count > 0)
			{
				cy += Buttons[0].Height + Theme.S(18f);
			}
			int sec = Theme.S(28f);
			_sysTop = cy;
			cy += sec;
			bool first = true;
			foreach (Item it in _items)
			{
				if (!it.System && first)
				{
					first = false;
					cy += Theme.S(10f);
					_appsTop = cy;
					cy += sec;
				}
				it.R = new Rectangle(pad - Theme.S(8f), cy, w - pad * 2 + Theme.S(16f), RowH);
				if (it.Btn != null)
				{
					it.Btn.Location = new Point(w - pad - it.Btn.Width, cy + (RowH - it.Btn.Height) / 2);
				}
				cy += RowH;
			}
			if (!_loaded)
			{
				cy += Theme.S(60f);
			}
			int h = cy + pad - Theme.S(6f);
			SetBounds(x, y, w, h);
			return h;
		}

		private void Stat(Graphics g, Rectangle r, string value, string label, Color c, string hint)
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
			foreach (char ch in name ?? "")
			{
				hsh = hsh * 31 + ch;
			}
			return pal[Math.Abs(hsh) % pal.Length];
		}

		private void SectionHead(Graphics g, int y, string text, string right)
		{
			TextRenderer.DrawText(g, text, Theme.Tiny, new Point(base.Pad, y + Theme.S(4f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int tw = TextRenderer.MeasureText(g, text, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
			int lx = base.Pad + tw + Theme.S(12f);
			int rx = base.Width - base.Pad;
			if (!string.IsNullOrEmpty(right))
			{
				int rw = TextRenderer.MeasureText(g, right, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
				TextRenderer.DrawText(g, right, Theme.Tiny, new Point(rx - rw, y + Theme.S(4f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				rx -= rw + Theme.S(12f);
			}
			if (rx > lx)
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(lx, 0, rx - lx, 1), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(8, 255, 255, 255), 0f))
				{
					g.FillRectangle(lb, lx, y + Theme.S(11f), rx - lx, 1);
				}
			}
		}

		protected override void PaintContent(Graphics g)
		{
			int pad = base.Pad;
			Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - Theme.S(44f)) / 2, Theme.S(44f), Theme.S(44f)), Glyph.Apps);
			int tx = _headR.X + Theme.S(58f);
			int th = Theme.Measure("Ag", Theme.Title).Height;
			int ty = _headR.Y + (_headR.Height - th - Theme.S(4f) - Theme.Measure("Ag", Theme.Small).Height) / 2;
			TextRenderer.DrawText(g, "Встроенные приложения Windows", Theme.Title, new Rectangle(tx, ty, _headR.Right - tx, th + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, "Удаляй по одному или все рекомендованные сразу. Вернуть приложение можно из Microsoft Store", Theme.Small, new Rectangle(tx, ty + th + Theme.S(4f), _headR.Right - tx, Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			int inst = _items.FindAll((Item i) => i.Installed && !i.System).Count;
			int gone = _items.FindAll((Item i) => !i.Installed && !i.System).Count;
			int rec = RecLeft;
			int sysInst = _items.FindAll((Item i) => i.Installed && i.System).Count;
			int gap = Theme.S(10f);
			int sw = (_statsR.Width - gap * 2) / 3;
			Stat(g, new Rectangle(_statsR.X, _statsR.Y, sw, _statsR.Height), _loaded ? rec.ToString() : "…", "можно удалить", (rec == 0) ? Theme.Green : Theme.Yellow, (rec == 0) ? "мусора не осталось" : "реклама, подписки и лишние сервисы");
			Stat(g, new Rectangle(_statsR.X + sw + gap, _statsR.Y, sw, _statsR.Height), _loaded ? inst.ToString() : "…", "установлено", Theme.Silver1, _loaded ? ("из списка, удалено: " + gone) : "");
			Stat(g, new Rectangle(_statsR.X + (sw + gap) * 2, _statsR.Y, _statsR.Width - (sw + gap) * 2, _statsR.Height), _loaded ? (2 - sysInst).ToString() + " / 2" : "…", "удалено", (sysInst == 0) ? Theme.Green : Theme.Silver2, "Edge и OneDrive");
			if (!_loaded)
			{
				return;
			}
			SectionHead(g, _sysTop, "СИСТЕМНЫЕ КОМПОНЕНТЫ", null);
			SectionHead(g, _appsTop, "ПРЕДУСТАНОВЛЕННЫЕ ПРИЛОЖЕНИЯ", inst + " установлено");
			Item prev = null;
			for (int idx = 0; idx < _items.Count; idx++)
			{
				Item it = _items[idx];
				Rectangle r = it.R;
				if (idx == _hot)
				{
					using (GraphicsPath hp = Theme.Round(r, Theme.S(10f)))
					{
						using (SolidBrush hb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
						{
							g.FillPath(hb, hp);
						}
					}
				}
				else if (prev != null && prev.System == it.System)
				{
					using (SolidBrush lb = new SolidBrush(Color.FromArgb(34, 255, 255, 255)))
					{
						g.FillRectangle(lb, r.X + Theme.S(60f), r.Y, r.Width - Theme.S(68f), 1);
					}
				}
				prev = it;
				int ic = Theme.S(36f);
				Rectangle ir = new Rectangle(r.X + Theme.S(8f), r.Y + (r.Height - ic) / 2, ic, ic);
				using (GraphicsPath p = Theme.Round(ir, ir.Width * 0.28f))
				{
					Color t = it.Installed ? Tint(it.Name) : Color.FromArgb(70, 70, 78);
					Theme.FillGradient(g, p, ir, Theme.Mix(t, Color.FromArgb(70, 70, 80), 0.35f), Theme.Mix(t, Color.FromArgb(30, 30, 36), 0.6f));
					using (Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255)))
					{
						g.DrawPath(pen, p);
					}
				}
				using (Font f = Theme.F(12f, semi: true))
				{
					TextRenderer.DrawText(g, it.Name.Substring(0, 1).ToUpperInvariant(), f, ir, it.Installed ? Theme.Text : Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				}
				int x0 = ir.Right + Theme.S(14f);
				int right = (it.Btn != null) ? (it.Btn.Left - Theme.S(14f)) : (r.Right - Theme.S(120f));
				int ly = r.Y + Theme.S(11f);
				int nw = Math.Min(Theme.Measure(it.Name, Theme.Title).Width + 2, Math.Max(Theme.S(60f), right - x0 - Theme.S(140f)));
				TextRenderer.DrawText(g, it.Name, Theme.Title, new Rectangle(x0, ly, nw, Theme.S(22f)), it.Installed ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				string chip = it.Installed ? ((it.Rec == 1) ? "Можно удалить" : (it.System ? "По желанию" : "Пригодится не всем")) : null;
				if (chip != null)
				{
					int cw = Theme.PillW(chip, Theme.Tiny);
					int cx = x0 + nw + Theme.S(10f);
					if (cx + cw <= right)
					{
						Theme.Pill(g, new Rectangle(cx, ly, cw, Theme.S(20f)), chip, Theme.Tiny, (it.Rec == 1) ? Theme.Yellow : Theme.Silver2, 24);
					}
				}
				TextRenderer.DrawText(g, it.Desc, Theme.Small, new Rectangle(x0, ly + Theme.S(24f), right - x0, Theme.S(20f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				if (!it.Installed)
				{
					// "removed" mark with a vector check
					string done = "Удалено";
					int dw = TextRenderer.MeasureText(g, done, Theme.Small, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
					int dx = r.Right - Theme.S(8f) - dw;
					int dy = r.Y + r.Height / 2;
					TextRenderer.DrawText(g, done, Theme.Small, new Point(dx, dy - Theme.S(9f)), Theme.Green, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
					float u = Theme.S(1f);
					float cx0 = dx - Theme.S(18f);
					using (Pen cp = new Pen(Theme.Green, Theme.S(1.7f)))
					{
						cp.StartCap = LineCap.Round;
						cp.EndCap = LineCap.Round;
						cp.LineJoin = LineJoin.Round;
						g.DrawLines(cp, new PointF[] { new PointF(cx0, dy), new PointF(cx0 + 4 * u, dy + 4 * u), new PointF(cx0 + 12 * u, dy - 4 * u) });
					}
				}
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			for (int i = 0; i < _items.Count; i++)
			{
				if (_items[i].R.Contains(e.Location))
				{
					h = i;
				}
			}
			if (h != _hot)
			{
				_hot = h;
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
	}
}
