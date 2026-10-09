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
using System.Globalization;
using System.Windows.Forms;

namespace WinWeaver
{
	// update pause: current state with a timeline and one-click durations ("1 неделя — до 16 окт")
	internal class UpdPauseView : Card
	{
		// 0 = "forever" (until the end of 2077), -2 = own duration (dialog)
		private const int Forever = 0;

		private const int Custom = -2;

		private static readonly int[] Days = new int[8] { 7, 14, 35, 91, 182, 365, Forever, Custom };

		private static readonly string[] Names = new string[8] { "1 неделя", "2 недели", "5 недель", "3 месяца", "Полгода", "1 год", "Навсегда", "Свой срок…" };

		private const int N = 8;

		public static int ForeverDays => Math.Max(1, (int)Math.Floor((new DateTime(2077, 12, 31, 0, 0, 0) - DateTime.Now).TotalDays));

		private static int RealDays(int d)
		{
			return (d == Forever) ? ForeverDays : d;
		}

		public event Action CustomClicked;

		private static CultureInfo Ru => L.Culture;

		private readonly Rectangle[] _chipR = new Rectangle[8];

		private readonly FButton _resume;

		private bool _loaded;

		private bool _paused;

		private DateTime _start;

		private DateTime _until;

		private DateTime _now = DateTime.Now;

		private int _hot = -1;

		private int _pending = -1;

		private Rectangle _headR;

		private Rectangle _barR;

		private Rectangle _noteR;

		private readonly Tween _fill;

		private readonly Tween _appear;

		public bool Busy;

		public event Action<int> PauseChosen;

		public event Action ResumeClicked;

		public UpdPauseView()
		{
			_fill = new Tween(this, 0f, 0.16f).Timed(800);
			_appear = new Tween(this, 0f, 0.14f);
			_resume = new FButton { Text = "Возобновить", Kind = BtnKind.Silver, Font = Theme.Body, Glyph = Glyph.Refresh };
			_resume.AutoWidth(Theme.S(30f));
			_resume.Visible = false;
			_resume.Click += delegate
			{
				if (!Busy)
				{
					ResumeClicked?.Invoke();
				}
			};
			Controls.Add(_resume);
		}

		public void Choose(int days)
		{
			if (!Busy && days > 0)
			{
				PauseChosen?.Invoke(days);
			}
		}

		public void SetPending(int days)
		{
			_pending = days;
			Invalidate();
		}

		private static DateTime ParseD(object o, DateTime def)
		{
			DateTime d;
			if (o != null && DateTime.TryParse(o.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
			{
				return d;
			}
			return def;
		}

		public override object Fetch(IEngine e)
		{
			return e.Eval("Get-FUpdPause");
		}

		public override void Apply(object v)
		{
			Hashtable h = v as Hashtable;
			if (h == null)
			{
				return;
			}
			_now = ParseD(h["Now"], DateTime.Now);
			_paused = h["Paused"] is bool && (bool)h["Paused"];
			_start = ParseD(h["Start"], _now);
			_until = ParseD(h["Until"], _now);
			_pending = -1;
			bool wasLoaded = _loaded;
			_loaded = true;
			_resume.Visible = _paused;
			float f = 0f;
			if (_paused)
			{
				double total = Math.Max(1.0, (_until - _start).TotalMinutes);
				f = (float)Math.Max(0.02, Math.Min(1.0, (_until - _now).TotalMinutes / total));
			}
			if (!wasLoaded)
			{
				_fill.Snap(0f);
				_appear.Target = 1f;
			}
			_fill.Target = f;
			LayoutAgain();
		}

		private void LayoutAgain()
		{
			if (Parent is Canvas)
			{
				((Canvas)Parent).Page.RelayoutSoon();
			}
			Invalidate();
		}

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Pad;
			_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(48f));
			_resume.Location = new Point(w - pad - _resume.Width, _headR.Y + (_headR.Height - _resume.Height) / 2);
			int cy = _headR.Bottom + Theme.S(18f);
			_barR = new Rectangle(pad, cy, w - pad * 2, Theme.S(58f));
			cy = _barR.Bottom + Theme.S(16f);
			int gap = Theme.S(8f);
			int cols = (w - pad * 2 >= Theme.S(960f)) ? 8 : 4;
			int cw = (w - pad * 2 - gap * (cols - 1)) / cols;
			int ch = Theme.S(56f);
			for (int i = 0; i < N; i++)
			{
				int c = i % cols;
				int r = i / cols;
				_chipR[i] = new Rectangle(pad + c * (cw + gap), cy + r * (ch + gap), (c == cols - 1) ? (w - pad * 2 - (cw + gap) * (cols - 1)) : cw, ch);
			}
			cy = _chipR[N - 1].Bottom + Theme.S(12f);
			_noteR = new Rectangle(pad, cy, w - pad * 2, Theme.S(20f));
			int h = _noteR.Bottom + pad - Theme.S(4f);
			SetBounds(x, y, w, h);
			return h;
		}

		private bool IsForever => _paused && _until.Year >= 2077;

		// which chip matches the current pause (by total length), Custom when none
		private int CurrentChip()
		{
			if (!_paused)
			{
				return -1;
			}
			int idx = CurrentChipIdx((_until - _start).TotalDays);
			return idx;
		}

		private int CurrentChipFor(int days)
		{
			int i = CurrentChipIdx(days);
			return (i >= 0) ? Days[i] : Custom;
		}

		private int CurrentChipIdx(double total)
		{
			if (_paused && _until.Year >= 2077 && total == (_until - _start).TotalDays)
			{
				return Array.IndexOf(Days, Forever);
			}
			for (int i = 0; i < N; i++)
			{
				if (Days[i] > 0 && Math.Abs(total - Days[i]) < 0.6)
				{
					return i;
				}
			}
			if (total >= ForeverDays - 1)
			{
				return Array.IndexOf(Days, Forever);
			}
			return Array.IndexOf(Days, Custom);
		}

		private static string Left(TimeSpan t)
		{
			if (t.TotalHours < 1)
			{
				return "меньше часа";
			}
			if (t.TotalDays < 1)
			{
				return (int)t.TotalHours + " ч";
			}
			int d = (int)t.TotalDays;
			int hr = t.Hours;
			if (d >= 730)
			{
				int y = d / 365;
				return "≈ " + y + " " + InstalledView.Plural(y, "год", "года", "лет");
			}
			if (d >= 62)
			{
				int m = (int)Math.Round(d / 30.44);
				return "≈ " + m + " " + InstalledView.Plural(m, "месяц", "месяца", "месяцев");
			}
			return d + " " + InstalledView.Plural(d, "день", "дня", "дней") + ((hr > 0 && d < 7) ? (" " + hr + " ч") : "");
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			int ic = Theme.S(44f);
			Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - ic) / 2, ic, ic), Glyph.Timer, _paused);
			int tx = _headR.X + ic + Theme.S(14f);
			int tr = (_paused ? _resume.Left : _headR.Right) - Theme.S(12f);
			string title = !_loaded ? "Пауза обновлений" : (_paused ? "Обновления на паузе" : "Пауза обновлений");
			string sub = !_loaded ? "Проверяю…" : (_paused ? (IsForever ? "Обновления выключены «навсегда» — до 2077 года" : ("Ничего не скачивается до " + _until.ToString((_until.Year != _now.Year) ? "d MMMM yyyy" : "dddd, d MMMM", Ru) + ", " + _until.ToString("HH:mm"))) : "Обновления приходят как обычно. Выбери срок ниже — пауза включится сразу");
			int th = Theme.Measure("Ag", Theme.Title).Height;
			int ty = _headR.Y + (_headR.Height - th - Theme.S(22f)) / 2;
			TextRenderer.DrawText(g, title, Theme.Title, new Rectangle(tx, ty, tr - tx, th + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, ty + th + Theme.S(4f), tr - tx, Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			// timeline
			Rectangle br = _barR;
			using (GraphicsPath p = Theme.Round(br, Theme.S(12f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			int ip = Theme.S(16f);
			Rectangle track = new Rectangle(br.X + ip, br.Bottom - Theme.S(18f), br.Width - ip * 2, Theme.S(6f));
			using (GraphicsPath tp = Theme.Round(track, track.Height / 2f))
			{
				using (SolidBrush tb = new SolidBrush(Color.FromArgb(34, 255, 255, 255)))
				{
					g.FillPath(tb, tp);
				}
			}
			float fv = _fill.Value;
			if (fv > 0.005f)
			{
				RectangleF fr = new RectangleF(track.X, track.Y, Math.Max(track.Height, track.Width * fv), track.Height);
				using (GraphicsPath fp = Theme.Round(fr, track.Height / 2f))
				{
					Theme.FillGradient(g, fp, fr, Theme.Yellow, Theme.Mix(Theme.Yellow, Theme.Silver1, 0.5f), 0f);
				}
			}
			int ly = br.Y + Theme.S(10f);
			if (!_loaded)
			{
				TextRenderer.DrawText(g, "…", Theme.Body, new Point(track.X, ly), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			else if (_paused)
			{
				TimeSpan left = _until - _now;
				using (Font big = Theme.F(12.5f, semi: true))
				{
					string lt = IsForever ? "Пауза до 2077 года" : ("Осталось " + Left(left));
					TextRenderer.DrawText(g, lt, big, new Point(track.X, ly), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				}
				string fmt = (_until.Year != _start.Year) ? "d MMM yyyy" : "d MMM";
				string rng = "с " + _start.ToString(fmt, Ru) + "  →  до " + _until.ToString(fmt, Ru);
				TextRenderer.DrawText(g, rng, Theme.Small, new Rectangle(track.X, ly, track.Width, Theme.S(22f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
			}
			else
			{
				TextRenderer.DrawText(g, "Пауза не стоит", Theme.Body, new Point(track.X, ly), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				TextRenderer.DrawText(g, "от недели до «навсегда»", Theme.Small, new Rectangle(track.X, ly, track.Width, Theme.S(22f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
			}
			// chips
			int curIdx = CurrentChip();
			for (int i = 0; i < N; i++)
			{
				Rectangle r = _chipR[i];
				int dd = RealDays(Days[i]);
				DateTime end = DateTime.Now.AddDays(Math.Max(0, dd));
				bool cur = i == curIdx;
				bool pend = _pending >= 0 && ((Days[i] == Custom) ? (CurrentChipFor(_pending) == Custom) : (RealDays(Days[i]) == _pending));
				bool hot = i == _hot && !Busy;
				using (GraphicsPath p = Theme.Round(r, Theme.S(10f)))
				{
					Color fill = cur || pend ? Color.FromArgb(40, Theme.Yellow) : (hot ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(12, 255, 255, 255));
					using (SolidBrush b = new SolidBrush(fill))
					{
						g.FillPath(b, p);
					}
					using (Pen pen = new Pen(cur || pend ? Color.FromArgb(150, Theme.Yellow) : (hot ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(34, 255, 255, 255))))
					{
						g.DrawPath(pen, p);
					}
				}
				Color tc = Busy && !pend ? Theme.Dim : Theme.Text;
				TextRenderer.DrawText(g, Names[i], Theme.Body, new Rectangle(r.X, r.Y + Theme.S(9f), r.Width, Theme.S(20f)), tc, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
				string sub2;
				if (pend)
				{
					sub2 = "включаю…";
				}
				else if (Days[i] == Custom)
				{
					sub2 = cur ? ("сейчас • до " + _until.ToString("d MMM yyyy", Ru)) : "любой срок";
				}
				else if (cur)
				{
					sub2 = "сейчас";
				}
				else if (Days[i] == Forever)
				{
					sub2 = "до конца 2077 года";
				}
				else
				{
					sub2 = "до " + end.ToString((end.Year != DateTime.Now.Year) ? "d MMM yyyy" : "d MMM", Ru);
				}
				TextRenderer.DrawText(g, sub2, Theme.Tiny, new Rectangle(r.X, r.Y + Theme.S(31f), r.Width, Theme.S(18f)), cur || pend ? Theme.Yellow : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
			}
			TextRenderer.DrawText(g, "Пауза останавливает и исправления безопасности. Когда срок выйдет, Windows сама скачает всё, что накопилось.", Theme.Tiny, _noteR, Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			for (int i = 0; i < N; i++)
			{
				if (_chipR[i].Contains(e.Location))
				{
					h = i;
				}
			}
			Cursor = (h >= 0 && !Busy) ? Cursors.Hand : Cursors.Default;
			if (h != _hot)
			{
				_hot = h;
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
			base.OnMouseClick(e);
			if (Busy || e.Button != MouseButtons.Left)
			{
				return;
			}
			for (int i = 0; i < N; i++)
			{
				if (_chipR[i].Contains(e.Location))
				{
					if (Days[i] == Custom)
					{
						CustomClicked?.Invoke();
					}
					else
					{
						PauseChosen?.Invoke(RealDays(Days[i]));
					}
					return;
				}
			}
		}
	}
}
