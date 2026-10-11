using System;
using System.Collections.Generic;
using AI = Avalonia.Input;

namespace WinWeaver;

/// <summary>
/// Палитра команд (Ctrl+K): поиск по разделам и всем параметрам, как в HTML-превью.
/// Затемнение и окно появляются с плавным fade/slide, стрелки выбирают пункт, Enter — переход.
/// </summary>
internal sealed class CommandPalette : Control
{
	public sealed class Item
	{
		public string Key;

		public string Text;

		public string Sub;

		public Action Run;
	}

	private readonly TextBox _box;

	private readonly Tween _show;

	private readonly Tween _selY;

	private readonly List<Item> _all = new List<Item>();

	private List<Item> _items = new List<Item>();

	private int _sel;

	private int _hot = -1;

	private int _scroll;

	private bool _closing;

	private readonly Font _fItem = Theme.F(9.5f);

	private readonly Font _fSub = Theme.F(8f);

	private readonly Font _fKey = new Font("JetBrains Mono", 7.5f);

	private readonly Font _fInput = Theme.F(11.5f);

	public Func<List<Item>> Source;

	private int BoxW => Math.Min(Theme.S(620f), base.Width - Theme.S(60f));

	private int RowH => Theme.S(40f);

	private int InputH => Theme.S(56f);

	private int MaxRows => Math.Max(3, Math.Min(9, (base.Height - Theme.S(220f)) / RowH));

	private Rectangle BoxR
	{
		get
		{
			int n = Math.Max(1, Math.Min(MaxRows, _items.Count));
			int h = InputH + Theme.S(12f) + n * RowH;
			int y = (int)((float)base.Height * 0.12f) + (int)(Theme.S(10f) * (1f - _show.Value));
			return new Rectangle((base.Width - BoxW) / 2, y, BoxW, h);
		}
	}

	public CommandPalette()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Visible = false;
		_show = new Tween(this, 0f).Timed(180).WithEase(Anim.EaseOut);
		_selY = new Tween(this, 0f, 0.35f);
		_box = new TextBox
		{
			BorderStyle = BorderStyle.None,
			BackColor = Color.FromArgb(21, 21, 24),
			ForeColor = Theme.Text,
			Font = _fInput,
			MaxLength = 80,
			PlaceholderText = "Найти настройку, раздел или команду…"
		};
		_box.TextChanged += delegate
		{
			Filter();
		};
		_box.Box.AddHandler(AI.InputElement.KeyDownEvent, OnBoxKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
		base.Controls.Add(_box);
	}

	private void OnBoxKey(object sender, AI.KeyEventArgs e)
	{
		switch (e.Key)
		{
		case AI.Key.Down:
			MoveSel(1);
			e.Handled = true;
			break;
		case AI.Key.Up:
			MoveSel(-1);
			e.Handled = true;
			break;
		case AI.Key.PageDown:
			MoveSel(MaxRows);
			e.Handled = true;
			break;
		case AI.Key.PageUp:
			MoveSel(-MaxRows);
			e.Handled = true;
			break;
		case AI.Key.Enter:
			RunSel();
			e.Handled = true;
			break;
		case AI.Key.Escape:
			Close();
			e.Handled = true;
			break;
		}
	}

	private void MoveSel(int d)
	{
		if (_items.Count == 0)
		{
			return;
		}
		_sel = Math.Max(0, Math.Min(_items.Count - 1, _sel + d));
		if (_sel < _scroll)
		{
			_scroll = _sel;
		}
		if (_sel >= _scroll + MaxRows)
		{
			_scroll = _sel - MaxRows + 1;
		}
		_selY.Target = _sel - _scroll;
		Invalidate();
	}

	public string DebugInfo => _show.Value + " closing=" + _closing + " items=" + _items.Count + " all=" + _all.Count;

	/// <summary>Прогрев: один невидимый кадр, чтобы первое открытие по Ctrl+K не подтормаживало.</summary>
	public void Warm()
	{
		if (Visible || Parent == null)
		{
			return;
		}
		_all.Clear();
		if (Source != null)
		{
			_all.AddRange(Source());
		}
		Filter();
		SetBounds(0, 0, Parent.ClientSize.Width, Parent.ClientSize.Height);
		_show.Snap(0f);
		_closing = true;
		Visible = true;
		BringToFront();
		Timer t = new Timer
		{
			Interval = 120
		};
		t.Tick += delegate
		{
			t.Stop();
			t.Dispose();
			if (_closing)
			{
				Visible = false;
				_closing = false;
			}
		};
		t.Start();
	}

	public string Query
	{
		set
		{
			_box.Text = value;
			Filter();
			Invalidate();
		}
	}

	public void Open()
	{
		_closing = false;
		_all.Clear();
		if (Source != null)
		{
			_all.AddRange(Source());
		}
		_box.Text = "";
		Filter();
		if (Parent != null)
		{
			SetBounds(0, 0, Parent.ClientSize.Width, Parent.ClientSize.Height);
		}
		Visible = true;
		BringToFront();
		_show.Snap(0f);
		_show.Target = 1f;
		PlaceBox();
		Invalidate(true);
		// Страховочная перерисовка после окончания fade-in.
		Timer t = new Timer
		{
			Interval = 230
		};
		t.Tick += delegate
		{
			t.Stop();
			t.Dispose();
			if (!_closing && Visible)
			{
				// Если кадры анимации не пришли (первое открытие), показываем сразу.
				_show.Snap(1f);
				PlaceBox();
				Invalidate(true);
			}
		};
		t.Start();
		_box.Focus();
	}

	public void Close()
	{
		if (!Visible || _closing)
		{
			return;
		}
		_closing = true;
		if (!Anim.Enabled)
		{
			Visible = false;
			return;
		}
		_show.Target = 0f;
		Timer t = new Timer
		{
			Interval = 170
		};
		t.Tick += delegate
		{
			t.Stop();
			t.Dispose();
			if (_closing)
			{
				Visible = false;
			}
		};
		t.Start();
	}

	private static bool Match(Item it, string[] words)
	{
		string hay = (it.Text + " " + it.Sub + " " + it.Key).ToLowerInvariant();
		foreach (string w in words)
		{
			if (hay.IndexOf(w, StringComparison.Ordinal) < 0)
			{
				return false;
			}
		}
		return true;
	}

	private void Filter()
	{
		string q = (_box.Text ?? "").Trim().ToLowerInvariant();
		if (q.Length == 0)
		{
			_items = _all.FindAll((Item i) => i.Key == "→" || i.Key == "⏎");
		}
		else
		{
			string[] words = q.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			_items = _all.FindAll((Item i) => Match(i, words));
			// Совпадение в начале названия — выше.
			_items.Sort((Item a, Item b) =>
			{
				int sa = Rank(a.Text, q);
				int sb = Rank(b.Text, q);
				return sa != sb ? sa.CompareTo(sb) : 0;
			});
			if (_items.Count > 60)
			{
				_items = _items.GetRange(0, 60);
			}
		}
		_sel = 0;
		_scroll = 0;
		_selY.Snap(0f);
		PlaceBox();
		Invalidate();
	}

	private static int Rank(string text, string q)
	{
		string t = text.ToLowerInvariant();
		if (t.StartsWith(q, StringComparison.Ordinal))
		{
			return 0;
		}
		int i = t.IndexOf(" " + q, StringComparison.Ordinal);
		if (i < 0)
		{
			i = t.IndexOf("(" + q, StringComparison.Ordinal);
		}
		return (i >= 0) ? 1 : 2;
	}

	private void PlaceBox()
	{
		Rectangle r = BoxR;
		int ph = _box.PreferredHeight;
		_box.SetBounds(r.X + Theme.S(48f), r.Y + (InputH - ph) / 2, r.Width - Theme.S(64f), ph);
	}

	private void RunSel()
	{
		if (_sel >= 0 && _sel < _items.Count)
		{
			Item it = _items[_sel];
			Close();
			it.Run?.Invoke();
		}
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		PlaceBox();
	}

	private int HitRow(Point p)
	{
		Rectangle r = BoxR;
		int top = r.Y + InputH + Theme.S(6f);
		if (p.X < r.X || p.X > r.Right || p.Y < top)
		{
			return -1;
		}
		int i = (p.Y - top) / RowH + _scroll;
		if (i >= _scroll + MaxRows || i >= _items.Count)
		{
			return -1;
		}
		return i;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int h = HitRow(e.Location);
		if (h != _hot)
		{
			_hot = h;
			if (h >= 0)
			{
				_sel = h;
				_selY.Target = h - _scroll;
			}
			Cursor = (h >= 0) ? Cursors.Hand : Cursors.Default;
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		int max = Math.Max(0, _items.Count - MaxRows);
		_scroll = Math.Max(0, Math.Min(max, _scroll - Math.Sign(e.Delta)));
		_sel = Math.Max(_scroll, Math.Min(_scroll + MaxRows - 1, _sel));
		_selY.Snap(_sel - _scroll);
		Invalidate();
		if (e is HandledMouseEventArgs)
		{
			((HandledMouseEventArgs)e).Handled = true;
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (!BoxR.Contains(e.Location))
		{
			Close();
			return;
		}
		int h = HitRow(e.Location);
		if (h >= 0)
		{
			_sel = h;
			RunSel();
		}
		else
		{
			_box.Focus();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		Theme.Smooth(g);
		float a = _show.Value;
		PlaceBox();
		using (SolidBrush dim = new SolidBrush(Color.FromArgb((int)(150f * a), 0, 0, 0)))
		{
			g.FillRectangle(dim, 0, 0, base.Width, base.Height);
		}
		Rectangle r = BoxR;
		using (GraphicsPath sh = Theme.Round(new RectangleF(r.X - Theme.S(1f), r.Y + Theme.S(8f), r.Width + Theme.S(2f), r.Height + Theme.S(8f)), Theme.S(14f)))
		{
			using SolidBrush shb = new SolidBrush(Color.FromArgb((int)(90f * a), 0, 0, 0));
			g.FillPath(shb, sh);
		}
		using (GraphicsPath path = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), Theme.S(14f)))
		{
			using (SolidBrush bg = new SolidBrush(Color.FromArgb((int)(255f * a), 21, 21, 24)))
			{
				g.FillPath(bg, path);
			}
			using Pen pen = new Pen(Color.FromArgb((int)(255f * a), 46, 46, 53), Theme.BW);
			g.DrawPath(pen, path);
		}
		Color ic = Color.FromArgb((int)(255f * a), Theme.Sub);
		float cx = r.X + Theme.S(24f);
		float cy = r.Y + InputH / 2f;
		using (Pen ip = new Pen(ic, Theme.S(1.7f)))
		{
			ip.StartCap = LineCap.Round;
			ip.EndCap = LineCap.Round;
			g.DrawEllipse(ip, cx - Theme.S(6f), cy - Theme.S(6f), Theme.S(11f), Theme.S(11f));
			g.DrawLine(ip, cx + Theme.S(3.5f), cy + Theme.S(3.5f), cx + Theme.S(7.5f), cy + Theme.S(7.5f));
		}
		if (string.IsNullOrEmpty(_box.Text))
		{
			TextRenderer.DrawText(g, L.T("Найти настройку, раздел или команду…"), _fInput, new Rectangle(r.X + Theme.S(48f), r.Y, r.Width - Theme.S(64f), InputH), Color.FromArgb((int)(255f * a), Theme.Dim), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		using (SolidBrush ln = new SolidBrush(Color.FromArgb((int)(255f * a), Theme.Line)))
		{
			g.FillRectangle(ln, r.X + 1, r.Y + InputH, r.Width - 2, 1);
		}
		int top = r.Y + InputH + Theme.S(6f);
		if (_items.Count == 0)
		{
			TextRenderer.DrawText(g, L.T("Ничего не найдено"), Theme.Small, new Rectangle(r.X, top, r.Width, RowH), Color.FromArgb((int)(255f * a), Theme.Dim), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
			return;
		}
		Rectangle clipR = new Rectangle(r.X, top, r.Width, Math.Min(MaxRows, _items.Count) * RowH);
		Region old = g.Clip;
		g.SetClip(clipR, CombineMode.Intersect);
		float sy = top + _selY.Value * RowH;
		using (GraphicsPath sp = Theme.Round(new RectangleF(r.X + Theme.S(6f), sy + Theme.S(2f), r.Width - Theme.S(12f), RowH - Theme.S(4f)), Theme.S(8f)))
		{
			using SolidBrush sb = new SolidBrush(Color.FromArgb((int)(255f * a), 34, 34, 40));
			g.FillPath(sb, sp);
		}
		int n = Math.Min(MaxRows, _items.Count - _scroll);
		for (int i = 0; i < n; i++)
		{
			Item it = _items[_scroll + i];
			int y = top + i * RowH;
			bool on = _scroll + i == _sel;
			bool sec = it.Key == "→";
			float dcx = r.X + Theme.S(24f);
			float dcy = y + RowH / 2f;
			using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(255f * a), sec ? Theme.Accent : (on ? Theme.Sub : Theme.Dim))))
			{
				float ds = sec ? Theme.S(7f) : Theme.S(5f);
				using GraphicsPath dp = Theme.Round(new RectangleF(dcx - ds / 2f, dcy - ds / 2f, ds, ds), Theme.S(2f));
				g.FillPath(db, dp);
			}
			int subW = string.IsNullOrEmpty(it.Sub) ? 0 : Math.Min(Theme.S(200f), Theme.Measure(it.Sub, _fSub).Width + Theme.S(10f));
			TextRenderer.DrawText(g, it.Text, _fItem, new Rectangle(r.X + Theme.S(42f), y, r.Width - Theme.S(72f) - subW, RowH), Color.FromArgb((int)(255f * a), on ? Color.White : Theme.Text), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (subW > 0)
			{
				TextRenderer.DrawText(g, it.Sub, _fSub, new Rectangle(r.Right - Theme.S(18f) - subW, y, subW, RowH), Color.FromArgb((int)(255f * a), Theme.Dim), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
		}
		g.Clip = old;
		old.Dispose();
	}
}
