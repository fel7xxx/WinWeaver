using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinWeaver;

internal class StatusBar : Control
{
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

	private double _lastTick = Anim.Now;

	private Rectangle _area;

	private bool _hover;

	private bool _closeHot;

	private readonly List<KeyValuePair<Rectangle, QItem>> _xHits = new List<KeyValuePair<Rectangle, QItem>>();

	private Rectangle _xHot = Rectangle.Empty;

	private readonly List<QItem> _queue = new List<QItem>();

	private QItem _cur;

	private readonly List<QItem> _done = new List<QItem>();

	private bool _expanded;

	private string _notice;

	private string _noticeKind = "info";

	private DateTime _noticeUntil = DateTime.MinValue;

	private bool _chipHot;

	private bool _bgOn;

	private string _bgTitle = "";

	private string _bgSub;

	private float _bgP = -1f;

	private bool _lastBg;

	private const int MaxRows = 7;

	private static readonly bool Layered = Win.IsWindows;

	private ToastWindow _win;

	private bool _showing;

	private bool _held;

	private bool _stripHot;

	private bool CurCancellable
	{
		get
		{
			if (_busy && _cur != null && !_cur.Cancelled)
			{
				return CanCancel(_cur.Title);
			}
			return false;
		}
	}

	public int Pending => _queue.Count;

	public string BgTitle
	{
		get
		{
			if (!_bgOn)
			{
				return null;
			}
			return _bgTitle;
		}
	}

	private bool BgMode
	{
		get
		{
			if (!_busy && _bgOn && _notice == null)
			{
				if (!string.IsNullOrEmpty(_text))
				{
					return (DateTime.Now - _shownAt).TotalSeconds > HoldSeconds;
				}
				return true;
			}
			return false;
		}
	}

	private int RowH => Theme.S(30f);

	private int SecH => Theme.S(24f);

	private int ListH
	{
		get
		{
			List<QItem> list = Rows();
			if (list.Count == 0)
			{
				return 0;
			}
			int num = Theme.S(6f) + Theme.S(8f);
			foreach (QItem item in list)
			{
				num += ((item.State == -1) ? SecH : RowH);
			}
			return num;
		}
	}

	private bool Strip
	{
		get
		{
			if (_bgOn)
			{
				return !BgMode;
			}
			return false;
		}
	}

	private int StripH
	{
		get
		{
			if (!Strip)
			{
				return 0;
			}
			return Theme.S(50f);
		}
	}

	private Rectangle StripR => new Rectangle(Theme.S(10f), HeadH + Theme.S(4f), base.Width - Theme.S(20f), StripH - Theme.S(14f));

	private bool HasChip
	{
		get
		{
			if (_queue.Count <= 0 && !_expanded && _done.Count <= 1)
			{
				if (_bgOn)
				{
					return _done.Count > 0;
				}
				return false;
			}
			return true;
		}
	}

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
			int num = Theme.Measure(ChipText, Theme.Small).Width + Theme.S(34f);
			int num2 = Theme.S(26f);
			int num3 = ((!_busy) ? (base.Width - Theme.S(48f)) : (base.Width - ((_p >= 0f) ? (Theme.Measure("100%", Theme.Title).Width + Theme.S(22f)) : Theme.S(14f)) - (CurCancellable ? Theme.S(34f) : 0)));
			return new Rectangle(num3 - num, (HeadH - num2) / 2, num, num2);
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
			if (value != _busy)
			{
				_busy = value;
				if (value)
				{
					_busySince = DateTime.Now;
					_took = TimeSpan.Zero;
				}
				else
				{
					_took = ((_busySince != DateTime.MinValue) ? (DateTime.Now - _busySince) : TimeSpan.Zero);
					_busySince = DateTime.MinValue;
					_shownAt = DateTime.Now;
					_p = -1f;
				}
				Reveal();
				Redraw();
			}
		}
	}

	private int Lift => Theme.S(22f);

	private int HeadH
	{
		get
		{
			if (!_busy && !BgMode && !(Sub() != ""))
			{
				return Theme.S(50f);
			}
			return Theme.S(62f);
		}
	}

	private int ToastH => HeadH + StripH + (_expanded ? ListH : 0);

	private bool Showing => _showing;

	internal float Fade => Anim.EaseOut(_vis);

	internal Rectangle ScreenRect
	{
		get
		{
			Control parent = base.Parent;
			if (parent == null)
			{
				return base.Bounds;
			}
			return new Rectangle(parent.PointToScreen(base.Location), base.Size);
		}
	}

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

	private double HoldSeconds
	{
		get
		{
			if (_text == "Готово")
			{
				return 1.6;
			}
			if (!(_kind == "err"))
			{
				if (!(_kind == "warn"))
				{
					return 4.5;
				}
				return 7.0;
			}
			return 9.0;
		}
	}

	private Rectangle CloseR => new Rectangle(base.Width - Theme.S(40f), (HeadH - Theme.S(26f)) / 2, Theme.S(26f), Theme.S(26f));

	public event Action<int, string, bool> CancelRequested;

	public event Action BgClicked;

	public static bool CanCancel(string title)
	{
		if (title != null)
		{
			return !title.StartsWith("Откат системы");
		}
		return true;
	}

	public void MarkCancelled(int id)
	{
		foreach (QItem item in _queue)
		{
			if (item.Id == id)
			{
				item.Cancelled = true;
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
		if (_bgOn)
		{
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

	public void JobBegin(int id, string title, bool queued)
	{
		QItem qItem = new QItem
		{
			Title = Clean(title),
			Id = id
		};
		if (queued && _cur != null)
		{
			_queue.Add(qItem);
			Notice("queue", "Добавлено в очередь" + (string.IsNullOrEmpty(qItem.Title) ? "" : (": " + qItem.Title)) + "  •  ждут: " + _queue.Count);
			Reveal();
			Relayout();
			Redraw();
		}
		else
		{
			if (!Showing)
			{
				_done.Clear();
				_expanded = false;
			}
			StartJob(qItem);
		}
	}

	private void StartJob(QItem q)
	{
		q.State = 1;
		q.Start = DateTime.Now;
		_cur = q;
		_kind = "info";
		_text = (string.IsNullOrEmpty(q.Title) ? "Выполняю действие…" : q.Title);
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
			QItem cur = _cur;
			cur.State = 2;
			cur.Kind = (cur.Cancelled ? "cancel" : _kind);
			cur.Took = DateTime.Now - cur.Start;
			cur.Result = (cur.Cancelled ? "Отменено" : ((_text != cur.Title && _text != "Выполняю действие…") ? _text : null));
			if (string.IsNullOrEmpty(cur.Title))
			{
				cur.Title = cur.Result ?? "Действие";
			}
			_done.Insert(0, cur);
			if (_done.Count > 12)
			{
				_done.RemoveAt(_done.Count - 1);
			}
			_cur = null;
			if (_queue.Count > 0)
			{
				QItem qItem = _queue[0];
				_queue.RemoveAt(0);
				StartJob(qItem);
				if (qItem.Cancelled)
				{
					_text = "Отменяю: " + qItem.Title;
				}
				Notice((cur.Kind == "err") ? "err" : ((cur.Kind == "cancel") ? "queue" : "ok"), ((cur.Kind == "err") ? "Ошибка: " : ((cur.Kind == "cancel") ? "Отменено: " : "Готово: ")) + cur.Title + ((_queue.Count > 0) ? ("  •  ждут: " + _queue.Count) : ""), 2.5);
			}
			else if (cur.Cancelled)
			{
				_kind = "warn";
				_text = "Отменено: " + cur.Title;
				_shownAt = DateTime.Now;
			}
		}
		Relayout();
		Redraw();
	}

	private List<QItem> Rows()
	{
		List<QItem> list = new List<QItem>();
		int num = 7;
		if (_cur != null)
		{
			list.Add(new QItem
			{
				Title = "Сейчас",
				State = -1
			});
			list.Add(_cur);
			num--;
		}
		if (_bgOn && !Strip)
		{
			list.Add(new QItem
			{
				Title = "В фоне",
				State = -1
			});
			list.Add(new QItem
			{
				Title = _bgTitle,
				State = 3
			});
			num--;
		}
		if (_queue.Count > 0)
		{
			list.Add(new QItem
			{
				Title = "В очереди • " + _queue.Count,
				State = -1
			});
			int num2 = Math.Max(1, Math.Min(_queue.Count, num - 1));
			for (int i = 0; i < num2; i++)
			{
				list.Add(_queue[i]);
			}
			if (_queue.Count > num2)
			{
				list.Add(new QItem
				{
					Title = "…и ещё " + (_queue.Count - num2),
					State = -2
				});
			}
			num -= num2;
		}
		if (_done.Count > 0 && num > 0)
		{
			list.Add(new QItem
			{
				Title = "Готово",
				State = -1
			});
			int num3 = Math.Min(_done.Count, num);
			for (int j = 0; j < num3; j++)
			{
				list.Add(_done[j]);
			}
		}
		return list;
	}

	public StatusBar()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.Visible = false;
		_pt = new Tween(this, 0f, 0.15f, Redraw).Timed(400);
		_tick = new Timer
		{
			Interval = 16
		};
		_tick.Tick += delegate
		{
			Step();
		};
	}

	public void Place(Rectangle area)
	{
		_area = area;
		Relayout();
	}

	private void Relayout()
	{
		if (_area.Width <= 0)
		{
			return;
		}
		int num = Theme.Measure(BgMode ? _bgTitle : _text, Theme.Body).Width;
		if (Strip)
		{
			num = Math.Max(num, Theme.Measure(_bgTitle, Theme.Small).Width + Theme.S(40f));
		}
		if (BgMode && !string.IsNullOrEmpty(_bgSub))
		{
			num = Math.Max(num, Theme.Measure(_bgSub, Theme.Tiny).Width);
		}
		int num2 = (HasChip ? (Theme.Measure(ChipText, Theme.Small).Width + Theme.S(44f)) : 0);
		int val = Math.Min(_area.Width - Theme.S(48f), Math.Max((_expanded || Strip) ? Theme.S(500f) : Theme.S(360f), num + Theme.S(170f) + num2));
		val = Math.Min(val, Theme.S(600f));
		int toastH = ToastH;
		float num3 = Anim.EaseOut(_vis);
		int y = _area.Bottom - Theme.S(20f) - toastH + (int)((1f - num3) * (float)Lift);
		Rectangle rectangle = new Rectangle(_area.X + (_area.Width - val) / 2, y, val, toastH);
		if (rectangle != base.Bounds)
		{
			bool num4 = rectangle.Size != base.Size;
			base.Bounds = rectangle;
			if (num4 && !Layered)
			{
				Reshape();
			}
		}
		if (Layered && _win != null && _win.Visible)
		{
			_win.Render();
		}
	}

	private void SetShowing(bool on)
	{
		if (on && _held)
		{
			// Во время экрана загрузки плашки не показываем — они появятся после него.
			return;
		}
		_showing = on;
		if (!Layered)
		{
			base.Visible = on;
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
					owner.Activated += delegate
					{
						if (_showing && _win != null && !_win.IsDisposed && !_win.Visible)
						{
							_win.Render();
							_win.ShowOn(owner);
						}
					};
				}
			}
			_win.Render();
			if (!_win.Visible && (owner == null || Form.ActiveForm != null))
			{
				_win.ShowOn(owner);
			}
		}
		else if (_win != null && _win.Visible)
		{
			_win.Hide();
		}
	}

	internal void PaintTo(Graphics g)
	{
		using PaintEventArgs e = new PaintEventArgs(g, new Rectangle(0, 0, base.Width, base.Height));
		OnPaint(e);
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
		if (base.Width <= 0 || base.Height <= 0)
		{
			return;
		}
		using GraphicsPath path = Theme.Round(new RectangleF(0f, 0f, base.Width, base.Height), Theme.S(12f));
		Region region = base.Region;
		base.Region = new Region(path);
		region?.Dispose();
	}

	private void Reveal()
	{
		if (!_held && (!string.IsNullOrEmpty(_text) || _bgOn))
		{
			_visTarget = 1f;
			_shownAt = DateTime.Now;
			if (!Showing)
			{
				_vis = (Anim.Enabled ? 0f : 1f);
				Relayout();
				SetShowing(on: true);
			}
			_lastTick = Anim.Now;
			_tick.Interval = 16;
			_tick.Start();
		}
	}

	public void Dismiss()
	{
		_visTarget = 0f;
		_lastTick = Anim.Now;
		_tick.Interval = 16;
		_tick.Start();
	}

	private void Step()
	{
		double now = Anim.Now;
		float num = Math.Min(50f, (float)(now - _lastTick));
		_lastTick = now;
		DateTime now2 = DateTime.Now;
		if (_vis != _visTarget)
		{
			float num2 = (Anim.Enabled ? (num / 220f) : 1f);
			_vis = ((_vis < _visTarget) ? Math.Min(_visTarget, _vis + num2) : Math.Max(_visTarget, _vis - num2));
			Relayout();
			if (_vis <= 0f && _visTarget <= 0f)
			{
				SetShowing(on: false);
				_tick.Stop();
				return;
			}
		}
		if (_notice != null && now2 > _noticeUntil)
		{
			_notice = null;
			Relayout();
			Redraw();
		}
		bool bgMode = BgMode;
		if (bgMode != _lastBg)
		{
			_lastBg = bgMode;
			Relayout();
			Redraw();
		}
		if (_busy || _bgOn)
		{
			_ang = (_ang + num * 0.4f) % 1800f;
			_phase = (_phase + num / 1600f) % 1f;
			Redraw();
		}
		else if (_visTarget > 0f && !_hover && !_expanded && !_bgOn && (now2 - _shownAt).TotalSeconds > HoldSeconds)
		{
			Dismiss();
		}
		else if (_vis == _visTarget && !_busy && !_bgOn && _notice == null)
		{
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
		Match match = Regex.Match(_text, "\\[(\\d+)/(\\d+)\\]");
		if (match.Success && int.Parse(match.Groups[2].Value) > 0)
		{
			_i = int.Parse(match.Groups[1].Value);
			_n = int.Parse(match.Groups[2].Value);
			float num = Math.Min(1f, ((float)_i - 0.5f) / (float)_n);
			if (_p < 0f)
			{
				_pt.Snap(0f);
			}
			_p = num;
			_pt.Target = num;
			_text = _text.Replace(match.Value, "").Replace("  ", " ").Replace(" :", ":")
				.Trim();
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
		int num = (int)t.TotalSeconds;
		if (num >= 60)
		{
			return num / 60 + ":" + (num % 60).ToString("00");
		}
		return num + " с";
	}

	private string Sub()
	{
		if (_notice != null && DateTime.Now <= _noticeUntil)
		{
			return _notice;
		}
		if (_busy)
		{
			string text = ((_busySince != DateTime.MinValue) ? Clock(DateTime.Now - _busySince) : "");
			if (_p >= 0f && _n > 0)
			{
				return "Шаг " + _i + " из " + _n + "  •  " + text;
			}
			return "Выполняется  •  " + text;
		}
		if (_took.TotalSeconds >= 1.5)
		{
			return ((_kind == "err") ? "Остановлено через " : "Заняло ") + Clock(_took);
		}
		return "";
	}

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
		_shownAt = DateTime.Now.AddSeconds(0.0 - HoldSeconds + 1.5);
		Redraw();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		bool flag = ((!_busy && !BgMode) || CurCancellable) && CloseR.Contains(e.Location);
		bool flag2 = ChipR.Contains(e.Location);
		bool flag3 = Strip && StripR.Contains(e.Location);
		Rectangle rectangle = Rectangle.Empty;
		if (_expanded)
		{
			foreach (KeyValuePair<Rectangle, QItem> xHit in _xHits)
			{
				if (xHit.Key.Contains(e.Location))
				{
					rectangle = xHit.Key;
				}
			}
		}
		if (flag != _closeHot || flag2 != _chipHot || flag3 != _stripHot || rectangle != _xHot)
		{
			_closeHot = flag;
			_chipHot = flag2;
			_stripHot = flag3;
			_xHot = rectangle;
			Cursor = ((flag || flag2 || flag3 || !rectangle.IsEmpty) ? Cursors.Hand : Cursors.Default);
			Redraw();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && CurCancellable && CloseR.Contains(e.Location))
		{
			this.CancelRequested?.Invoke(_cur.Id, _cur.Title, arg3: true);
			base.OnMouseClick(e);
			return;
		}
		if (e.Button == MouseButtons.Left && _expanded)
		{
			foreach (KeyValuePair<Rectangle, QItem> xHit in _xHits)
			{
				if (xHit.Key.Contains(e.Location))
				{
					this.CancelRequested?.Invoke(xHit.Value.Id, xHit.Value.Title, xHit.Value == _cur);
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
		}
		else if (e.Button == MouseButtons.Left && Strip && StripR.Contains(e.Location))
		{
			this.BgClicked?.Invoke();
			base.OnMouseClick(e);
		}
		else if (BgMode && e.Button == MouseButtons.Left && (!_expanded || e.Y < HeadH))
		{
			this.BgClicked?.Invoke();
			base.OnMouseClick(e);
		}
		else
		{
			if (!_busy && e.Button == MouseButtons.Left && (!_expanded || e.Y < HeadH || CloseR.Contains(e.Location)))
			{
				_expanded = false;
				Dismiss();
			}
			base.OnMouseClick(e);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		bool bgMode = BgMode;
		bool flag = _busy || bgMode;
		string text = (bgMode ? _bgTitle : _text);
		float num = (bgMode ? _bgP : ((_p >= 0f) ? _pt.Value : (-1f)));
		Theme.Smooth(graphics);
		Color color = (flag ? Theme.Silver1 : Theme.Kind(_kind));
		if (!flag && _kind == "info")
		{
			color = Theme.Silver1;
		}
		Rectangle rect = new Rectangle(0, 0, base.Width, base.Height);
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, -1, base.Width, base.Height + 2), Theme.Card1, Theme.Card1, 90f))
		{
			graphics.FillRectangle(brush, rect);
		}
		using (GraphicsPath path = Theme.Round(new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f), Theme.S(12f)))
		{
			using Pen pen = new Pen(Theme.BorderHi, Theme.BW);
			graphics.DrawPath(pen, path);
		}
		int x = Theme.S(14f);
		int num2 = Theme.S(34f);
		int headH = HeadH;
		Rectangle rectangle = new Rectangle(x, (headH - num2) / 2, num2, num2);
		if (flag)
		{
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
			{
				graphics.FillEllipse(brush2, rectangle);
			}
			Rectangle rect2 = Rectangle.Inflate(rectangle, -Theme.S(8f), -Theme.S(8f));
			using (Pen pen2 = new Pen(Color.FromArgb(40, 255, 255, 255), Theme.S(2.2f)))
			{
				graphics.DrawEllipse(pen2, rect2);
			}
			using Pen pen3 = new Pen(Theme.Silver1, Theme.S(2.2f))
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			float num3 = 70f + 50f * (float)Math.Sin((double)_ang * Math.PI / 180.0);
			graphics.DrawArc(pen3, rect2, _ang * 1.4f % 360f, num3 + 40f);
		}
		else
		{
			using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(40, color)))
			{
				graphics.FillEllipse(brush3, rectangle);
			}
			using (Pen pen4 = new Pen(Color.FromArgb(110, color)))
			{
				graphics.DrawEllipse(pen4, rectangle);
			}
			DrawMark(graphics, rectangle, color);
		}
		int num4 = rectangle.Right + Theme.S(12f);
		string text2 = ((flag && num >= 0f) ? ((int)Math.Round(num * 100f) + "%") : "");
		int num5 = ((!flag) ? Theme.S(48f) : ((text2 != "") ? (Theme.Measure("100%", Theme.Title).Width + Theme.S(22f)) : Theme.S(16f)));
		bool flag2 = !bgMode && CurCancellable;
		if (flag2)
		{
			num5 += Theme.S(34f);
		}
		Rectangle chipR = ChipR;
		int width = (chipR.IsEmpty ? (base.Width - num5) : (chipR.X - Theme.S(10f))) - num4;
		string text3 = (bgMode ? (_bgSub ?? "") : Sub());
		if (text3 != "")
		{
			TextRenderer.DrawText(graphics, text, Theme.Body, new Rectangle(num4, headH / 2 - Theme.S(20f), width, Theme.S(20f)), Theme.Text, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(graphics, text3, Theme.Tiny, new Rectangle(num4, headH / 2 + Theme.S(2f), width, Theme.S(16f)), SubColor(text3), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		else
		{
			TextRenderer.DrawText(graphics, text, Theme.Body, new Rectangle(num4, 0, width, headH), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
		if (text2 != "")
		{
			TextRenderer.DrawText(graphics, text2, Theme.Title, new Rectangle(base.Width - num5, 0, num5 - Theme.S(16f) - (flag2 ? Theme.S(34f) : 0), headH), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
		if (flag2)
		{
			DrawStop(graphics, CloseR, _closeHot);
		}
		if (!flag)
		{
			Rectangle closeR = CloseR;
			if (_closeHot)
			{
				using GraphicsPath path2 = Theme.Round(closeR, Theme.S(7f));
				using SolidBrush brush4 = new SolidBrush(Color.FromArgb(26, 255, 255, 255));
				graphics.FillPath(brush4, path2);
			}
			float num6 = Theme.S(4.5f);
			PointF pointF = new PointF((float)closeR.X + (float)closeR.Width / 2f, (float)closeR.Y + (float)closeR.Height / 2f);
			using Pen pen5 = new Pen(_closeHot ? Theme.Text : Theme.Dim, Math.Max(1.4f, Theme.S(1.5f)))
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			graphics.DrawLine(pen5, pointF.X - num6, pointF.Y - num6, pointF.X + num6, pointF.Y + num6);
			graphics.DrawLine(pen5, pointF.X - num6, pointF.Y + num6, pointF.X + num6, pointF.Y - num6);
		}
		if (flag)
		{
			RectangleF rectangleF = new RectangleF(Theme.S(12f), headH - Theme.S(4f), base.Width - Theme.S(24f), Theme.S(3f));
			using (SolidBrush brush5 = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
			{
				graphics.FillRectangle(brush5, rectangleF);
			}
			if (num >= 0f)
			{
				RectangleF rect3 = new RectangleF(rectangleF.X, rectangleF.Y, Math.Max(Theme.S(4f), rectangleF.Width * num), rectangleF.Height);
				using LinearGradientBrush brush6 = new LinearGradientBrush(new RectangleF(rect3.X - 1f, rect3.Y, rect3.Width + 2f, rect3.Height), Theme.Silver2, Theme.Silver1, 0f);
				graphics.FillRectangle(brush6, rect3);
			}
			else
			{
				float num7 = rectangleF.Width * 0.28f;
				float num8 = rectangleF.X - num7 + (rectangleF.Width + num7) * _phase;
				RectangleF rect4 = RectangleF.Intersect(new RectangleF(num8, rectangleF.Y, num7, rectangleF.Height), rectangleF);
				if (rect4.Width > 1f)
				{
					using LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(num8 - 1f, rectangleF.Y, num7 + 2f, rectangleF.Height), Color.Transparent, Color.Transparent, 0f);
					ColorBlend colorBlend = new ColorBlend();
					colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
					colorBlend.Colors = new Color[3]
					{
						Color.FromArgb(0, Theme.Silver1),
						Theme.Silver1,
						Color.FromArgb(0, Theme.Silver1)
					};
					linearGradientBrush.InterpolationColors = colorBlend;
					graphics.FillRectangle(linearGradientBrush, rect4);
				}
			}
		}
		if (!chipR.IsEmpty)
		{
			DrawChip(graphics, chipR);
		}
		if (Strip)
		{
			DrawStrip(graphics, StripR);
		}
		if (_expanded)
		{
			DrawList(graphics, headH + StripH);
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
		Color color = ((_queue.Count > 0) ? Theme.Yellow : Theme.Silver1);
		using (GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, (float)r.Width - 1f, (float)r.Height - 1f), (float)r.Height / 2f))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(_chipHot ? 52 : (_expanded ? 40 : 26), color)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(_chipHot ? 150 : 90, color));
			g.DrawPath(pen, path);
		}
		TextRenderer.DrawText(r: new Rectangle(r.X + Theme.S(12f), r.Y, r.Width - Theme.S(30f), r.Height), dc: g, text: ChipText, font: Theme.Small, fore: Theme.Mix(color, Color.White, 0.25f), flags: TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		float num = r.Right - Theme.S(14f);
		float num2 = (float)r.Y + (float)r.Height / 2f;
		float num3 = Theme.S(3.5f);
		using Pen pen2 = new Pen(Theme.Mix(color, Color.White, 0.25f), Math.Max(1.3f, Theme.S(1.4f)))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		if (_expanded)
		{
			g.DrawLines(pen2, new PointF[3]
			{
				new PointF(num - num3, num2 - num3 * 0.5f),
				new PointF(num, num2 + num3 * 0.5f),
				new PointF(num + num3, num2 - num3 * 0.5f)
			});
		}
		else
		{
			g.DrawLines(pen2, new PointF[3]
			{
				new PointF(num - num3, num2 + num3 * 0.5f),
				new PointF(num, num2 - num3 * 0.5f),
				new PointF(num + num3, num2 + num3 * 0.5f)
			});
		}
	}

	private void DrawStrip(Graphics g, Rectangle r)
	{
		Theme.Smooth(g);
		using (GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, (float)r.Width - 1f, (float)r.Height - 1f), Theme.S(8f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(_stripHot ? 30 : 18, 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(28, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		int num = Theme.S(16f);
		Rectangle rect = new Rectangle(r.X + Theme.S(10f), r.Y + (r.Height - num) / 2 - Theme.S(1f), num, num);
		using (Pen pen2 = new Pen(Color.FromArgb(40, 255, 255, 255), Theme.S(2f)))
		{
			g.DrawEllipse(pen2, rect);
		}
		using (Pen pen3 = new Pen(Theme.Silver1, Theme.S(2f))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		})
		{
			g.DrawArc(pen3, rect, _ang * 1.4f % 360f, 110f);
		}
		string text = ((_bgP >= 0f) ? ((int)Math.Round(_bgP * 100f) + "%") : "");
		int num2 = Theme.Measure("в фоне 100%", Theme.Small).Width + Theme.S(4f);
		int num3 = rect.Right + Theme.S(10f);
		string text2 = "В фоне: " + _bgTitle + (string.IsNullOrEmpty(_bgSub) ? "" : ("  •  " + _bgSub));
		TextRenderer.DrawText(g, text2, Theme.Small, new Rectangle(num3, r.Y, r.Right - Theme.S(10f) - num2 - num3, r.Height - Theme.S(3f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, (text != "") ? text : "в фоне", Theme.Small, new Rectangle(r.Right - Theme.S(10f) - num2, r.Y, num2, r.Height - Theme.S(3f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		if (_bgP >= 0f)
		{
			RectangleF rect2 = new RectangleF(num3, r.Bottom - Theme.S(6f), r.Right - Theme.S(10f) - num3, Theme.S(2f));
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
			{
				g.FillRectangle(brush2, rect2);
			}
			using SolidBrush brush3 = new SolidBrush(Theme.Silver2);
			g.FillRectangle(brush3, rect2.X, rect2.Y, Math.Max(Theme.S(3f), rect2.Width * _bgP), rect2.Height);
		}
	}

	private void DrawList(Graphics g, int top)
	{
		List<QItem> list = Rows();
		if (list.Count == 0)
		{
			return;
		}
		Theme.Smooth(g);
		using (Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255)))
		{
			g.DrawLine(pen, Theme.S(14f), top, base.Width - Theme.S(14f), top);
		}
		_xHits.Clear();
		int num = top + Theme.S(6f);
		int count = list.Count;
		int num2 = 0;
		for (int i = 0; i < count; i++)
		{
			QItem qItem = list[i];
			if (qItem.State == -1)
			{
				TextRenderer.DrawText(g, qItem.Title.ToUpperInvariant(), Theme.Tiny, new Rectangle(Theme.S(20f), num, base.Width - Theme.S(40f), SecH), Theme.Dim, TextFormatFlags.Bottom | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
				num += SecH;
				continue;
			}
			if (qItem.State == -2)
			{
				TextRenderer.DrawText(g, qItem.Title, Theme.Small, new Rectangle(Theme.S(48f), num, base.Width - Theme.S(60f), RowH), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				num += RowH;
				continue;
			}
			bool flag = false;
			Rectangle rectangle = new Rectangle(Theme.S(10f), num, base.Width - Theme.S(20f), RowH);
			if (qItem.State == 1 || qItem.State == 3)
			{
				using GraphicsPath path = Theme.Round(rectangle, Theme.S(8f));
				using SolidBrush brush = new SolidBrush(Color.FromArgb(16, 255, 255, 255));
				g.FillPath(brush, path);
			}
			int num3 = Theme.S(18f);
			Rectangle rectangle2 = new Rectangle(rectangle.X + Theme.S(10f), rectangle.Y + (RowH - num3) / 2, num3, num3);
			string text;
			Color fore;
			if (qItem.State == 1 || qItem.State == 3)
			{
				using (Pen pen2 = new Pen(Color.FromArgb(40, 255, 255, 255), Theme.S(2f)))
				{
					g.DrawEllipse(pen2, Rectangle.Inflate(rectangle2, -Theme.S(2f), -Theme.S(2f)));
				}
				using (Pen pen3 = new Pen(Theme.Silver1, Theme.S(2f))
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round
				})
				{
					g.DrawArc(pen3, Rectangle.Inflate(rectangle2, -Theme.S(2f), -Theme.S(2f)), _ang * 1.4f % 360f, 110f);
				}
				text = ((qItem.State != 3) ? ((_p >= 0f) ? ((int)Math.Round(_pt.Value * 100f) + "%") : "выполняется") : ((_bgP >= 0f) ? ("в фоне " + (int)Math.Round(_bgP * 100f) + "%") : "в фоне"));
				fore = Theme.Text;
			}
			else if (qItem.State == 0)
			{
				num2++;
				using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(34, Theme.Yellow)))
				{
					g.FillEllipse(brush2, rectangle2);
				}
				using (Pen pen4 = new Pen(Color.FromArgb(120, Theme.Yellow)))
				{
					g.DrawEllipse(pen4, rectangle2);
				}
				TextRenderer.DrawText(g, num2.ToString(), Theme.Tiny, rectangle2, Theme.Yellow, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				text = "в очереди";
				fore = Theme.Yellow;
			}
			else
			{
				Color color = ((qItem.Kind == "err") ? Theme.Red : ((qItem.Kind == "warn" || qItem.Kind == "cancel") ? Theme.Yellow : Theme.Green));
				using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(34, color)))
				{
					g.FillEllipse(brush3, rectangle2);
				}
				PointF pointF = new PointF((float)rectangle2.X + (float)rectangle2.Width / 2f, (float)rectangle2.Y + (float)rectangle2.Height / 2f);
				float num4 = (float)rectangle2.Width * 0.22f;
				using (Pen pen5 = new Pen(color, Math.Max(1.4f, Theme.S(1.6f)))
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				})
				{
					if (qItem.Kind == "cancel")
					{
						g.DrawLine(pen5, pointF.X - num4, pointF.Y, pointF.X + num4, pointF.Y);
					}
					else if (qItem.Kind == "err")
					{
						g.DrawLine(pen5, pointF.X - num4, pointF.Y - num4, pointF.X + num4, pointF.Y + num4);
						g.DrawLine(pen5, pointF.X - num4, pointF.Y + num4, pointF.X + num4, pointF.Y - num4);
					}
					else
					{
						g.DrawLines(pen5, new PointF[3]
						{
							new PointF(pointF.X - num4, pointF.Y),
							new PointF(pointF.X - num4 * 0.25f, pointF.Y + num4 * 0.75f),
							new PointF(pointF.X + num4 * 1.05f, pointF.Y - num4 * 0.7f)
						});
					}
				}
				text = ((qItem.Kind == "cancel") ? "отменено" : ((qItem.Took.TotalSeconds >= 1.0) ? Clock(qItem.Took) : "готово"));
				fore = Theme.Dim;
			}
			if (qItem.Cancelled && qItem.State != 2)
			{
				text = "отменяется";
				fore = Theme.Dim;
			}
			int num5 = Theme.Measure("в фоне 100%", Theme.Small).Width + Theme.S(6f);
			if ((qItem.State == 0 || qItem.State == 1) && !qItem.Cancelled && CanCancel(qItem.Title))
			{
				int num6 = Theme.S(22f);
				Rectangle rectangle3 = new Rectangle(rectangle.Right - Theme.S(6f) - num6, rectangle.Y + (RowH - num6) / 2, num6, num6);
				_xHits.Add(new KeyValuePair<Rectangle, QItem>(rectangle3, qItem));
				DrawStop(g, rectangle3, rectangle3 == _xHot, small: true);
				rectangle = new Rectangle(rectangle.X, rectangle.Y, rectangle.Width - num6 - Theme.S(6f), rectangle.Height);
			}
			Rectangle r = new Rectangle(rectangle2.Right + Theme.S(10f), rectangle.Y, rectangle.Right - Theme.S(12f) - num5 - (rectangle2.Right + Theme.S(10f)), RowH);
			string text2 = (flag ? ("…и ещё " + (list.Count - count + 1)) : (string.IsNullOrEmpty(qItem.Title) ? "Действие" : qItem.Title));
			TextRenderer.DrawText(g, text2, Theme.Small, r, (qItem.State == 2) ? Theme.Sub : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			if (!flag)
			{
				TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(rectangle.Right - Theme.S(12f) - num5, rectangle.Y, num5, RowH), fore, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
			num += RowH;
		}
	}

	private void DrawStop(Graphics g, Rectangle r, bool hot, bool small = false)
	{
		Theme.Smooth(g);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(hot ? 70 : 26, hot ? Theme.Red : Color.White)))
		{
			g.FillEllipse(brush, r);
		}
		using (Pen pen = new Pen(Color.FromArgb(hot ? 160 : 60, hot ? Theme.Red : Color.White)))
		{
			g.DrawEllipse(pen, (float)r.X + 0.5f, (float)r.Y + 0.5f, (float)r.Width - 1f, (float)r.Height - 1f);
		}
		float num = Theme.S(small ? 3.2f : 4f);
		using GraphicsPath path = Theme.Round(new RectangleF((float)r.X + (float)r.Width / 2f - num, (float)r.Y + (float)r.Height / 2f - num, num * 2f, num * 2f), Theme.S(1.5f));
		using SolidBrush brush2 = new SolidBrush(hot ? Theme.Text : Theme.Sub);
		g.FillPath(brush2, path);
	}

	private void DrawMark(Graphics g, Rectangle ir, Color tone)
	{
		PointF pointF = new PointF((float)ir.X + (float)ir.Width / 2f, (float)ir.Y + (float)ir.Height / 2f);
		float num = (float)ir.Width * 0.2f;
		using Pen pen = new Pen(Theme.Mix(tone, Color.White, 0.2f), Math.Max(1.8f, Theme.S(2f)))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		switch (_kind)
		{
		case "ok":
			g.DrawLines(pen, new PointF[3]
			{
				new PointF(pointF.X - num, pointF.Y + num * 0.05f),
				new PointF(pointF.X - num * 0.25f, pointF.Y + num * 0.75f),
				new PointF(pointF.X + num * 1.05f, pointF.Y - num * 0.7f)
			});
			break;
		case "err":
			g.DrawLine(pen, pointF.X - num * 0.8f, pointF.Y - num * 0.8f, pointF.X + num * 0.8f, pointF.Y + num * 0.8f);
			g.DrawLine(pen, pointF.X - num * 0.8f, pointF.Y + num * 0.8f, pointF.X + num * 0.8f, pointF.Y - num * 0.8f);
			break;
		case "warn":
			g.DrawLine(pen, pointF.X, pointF.Y - num, pointF.X, pointF.Y + num * 0.25f);
			g.DrawLine(pen, pointF.X, pointF.Y + num * 0.95f, pointF.X, pointF.Y + num);
			break;
		default:
			g.DrawLine(pen, pointF.X, pointF.Y - num * 0.1f, pointF.X, pointF.Y + num);
			g.DrawLine(pen, pointF.X, pointF.Y - num * 0.95f, pointF.X, pointF.Y - num * 0.9f);
			break;
		}
	}
}
