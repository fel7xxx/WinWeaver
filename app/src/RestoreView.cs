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
using System.Windows.Forms;

namespace WinWeaver
{
	// Restore points: protection status, space, a timeline of points with their own "roll back" / "delete" buttons
	internal class RestoreView : Card
	{
		public class Item
		{
			public int Seq;
			public string Desc;
			public string Date;
			public DateTime? When;
			public int Type;
			public Rectangle R;
			public Rectangle RollR;
			public Rectangle DelR;
		}

		private readonly List<Item> _items = new List<Item>();

		private readonly List<FButton> _buttons = new List<FButton>();

		private Hashtable _info;

		private bool _loaded;

		private int _hot = -1;

		private string _hotBtn;

		// last known number of points: keeps the card height stable between visits (no jump after loading)
		private static int _lastCount = 3;

		private Rectangle _headR;

		private Rectangle _statsR;

		private int _listTop;

		// "Создать точку" / "Включить защиту" share one slot of a fixed width, so the button row never shifts
		private readonly FButton _create;

		private readonly FButton _wizard;

		private readonly FButton _props;

		private readonly FButton _old;

		private readonly FButton _refresh;

		public event Action CreateClicked;

		public event Action EnableClicked;

		public event Action DeleteOldClicked;

		public event Action<Item> RollbackClicked;

		public event Action<Item> DeleteClicked;

		private int RowH => Theme.S(66f);

		public int Count => _items.Count;

		public RestoreView()
		{
			_create = Btn("Включить защиту", BtnKind.Silver, null, delegate
			{
				if (On)
				{
					CreateClicked?.Invoke();
				}
				else
				{
					EnableClicked?.Invoke();
				}
			});
			_create.Text = "Создать точку";
			_create.Enabled = false;
			_wizard = Btn("Мастер восстановления", BtnKind.Ghost, null, delegate
			{
				Shell.Open(Path.Combine(Shell.WinDir, "System32\\rstrui.exe"));
			});
			_props = Btn("Настройки защиты", BtnKind.Ghost, null, delegate
			{
				Shell.Open(Path.Combine(Shell.WinDir, "System32\\SystemPropertiesProtection.exe"));
			});
			_old = Btn("Удалить старые", BtnKind.Ghost, null, delegate
			{
				DeleteOldClicked?.Invoke();
			});
			_refresh = Btn("Обновить", BtnKind.Ghost, Glyph.Refresh, delegate
			{
				if (RefreshWorker != null && base.Parent is Canvas)
				{
					((Canvas)base.Parent).Page.Refresh(RefreshWorker);
				}
			});
			_old.Enabled = false;
		}

		public Worker RefreshWorker;

		private FButton Btn(string text, BtnKind kind, string glyph, Action click)
		{
			FButton b = new FButton { Text = text, Kind = kind, Font = Theme.Body, Glyph = glyph };
			b.AutoWidth(Theme.S(30f));
			b.Click += delegate
			{
				click();
			};
			_buttons.Add(b);
			base.Controls.Add(b);
			return b;
		}

		private bool On => _info != null && _info["On"] is bool && (bool)_info["On"];

		private static double Dbl(object o)
		{
			if (o == null)
			{
				return 0.0;
			}
			try
			{
				return Convert.ToDouble(o, CultureInfo.InvariantCulture);
			}
			catch
			{
				return 0.0;
			}
		}

		public override object Fetch(IEngine e)
		{
			return new object[2]
			{
				e.Eval("Get-FRestoreInfo"),
				e.Eval("Get-FRestoreList")
			};
		}

		public override void Apply(object v)
		{
			object[] a = v as object[];
			if (a == null)
			{
				return;
			}
			_info = a[0] as Hashtable;
			_items.Clear();
			if (a[1] is object[] arr)
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
						Seq = (h["Seq"] is int) ? ((int)h["Seq"]) : 0,
						Desc = (h["Desc"] ?? "").ToString(),
						Date = (h["Date"] ?? "").ToString(),
						Type = (h["Type"] is int) ? ((int)h["Type"]) : 12
					};
					if (DateTime.TryParse((h["Iso"] ?? "").ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
					{
						it.When = dt;
						it.Date = dt.ToString("dd.MM.yyyy  HH:mm");
					}
					_items.Add(it);
				}
			}
			_items.Sort((Item x, Item y) => y.Seq.CompareTo(x.Seq));
			_loaded = true;
			Skeleton.Track(this, false);
			_lastCount = _items.Count;
			_create.Text = On ? "Создать точку" : "Включить защиту";
			_create.Enabled = true;
			_old.Enabled = _items.Count > 1;
			_create.Invalidate();
			Invalidate();
		}

		// ---------- texts ----------
		private static string Plural(int n, string one, string few, string many)
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

		private static string Ago(DateTime? w)
		{
			if (!w.HasValue)
			{
				return "";
			}
			TimeSpan d = DateTime.Now - w.Value;
			if (d.TotalMinutes < 2.0)
			{
				return "только что";
			}
			if (d.TotalHours < 1.0)
			{
				int m = (int)d.TotalMinutes;
				return m + " " + Plural(m, "минуту", "минуты", "минут") + " назад";
			}
			if (d.TotalHours < 24.0)
			{
				int h = (int)d.TotalHours;
				return h + " " + Plural(h, "час", "часа", "часов") + " назад";
			}
			int days = (int)(DateTime.Now.Date - w.Value.Date).TotalDays;
			if (days <= 1)
			{
				return "вчера";
			}
			if (days < 31)
			{
				return days + " " + Plural(days, "день", "дня", "дней") + " назад";
			}
			int mo = days / 30;
			return mo + " " + Plural(mo, "месяц", "месяца", "месяцев") + " назад";
		}

		private static bool Ours(Item it)
		{
			string d = it.Desc ?? "";
			return d.StartsWith("WinWeaver", StringComparison.OrdinalIgnoreCase);
		}

		private static void Kind(Item it, out string chip, out Color c, out string title)
		{
			string d = it.Desc ?? "";
			title = d;
			if (Ours(it))
			{
				chip = "WinWeaver";
				c = Theme.Silver1;
				title = d.Contains("auto") ? "Автоматическая точка" : "Точка от WinWeaver";
				return;
			}
			string low = d.ToLowerInvariant();
			if (low.Contains("windows update") || low.Contains("установщик модулей") || low.Contains("modules installer") || low.Contains("обновлени"))
			{
				chip = "Обновление Windows";
				c = Color.FromArgb(110, 190, 200);
				return;
			}
			switch (it.Type)
			{
			case 0:
				chip = "Установка программы";
				c = Color.FromArgb(125, 165, 235);
				break;
			case 1:
				chip = "Удаление программы";
				c = Color.FromArgb(125, 165, 235);
				break;
			case 10:
				chip = "Драйвер";
				c = Color.FromArgb(175, 145, 235);
				break;
			case 13:
				chip = "Отменённая операция";
				c = Theme.Dim;
				break;
			default:
				chip = (low.Contains("driver") || low.Contains("драйвер")) ? "Драйвер" : "Изменение настроек";
				c = (chip == "Драйвер") ? Color.FromArgb(175, 145, 235) : Theme.Silver2;
				break;
			}
			if (string.IsNullOrEmpty(title))
			{
				title = "Без описания";
			}
		}

		private static string Size(double b)
		{
			if (b <= 0.0)
			{
				return "0 ГБ";
			}
			double gb = b / 1073741824.0;
			if (gb < 1.0)
			{
				return Math.Round(b / 1048576.0) + " МБ";
			}
			return gb.ToString((gb < 10.0) ? "0.0" : "0", L.Culture) + " ГБ";
		}

		// ---------- layout ----------
		public override int DoLayout(int x, int y, int w)
		{
			int pad = base.Pad;
			_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(64f));
			int cy = _headR.Bottom + Theme.S(16f);
			_statsR = new Rectangle(pad, cy, w - pad * 2, Theme.S(78f));
			cy = _statsR.Bottom + Theme.S(14f);
			int lx = pad;
			int bh = 0;
			foreach (FButton b in _buttons)
			{
				if (lx + b.Width > w - pad && lx > pad)
				{
					lx = pad;
					cy += b.Height + Theme.S(8f);
				}
				b.Location = new Point(lx, cy);
				lx += b.Width + Theme.S(8f);
				bh = b.Height;
			}
			cy += bh + Theme.S(20f);
			_listTop = cy;
			cy += Theme.S(30f);
			int bh2 = Theme.S(32f);
			int dw = Theme.Measure("Удалить", Theme.Small).Width + Theme.S(28f);
			int rw = Theme.Measure("Откатить", Theme.Small).Width + Theme.S(28f);
			foreach (Item it in _items)
			{
				it.R = new Rectangle(pad - Theme.S(8f), cy, w - pad * 2 + Theme.S(16f), RowH);
				int bx = w - pad;
				it.DelR = new Rectangle(bx - dw, cy + (RowH - bh2) / 2, dw, bh2);
				it.RollR = new Rectangle(it.DelR.Left - Theme.S(8f) - rw, it.DelR.Top, rw, bh2);
				cy += RowH;
			}
			if (!_loaded)
			{
				cy += RowH * Math.Max(1, _lastCount);
			}
			else if (_items.Count == 0)
			{
				cy += Theme.S(96f);
			}
			int h = cy + pad - Theme.S(4f);
			SetBounds(x, y, w, h);
			return h;
		}

		// ---------- paint ----------
		private void Tile(Graphics g, Rectangle r)
		{
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
		}

		private void Stat(Graphics g, Rectangle r, string label, string value, string unit, string hint, Color c, float bar = -1f)
		{
			Tile(g, r);
			int barW = Theme.S(3f);
			using (GraphicsPath bp = Theme.Round(new RectangleF(r.X + Theme.S(14f), r.Y + Theme.S(16f), barW, r.Height - Theme.S(32f)), barW / 2f))
			{
				using (SolidBrush b2 = new SolidBrush(c))
				{
					g.FillPath(b2, bp);
				}
			}
			int tx = r.X + Theme.S(28f);
			TextRenderer.DrawText(g, label, Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(10f), r.Right - tx - Theme.S(10f), Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			using (Font big = Theme.F(17f, semi: true))
			{
				Size vs = TextRenderer.MeasureText(g, value, big, System.Drawing.Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
				TextRenderer.DrawText(g, value, big, new Point(tx, r.Y + Theme.S(26f)), Theme.Text, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
				if (!string.IsNullOrEmpty(unit))
				{
					TextRenderer.DrawText(g, unit, Theme.Small, new Rectangle(tx + vs.Width + Theme.S(6f), r.Y + Theme.S(26f), r.Right - tx - vs.Width - Theme.S(14f), vs.Height - Theme.S(3f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Bottom | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				}
			}
			if (bar >= 0f)
			{
				RectangleF br = new RectangleF(tx, r.Bottom - Theme.S(16f), r.Right - tx - Theme.S(14f), Theme.S(5f));
				using (GraphicsPath tp = Theme.Round(br, br.Height / 2f))
				{
					using (SolidBrush tb = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
					{
						g.FillPath(tb, tp);
					}
				}
				float fw = Math.Max(br.Height, br.Width * Math.Min(1f, bar));
				using (GraphicsPath fp = Theme.Round(new RectangleF(br.X, br.Y, fw, br.Height), br.Height / 2f))
				{
					using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(br.X, br.Y, Math.Max(1f, fw), br.Height), Theme.Silver1, Theme.Silver2, 0f))
					{
						g.FillPath(fb, fp);
					}
				}
			}
			else
			{
				TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(tx, r.Bottom - Theme.S(24f), r.Right - tx - Theme.S(10f), Theme.S(16f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
		}

		private void ShieldIcon(Graphics g, Rectangle r)
		{
			Color tint = !_loaded ? Theme.Silver2 : (On ? Theme.Green : Theme.Yellow);
			using (GraphicsPath p = Theme.Round(r, Theme.S(16f)))
			{
				Theme.FillGradient(g, p, r, Theme.Mix(Color.FromArgb(62, 64, 74), tint, 0.18f), Color.FromArgb(30, 30, 36));
				using (Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			float u = Theme.S(1f);
			float cx = r.X + r.Width / 2f;
			float top = r.Y + 15f * u;
			float w = 26f * u;
			float h = 32f * u;
			using (GraphicsPath s = new GraphicsPath())
			{
				s.AddLine(cx, top, cx + w / 2f, top + 5f * u);
				s.AddBezier(cx + w / 2f, top + 5f * u, cx + w / 2f, top + h * 0.62f, cx + w * 0.3f, top + h * 0.85f, cx, top + h);
				s.AddBezier(cx, top + h, cx - w * 0.3f, top + h * 0.85f, cx - w / 2f, top + h * 0.62f, cx - w / 2f, top + 5f * u);
				s.CloseFigure();
				using (SolidBrush sb = new SolidBrush(Color.FromArgb(36, tint)))
				{
					g.FillPath(sb, s);
				}
				using (Pen sp = new Pen(tint, 2f * u))
				{
					sp.LineJoin = LineJoin.Round;
					g.DrawPath(sp, s);
				}
			}
			using (Pen cp = new Pen(tint, 2.2f * u))
			{
				cp.StartCap = LineCap.Round;
				cp.EndCap = LineCap.Round;
				float my = top + h * 0.48f;
				if (!_loaded || On)
				{
					g.DrawLines(cp, new PointF[]
					{
						new PointF(cx - 6f * u, my),
						new PointF(cx - 1.5f * u, my + 4.5f * u),
						new PointF(cx + 6.5f * u, my - 4f * u)
					});
				}
				else
				{
					g.DrawLine(cp, cx, my - 6f * u, cx, my + 2f * u);
					g.FillEllipse(new SolidBrush(tint), cx - 1.4f * u, my + 5f * u, 2.8f * u, 2.8f * u);
				}
			}
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			int pad = base.Pad;
			Rectangle ir = new Rectangle(_headR.X, _headR.Y, Theme.S(64f), Theme.S(64f));
			ShieldIcon(g, ir);
			int tx = ir.Right + Theme.S(16f);
			Item last = (_items.Count > 0) ? _items[0] : null;
			string drive = (_info != null && _info["Drive"] != null) ? _info["Drive"].ToString() : "C:";
			string title = !_loaded ? "Проверяю защиту системы…" : (On ? "Защита системы включена" : "Защита системы выключена");
			string sub = !_loaded ? "" : (On ? ("Диск " + drive + " • откат вернёт систему, программы и драйверы, но не тронет личные файлы") : ("Без неё нельзя создать точку и откатить систему. Включи — это займёт секунду"));
			using (Font tf = Theme.F(15f, semi: true))
			{
				int th = Theme.Measure("Ag", tf).Height;
				int ty = _headR.Y + Theme.S(8f);
				TextRenderer.DrawText(g, title, tf, new Rectangle(tx, ty, _headR.Right - tx, th + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, ty + th + Theme.S(6f), _headR.Right - tx, Theme.S(20f)), On ? Theme.Sub : Theme.Yellow, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			// stats
			int gap = Theme.S(10f);
			int sw = (_statsR.Width - gap * 2) / 3;
			string lastV = "нет";
			string lastU = "";
			string lastH = "создай первую точку";
			Color lastC = Theme.Red;
			if (last != null)
			{
				string ago = Ago(last.When);
				if (ago.Length == 0)
				{
					lastV = last.Date;
				}
				else
				{
					int sp = ago.IndexOf(' ');
					if (char.IsDigit(ago[0]) && sp > 0)
					{
						lastV = ago.Substring(0, sp);
						lastU = ago.Substring(sp + 1);
					}
					else
					{
						lastV = ago;
					}
				}
				lastH = last.Date;
				lastC = (last.When.HasValue && (DateTime.Now - last.When.Value).TotalDays > 7.0) ? Theme.Yellow : Theme.Green;
				if (lastC == Theme.Yellow)
				{
					lastH = "давно — лучше создать новую";
				}
			}
			Stat(g, new Rectangle(_statsR.X, _statsR.Y, sw, _statsR.Height), "ПОСЛЕДНЯЯ ТОЧКА", _loaded ? lastV : "…", lastU, lastH, _loaded ? lastC : Theme.Dim);
			string oldest = (_items.Count > 0) ? ("самая старая — " + _items[_items.Count - 1].Date.Split(' ')[0]) : "пока пусто";
			Stat(g, new Rectangle(_statsR.X + sw + gap, _statsR.Y, sw, _statsR.Height), "ТОЧЕК НА ДИСКЕ", _loaded ? _items.Count.ToString() : "…", _loaded ? Plural(_items.Count, "точка", "точки", "точек") : "", oldest, Theme.Silver1);
			double used = (_info != null) ? Dbl(_info["Used"]) : 0.0;
			double max = (_info != null) ? Dbl(_info["Max"]) : 0.0;
			double cap = (_info != null) ? Dbl(_info["Cap"]) : 0.0;
			bool limited = max > 0.0 && (cap <= 0.0 || max <= cap);
			string unit = limited ? ("из " + Size(max) + ((cap > 0.0) ? (" • " + Math.Round(max * 100.0 / cap) + "% диска") : "")) : "без лимита";
			Stat(g, new Rectangle(_statsR.X + (sw + gap) * 2, _statsR.Y, _statsR.Width - (sw + gap) * 2, _statsR.Height), "МЕСТО ПОД ТОЧКИ", _loaded ? Size(used) : "…", _loaded ? unit : "", "", Theme.Silver2, (_loaded && limited) ? ((float)(used / max)) : (-1f));
			if (!_loaded)
			{
				// skeleton rows of the same height as the real ones
				int sy = _listTop + Theme.S(30f);
				for (int i = 0; i < Math.Max(1, _lastCount); i++)
				{
					Skeleton.Row(g, new Rectangle(pad - Theme.S(8f), sy, base.Width - pad * 2, RowH), this, 12);
					sy += RowH;
				}
				return;
			}
			// timeline
			TextRenderer.DrawText(g, "ХРОНОЛОГИЯ", Theme.Tiny, new Point(pad, _listTop + Theme.S(6f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int hw = TextRenderer.MeasureText(g, "ХРОНОЛОГИЯ", Theme.Tiny, System.Drawing.Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
			string rt = (_items.Count > 0) ? "новые сверху" : "";
			int rw = TextRenderer.MeasureText(g, rt, Theme.Tiny, System.Drawing.Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
			TextRenderer.DrawText(g, rt, Theme.Tiny, new Point(base.Width - pad - rw, _listTop + Theme.S(6f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int lx0 = pad + hw + Theme.S(12f);
			int lx1 = base.Width - pad - rw - Theme.S(12f);
			if (lx1 > lx0)
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(lx0, 0, lx1 - lx0, 1), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(8, 255, 255, 255), 0f))
				{
					g.FillRectangle(lb, lx0, _listTop + Theme.S(13f), lx1 - lx0, 1);
				}
			}
			if (_items.Count == 0)
			{
				Rectangle er = new Rectangle(pad, _listTop + Theme.S(36f), base.Width - pad * 2, Theme.S(80f));
				Tile(g, er);
				TextRenderer.DrawText(g, On ? "Точек восстановления пока нет" : "Точек нет — защита системы выключена", Theme.Title, new Rectangle(er.X, er.Y + Theme.S(16f), er.Width, Theme.S(24f)), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
				TextRenderer.DrawText(g, On ? "Нажми «Создать точку» — это займёт до минуты" : "Нажми «Включить защиту», затем создай первую точку", Theme.Small, new Rectangle(er.X, er.Y + Theme.S(44f), er.Width, Theme.S(20f)), Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
				return;
			}
			int lineX = pad + Theme.S(10f);
			Item first = _items[0];
			Item lastIt = _items[_items.Count - 1];
			using (LinearGradientBrush tl = new LinearGradientBrush(new Rectangle(lineX - 1, first.R.Y, 2, Math.Max(2, lastIt.R.Bottom - first.R.Y)), Color.FromArgb(90, 255, 255, 255), Color.FromArgb(14, 255, 255, 255), 90f))
			{
				g.FillRectangle(tl, lineX - 1, first.R.Y + RowH / 2, 2, Math.Max(0, lastIt.R.Y - first.R.Y));
			}
			for (int i = 0; i < _items.Count; i++)
			{
				Item it = _items[i];
				Rectangle r = it.R;
				if (i == _hot)
				{
					using (GraphicsPath hp = Theme.Round(new Rectangle(r.X + Theme.S(28f), r.Y + Theme.S(3f), r.Width - Theme.S(28f), r.Height - Theme.S(6f)), Theme.S(10f)))
					{
						using (SolidBrush hb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
						{
							g.FillPath(hb, hp);
						}
					}
				}
				Kind(it, out var chip, out var cc, out var t);
				float cy = r.Y + r.Height / 2f;
				float dr = (i == 0) ? Theme.S(6f) : Theme.S(4.5f);
				if (i == 0)
				{
					using (SolidBrush gb = new SolidBrush(Color.FromArgb(40, cc)))
					{
						g.FillEllipse(gb, lineX - dr * 2f, cy - dr * 2f, dr * 4f, dr * 4f);
					}
				}
				using (SolidBrush bg = new SolidBrush(Theme.Card2))
				{
					g.FillEllipse(bg, lineX - dr - 2f, cy - dr - 2f, dr * 2f + 4f, dr * 2f + 4f);
				}
				using (SolidBrush db = new SolidBrush(cc))
				{
					g.FillEllipse(db, lineX - dr, cy - dr, dr * 2f, dr * 2f);
				}
				int x0 = pad + Theme.S(36f);
				int right = it.RollR.Left - Theme.S(14f);
				RowBtn(g, it.RollR, "Откатить", i + ":roll");
				RowBtn(g, it.DelR, "Удалить", i + ":del");
				int ly = r.Y + Theme.S(12f);
				int nw = Math.Min(Theme.Measure(t, Theme.Title).Width + 2, Math.Max(Theme.S(80f), right - x0 - Theme.S(200f)));
				TextRenderer.DrawText(g, t, Theme.Title, new Rectangle(x0, ly, nw, Theme.S(22f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				int cx = x0 + nw + Theme.S(10f);
				int cw = Theme.PillW(chip, Theme.Tiny);
				if (cx + cw <= right)
				{
					Theme.Pill(g, new Rectangle(cx, ly, cw, Theme.S(20f)), chip, Theme.Tiny, cc, 24);
					cx += cw + Theme.S(6f);
				}
				if (i == 0)
				{
					int lw = Theme.PillW("последняя", Theme.Tiny);
					if (cx + lw <= right)
					{
						Theme.Pill(g, new Rectangle(cx, ly, lw, Theme.S(20f)), "последняя", Theme.Tiny, Theme.Green, 22);
					}
				}
				string ago = Ago(it.When);
				string meta = it.Date + ((ago.Length > 0) ? ("  •  " + ago) : "") + "  •  №" + it.Seq + ((Ours(it) && t != it.Desc) ? ("  •  " + it.Desc) : "");
				TextRenderer.DrawText(g, meta, Theme.Small, new Rectangle(x0, ly + Theme.S(25f), right - x0, Theme.S(20f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
		}

		private void RowBtn(Graphics g, Rectangle r, string text, string key)
		{
			bool hot = _hotBtn == key;
			bool danger = key.EndsWith(":del");
			using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), r.Height / 2f))
			{
				Theme.FillGradient(g, p, r, hot ? Theme.Mix(Theme.Control1, danger ? Theme.Red : Color.White, danger ? 0.18f : 0.12f) : Theme.Control1, Theme.Control2);
				using (Pen pen = new Pen(hot ? (danger ? Color.FromArgb(140, Theme.Red) : Theme.BorderHi) : Theme.Border))
				{
					g.DrawPath(pen, p);
				}
			}
			TextRenderer.DrawText(g, text, Theme.Small, r, (hot && danger) ? Theme.Red : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		private string HitBtn(Point pt, out Item it)
		{
			it = null;
			for (int i = 0; i < _items.Count; i++)
			{
				if (_items[i].RollR.Contains(pt))
				{
					it = _items[i];
					return i + ":roll";
				}
				if (_items[i].DelR.Contains(pt))
				{
					it = _items[i];
					return i + ":del";
				}
			}
			return null;
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
			Item dummy;
			string hb = HitBtn(e.Location, out dummy);
			if (h != _hot || hb != _hotBtn)
			{
				_hot = h;
				_hotBtn = hb;
				Cursor = (hb != null) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			Item it;
			string hb = HitBtn(e.Location, out it);
			if (hb != null && e.Button == MouseButtons.Left)
			{
				if (hb.EndsWith(":roll"))
				{
					RollbackClicked?.Invoke(it);
				}
				else
				{
					DeleteClicked?.Invoke(it);
				}
			}
			base.OnMouseClick(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (_hot != -1 || _hotBtn != null)
			{
				_hot = -1;
				_hotBtn = null;
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
