// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WinWeaver
{
	// Floating task toast at the bottom of the content area (replaces the old green status line).
	// While something runs: spinner, task name, "Шаг i из n • 0:12", percent and a progress bar.
	// When it finishes: tone icon, result and how long it took; hides itself after a few seconds.
	internal class StatusBar : Control
	{
		private string _text = "";

		private string _kind = "info";

		private bool _busy;

		private float _ang;

		private float _phase;

		private float _p = -1f;

		private readonly Tween _pt;

		private int _i;

		private int _n;

		private DateTime _busySince = DateTime.MinValue;

		private TimeSpan _took = TimeSpan.Zero;

		private DateTime _shownAt = DateTime.Now;

		private readonly Timer _tick;

		private float _vis;

		private float _visTarget;

		private DateTime _lastTick = DateTime.Now;

		private Rectangle _area;

		private bool _hover;

		private bool _closeHot;

		// ---------- queue of actions ----------
		private class QItem
		{
			public string Title;

			public string Result;

			public string Kind = "info";

			public int State;

			public DateTime Start;

			public TimeSpan Took;

			public int Id;

			public bool Cancelled;
		}

		// user asked to stop a job: (id, title, running now)
		public event Action<int, string, bool> CancelRequested;

		public static bool CanCancel(string title)
		{
			return title == null || !title.StartsWith("Откат системы");
		}

		public void MarkCancelled(int id)
		{
			foreach (QItem q in _queue)
			{
				if (q.Id == id)
				{
					q.Cancelled = true;
				}
			}
			if (_cur != null && _cur.Id == id)
			{
				_cur.Cancelled = true;
				_text = "Отменяю: " + _cur.Title;
			}
			Relayout();
			Redraw();
		}

		private readonly List<KeyValuePair<Rectangle, QItem>> _xHits = new List<KeyValuePair<Rectangle, QItem>>();

		private Rectangle _xHot = Rectangle.Empty;

		private bool CurCancellable => _busy && _cur != null && !_cur.Cancelled && CanCancel(_cur.Title);

		private readonly List<QItem> _queue = new List<QItem>();

		private QItem _cur;

		private readonly List<QItem> _done = new List<QItem>();

		private bool _expanded;

		private string _notice;

		private string _noticeKind = "info";

		private DateTime _noticeUntil = DateTime.MinValue;

		private bool _chipHot;

		public int Pending => _queue.Count;

		// ---------- long background work outside the queue (BitLocker decryption…) ----------
		private bool _bgOn;

		private string _bgTitle = "";

		private string _bgSub;

		private float _bgP = -1f;

		private bool _lastBg;

		public event Action BgClicked;

		public string BgTitle => _bgOn ? _bgTitle : null;

		// shown when nothing else is on the toast: spinner, title, details, progress; click opens its page
		private bool BgMode => !_busy && _bgOn && _notice == null && (string.IsNullOrEmpty(_text) || (DateTime.Now - _shownAt).TotalSeconds > HoldSeconds);

		public void SetBackground(string title, string sub, float p)
		{
			_bgOn = true;
			_bgTitle = title ?? "";
			_bgSub = sub;
			_bgP = p;
			if (!Showing)
			{
				_text = "";
				_kind = "info";
				_expanded = false;
				Reveal();
			}
			_tick.Start();
			Relayout();
			Redraw();
		}

		public void ClearBackground(string kind = null, string text = null)
		{
			if (!_bgOn)
			{
				return;
			}
			_bgOn = false;
			if (text != null)
			{
				Set(kind ?? "ok", text);
			}
			else if (string.IsNullOrEmpty(_text))
			{
				Dismiss();
			}
			Relayout();
			Redraw();
		}

		private static string Clean(string t)
		{
			if (string.IsNullOrEmpty(t))
			{
				return t;
			}
			return Regex.Replace(t, "\\s*\\[\\d+/\\d+\\]", "").Trim();
		}

		private void Notice(string kind, string text, double seconds = 3.0)
		{
			_notice = text;
			_noticeKind = kind;
			_noticeUntil = DateTime.Now.AddSeconds(seconds);
		}

		// a new action: runs at once, or waits behind the current one
		public void JobBegin(int id, string title, bool queued)
		{
			QItem q = new QItem { Title = Clean(title), Id = id };
			if (queued && _cur != null)
			{
				_queue.Add(q);
				Notice("queue", "Добавлено в очередь" + (string.IsNullOrEmpty(q.Title) ? "" : (": " + q.Title)) + "  •  ждут: " + _queue.Count);
				Reveal();
				Relayout();
				Redraw();
				return;
			}
			if (!Showing)
			{
				_done.Clear();
				_expanded = false;
			}
			StartJob(q);
		}

		private void StartJob(QItem q)
		{
			q.State = 1;
			q.Start = DateTime.Now;
			_cur = q;
			// the toast always shows the action that is running now, never the text of the previous one
			_kind = "info";
			_text = string.IsNullOrEmpty(q.Title) ? "Выполняю действие…" : q.Title;
			_p = -1f;
			_i = 0;
			_n = 0;
			_pt.Snap(0f);
			_busySince = DateTime.Now;
			_took = TimeSpan.Zero;
			Reveal();
			Relayout();
			Redraw();
		}

		internal void DemoExpand()
		{
			_expanded = true;
			Relayout();
			Redraw();
		}

		public void JobEnd()
		{
			if (_cur != null)
			{
				QItem d = _cur;
				d.State = 2;
				d.Kind = d.Cancelled ? "cancel" : _kind;
				d.Took = DateTime.Now - d.Start;
				d.Result = d.Cancelled ? "Отменено" : ((_text != d.Title && _text != "Выполняю действие…") ? _text : null);
				if (string.IsNullOrEmpty(d.Title))
				{
					d.Title = d.Result ?? "Действие";
				}
				_done.Insert(0, d);
				if (_done.Count > 12)
				{
					_done.RemoveAt(_done.Count - 1);
				}
				_cur = null;
				if (_queue.Count > 0)
				{
					QItem next = _queue[0];
					_queue.RemoveAt(0);
					StartJob(next);
					if (next.Cancelled)
					{
						_text = "Отменяю: " + next.Title;
					}
					Notice((d.Kind == "err") ? "err" : ((d.Kind == "cancel") ? "queue" : "ok"), ((d.Kind == "err") ? "Ошибка: " : ((d.Kind == "cancel") ? "Отменено: " : "Готово: ")) + d.Title + ((_queue.Count > 0) ? ("  •  ждут: " + _queue.Count) : ""), 2.5);
				}
				else if (d.Cancelled)
				{
					_kind = "warn";
					_text = "Отменено: " + d.Title;
					_shownAt = DateTime.Now;
				}
			}
			Relayout();
			Redraw();
		}

		private const int MaxRows = 7;

		private int RowH => Theme.S(30f);

		private int SecH => Theme.S(24f);

		// grouped list: Сейчас → В фоне → В очереди → Готово (State -1 = section caption)
		private List<QItem> Rows()
		{
			List<QItem> list = new List<QItem>();
			int left = MaxRows;
			if (_cur != null)
			{
				list.Add(new QItem { Title = "Сейчас", State = -1 });
				list.Add(_cur);
				left--;
			}
			if (_bgOn && !Strip)
			{
				list.Add(new QItem { Title = "В фоне", State = -1 });
				list.Add(new QItem { Title = _bgTitle, State = 3 });
				left--;
			}
			if (_queue.Count > 0)
			{
				list.Add(new QItem { Title = "В очереди • " + _queue.Count, State = -1 });
				int n = Math.Max(1, Math.Min(_queue.Count, left - 1));
				for (int i = 0; i < n; i++)
				{
					list.Add(_queue[i]);
				}
				if (_queue.Count > n)
				{
					list.Add(new QItem { Title = "…и ещё " + (_queue.Count - n), State = -2 });
				}
				left -= n;
			}
			if (_done.Count > 0 && left > 0)
			{
				list.Add(new QItem { Title = "Готово", State = -1 });
				int n = Math.Min(_done.Count, left);
				for (int i = 0; i < n; i++)
				{
					list.Add(_done[i]);
				}
			}
			return list;
		}

		private int ListH
		{
			get
			{
				List<QItem> rows = Rows();
				if (rows.Count == 0)
				{
					return 0;
				}
				int h = Theme.S(6f) + Theme.S(8f);
				foreach (QItem q in rows)
				{
					h += (q.State == -1) ? SecH : RowH;
				}
				return h;
			}
		}

		// background work stays visible as its own line under whatever runs on the toast
		private bool Strip => _bgOn && !BgMode;

		private int StripH => Strip ? Theme.S(50f) : 0;

		private Rectangle StripR => new Rectangle(Theme.S(10f), HeadH + Theme.S(4f), Width - Theme.S(20f), StripH - Theme.S(14f));

		private bool HasChip => _queue.Count > 0 || _expanded || _done.Count > 1 || (_bgOn && _done.Count > 0);

		private string ChipText
		{
			get
			{
				if (_queue.Count > 0)
				{
					return "+" + _queue.Count + " в очереди";
				}
				return "Список";
			}
		}

		private Rectangle ChipR
		{
			get
			{
				if (!HasChip)
				{
					return Rectangle.Empty;
				}
				int w = Theme.Measure(ChipText, Theme.Small).Width + Theme.S(34f);
				int h = Theme.S(26f);
				int right;
				if (_busy)
				{
					right = Width - ((_p >= 0f) ? (Theme.Measure("100%", Theme.Title).Width + Theme.S(22f)) : Theme.S(14f)) - (CurCancellable ? Theme.S(34f) : 0);
				}
				else
				{
					right = Width - Theme.S(48f);
				}
				return new Rectangle(right - w, (HeadH - h) / 2, w, h);
			}
		}

		public bool Busy
		{
			get
			{
				return _busy;
			}
			set
			{
				if (value == _busy)
				{
					return;
				}
				_busy = value;
				if (value)
				{
					_busySince = DateTime.Now;
					_took = TimeSpan.Zero;
				}
				else
				{
					_took = (_busySince != DateTime.MinValue) ? (DateTime.Now - _busySince) : TimeSpan.Zero;
					_busySince = DateTime.MinValue;
					_shownAt = DateTime.Now;
					_p = -1f;
				}
				Reveal();
				Redraw();
			}
		}

		public StatusBar()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			Visible = false;
			_pt = new Tween(this, 0f, 0.15f, Redraw).Timed(400);
			_tick = new Timer { Interval = 16 };
			_tick.Tick += delegate
			{
				Step();
			};
		}

		private int Lift => Theme.S(22f);

		// called by the main form: the toast floats at the bottom-centre of this rectangle
		public void Place(Rectangle area)
		{
			_area = area;
			Relayout();
		}

		private int HeadH => (_busy || BgMode || Sub() != "") ? Theme.S(62f) : Theme.S(50f);

		private int ToastH => HeadH + StripH + (_expanded ? ListH : 0);

		private void Relayout()
		{
			if (_area.Width <= 0)
			{
				return;
			}
			int tw = Theme.Measure(BgMode ? _bgTitle : _text, Theme.Body).Width;
			if (Strip)
			{
				tw = Math.Max(tw, Theme.Measure(_bgTitle, Theme.Small).Width + Theme.S(40f));
			}
			if (BgMode && !string.IsNullOrEmpty(_bgSub))
			{
				tw = Math.Max(tw, Theme.Measure(_bgSub, Theme.Tiny).Width);
			}
			int extra = HasChip ? (Theme.Measure(ChipText, Theme.Small).Width + Theme.S(44f)) : 0;
			int w = Math.Min(_area.Width - Theme.S(48f), Math.Max((_expanded || Strip) ? Theme.S(500f) : Theme.S(360f), tw + Theme.S(170f) + extra));
			w = Math.Min(w, Theme.S(600f));
			int h = ToastH;
			float e = Anim.EaseOut(_vis);
			int y = _area.Bottom - Theme.S(20f) - h + (int)((1f - e) * Lift);
			Rectangle b = new Rectangle(_area.X + (_area.Width - w) / 2, y, w, h);
			if (b != Bounds)
			{
				bool resized = b.Size != Size;
				Bounds = b;
				if (resized && !Layered)
				{
					Reshape();
				}
			}
			if (Layered && _win != null && _win.Visible)
			{
				_win.Render();
			}
		}

		// On Windows the toast is a per-pixel-alpha window: real fade and a dark glow around it,
		// so it never melts into the page below. Elsewhere (Mono) it is a plain child control.
		private static readonly bool Layered = Win.IsWindows;

		private ToastWindow _win;

		private bool _showing;

		private bool Showing => _showing;

		private void SetShowing(bool on)
		{
			_showing = on;
			if (!Layered)
			{
				Visible = on;
				if (on)
				{
					BringToFront();
				}
				return;
			}
			Form owner = FindForm();
			if (on)
			{
				if (_win == null || _win.IsDisposed)
				{
					_win = new ToastWindow(this);
					if (owner != null)
					{
						owner.Move += delegate
						{
							if (_showing)
							{
								_win.Render();
							}
						};
					}
				}
				_win.Render();
				if (!_win.Visible)
				{
					_win.ShowOn(owner);
				}
			}
			else if (_win != null && _win.Visible)
			{
				_win.Hide();
			}
		}

		internal float Fade => Anim.EaseOut(_vis);

		internal Rectangle ScreenRect
		{
			get
			{
				Control p = Parent;
				if (p == null)
				{
					return Bounds;
				}
				return new Rectangle(p.PointToScreen(Location), Size);
			}
		}

		internal void PaintTo(Graphics g)
		{
			using (PaintEventArgs pe = new PaintEventArgs(g, new Rectangle(0, 0, Width, Height)))
			{
				OnPaint(pe);
			}
		}

		internal void WinMove(Point p)
		{
			OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, p.X, p.Y, 0));
		}

		internal void WinEnter()
		{
			OnMouseEnter(EventArgs.Empty);
		}

		internal void WinLeave()
		{
			OnMouseLeave(EventArgs.Empty);
		}

		internal void WinClick(Point p, MouseButtons b)
		{
			OnMouseClick(new MouseEventArgs(b, 1, p.X, p.Y, 0));
		}

		private void Redraw()
		{
			if (Layered)
			{
				if (_win != null && _win.Visible)
				{
					_win.Render();
				}
			}
			else
			{
				Invalidate();
			}
		}

		private void Reshape()
		{
			if (Width <= 0 || Height <= 0)
			{
				return;
			}
			using (GraphicsPath path = Theme.Round(new RectangleF(0f, 0f, Width, Height), Theme.S(12f)))
			{
				Region old = Region;
				Region = new Region(path);
				old?.Dispose();
			}
		}

		private bool _held;

		// while the start screen is up the toast stays hidden; released afterwards
		public bool Held
		{
			get
			{
				return _held;
			}
			set
			{
				_held = value;
				if (!value && _bgOn)
				{
					_text = "";
					Reveal();
				}
			}
		}

		private void Reveal()
		{
			if (_held || (string.IsNullOrEmpty(_text) && !_bgOn))
			{
				return;
			}
			_visTarget = 1f;
			_shownAt = DateTime.Now;
			if (!Showing)
			{
				_vis = Anim.Enabled ? 0f : 1f;
				Relayout();
				SetShowing(true);
			}
			_lastTick = DateTime.Now;
			_tick.Start();
		}

		public void Dismiss()
		{
			_visTarget = 0f;
			_lastTick = DateTime.Now;
			_tick.Start();
		}

		private double HoldSeconds
		{
			get
			{
				if (_text == "Готово")
				{
					return 1.6;
				}
				return (_kind == "err") ? 9.0 : ((_kind == "warn") ? 7.0 : 4.5);
			}
		}

		private void Step()
		{
			DateTime now = DateTime.Now;
			float dt = (float)(now - _lastTick).TotalMilliseconds;
			_lastTick = now;
			if (_vis != _visTarget)
			{
				float d = Anim.Enabled ? (dt / 220f) : 1f;
				_vis = (_vis < _visTarget) ? Math.Min(_visTarget, _vis + d) : Math.Max(_visTarget, _vis - d);
				Relayout();
				if (_vis <= 0f && _visTarget <= 0f)
				{
					SetShowing(false);
					_tick.Stop();
					return;
				}
			}
			if (_notice != null && now > _noticeUntil)
			{
				_notice = null;
				Relayout();
				Redraw();
			}
			bool bgNow = BgMode;
			if (bgNow != _lastBg)
			{
				_lastBg = bgNow;
				Relayout();
				Redraw();
			}
			if (_busy || _bgOn)
			{
				_ang = (_ang + dt * 0.4f) % 1800f;
				_phase = (_phase + dt / 1600f) % 1f;
				Redraw();
			}
			else if (_visTarget > 0f && !_hover && !_expanded && !_bgOn && (now - _shownAt).TotalSeconds > HoldSeconds)
			{
				Dismiss();
			}
			else if (_vis == _visTarget && !_busy && !_bgOn && _notice == null)
			{
				// idle and fully shown: slow tick is enough to time the auto-hide
				_tick.Interval = 200;
				return;
			}
			_tick.Interval = 16;
		}

		public void Set(string kind, string text)
		{
			if (_cur != null && _cur.Cancelled)
			{
				return;
			}
			_kind = kind ?? "info";
			_text = text ?? "";
			if (_cur != null && string.IsNullOrEmpty(_cur.Title) && _text != "")
			{
				_cur.Title = Clean(_text);
			}
			// "… [3/18]: step" from long operations drives the progress bar
			Match m = Regex.Match(_text, "\\[(\\d+)/(\\d+)\\]");
			if (m.Success && int.Parse(m.Groups[2].Value) > 0)
			{
				_i = int.Parse(m.Groups[1].Value);
				_n = int.Parse(m.Groups[2].Value);
				float p = Math.Min(1f, (_i - 0.5f) / _n);
				if (_p < 0f)
				{
					_pt.Snap(0f);
				}
				_p = p;
				_pt.Target = p;
				_text = _text.Replace(m.Value, "").Replace("  ", " ").Replace(" :", ":").Trim();
			}
			else if (!_busy)
			{
				_p = -1f;
			}
			Reveal();
			Relayout();
			Redraw();
		}

		private static string Clock(TimeSpan t)
		{
			int s = (int)t.TotalSeconds;
			return (s < 60) ? (s + " с") : ((s / 60) + ":" + (s % 60).ToString("00"));
		}

		private string Sub()
		{
			if (_notice != null && DateTime.Now <= _noticeUntil)
			{
				return _notice;
			}
			if (_busy)
			{
				string el = (_busySince != DateTime.MinValue) ? Clock(DateTime.Now - _busySince) : "";
				if (_p >= 0f && _n > 0)
				{
					return "Шаг " + _i + " из " + _n + "  •  " + el;
				}
				return "Выполняется  •  " + el;
			}
			if (_took.TotalSeconds >= 1.5)
			{
				return ((_kind == "err") ? "Остановлено через " : "Заняло ") + Clock(_took);
			}
			return "";
		}

		private Rectangle CloseR => new Rectangle(Width - Theme.S(40f), (HeadH - Theme.S(26f)) / 2, Theme.S(26f), Theme.S(26f));

		protected override void OnMouseEnter(EventArgs e)
		{
			_hover = true;
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hover = false;
			_closeHot = false;
			_chipHot = false;
			_stripHot = false;
			_xHot = Rectangle.Empty;
			_shownAt = DateTime.Now.AddSeconds(-HoldSeconds + 1.5);
			Redraw();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			bool h = ((!_busy && !BgMode) || CurCancellable) && CloseR.Contains(e.Location);
			bool c = ChipR.Contains(e.Location);
			bool st = Strip && StripR.Contains(e.Location);
			Rectangle xh = Rectangle.Empty;
			if (_expanded)
			{
				foreach (KeyValuePair<Rectangle, QItem> kv in _xHits)
				{
					if (kv.Key.Contains(e.Location))
					{
						xh = kv.Key;
					}
				}
			}
			if (h != _closeHot || c != _chipHot || st != _stripHot || xh != _xHot)
			{
				_closeHot = h;
				_chipHot = c;
				_stripHot = st;
				_xHot = xh;
				Cursor = (h || c || st || !xh.IsEmpty) ? Cursors.Hand : Cursors.Default;
				Redraw();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && CurCancellable && CloseR.Contains(e.Location))
			{
				CancelRequested?.Invoke(_cur.Id, _cur.Title, true);
				base.OnMouseClick(e);
				return;
			}
			if (e.Button == MouseButtons.Left && _expanded)
			{
				foreach (KeyValuePair<Rectangle, QItem> kv in _xHits)
				{
					if (kv.Key.Contains(e.Location))
					{
						CancelRequested?.Invoke(kv.Value.Id, kv.Value.Title, kv.Value == _cur);
						base.OnMouseClick(e);
						return;
					}
				}
			}
			if (e.Button == MouseButtons.Left && ChipR.Contains(e.Location))
			{
				_expanded = !_expanded;
				_shownAt = DateTime.Now;
				Relayout();
				Redraw();
				base.OnMouseClick(e);
				return;
			}
			if (e.Button == MouseButtons.Left && Strip && StripR.Contains(e.Location))
			{
				BgClicked?.Invoke();
				base.OnMouseClick(e);
				return;
			}
			if (BgMode && e.Button == MouseButtons.Left && (!_expanded || e.Y < HeadH))
			{
				BgClicked?.Invoke();
				base.OnMouseClick(e);
				return;
			}
			if (!_busy && e.Button == MouseButtons.Left && (!_expanded || e.Y < HeadH || CloseR.Contains(e.Location)))
			{
				_expanded = false;
				Dismiss();
			}
			base.OnMouseClick(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			bool bg = BgMode;
			bool look = _busy || bg;
			string txt = bg ? _bgTitle : _text;
			float pv = bg ? _bgP : ((_p >= 0f) ? _pt.Value : -1f);
			Theme.Smooth(g);
			Color tone = look ? Theme.Silver1 : Theme.Kind(_kind);
			if (!look && _kind == "info")
			{
				tone = Theme.Silver1;
			}
			Rectangle all = new Rectangle(0, 0, Width, Height);
			using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, -1, Width, Height + 2), Color.FromArgb(44, 44, 52), Color.FromArgb(31, 31, 37), 90f))
			{
				g.FillRectangle(b, all);
			}
			// tinted wash from the left
			using (LinearGradientBrush wb = new LinearGradientBrush(new Rectangle(-1, 0, Width / 2 + 2, Height), Color.FromArgb(28, tone), Color.FromArgb(0, tone), 0f))
			{
				g.FillRectangle(wb, 0, 0, Width / 2, Height);
			}
			using (GraphicsPath bp = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.S(12f)))
			{
				using (Pen pen = new Pen(Color.FromArgb(90, Theme.Mix(tone, Color.White, 0.3f))))
				{
					g.DrawPath(pen, bp);
				}
			}
			int pad = Theme.S(14f);
			int ic = Theme.S(34f);
			int hh = HeadH;
			Rectangle ir = new Rectangle(pad, (hh - ic) / 2, ic, ic);
			if (look)
			{
				using (SolidBrush ib = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
				{
					g.FillEllipse(ib, ir);
				}
				Rectangle sr = Rectangle.Inflate(ir, -Theme.S(8f), -Theme.S(8f));
				using (Pen tp = new Pen(Color.FromArgb(40, 255, 255, 255), Theme.S(2.2f)))
				{
					g.DrawEllipse(tp, sr);
				}
				using (Pen ap = new Pen(Theme.Silver1, Theme.S(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				{
					float sweep = 70f + 50f * (float)Math.Sin(_ang * Math.PI / 180.0);
					g.DrawArc(ap, sr, (_ang * 1.4f) % 360f, sweep + 40f);
				}
			}
			else
			{
				using (SolidBrush ib = new SolidBrush(Color.FromArgb(40, tone)))
				{
					g.FillEllipse(ib, ir);
				}
				using (Pen ip = new Pen(Color.FromArgb(110, tone)))
				{
					g.DrawEllipse(ip, ir);
				}
				DrawMark(g, ir, tone);
			}
			int tx = ir.Right + Theme.S(12f);
			string pct = (look && pv >= 0f) ? ((int)Math.Round(pv * 100f) + "%") : "";
			int right = !look ? Theme.S(48f) : ((pct != "") ? (Theme.Measure("100%", Theme.Title).Width + Theme.S(22f)) : Theme.S(16f));
			bool stop = !bg && CurCancellable;
			if (stop)
			{
				right += Theme.S(34f);
			}
			Rectangle chip = ChipR;
			int tw = (chip.IsEmpty ? (Width - right) : (chip.X - Theme.S(10f))) - tx;
			string sub = (bg ? (_bgSub ?? "") : Sub());
			if (sub != "")
			{
				TextRenderer.DrawText(g, txt, Theme.Body, new Rectangle(tx, hh / 2 - Theme.S(20f), tw, Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.Bottom);
				TextRenderer.DrawText(g, sub, Theme.Tiny, new Rectangle(tx, hh / 2 + Theme.S(2f), tw, Theme.S(16f)), SubColor(sub), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			else
			{
				TextRenderer.DrawText(g, txt, Theme.Body, new Rectangle(tx, 0, tw, hh), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
			if (pct != "")
			{
				TextRenderer.DrawText(g, pct, Theme.Title, new Rectangle(Width - right, 0, right - Theme.S(16f) - (stop ? Theme.S(34f) : 0), hh), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
			}
			if (stop)
			{
				DrawStop(g, CloseR, _closeHot);
			}
			if (!look)
			{
				Rectangle cr = CloseR;
				if (_closeHot)
				{
					using (GraphicsPath cp = Theme.Round(cr, Theme.S(7f)))
					{
						using (SolidBrush chb = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
						{
							g.FillPath(chb, cp);
						}
					}
				}
				float k = Theme.S(4.5f);
				PointF c = new PointF(cr.X + cr.Width / 2f, cr.Y + cr.Height / 2f);
				using (Pen xp = new Pen(_closeHot ? Theme.Text : Theme.Dim, Math.Max(1.4f, Theme.S(1.5f))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				{
					g.DrawLine(xp, c.X - k, c.Y - k, c.X + k, c.Y + k);
					g.DrawLine(xp, c.X - k, c.Y + k, c.X + k, c.Y - k);
				}
			}
			// progress along the bottom edge: real percent, or a running shimmer while the length is unknown
			if (look)
			{
				RectangleF pr = new RectangleF(Theme.S(12f), hh - Theme.S(4f), Width - Theme.S(24f), Theme.S(3f));
				using (SolidBrush tb = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
				{
					g.FillRectangle(tb, pr);
				}
				if (pv >= 0f)
				{
					RectangleF fr = new RectangleF(pr.X, pr.Y, Math.Max(Theme.S(4f), pr.Width * pv), pr.Height);
					using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(fr.X - 1f, fr.Y, fr.Width + 2f, fr.Height), Theme.Silver2, Theme.Silver1, 0f))
					{
						g.FillRectangle(fb, fr);
					}
				}
				else
				{
					float seg = pr.Width * 0.28f;
					float x = pr.X - seg + (pr.Width + seg) * _phase;
					RectangleF sr2 = RectangleF.Intersect(new RectangleF(x, pr.Y, seg, pr.Height), pr);
					if (sr2.Width > 1f)
					{
						using (LinearGradientBrush sb = new LinearGradientBrush(new RectangleF(x - 1f, pr.Y, seg + 2f, pr.Height), Color.Transparent, Color.Transparent, 0f))
						{
							ColorBlend cb = new ColorBlend();
							cb.Positions = new float[3] { 0f, 0.5f, 1f };
							cb.Colors = new Color[3] { Color.FromArgb(0, Theme.Silver1), Theme.Silver1, Color.FromArgb(0, Theme.Silver1) };
							sb.InterpolationColors = cb;
							g.FillRectangle(sb, sr2);
						}
					}
				}
			}
			if (!chip.IsEmpty)
			{
				DrawChip(g, chip);
			}
			if (Strip)
			{
				DrawStrip(g, StripR);
			}
			if (_expanded)
			{
				DrawList(g, hh + StripH);
			}
		}

		private Color SubColor(string sub)
		{
			if (_notice != null && sub == _notice)
			{
				switch (_noticeKind)
				{
				case "queue":
					return Theme.Yellow;
				case "ok":
					return Theme.Green;
				case "err":
					return Theme.Red;
				}
			}
			return Theme.Sub;
		}

		private void DrawChip(Graphics g, Rectangle r)
		{
			Theme.Smooth(g);
			bool q = _queue.Count > 0;
			Color tone = q ? Theme.Yellow : Theme.Silver1;
			using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), r.Height / 2f))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(_chipHot ? 52 : (_expanded ? 40 : 26), tone)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(_chipHot ? 150 : 90, tone)))
				{
					g.DrawPath(pen, p);
				}
			}
			Rectangle tr = new Rectangle(r.X + Theme.S(12f), r.Y, r.Width - Theme.S(30f), r.Height);
			TextRenderer.DrawText(g, ChipText, Theme.Small, tr, Theme.Mix(tone, Color.White, 0.25f), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			// chevron: up when the list is closed (it opens upwards), down when open
			float cx = r.Right - Theme.S(14f);
			float cy = r.Y + r.Height / 2f;
			float k = Theme.S(3.5f);
			using (Pen cp = new Pen(Theme.Mix(tone, Color.White, 0.25f), Math.Max(1.3f, Theme.S(1.4f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				if (_expanded)
				{
					g.DrawLines(cp, new PointF[3] { new PointF(cx - k, cy - k * 0.5f), new PointF(cx, cy + k * 0.5f), new PointF(cx + k, cy - k * 0.5f) });
				}
				else
				{
					g.DrawLines(cp, new PointF[3] { new PointF(cx - k, cy + k * 0.5f), new PointF(cx, cy - k * 0.5f), new PointF(cx + k, cy + k * 0.5f) });
				}
			}
		}

		private bool _stripHot;

		private void DrawStrip(Graphics g, Rectangle r)
		{
			Theme.Smooth(g);
			using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), Theme.S(8f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(_stripHot ? 30 : 18, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			int d = Theme.S(16f);
			Rectangle dot = new Rectangle(r.X + Theme.S(10f), r.Y + (r.Height - d) / 2 - Theme.S(1f), d, d);
			using (Pen tp = new Pen(Color.FromArgb(40, 255, 255, 255), Theme.S(2f)))
			{
				g.DrawEllipse(tp, dot);
			}
			using (Pen ap = new Pen(Theme.Silver1, Theme.S(2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawArc(ap, dot, (_ang * 1.4f) % 360f, 110f);
			}
			string pct = (_bgP >= 0f) ? ((int)Math.Round(_bgP * 100f) + "%") : "";
			int pw = Theme.Measure("в фоне 100%", Theme.Small).Width + Theme.S(4f);
			int tx = dot.Right + Theme.S(10f);
			string t = "В фоне: " + _bgTitle + (string.IsNullOrEmpty(_bgSub) ? "" : ("  •  " + _bgSub));
			TextRenderer.DrawText(g, t, Theme.Small, new Rectangle(tx, r.Y, r.Right - Theme.S(10f) - pw - tx, r.Height - Theme.S(3f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, (pct != "") ? pct : "в фоне", Theme.Small, new Rectangle(r.Right - Theme.S(10f) - pw, r.Y, pw, r.Height - Theme.S(3f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			if (_bgP >= 0f)
			{
				RectangleF pr = new RectangleF(tx, r.Bottom - Theme.S(6f), r.Right - Theme.S(10f) - tx, Theme.S(2f));
				using (SolidBrush tb = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
				{
					g.FillRectangle(tb, pr);
				}
				using (SolidBrush fb = new SolidBrush(Theme.Silver2))
				{
					g.FillRectangle(fb, pr.X, pr.Y, Math.Max(Theme.S(3f), pr.Width * _bgP), pr.Height);
				}
			}
		}

		private void DrawList(Graphics g, int top)
		{
			List<QItem> rows = Rows();
			if (rows.Count == 0)
			{
				return;
			}
			Theme.Smooth(g);
			using (Pen sp = new Pen(Color.FromArgb(22, 255, 255, 255)))
			{
				g.DrawLine(sp, Theme.S(14f), top, Width - Theme.S(14f), top);
			}
			_xHits.Clear();
			int y = top + Theme.S(6f);
			int n = rows.Count;
			int waitNo = 0;
			for (int i = 0; i < n; i++)
			{
				QItem q = rows[i];
				if (q.State == -1)
				{
					TextRenderer.DrawText(g, q.Title.ToUpperInvariant(), Theme.Tiny, new Rectangle(Theme.S(20f), y, Width - Theme.S(40f), SecH), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
					y += SecH;
					continue;
				}
				if (q.State == -2)
				{
					TextRenderer.DrawText(g, q.Title, Theme.Small, new Rectangle(Theme.S(48f), y, Width - Theme.S(60f), RowH), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					y += RowH;
					continue;
				}
				bool last = false;
				Rectangle rr = new Rectangle(Theme.S(10f), y, Width - Theme.S(20f), RowH);
				if (q.State == 1 || q.State == 3)
				{
					using (GraphicsPath hp = Theme.Round(rr, Theme.S(8f)))
					{
						using (SolidBrush hb = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
						{
							g.FillPath(hb, hp);
						}
					}
				}
				int d = Theme.S(18f);
				Rectangle dot = new Rectangle(rr.X + Theme.S(10f), rr.Y + (RowH - d) / 2, d, d);
				string state;
				Color sc;
				if (q.State == 1 || q.State == 3)
				{
					using (Pen tp = new Pen(Color.FromArgb(40, 255, 255, 255), Theme.S(2f)))
					{
						g.DrawEllipse(tp, Rectangle.Inflate(dot, -Theme.S(2f), -Theme.S(2f)));
					}
					using (Pen ap = new Pen(Theme.Silver1, Theme.S(2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
					{
						g.DrawArc(ap, Rectangle.Inflate(dot, -Theme.S(2f), -Theme.S(2f)), (_ang * 1.4f) % 360f, 110f);
					}
					if (q.State == 3)
					{
						state = (_bgP >= 0f) ? ("в фоне " + (int)Math.Round(_bgP * 100f) + "%") : "в фоне";
					}
					else
					{
						state = (_p >= 0f) ? ((int)Math.Round(_pt.Value * 100f) + "%") : "выполняется";
					}
					sc = Theme.Text;
				}
				else if (q.State == 0)
				{
					waitNo++;
					using (SolidBrush wb = new SolidBrush(Color.FromArgb(34, Theme.Yellow)))
					{
						g.FillEllipse(wb, dot);
					}
					using (Pen wp = new Pen(Color.FromArgb(120, Theme.Yellow)))
					{
						g.DrawEllipse(wp, dot);
					}
					TextRenderer.DrawText(g, waitNo.ToString(), Theme.Tiny, dot, Theme.Yellow, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					state = "в очереди";
					sc = Theme.Yellow;
				}
				else
				{
					Color tone = (q.Kind == "err") ? Theme.Red : ((q.Kind == "warn" || q.Kind == "cancel") ? Theme.Yellow : Theme.Green);
					using (SolidBrush db = new SolidBrush(Color.FromArgb(34, tone)))
					{
						g.FillEllipse(db, dot);
					}
					PointF c = new PointF(dot.X + dot.Width / 2f, dot.Y + dot.Height / 2f);
					float k = dot.Width * 0.22f;
					using (Pen mp = new Pen(tone, Math.Max(1.4f, Theme.S(1.6f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
					{
						if (q.Kind == "cancel")
						{
							g.DrawLine(mp, c.X - k, c.Y, c.X + k, c.Y);
						}
						else if (q.Kind == "err")
						{
							g.DrawLine(mp, c.X - k, c.Y - k, c.X + k, c.Y + k);
							g.DrawLine(mp, c.X - k, c.Y + k, c.X + k, c.Y - k);
						}
						else
						{
							g.DrawLines(mp, new PointF[3] { new PointF(c.X - k, c.Y), new PointF(c.X - k * 0.25f, c.Y + k * 0.75f), new PointF(c.X + k * 1.05f, c.Y - k * 0.7f) });
						}
					}
					state = (q.Kind == "cancel") ? "отменено" : ((q.Took.TotalSeconds >= 1.0) ? Clock(q.Took) : "готово");
					sc = Theme.Dim;
				}
				if (q.Cancelled && q.State != 2)
				{
					state = "отменяется";
					sc = Theme.Dim;
				}
				int sw = Theme.Measure("в фоне 100%", Theme.Small).Width + Theme.S(6f);
				if ((q.State == 0 || q.State == 1) && !q.Cancelled && CanCancel(q.Title))
				{
					int xs = Theme.S(22f);
					Rectangle xr = new Rectangle(rr.Right - Theme.S(6f) - xs, rr.Y + (RowH - xs) / 2, xs, xs);
					_xHits.Add(new KeyValuePair<Rectangle, QItem>(xr, q));
					DrawStop(g, xr, xr == _xHot, small: true);
					rr = new Rectangle(rr.X, rr.Y, rr.Width - xs - Theme.S(6f), rr.Height);
				}
				Rectangle tr = new Rectangle(dot.Right + Theme.S(10f), rr.Y, rr.Right - Theme.S(12f) - sw - (dot.Right + Theme.S(10f)), RowH);
				string title = last ? ("…и ещё " + (rows.Count - n + 1)) : (string.IsNullOrEmpty(q.Title) ? "Действие" : q.Title);
				TextRenderer.DrawText(g, title, Theme.Small, tr, (q.State == 2) ? Theme.Sub : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				if (!last)
				{
					TextRenderer.DrawText(g, state, Theme.Small, new Rectangle(rr.Right - Theme.S(12f) - sw, rr.Y, sw, RowH), sc, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				}
				y += RowH;
			}
		}

		// round "stop" button: square inside a circle
		private void DrawStop(Graphics g, Rectangle r, bool hot, bool small = false)
		{
			Theme.Smooth(g);
			using (SolidBrush b = new SolidBrush(Color.FromArgb(hot ? 70 : 26, hot ? Theme.Red : Color.White)))
			{
				g.FillEllipse(b, r);
			}
			using (Pen p = new Pen(Color.FromArgb(hot ? 160 : 60, hot ? Theme.Red : Color.White)))
			{
				g.DrawEllipse(p, r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
			}
			float k = Theme.S(small ? 3.2f : 4f);
			using (GraphicsPath sq = Theme.Round(new RectangleF(r.X + r.Width / 2f - k, r.Y + r.Height / 2f - k, k * 2f, k * 2f), Theme.S(1.5f)))
			{
				using (SolidBrush sb = new SolidBrush(hot ? Theme.Text : Theme.Sub))
				{
					g.FillPath(sb, sq);
				}
			}
		}

		private void DrawMark(Graphics g, Rectangle ir, Color tone)
		{
			PointF c = new PointF(ir.X + ir.Width / 2f, ir.Y + ir.Height / 2f);
			float k = ir.Width * 0.2f;
			using (Pen p = new Pen(Theme.Mix(tone, Color.White, 0.2f), Math.Max(1.8f, Theme.S(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				switch (_kind)
				{
				case "ok":
					g.DrawLines(p, new PointF[3]
					{
						new PointF(c.X - k, c.Y + k * 0.05f),
						new PointF(c.X - k * 0.25f, c.Y + k * 0.75f),
						new PointF(c.X + k * 1.05f, c.Y - k * 0.7f)
					});
					break;
				case "err":
					g.DrawLine(p, c.X - k * 0.8f, c.Y - k * 0.8f, c.X + k * 0.8f, c.Y + k * 0.8f);
					g.DrawLine(p, c.X - k * 0.8f, c.Y + k * 0.8f, c.X + k * 0.8f, c.Y - k * 0.8f);
					break;
				case "warn":
					g.DrawLine(p, c.X, c.Y - k, c.X, c.Y + k * 0.25f);
					g.DrawLine(p, c.X, c.Y + k * 0.95f, c.X, c.Y + k);
					break;
				default:
					g.DrawLine(p, c.X, c.Y - k * 0.1f, c.X, c.Y + k);
					g.DrawLine(p, c.X, c.Y - k * 0.95f, c.X, c.Y - k * 0.9f);
					break;
				}
			}
		}
	}
}
