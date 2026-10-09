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
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WinWeaver
{
	// Startup manager: summary tiles, filter, one row per program with its icon, publisher, advice chip and a switch
	internal class StartupView : Card
	{
		public class Item
		{
			public string Id;
			public string Name;
			public string Company;
			public string Desc;
			public string Source;
			public string Command;
			public string Path;
			public bool Exists;
			public bool On;
			public int Advice; // 1 = can be disabled, 2 = better keep, 0 = neutral
			public string Why;
			public Image Icon;
			public ToggleSwitch Toggle;
			public Rectangle R;
			public Rectangle MoreR;
			public int Delay;
		}

		private static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

		private readonly List<Item> _items = new List<Item>();

		private readonly Segment _filter = new Segment { Items = new string[4] { "Все", "Включённые", "Выключенные", "Советуем выкл." } };

		public readonly List<FButton> Buttons = new List<FButton>();

		public event Action<Item, bool> ItemToggled;

		public event Action<Item, Point> MenuClicked;

		public List<Item> Items => _items;

		private bool _hotMore;

		private bool _loaded;

		private int _hot = -1;

		private Rectangle _headR;

		private Rectangle _statsR;

		private int _listTop;

		private readonly Tween _appear;

		private const string Keep = "securityhealth|defender|msmpeng|realtek|rtkaud|rthdvcpl|nahimic|waves|maxxaudio|dolby|nvidia|nvcontainer|nvbackend|\\bamd\\b|radeon|igfx|intel|synaptics|\\belan|alps|ctfmon|bluetooth|kaspersky|\\beset\\b|egui|avast|\\bavg\\b|bitdefender|drweb|malwarebytes|norton|mcafee|wacom|touchpad|ibmpmsvc|asus.*armoury|lenovo.*vantage|hp.*hotkey|fn.?key|hotkey";

		private const string Off = "discord|steam|epic|spotify|telegram|teams|skype|zoom|origin|eadesktop|\\bea\\b app|battle\\.net|ubisoft|uplay|goggalaxy|gog galaxy|adobe|creative cloud|ccleaner|onedrive|\\bedge\\b|msedge|torrent|viber|whatsapp|yandex|opera|chrome|brave|vivaldi|googleupdate|google update|dropbox|itunes|icloud|jusched|java update|cortana|riotclient|riot client|overwolf|slack|notion|figma|\\bobs\\b|vkplay|vk play|lghub_updater|update\\.exe|updater|autoupdate";

		public StartupView()
		{
			_appear = new Tween(this, 0f, 0.12f);
			_filter.Selected = 0;
			_filter.Changed += delegate
			{
				_hot = -1;
				LayoutAgain();
			};
			base.Controls.Add(_filter);
		}

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

		private static void Classify(Item it)
		{
			string hay = (it.Name + " " + it.Path + " " + it.Company + " " + it.Desc).ToLowerInvariant();
			if (Regex.IsMatch(hay, Keep))
			{
				it.Advice = 2;
				it.Why = "Драйвер, звук, тачпад или защита — без неё могут пропасть значки, горячие клавиши или звук";
			}
			else if (Regex.IsMatch(hay, Off))
			{
				it.Advice = 1;
				it.Why = "Не нужна сразу после входа — откроешь, когда понадобится. Windows загрузится быстрее";
			}
			if (!it.Exists && !string.IsNullOrEmpty(it.Path))
			{
				it.Advice = 1;
				it.Why = "Файл программы не найден — запись, скорее всего, осталась от удалённой программы";
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
						using (Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(path))
						{
							if (ic != null)
							{
								img = ic.ToBitmap();
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
			object v = e.Eval("Get-FStartup");
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
						Company = Str(h["Company"]),
						Desc = Str(h["Desc"]),
						Source = Str(h["Source"]),
						Command = Str(h["Command"]),
						Path = Str(h["Path"]),
						Exists = !(h["Exists"] is bool) || (bool)h["Exists"],
						On = h["Enabled"] is bool && (bool)h["Enabled"]
					};
					try
					{
						it.Delay = (h["Delay"] == null) ? 0 : Convert.ToInt32(h["Delay"]);
					}
					catch
					{
						it.Delay = 0;
					}
					if (it.Name == "")
					{
						it.Name = it.Id;
					}
					Classify(it);
					it.Icon = it.Exists ? LoadIcon(it.Path) : null;
					list.Add(it);
				}
			}
			list.Sort(delegate(Item a, Item b)
			{
				if (a.On != b.On)
				{
					return a.On ? -1 : 1;
				}
				return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			});
			return list;
		}

		public override void Apply(object v)
		{
			List<Item> list = v as List<Item>;
			if (list == null)
			{
				return;
			}
			Dictionary<string, ToggleSwitch> keep = new Dictionary<string, ToggleSwitch>();
			foreach (Item old in _items)
			{
				if (old.Toggle != null && !keep.ContainsKey(old.Id))
				{
					keep[old.Id] = old.Toggle;
				}
			}
			_items.Clear();
			foreach (Item it in list)
			{
				Item item = it;
				ToggleSwitch ts;
				if (keep.TryGetValue(item.Id, out ts))
				{
					keep.Remove(item.Id);
					ts.Tag = item;
					if (ts.On != item.On)
					{
						ts.On = item.On;
					}
				}
				else
				{
					ts = new ToggleSwitch();
					ts.Tag = item;
					ts.SetOnNoAnim(item.On);
					ToggleSwitch t0 = ts;
					ts.Toggled += delegate
					{
						Item cur = t0.Tag as Item;
						if (cur == null)
						{
							return;
						}
						cur.On = !cur.On;
						t0.On = cur.On;
						Invalidate();
						ItemToggled?.Invoke(cur, cur.On);
					};
					base.Controls.Add(ts);
				}
				item.Toggle = ts;
				_items.Add(item);
			}
			foreach (ToggleSwitch dead in keep.Values)
			{
				base.Controls.Remove(dead);
				dead.Dispose();
			}
			if (!_loaded)
			{
				_loaded = true;
				Skeleton.Track(this, false);
				_appear.Target = 1f;
			}
			LayoutAgain();
		}

		// called by MainForm when the engine refused a change
		public void Revert(Item it)
		{
			it.On = !it.On;
			it.Toggle.On = it.On;
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

		private bool Shown(Item it)
		{
			switch (_filter.Selected)
			{
			case 1:
				return it.On;
			case 2:
				return !it.On;
			case 3:
				return it.Advice == 1 && it.On;
			default:
				return true;
			}
		}

		private int Count(Func<Item, bool> f)
		{
			int n = 0;
			foreach (Item it in _items)
			{
				if (f(it))
				{
					n++;
				}
			}
			return n;
		}

		private int RowH => Theme.S(66f);

		public override int DoLayout(int x, int y, int w)
		{
			int pad = base.Pad;
			int hy = pad;
			int titleH = Theme.Measure("Ag", Theme.Title).Height + Theme.S(4f) + Theme.Measure("Ag", Theme.Small).Height;
			int bx = w - pad;
			int btnW = 0;
			foreach (FButton b in Buttons)
			{
				btnW += b.Width + Theme.S(8f);
			}
			bool btnBelow = w - pad * 2 - Theme.S(56f) - Theme.S(330f) < btnW;
			_headR = new Rectangle(pad, hy, w - pad * 2, Math.Max(Theme.S(44f), titleH));
			int cy = _headR.Bottom + Theme.S(16f);
			if (!btnBelow)
			{
				for (int i = Buttons.Count - 1; i >= 0; i--)
				{
					FButton b = Buttons[i];
					bx -= b.Width;
					b.Location = new Point(bx, hy + (_headR.Height - b.Height) / 2);
					bx -= Theme.S(8f);
				}
			}
			else
			{
				int lx = pad;
				foreach (FButton b in Buttons)
				{
					b.Location = new Point(lx, cy);
					lx += b.Width + Theme.S(8f);
				}
				cy += Buttons[0].Height + Theme.S(16f);
			}
			_statsR = new Rectangle(pad, cy, w - pad * 2, Theme.S(74f));
			cy = _statsR.Bottom + Theme.S(16f);
			int fw = Math.Min(w - pad * 2, Theme.S(560f));
			_filter.SetBounds(pad, cy, fw, _filter.Height);
			cy += _filter.Height + Theme.S(12f);
			_listTop = cy;
			int ry = cy;
			int n = 0;
			foreach (Item it in _items)
			{
				bool vis = Shown(it);
				if (it.Toggle.Visible != vis)
				{
					it.Toggle.Visible = vis;
				}
				if (!vis)
				{
					it.R = Rectangle.Empty;
					continue;
				}
				it.R = new Rectangle(pad - Theme.S(8f), ry, w - pad * 2 + Theme.S(16f), RowH);
				it.Toggle.Location = new Point(w - pad - it.Toggle.Width, ry + (RowH - it.Toggle.Height) / 2);
				int mb = Theme.S(32f);
				it.MoreR = new Rectangle(it.Toggle.Left - Theme.S(14f) - mb, ry + (RowH - mb) / 2, mb, mb);
				ry += RowH;
				n++;
			}
			if (!_loaded)
			{
				ry += RowH * 5;
			}
			else if (n == 0)
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
				if (it.Icon != null)
				{
					int s = r.Width - Theme.S(10f);
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					g.DrawImage(it.Icon, new Rectangle(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s));
					return;
				}
			}
			string letter = string.IsNullOrEmpty(it.Name) ? "?" : it.Name.Substring(0, 1).ToUpperInvariant();
			using (Font f = Theme.F(13f, semi: true))
			{
				TextRenderer.DrawText(g, letter, f, r, Color.FromArgb(236, 236, 240), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
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

		protected override void PaintContent(Graphics g)
		{
			int pad = base.Pad;
			Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - Theme.S(44f)) / 2, Theme.S(44f), Theme.S(44f)), Glyph.Bolt);
			int tx = _headR.X + Theme.S(58f);
			int tr = (Buttons.Count > 0 && Buttons[0].Top < _headR.Bottom) ? (Buttons[0].Left - Theme.S(12f)) : _headR.Right;
			int th = Theme.Measure("Ag", Theme.Title).Height;
			int ty = _headR.Y + (_headR.Height - th - Theme.S(4f) - Theme.Measure("Ag", Theme.Small).Height) / 2;
			TextRenderer.DrawText(g, "Программы при входе в Windows", Theme.Title, new Rectangle(tx, ty, tr - tx, th + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, "Выключение не удаляет программу. В меню «•••» — отложенный запуск и удаление записи", Theme.Small, new Rectangle(tx, ty + th + Theme.S(4f), tr - tx, Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			// summary tiles
			int total = _items.Count;
			int on = Count((Item i) => i.On);
			int can = Count((Item i) => i.On && i.Advice == 1);
			int gap = Theme.S(10f);
			int sw = (_statsR.Width - gap * 2) / 3;
			string load = (on <= 3) ? "быстрый вход" : ((on <= 7) ? "средняя нагрузка" : "медленный вход");
			Stat(g, new Rectangle(_statsR.X, _statsR.Y, sw, _statsR.Height), _loaded ? total.ToString() : "…", "в списке", Theme.Silver2, "программ, которые могут стартовать сами");
			Stat(g, new Rectangle(_statsR.X + sw + gap, _statsR.Y, sw, _statsR.Height), _loaded ? on.ToString() : "…", "включено", (on <= 3) ? Theme.Green : ((on <= 7) ? Theme.Silver1 : Theme.Yellow), _loaded ? ("при входе: " + load) : "");
			Stat(g, new Rectangle(_statsR.X + (sw + gap) * 2, _statsR.Y, _statsR.Width - (sw + gap) * 2, _statsR.Height), _loaded ? can.ToString() : "…", "можно выключить", (can == 0) ? Theme.Green : Theme.Yellow, (can == 0) ? "лишнего в автозагрузке нет" : "фильтр «Советуем выкл.» покажет их");
			// rows
			int n = 0;
			for (int idx = 0; idx < _items.Count; idx++)
			{
				Item it = _items[idx];
				if (it.R.IsEmpty)
				{
					continue;
				}
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
				else if (n > 0)
				{
					using (SolidBrush lb = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
					{
						g.FillRectangle(lb, r.X + Theme.S(60f), r.Y, r.Width - Theme.S(68f), 1);
					}
				}
				n++;
				int ic = Theme.S(38f);
				Rectangle ir = new Rectangle(r.X + Theme.S(8f), r.Y + (r.Height - ic) / 2, ic, ic);
				DrawAppIcon(g, it, ir);
				int x0 = ir.Right + Theme.S(14f);
				int right = it.MoreR.Left - Theme.S(12f);
				InstalledView.Btn(g, it.MoreR, null, Glyph.More, idx == _hot && _hotMore, danger: false);
				// name + chips
				int nameW = Math.Min(Theme.Measure(it.Name, Theme.Title).Width + 2, Math.Max(Theme.S(60f), right - x0 - Theme.S(150f)));
				int ly = r.Y + Theme.S(12f);
				Color nameC = it.On ? Theme.Text : Theme.Sub;
				TextRenderer.DrawText(g, it.Name, Theme.Title, new Rectangle(x0, ly, nameW, Theme.S(22f)), nameC, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				int cx = x0 + nameW + Theme.S(10f);
				string chip = null;
				Color cc = Theme.Sub;
				if (!it.Exists && !string.IsNullOrEmpty(it.Path))
				{
					chip = "Файл не найден";
					cc = Theme.Red;
				}
				else if (it.Advice == 1)
				{
					chip = it.On ? "Можно выключить" : "Выключено — так и надо";
					cc = it.On ? Theme.Yellow : Theme.Green;
				}
				else if (it.Advice == 2)
				{
					chip = it.On ? "Лучше оставить" : "Лучше включить";
					cc = it.On ? Theme.Green : Theme.Yellow;
				}
				if (chip != null)
				{
					int cw = Theme.PillW(chip, Theme.Tiny);
					if (cx + cw <= right)
					{
						Theme.Pill(g, new Rectangle(cx, ly, cw, Theme.S(20f)), chip, Theme.Tiny, cc, 24);
						cx += cw + Theme.S(6f);
					}
				}
				if (it.Delay > 0 && it.On)
				{
					string dl = "через " + ((it.Delay % 60 == 0) ? (it.Delay / 60 + " мин") : (it.Delay + " с"));
					int dw = Theme.PillW(dl, Theme.Tiny);
					if (cx + dw <= right)
					{
						Theme.Pill(g, new Rectangle(cx, ly, dw, Theme.S(20f)), dl, Theme.Tiny, Theme.Silver2, 18);
					}
				}
				// second line: publisher • where • file
				string who = (it.Company != "") ? it.Company : ((it.Desc != "") ? it.Desc : "Издатель неизвестен");
				string line = who + "   •   " + it.Source;
				string file = (it.Path != "") ? System.IO.Path.GetFileName(it.Path) : it.Command;
				if (!string.IsNullOrEmpty(file))
				{
					line += "   •   " + file;
				}
				if (idx == _hot && !string.IsNullOrEmpty(it.Why))
				{
					line = it.Why;
				}
				TextRenderer.DrawText(g, line, Theme.Small, new Rectangle(x0, ly + Theme.S(25f), right - x0, Theme.S(20f)), (idx == _hot && it.Why != null) ? Theme.Mix(Theme.Sub, Theme.Text, 0.4f) : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			if (!_loaded)
			{
				for (int i = 0; i < 5; i++)
				{
					Skeleton.Row(g, new Rectangle(pad - Theme.S(8f), _listTop + i * RowH, base.Width - pad * 2, RowH), this);
				}
			}
			if (n == 0 && _loaded)
			{
				Rectangle er = new Rectangle(pad, _listTop + Theme.S(10f), base.Width - pad * 2, Theme.S(50f));
				string msg = (_items.Count == 0) ? "Автозагрузка пуста — Windows стартует максимально быстро" : ((_filter.Selected == 3) ? "Отлично: лишних программ в автозагрузке нет" : "Здесь пусто — выбери другой фильтр");
				TextRenderer.DrawText(g, msg, Theme.Body, er, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			for (int i = 0; i < _items.Count; i++)
			{
				if (!_items[i].R.IsEmpty && _items[i].R.Contains(e.Location))
				{
					h = i;
				}
			}
			bool hm = h >= 0 && _items[h].MoreR.Contains(e.Location);
			Cursor = hm ? Cursors.Hand : Cursors.Default;
			if (h != _hot || hm != _hotMore)
			{
				_hot = h;
				_hotMore = hm;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			foreach (Item it in _items)
			{
				if (it.R.IsEmpty || !it.R.Contains(e.Location))
				{
					continue;
				}
				if (it.MoreR.Contains(e.Location) || e.Button == MouseButtons.Right)
				{
					Point at = it.MoreR.Contains(e.Location) ? new Point(it.MoreR.Left, it.MoreR.Bottom + Theme.S(4f)) : e.Location;
					MenuClicked?.Invoke(it, PointToScreen(at));
				}
				return;
			}
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
	
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Skeleton.Track(this, !_loaded);
		}
	}
}
