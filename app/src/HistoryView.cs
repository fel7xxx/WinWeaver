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
	// List of recent changes from the engine history (Get-FHistory). Every change can be rolled back on its own,
	// auto-tune runs are grouped and can also be rolled back in one click.
	internal class HistoryView : Card
	{
		private class Entry
		{
			public string Id;

			public DateTime Time;

			public string Title;

			public string Cat;

			public string Group;

			public string GroupTitle;

			public bool Can;

			public bool Done;

			public string Note;
		}

		private enum RowKind
		{
			Day,
			Group,
			Item
		}

		private class Row
		{
			public RowKind Kind;

			public string Text;

			public Entry E;

			public List<Entry> Items;

			public Rectangle R;

			public Rectangle Btn;

			public bool Indent;

			public bool Last;
		}

		private class HitR
		{
			public Rectangle R;

			public string Kind;

			public string Arg;

			public string Title;
		}

		private static CultureInfo Ru => L.Culture;

		public readonly bool Compact;

		public int CompactMax = 5;

		private List<Entry> _items = new List<Entry>();

		private readonly List<Row> _rows = new List<Row>();

		private readonly List<HitR> _hits = new List<HitR>();

		private readonly HashSet<string> _open = new HashSet<string>();

		private readonly HashSet<string> _seenGroups = new HashSet<string>();

		private string _hover;

		private string _busy;

		private float _appear = 1f;

		private bool _loaded;

		private string _strikeId;

		private float _strike = 1f;

		private Rectangle _headR;

		private Rectangle _allR;

		public Action<string, string> UndoItem;

		public Action<string, string> UndoGroup;

		public Action ShowAll;

		public int ActiveCount
		{
			get
			{
				int n = 0;
				foreach (Entry item in _items)
				{
					if (!item.Done)
					{
						n++;
					}
				}
				return n;
			}
		}

		private static CultureInfo MakeRu()
		{
			try
			{
				return new CultureInfo("ru-RU");
			}
			catch
			{
				return CultureInfo.InvariantCulture;
			}
		}

		public HistoryView(bool compact)
		{
			Compact = compact;
		}

		public void SetBusy(string key)
		{
			_busy = key;
			Invalidate();
		}

		public override object Fetch(IEngine e)
		{
			return e.Eval("Get-FHistory");
		}

		private static string Str(object o)
		{
			if (o != null)
			{
				return o.ToString();
			}
			return "";
		}

		public override void Apply(object v)
		{
			List<Entry> list = new List<Entry>();
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
					DateTime t;
					if (!DateTime.TryParse(Str(h["Time"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
					{
						t = DateTime.MinValue;
					}
					list.Add(new Entry
					{
						Id = Str(h["Id"]),
						Time = t,
						Title = Str(h["Title"]),
						Cat = Str(h["Cat"]),
						Group = Str(h["Group"]),
						GroupTitle = Str(h["GroupTitle"]),
						Can = h["Can"] is bool && (bool)h["Can"],
						Done = h["Done"] is bool && (bool)h["Done"],
						Note = Str(h["Note"])
					});
				}
			}
			string newlyDone = null;
			Dictionary<string, bool> before = new Dictionary<string, bool>();
			foreach (Entry item in _items)
			{
				before[item.Id] = item.Done;
			}
			foreach (Entry item2 in list)
			{
				if (before.TryGetValue(item2.Id, out var d) && !d && item2.Done)
				{
					newlyDone = item2.Id;
				}
			}
			bool changed = list.Count != _items.Count;
			_items = list;
			// the newest auto-tune run starts expanded, older ones collapsed
			bool first = true;
			foreach (Entry item3 in _items)
			{
				if (item3.Group == "")
				{
					continue;
				}
				if (_seenGroups.Add(item3.Group) && first)
				{
					_open.Add(item3.Group);
				}
				first = false;
			}
			_busy = null;
			if (!_loaded || changed)
			{
				_loaded = true;
				Appear();
			}
			if (newlyDone != null && Anim.Enabled)
			{
				_strikeId = newlyDone;
				_strike = 0f;
				Anim.Start(this, 420, delegate(float t)
				{
					_strike = t;
					Invalidate();
				}, delegate
				{
					_strike = 1f;
					Invalidate();
				}, "hist-strike-" + GetHashCode());
			}
			RelayoutPage();
		}

		private void Appear()
		{
			if (!Anim.Enabled)
			{
				_appear = 1f;
				return;
			}
			_appear = 0f;
			Anim.Start(this, 650, delegate(float t)
			{
				_appear = t;
				Invalidate();
			}, delegate
			{
				_appear = 1f;
				Invalidate();
			}, "hist-appear-" + GetHashCode());
		}

		public void PlayAppear()
		{
			if (_loaded)
			{
				Appear();
			}
		}

		private void RelayoutPage()
		{
			for (Control p = Parent; p != null; p = p.Parent)
			{
				if (p is Page page)
				{
					page.RelayoutSoon();
					return;
				}
			}
			Invalidate();
		}

		private static string DayName(DateTime t)
		{
			if (t == DateTime.MinValue)
			{
				return "Раньше";
			}
			DateTime today = DateTime.Today;
			if (t.Date == today)
			{
				return "Сегодня";
			}
			if (t.Date == today.AddDays(-1.0))
			{
				return "Вчера";
			}
			return t.ToString((t.Year == today.Year) ? "d MMMM" : "d MMMM yyyy", Ru);
		}

		private static string TimeText(DateTime t)
		{
			if (t == DateTime.MinValue)
			{
				return "";
			}
			return t.ToString("HH:mm", Ru);
		}

		public static string CatName(string c)
		{
			switch (c)
			{
			case "privacy":
				return "Приватность";
			case "protect":
				return "Защита";
			case "update":
				return "Обновления";
			case "ui":
				return "Оформление";
			case "game":
				return "Игры";
			case "apps":
				return "Программы";
			case "clean":
				return "Очистка";
			case "net":
				return "Сеть";
			case "restore":
				return "Восстановление";
			case "fix":
				return "Исправления";
			case "app":
				return Brand.Name;
			default:
				return "Прочее";
			}
		}

		public static string CatGlyph(string c)
		{
			switch (c)
			{
			case "privacy":
				return Glyph.Lock;
			case "protect":
				return Glyph.Shield;
			case "update":
				return Glyph.Sync;
			case "ui":
				return Glyph.Brush;
			case "game":
				return Glyph.Game;
			case "apps":
				return Glyph.Apps;
			case "clean":
				return Glyph.Disk;
			case "net":
				return Glyph.Globe;
			case "restore":
				return Glyph.Restore;
			case "fix":
				return Glyph.Repair;
			case "app":
				return Glyph.Settings;
			default:
				return Glyph.Info;
			}
		}

		// soft accent per category: used for the timeline dots on the home page
		public static Color CatColor(string c)
		{
			switch (c)
			{
			case "privacy":
				return Color.FromArgb(130, 170, 255);
			case "protect":
				return Color.FromArgb(111, 207, 151);
			case "update":
				return Color.FromArgb(110, 200, 225);
			case "ui":
				return Color.FromArgb(185, 150, 255);
			case "game":
				return Color.FromArgb(240, 165, 100);
			case "apps":
				return Color.FromArgb(235, 135, 185);
			case "clean":
				return Color.FromArgb(226, 194, 107);
			case "net":
				return Color.FromArgb(95, 205, 180);
			case "fix":
				return Color.FromArgb(240, 130, 115);
			default:
				return Theme.Silver2;
			}
		}

		// split into units: a single change, or all changes of one auto-tune run
		private List<Row> Units()
		{
			List<Row> units = new List<Row>();
			Dictionary<string, Row> groups = new Dictionary<string, Row>();
			foreach (Entry e in _items)
			{
				if (e.Group != "")
				{
					if (!groups.TryGetValue(e.Group, out var g))
					{
						g = new Row
						{
							Kind = RowKind.Group,
							Text = ((e.GroupTitle != "") ? e.GroupTitle : "Группа изменений"),
							E = e,
							Items = new List<Entry>()
						};
						groups[e.Group] = g;
						units.Add(g);
					}
					g.Items.Add(e);
				}
				else
				{
					units.Add(new Row
					{
						Kind = RowKind.Item,
						E = e
					});
				}
			}
			return units;
		}

		private int ItemH => Theme.S(54f);

		private int GroupH => Theme.S(60f);

		private int DayH => Theme.S(32f);

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Pad;
			_rows.Clear();
			int top = pad;
			int headH = Theme.S(44f);
			_headR = new Rectangle(pad, top, w - pad * 2, headH);
			top += headH + Theme.S(Compact ? 14f : 8f);
			List<Row> units = Units();
			if (Compact)
			{
				int k = 0;
				foreach (Row u in units)
				{
					if (k++ >= CompactMax)
					{
						break;
					}
					int h = Theme.S((u.Kind == RowKind.Group) ? 58f : 52f);
					u.R = new Rectangle(pad, top, w - pad * 2, h);
					_rows.Add(u);
					top += h;
				}
			}
			else
			{
				string day = null;
				foreach (Row u2 in units)
				{
					string dn = DayName(u2.E.Time);
					if (dn != day)
					{
						day = dn;
						_rows.Add(new Row
						{
							Kind = RowKind.Day,
							Text = dn,
							R = new Rectangle(pad, top, w - pad * 2, DayH)
						});
						top += DayH;
					}
					if (u2.Kind == RowKind.Group)
					{
						u2.R = new Rectangle(pad, top, w - pad * 2, GroupH);
						_rows.Add(u2);
						top += GroupH;
						if (_open.Contains(u2.E.Group))
						{
							for (int i = 0; i < u2.Items.Count; i++)
							{
								Row r = new Row
								{
									Kind = RowKind.Item,
									E = u2.Items[i],
									Indent = true,
									Last = (i == u2.Items.Count - 1),
									R = new Rectangle(pad, top, w - pad * 2, ItemH)
								};
								_rows.Add(r);
								top += ItemH;
							}
							top += Theme.S(6f);
						}
					}
					else
					{
						u2.R = new Rectangle(pad, top, w - pad * 2, ItemH);
						_rows.Add(u2);
						top += ItemH;
					}
				}
			}
			if (_items.Count == 0)
			{
				top += Theme.S(Compact ? 52f : 120f);
			}
			int hh = top + pad - Theme.S(4f);
			SetBounds(x, y, w, hh);
			Invalidate();
			return hh;
		}

		private float RowA(int k)
		{
			return Anim.EaseOut(Anim.Clamp(_appear * 1.8f - (float)Math.Min(k, 10) * 0.07f));
		}

		private static Color Fade(Color c, float a)
		{
			return Theme.Mix(Theme.Card1, c, a);
		}

		private void Button(Graphics g, Rectangle r, string text, string key, bool primary, float a)
		{
			bool hot = _hover == key;
			bool busy = _busy != null && _busy == key;
			using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), r.Height / 2f))
			{
				if (primary)
				{
					Theme.FillGradient(g, p, r, Fade(hot ? Color.White : Theme.Silver1, a), Fade(Theme.Silver2, a));
				}
				else
				{
					Theme.FillGradient(g, p, r, Fade(hot ? Theme.Mix(Theme.Control1, Color.White, 0.12f) : Theme.Control1, a), Fade(Theme.Control2, a));
					using (Pen pen = new Pen(Fade(hot ? Theme.BorderHi : Theme.Border, a)))
					{
						g.DrawPath(pen, p);
					}
				}
			}
			TextRenderer.DrawText(g, busy ? "Откатываю…" : text, Theme.Small, r, Fade(primary ? Theme.OnSilver : Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			_hits.Clear();
			int pad = Pad;
			PaintHead(g);
			if (_items.Count == 0)
			{
				PaintEmpty(g);
				return;
			}
			int k = 0;
			for (int i = 0; i < _rows.Count; i++)
			{
				Row r = _rows[i];
				float a = RowA(k++);
				int lift = (int)((1f - a) * Theme.S(10f));
				Rectangle rr = r.R;
				rr.Offset(0, lift);
				if (Compact)
				{
					PaintCompactRow(g, r, rr, a, i == 0, i == _rows.Count - 1);
					continue;
				}
				switch (r.Kind)
				{
				case RowKind.Day:
					TextRenderer.DrawText(g, r.Text.ToUpperInvariant(), Theme.Tiny, new Rectangle(rr.X + Theme.S(2f), rr.Y + Theme.S(10f), rr.Width, Theme.S(18f)), Fade(Theme.Dim, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
					break;
				case RowKind.Group:
					PaintGroup(g, r, rr, a);
					break;
				default:
					PaintItem(g, r, rr, a);
					break;
				}
			}
			if (Compact)
			{
				return;
			}
		}

		private void PaintHead(Graphics g)
		{
			Rectangle h = _headR;
			if (Compact)
			{
				PaintCompactHead(g, h);
				return;
			}
			int total = _items.Count;
			int act = 0;
			int can = 0;
			foreach (Entry item in _items)
			{
				if (!item.Done)
				{
					act++;
					if (item.Can)
					{
						can++;
					}
				}
			}
			TextRenderer.DrawText(g, "Журнал изменений", Theme.Title, new Point(h.X, h.Y + Theme.S(2f)), Theme.Text, TextFormatFlags.NoPrefix);
			string sum = (total == 0) ? "Пока пусто" : ("Всего: " + total + "  •  действуют: " + act + "  •  можно откатить: " + can);
			TextRenderer.DrawText(g, sum, Theme.Small, new Point(h.X, h.Y + Theme.S(24f)), Theme.Sub, TextFormatFlags.NoPrefix);
			using (Pen pen = new Pen(Theme.Border))
			{
				g.DrawLine(pen, h.X, h.Bottom + Theme.S(2f), h.Right, h.Bottom + Theme.S(2f));
			}
		}

		private void PaintEmpty(Graphics g)
		{
			Rectangle r = new Rectangle(_headR.X, _headR.Bottom + Theme.S(Compact ? 18f : 24f), _headR.Width, Theme.S(Compact ? 40f : 70f));
			string t = Compact ? "Здесь появятся все изменения, которые делает программа. Любое можно откатить." : "Изменений пока нет.\nВсё, что меняет программа — переключатели, кнопки и автонастройка, — появится здесь, и любое изменение можно будет откатить отдельно.";
			TextRenderer.DrawText(g, t, Compact ? Theme.Small : Theme.Body, r, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | (Compact ? TextFormatFlags.Default : TextFormatFlags.HorizontalCenter));
		}

		private void Counts(out int act, out int can)
		{
			act = 0;
			can = 0;
			foreach (Entry item in _items)
			{
				if (!item.Done)
				{
					act++;
					if (item.Can)
					{
						can++;
					}
				}
			}
		}

		private void PaintCompactHead(Graphics g, Rectangle h)
		{
			Rectangle ib = new Rectangle(h.X, h.Y + (h.Height - Theme.S(38f)) / 2, Theme.S(38f), Theme.S(38f));
			Theme.IconBox(g, ib, Glyph.Clock, bright: false, 0f, Theme.IconSmall);
			int act, can;
			Counts(out act, out can);
			string bt = "Вся история  →";
			int bw = Theme.Measure(bt, Theme.Small).Width + Theme.S(30f);
			Rectangle br = new Rectangle(h.Right - bw, h.Y + (h.Height - Theme.S(32f)) / 2, bw, Theme.S(32f));
			int tx = ib.Right + Theme.S(14f);
			int tw = br.Left - tx - Theme.S(12f);
			TextRenderer.DrawText(g, "Недавние изменения", Theme.Title, new Rectangle(tx, h.Y + Theme.S(2f), tw, Theme.S(22f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			string sub = (_items.Count == 0) ? "Пока ничего не менялось" : ("Действуют: " + act + "  •  можно откатить: " + can + ((_items.Count > CompactMax) ? ("  •  всего: " + _items.Count) : ""));
			TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, h.Y + Theme.S(24f), tw, Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (_items.Count > 0)
			{
				Button(g, br, bt, "all", primary: false, 1f);
				_hits.Add(new HitR { R = br, Kind = "all" });
			}
			using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(h.X, h.Bottom + Theme.S(6f), Math.Max(2, h.Width), 2), Theme.Border, Color.FromArgb(0, Theme.Border), 0f))
			{
				g.FillRectangle(lb, h.X, h.Bottom + Theme.S(7f), h.Width, 1);
			}
		}

		// home page: a timeline of the latest changes with colored category dots
		private void PaintCompactRow(Graphics g, Row r, Rectangle rr, float a, bool first, bool last)
		{
			Entry e = r.E;
			bool group = r.Kind == RowKind.Group;
			string key = group ? ("g:" + e.Group) : ("i:" + e.Id);
			bool hot = _hover == key || _hover == "row:" + key;
			Rectangle bg = new Rectangle(rr.X - Theme.S(8f), rr.Y + Theme.S(2f), rr.Width + Theme.S(16f), rr.Height - Theme.S(4f));
			if (hot)
			{
				using (GraphicsPath p = Theme.Round(bg, Theme.S(10f)))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(14 * a), 255, 255, 255)))
					{
						g.FillPath(b, p);
					}
				}
			}
			int cx = rr.X + Theme.S(15f);
			int cy = rr.Y + rr.Height / 2;
			using (Pen lp = new Pen(Fade(Theme.Border, a), Math.Max(1f, Theme.S(1.5f))))
			{
				if (!first)
				{
					g.DrawLine(lp, cx, rr.Y - Theme.S(1f), cx, cy);
				}
				if (!last)
				{
					g.DrawLine(lp, cx, cy, cx, rr.Bottom + Theme.S(1f));
				}
			}
			int undo = 0;
			int done = 0;
			string cat = e.Cat;
			if (group)
			{
				foreach (Entry item in r.Items)
				{
					if (item.Done)
					{
						done++;
					}
					else if (item.Can)
					{
						undo++;
					}
				}
				cat = "group";
			}
			bool isDone = group ? (done == r.Items.Count) : e.Done;
			Color cc = group ? Theme.Silver1 : CatColor(e.Cat);
			if (isDone)
			{
				cc = Theme.Dim;
			}
			int d = Theme.S(group ? 30f : 26f);
			Rectangle dot = new Rectangle(cx - d / 2, cy - d / 2, d, d);
			using (SolidBrush cb = new SolidBrush(Theme.Card2))
			{
				g.FillEllipse(cb, dot);
			}
			using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(40 * a), cc)))
			{
				g.FillEllipse(db, dot);
			}
			using (Pen dp = new Pen(Color.FromArgb((int)(150 * a), cc), Math.Max(1f, Theme.S(1.2f))))
			{
				g.DrawEllipse(dp, dot);
			}
			Theme.DrawIcon(g, group ? Glyph.Bolt : CatGlyph(e.Cat), Theme.IconSmall, dot, Fade(cc, a));
			// right side: rollback button or a quiet state label
			string btnT = group ? "Откатить всё" : "Откатить";
			int bw = Theme.Measure(btnT, Theme.Small).Width + Theme.S(28f);
			Rectangle br = new Rectangle(rr.Right - bw, rr.Y + (rr.Height - Theme.S(30f)) / 2, bw, Theme.S(30f));
			int right = br.Left;
			if (!isDone && (group ? (undo > 0) : e.Can))
			{
				Button(g, br, btnT, key, primary: false, a);
				_hits.Add(new HitR { R = br, Kind = group ? "group" : "item", Arg = group ? e.Group : e.Id, Title = group ? r.Text : e.Title });
			}
			else
			{
				string st = isDone ? "Откачено" : "Без отката";
				Color sc = isDone ? Theme.Green : Theme.Dim;
				Size ss = Theme.Measure(st, Theme.Tiny);
				int sx = rr.Right - ss.Width - Theme.S(6f);
				int dd = Theme.S(6f);
				using (SolidBrush sb = new SolidBrush(Fade(sc, a)))
				{
					g.FillEllipse(sb, sx - dd - Theme.S(6f), cy - dd / 2, dd, dd);
				}
				TextRenderer.DrawText(g, st, Theme.Tiny, new Rectangle(sx, cy - ss.Height / 2 - 1, ss.Width + Theme.S(4f), ss.Height + 2), Fade(isDone ? Theme.Green : Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				right = sx - dd - Theme.S(12f);
			}
			int tx = cx + Theme.S(group ? 26f : 24f);
			int tw = Math.Max(10, right - tx - Theme.S(12f));
			string title = group ? r.Text : e.Title;
			Rectangle titleR = new Rectangle(tx, rr.Y + Theme.S(7f), tw, Theme.S(20f));
			TextRenderer.DrawText(g, title, group ? Theme.Title : Theme.Body, titleR, Fade(isDone ? Theme.Dim : Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (!group && e.Done)
			{
				float sp = (_strikeId == e.Id) ? Anim.EaseOut(_strike) : 1f;
				int w = Math.Min(tw, Theme.Measure(e.Title, Theme.Body).Width);
				int ly = titleR.Y + titleR.Height / 2 + Theme.S(1f);
				using (Pen sp2 = new Pen(Fade(Theme.Dim, a), Math.Max(1f, Theme.S(1.2f))))
				{
					g.DrawLine(sp2, tx, ly, tx + (int)(w * sp), ly);
				}
			}
			// meta line: category (in its color) • when • count/note
			int my = rr.Y + Theme.S(29f);
			int mx = tx;
			string catT = group ? "Автонастройка" : CatName(e.Cat);
			Size cs = Theme.Measure(catT, Theme.Tiny);
			TextRenderer.DrawText(g, catT, Theme.Tiny, new Rectangle(mx, my, Math.Min(cs.Width + Theme.S(4f), tw), Theme.S(18f)), Fade(isDone ? Theme.Dim : Theme.Mix(cc, Theme.Text, 0.15f), a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			mx += cs.Width + Theme.S(4f);
			string meta = "  •  " + DayName(e.Time) + ", " + TimeText(e.Time);
			if (group)
			{
				meta += "  •  изменений: " + r.Items.Count + ((done > 0 && done < r.Items.Count) ? (", откачено: " + done) : "");
			}
			else if (e.Note != "")
			{
				meta += "  •  " + e.Note;
			}
			if (mx < tx + tw)
			{
				TextRenderer.DrawText(g, meta, Theme.Tiny, new Rectangle(mx, my, tx + tw - mx, Theme.S(18f)), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			_hits.Insert(0, new HitR { R = bg, Kind = "row", Arg = key });
		}

		private void PaintGroup(Graphics g, Row r, Rectangle rr, float a)
		{
			string gk = "g:" + r.E.Group;
			bool hot = _hover == gk || _hover == "t:" + r.E.Group;
			Rectangle bg = new Rectangle(rr.X - Theme.S(8f), rr.Y + Theme.S(3f), rr.Width + Theme.S(16f), rr.Height - Theme.S(6f));
			using (GraphicsPath p = Theme.Round(bg, Theme.S(10f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb((int)((hot ? 30 : 16) * a), 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
			}
			int ib = Theme.S(34f);
			Rectangle icon = new Rectangle(rr.X + Theme.S(4f), rr.Y + (rr.Height - ib) / 2, ib, ib);
			Theme.IconBox(g, icon, Glyph.Bolt, bright: a > 0.5f, 0f, Theme.IconSmall);
			int undo = 0;
			int done = 0;
			foreach (Entry item in r.Items)
			{
				if (item.Done)
				{
					done++;
				}
				else if (item.Can)
				{
					undo++;
				}
			}
			string btnT = "Откатить всё";
			int bw = Theme.Measure(btnT, Theme.Small).Width + Theme.S(30f);
			int bx = rr.Right - bw - Theme.S(4f);
			int tx = icon.Right + Theme.S(12f);
			int tw = bx - tx - Theme.S(36f);
			TextRenderer.DrawText(g, r.Text, Theme.Title, new Rectangle(tx, rr.Y + Theme.S(11f), tw, Theme.S(20f)), Fade((done == r.Items.Count) ? Theme.Sub : Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			string sub = TimeText(r.E.Time) + "  •  изменений: " + r.Items.Count + ((done > 0) ? ("  •  откачено: " + done) : "");
			if (Compact)
			{
				sub = DayName(r.E.Time) + ", " + sub;
			}
			TextRenderer.DrawText(g, sub, Theme.Tiny, new Rectangle(tx, rr.Y + Theme.S(32f), tw, Theme.S(18f)), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (!Compact)
			{
				// chevron toggles the list of steps
				bool open = _open.Contains(r.E.Group);
				Rectangle cr = new Rectangle(bx - Theme.S(34f), rr.Y + (rr.Height - Theme.S(28f)) / 2, Theme.S(28f), Theme.S(28f));
				using (Pen pen = new Pen(Fade(hot ? Theme.Text : Theme.Sub, a), Math.Max(1.4f, Theme.S(1.6f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
				{
					float cx = cr.X + cr.Width / 2f;
					float cy = cr.Y + cr.Height / 2f;
					float s = Theme.S(4f);
					if (open)
					{
						g.DrawLines(pen, new PointF[3] { new PointF(cx - s, cy + s / 2f), new PointF(cx, cy - s / 2f), new PointF(cx + s, cy + s / 2f) });
					}
					else
					{
						g.DrawLines(pen, new PointF[3] { new PointF(cx - s, cy - s / 2f), new PointF(cx, cy + s / 2f), new PointF(cx + s, cy - s / 2f) });
					}
				}
				_hits.Add(new HitR { R = new Rectangle(rr.X, rr.Y, bx - rr.X - Theme.S(4f), rr.Height), Kind = "toggle", Arg = r.E.Group });
			}
			else
			{
				_hits.Add(new HitR { R = new Rectangle(rr.X, rr.Y, bx - rr.X - Theme.S(4f), rr.Height), Kind = "all" });
			}
			Rectangle br = new Rectangle(bx, rr.Y + (rr.Height - Theme.S(32f)) / 2, bw, Theme.S(32f));
			if (undo > 0)
			{
				Button(g, br, btnT, gk, primary: false, a);
				_hits.Add(new HitR { R = br, Kind = "group", Arg = r.E.Group, Title = r.Text });
			}
			else
			{
				string st = (done > 0) ? "Откачено" : "Только точкой восстановления";
				int sw = Theme.PillW(st, Theme.Tiny);
				Theme.Pill(g, new Rectangle(rr.Right - sw - Theme.S(4f), rr.Y + (rr.Height - Theme.S(22f)) / 2, sw, Theme.S(22f)), st, Theme.Tiny, Fade(Theme.Dim, a), 20);
			}
		}

		private void PaintItem(Graphics g, Row r, Rectangle rr, float a)
		{
			Entry e = r.E;
			string key = "i:" + e.Id;
			bool hot = _hover == key;
			int x = rr.X + (r.Indent ? Theme.S(20f) : Theme.S(4f));
			if (r.Indent)
			{
				// thread line from the group header
				using (Pen pen = new Pen(Fade(Theme.Border, a), Math.Max(1f, Theme.S(1.5f))))
				{
					int lx = rr.X + Theme.S(21f);
					g.DrawLine(pen, lx, rr.Y, lx, r.Last ? (rr.Y + rr.Height / 2) : rr.Bottom);
					g.DrawLine(pen, lx, rr.Y + rr.Height / 2, lx + Theme.S(10f), rr.Y + rr.Height / 2);
				}
				x += Theme.S(14f);
			}
			int ib = Theme.S(30f);
			Rectangle icon = new Rectangle(x, rr.Y + (rr.Height - ib) / 2, ib, ib);
			if (r.Indent)
			{
				int d = Theme.S(8f);
				Rectangle dot = new Rectangle(x + Theme.S(2f), rr.Y + (rr.Height - d) / 2, d, d);
				using (SolidBrush db = new SolidBrush(Fade(e.Done ? Theme.Dim : (e.Can ? Theme.Silver1 : Theme.Silver2), a)))
				{
					g.FillEllipse(db, dot);
				}
				icon = new Rectangle(x, icon.Y, Theme.S(12f), ib);
			}
			else
			{
				Theme.IconBox(g, icon, CatGlyph(e.Cat), bright: false, hot ? 1f : 0f, Theme.IconSmall);
			}
			string btnT = "Откатить";
			int bw = Theme.Measure(btnT, Theme.Small).Width + Theme.S(30f);
			int bx = rr.Right - bw - Theme.S(4f);
			int tx = icon.Right + Theme.S(12f);
			int tw = bx - tx - Theme.S(12f);
			Color tc = e.Done ? Theme.Dim : Theme.Text;
			Rectangle titleR = new Rectangle(tx, rr.Y + Theme.S(8f), tw, Theme.S(20f));
			TextRenderer.DrawText(g, e.Title, Theme.Body, titleR, Fade(tc, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (e.Done)
			{
				float sp = (_strikeId == e.Id) ? Anim.EaseOut(_strike) : 1f;
				int w = Math.Min(tw, Theme.Measure(e.Title, Theme.Body).Width);
				int ly = titleR.Y + titleR.Height / 2 + Theme.S(1f);
				using (Pen sp2 = new Pen(Fade(Theme.Dim, a), Math.Max(1f, Theme.S(1.2f))))
				{
					g.DrawLine(sp2, tx, ly, tx + (int)(w * sp), ly);
				}
			}
			string sub = TimeText(e.Time);
			if (!r.Indent)
			{
				sub = (Compact ? (DayName(e.Time) + ", ") : "") + sub + "  •  " + CatName(e.Cat);
			}
			if (e.Note != "")
			{
				sub = sub + "  •  " + e.Note;
			}
			TextRenderer.DrawText(g, sub, Theme.Tiny, new Rectangle(tx, rr.Y + Theme.S(30f), tw, Theme.S(18f)), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			Rectangle br = new Rectangle(bx, rr.Y + (rr.Height - Theme.S(30f)) / 2, bw, Theme.S(30f));
			if (e.Done)
			{
				string st = "Откачено";
				int sw = Theme.PillW(st, Theme.Tiny);
				Theme.Pill(g, new Rectangle(rr.Right - sw - Theme.S(4f), rr.Y + (rr.Height - Theme.S(22f)) / 2, sw, Theme.S(22f)), st, Theme.Tiny, Fade(Theme.Green, a * 0.8f), 20);
			}
			else if (e.Can)
			{
				Button(g, br, btnT, key, primary: false, a);
				_hits.Add(new HitR { R = br, Kind = "item", Arg = e.Id, Title = e.Title });
			}
			else
			{
				string st2 = "Без отката";
				int sw2 = Theme.PillW(st2, Theme.Tiny);
				Theme.Pill(g, new Rectangle(rr.Right - sw2 - Theme.S(4f), rr.Y + (rr.Height - Theme.S(22f)) / 2, sw2, Theme.S(22f)), st2, Theme.Tiny, Fade(Theme.Dim, a), 14);
			}
			if (!r.Indent && !Compact)
			{
				using (Pen pen2 = new Pen(Color.FromArgb((int)(28 * a), 255, 255, 255)))
				{
					g.DrawLine(pen2, tx, rr.Bottom - 1, rr.Right, rr.Bottom - 1);
				}
			}
		}

		private HitR Hit(Point p)
		{
			for (int i = _hits.Count - 1; i >= 0; i--)
			{
				if (_hits[i].R.Contains(p))
				{
					return _hits[i];
				}
			}
			return null;
		}

		private static string KeyOf(HitR h)
		{
			if (h == null)
			{
				return null;
			}
			switch (h.Kind)
			{
			case "item":
				return "i:" + h.Arg;
			case "group":
				return "g:" + h.Arg;
			case "toggle":
				return "t:" + h.Arg;
			case "row":
				return "row:" + h.Arg;
			default:
				return h.Kind;
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			HitR h = Hit(e.Location);
			string k = KeyOf(h);
			if (k != _hover)
			{
				_hover = k;
				Cursor = (h != null) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hover = null;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			HitR h = Hit(e.Location);
			if (h != null && e.Button == MouseButtons.Left && _busy == null)
			{
				switch (h.Kind)
				{
				case "item":
					UndoItem?.Invoke(h.Arg, h.Title);
					break;
				case "group":
					UndoGroup?.Invoke(h.Arg, h.Title);
					break;
				case "toggle":
					if (!_open.Remove(h.Arg))
					{
						_open.Add(h.Arg);
					}
					RelayoutPage();
					break;
				case "all":
				case "row":
					ShowAll?.Invoke();
					break;
				}
			}
			base.OnMouseClick(e);
		}
	}
}
