using System;
using System.Collections.Generic;

namespace WinWeaver;

/// <summary>
/// Верхняя навигация: группы («Главная», «Система», «Инструменты», «Настройки»)
/// и строка подвкладок выбранной группы. Публичный API совместим с прежним Sidebar.
/// </summary>
internal class TopNav : Control
{
	public class Item
	{
		public string Key;
		public string Text;
		public string Glyph;
		public bool Bottom;
		public int Badge;
		public Rectangle R;
	}

	private class Group
	{
		public string Key;
		public string Text;
		public string[] Keys;
		public Rectangle R;
		public string Last;
	}

	public readonly List<Item> Items = new List<Item>();

	private readonly List<Group> _groups = new List<Group>
	{
		new Group { Key = "g_home", Text = "Главная", Keys = new[] { "home" } },
		new Group { Key = "g_sys", Text = "Система", Keys = new[] { "review", "games", "privacy", "ui", "net" } },
		new Group { Key = "g_tools", Text = "Инструменты", Keys = new[] { "apps", "system", "fixes", "restore", "history" } },
		new Group { Key = "g_set", Text = "Настройки", Keys = new[] { "settings" } },
	};

	public string Selected;
	public Image Logo;
	public string Version = "v1.0.1";
	public string RebootText;
	public string RebootSub;
	public bool RebootScheduled;
	public string UpdateText;
	public string UpdateSub;

	private readonly Font _fLogo = Theme.F(11.5f, semi: true);
	private readonly Font _fGrp = Theme.F(9f, semi: true);
	private readonly Font _fTab = Theme.F(9f);
	private readonly Font _fTabSel = Theme.F(9f, semi: true);
	private readonly Font _fBadge = Theme.F(7f, semi: true);
	private readonly Font _fKey = new Font("JetBrains Mono", 7f);
	private readonly Font _fMono = new Font("JetBrains Mono", 7.5f);

	private readonly Tween _indX;
	private readonly Tween _indW;
	private readonly Tween _hotA;
	private bool _placed;

	private Rectangle _grpBox, _tabBox, _prevR, _nextR, _searchR, _heartR, _rbR, _upR;
	private bool _searchCompact;
	private int _scroll;
	private bool _showKeys = true, _showIcons = true;
	private string _hot;
	private string _hotLast;

	public event Action<string> Navigate;
	public event Action SearchClicked;
	public event Action<Point> RebootClicked;
	public event Action UpdateClicked;
	public event Action SupportClicked;
	public event Action HeightChanged;

	public int TopH => Theme.S(60f);
	public int SubH => Theme.S(58f);

	public int DesiredHeight => TopH + (CurKids().Count > 1 ? SubH : 0);

	public TopNav()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Cursor = Cursors.Default;
		_indX = new Tween(this, 0f, 0.22f);
		_indW = new Tween(this, 0f, 0.22f);
		_hotA = new Tween(this, 0f, 0.3f);
	}

	public void Add(string key, string text, string glyph, bool bottom = false)
	{
		if (Find(key) != null) return;
		Items.Add(new Item { Key = key, Text = text, Glyph = glyph, Bottom = bottom });
	}

	private Item Find(string key)
	{
		foreach (Item i in Items) if (i.Key == key) return i;
		return null;
	}

	public void SetBadge(string key, int n)
	{
		Item i = Find(key);
		if (i != null && i.Badge != n)
		{
			i.Badge = n;
			Invalidate();
		}
	}

	public void SetUpdate(string text, string sub)
	{
		if (text == UpdateText && sub == UpdateSub) return;
		UpdateText = text; UpdateSub = sub; Invalidate();
	}

	public void SetReboot(string text, string sub, bool scheduled)
	{
		if (text == RebootText && sub == RebootSub && scheduled == RebootScheduled) return;
		RebootText = text; RebootSub = sub; RebootScheduled = scheduled; Invalidate();
	}

	private Group GroupOf(string key)
	{
		foreach (Group g in _groups) if (Array.IndexOf(g.Keys, key) >= 0) return g;
		return null;
	}

	private List<Item> Kids(Group g)
	{
		List<Item> list = new List<Item>();
		if (g == null) return list;
		foreach (string k in g.Keys)
		{
			Item i = Find(k);
			if (i != null) list.Add(i);
		}
		return list;
	}

	private List<Item> CurKids() => Kids(GroupOf(Selected));

	private int GroupBadge(Group g)
	{
		if (g.Key == "g_sys")
		{
			Item r = Find("review");
			if (r != null) return r.Badge;
		}
		return 0;
	}

	public void Select(string key)
	{
		int h0 = DesiredHeight;
		Selected = key;
		Group g = GroupOf(key);
		if (g != null) g.Last = key;
		Calc();
		Item it = Find(key);
		if (it != null && !it.R.IsEmpty)
		{
			if (!_placed) { _indX.Snap(it.R.X); _indW.Snap(it.R.Width); _placed = true; }
			else { _indX.Target = it.R.X; _indW.Target = it.R.Width; }
		}
		Invalidate();
		if (DesiredHeight != h0) HeightChanged?.Invoke();
	}

	/// <summary>Ctrl+Tab / Ctrl+Shift+Tab.</summary>
	public void Step(int d)
	{
		List<Item> kids = CurKids();
		if (kids.Count < 2) return;
		int i = kids.FindIndex(x => x.Key == Selected);
		i = ((i + d) % kids.Count + kids.Count) % kids.Count;
		Navigate?.Invoke(kids[i].Key);
	}

	/// <summary>Ctrl+1…9 — сразу к подвкладке.</summary>
	public void Jump(int n)
	{
		List<Item> kids = CurKids();
		if (n >= 0 && n < kids.Count) Navigate?.Invoke(kids[n].Key);
	}

	public static Color Tone(string key)
	{
		return key switch
		{
			"review" => Theme.Orange,
			"games" => Theme.Yellow,
			"privacy" => Theme.Accent,
			"ui" => Theme.Blue,
			"net" => Theme.Green,
			"apps" => Theme.Cyan,
			"system" => Theme.Green,
			"fixes" => Theme.Orange,
			"restore" => Theme.Blue,
			"history" => Color.FromArgb(192, 132, 252),
			_ => Theme.Sub,
		};
	}

	private int TabW(Item i, bool sel, bool icons, bool keys, int idx)
	{
		int w = Theme.S(12f) + Theme.Measure(L.T(i.Text), sel ? _fTabSel : _fTab).Width + Theme.S(12f);
		if (icons) w += Theme.S(30f);
		if (i.Badge > 0) w += Theme.Measure(BadgeText(i.Badge), _fBadge).Width + Theme.S(16f);
		if (keys) w += Theme.Measure((idx + 1).ToString(), _fKey).Width + Theme.S(16f);
		return w;
	}

	private static string BadgeText(int n) => n > 99 ? "99+" : n.ToString();

	private void Calc()
	{
		int W = Width;
		// --- верхняя строка
		int x = Theme.S(20f) + Theme.S(26f) + Theme.S(10f) + Theme.Measure("WinWeaver", _fLogo).Width + Theme.S(26f);
		int gy = (TopH - Theme.S(40f)) / 2;
		int gx = x + Theme.S(4f);
		foreach (Group g in _groups)
		{
			int bw = Theme.S(14f) + Theme.Measure(L.T(g.Text), _fGrp).Width + Theme.S(14f);
			int b = GroupBadge(g);
			if (b > 0) bw += Theme.Measure(BadgeText(b), _fBadge).Width + Theme.S(16f);
			if (Kids(g).Count > 1) bw += Theme.S(0f);
			g.R = new Rectangle(gx, gy + Theme.S(4f), bw, Theme.S(32f));
			gx += bw + Theme.S(2f);
		}
		_grpBox = new Rectangle(x, gy, gx - x + Theme.S(2f), Theme.S(40f));
		// справа: поддержка, поиск, уведомления
		int right = W - Theme.S(20f);
		_heartR = new Rectangle(right - Theme.S(36f), (TopH - Theme.S(36f)) / 2, Theme.S(36f), Theme.S(36f));
		right = _heartR.X - Theme.S(8f);
		int room = right - _grpBox.Right - Theme.S(16f);
		int sw = Theme.S(250f);
		_searchCompact = false;
		int pills = 0;
		int rbW = RebootText != null ? PillW(RebootText) : 0;
		int upW = UpdateText != null ? PillW(UpdateText) : 0;
		pills = (rbW > 0 ? rbW + Theme.S(8f) : 0) + (upW > 0 ? upW + Theme.S(8f) : 0);
		if (sw + pills > room) sw = Math.Max(Theme.S(170f), room - pills);
		if (sw + pills > room) { sw = Theme.S(36f); _searchCompact = true; }
		_searchR = new Rectangle(right - sw, (TopH - Theme.S(36f)) / 2, sw, Theme.S(36f));
		right = _searchR.X - Theme.S(8f);
		int left = _grpBox.Right + Theme.S(12f);
		_rbR = Rectangle.Empty; _upR = Rectangle.Empty;
		if (rbW > 0 && right - rbW >= left) { _rbR = new Rectangle(right - rbW, (TopH - Theme.S(32f)) / 2, rbW, Theme.S(32f)); right = _rbR.X - Theme.S(8f); }
		if (upW > 0 && right - upW >= left) { _upR = new Rectangle(right - upW, (TopH - Theme.S(32f)) / 2, upW, Theme.S(32f)); }
		// --- строка подвкладок
		foreach (Item i in Items) i.R = Rectangle.Empty;
		List<Item> kids = CurKids();
		if (kids.Count < 2) { _tabBox = _prevR = _nextR = Rectangle.Empty; return; }
		int ty = TopH + Theme.S(4f);
		int th = Theme.S(46f);
		int arrow = Theme.S(34f);
		_prevR = new Rectangle(Theme.S(20f), ty + (th - arrow) / 2, arrow, arrow);
		_nextR = new Rectangle(W - Theme.S(20f) - arrow, ty + (th - arrow) / 2, arrow, arrow);
		int bx = _prevR.Right + Theme.S(10f);
		int avail = _nextR.X - Theme.S(10f) - bx - Theme.S(10f);
		bool icons = true, keys = true;
		int Sum(bool ic, bool ks)
		{
			int s = 0;
			for (int k = 0; k < kids.Count; k++) s += TabW(kids[k], kids[k].Key == Selected, ic, ks, k) + Theme.S(4f);
			return s - Theme.S(4f);
		}
		if (Sum(icons, keys) > avail) keys = false;
		if (Sum(icons, keys) > avail) icons = false;
		_showIcons = icons; _showKeys = keys;
		int total = Sum(icons, keys);
		int boxW = Math.Min(avail, total) + Theme.S(10f);
		_tabBox = new Rectangle(bx, ty, boxW, th);
		_nextR = new Rectangle(_tabBox.Right + Theme.S(10f), _nextR.Y, arrow, arrow);
		int cx = bx + Theme.S(5f);
		for (int k = 0; k < kids.Count; k++)
		{
			int w = TabW(kids[k], kids[k].Key == Selected, icons, keys, k);
			kids[k].R = new Rectangle(cx, ty + Theme.S(5f), w, th - Theme.S(10f));
			cx += w + Theme.S(4f);
		}
		// прокрутка, чтобы выбранная вкладка была видна
		_scroll = 0;
		if (total > avail)
		{
			Item s = Find(Selected);
			if (s != null && s.R.Right > _tabBox.Right - Theme.S(5f)) _scroll = s.R.Right - (_tabBox.Right - Theme.S(5f));
			foreach (Item i in kids) i.R.Offset(-_scroll, 0);
		}
	}

	private int PillW(string text) => Theme.S(30f) + Math.Min(Theme.S(220f), Theme.Measure(Theme.Up(text), _fTab).Width) + Theme.S(14f);

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Calc();
		Item it = Find(Selected);
		if (it != null) { _indX.Snap(it.R.X); _indW.Snap(it.R.Width); }
		Invalidate();
	}

	private string HitKey(Point p)
	{
		foreach (Group g in _groups) if (g.R.Contains(p)) return g.Key;
		foreach (Item i in CurKids()) if (i.R.Contains(p) && _tabBox.Contains(p)) return i.Key;
		if (_prevR.Contains(p)) return "<";
		if (_nextR.Contains(p)) return ">";
		if (_searchR.Contains(p)) return "search";
		if (_heartR.Contains(p)) return "heart";
		if (!_rbR.IsEmpty && _rbR.Contains(p)) return "reboot";
		if (!_upR.IsEmpty && _upR.Contains(p)) return "update";
		if (p.X < _grpBox.X && p.Y < TopH) return "logo";
		return null;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		string h = HitKey(e.Location);
		if (h != _hot)
		{
			_hot = h;
			if (h != null) { _hotLast = h; _hotA.Snap(0f); _hotA.Target = 1f; }
			else _hotA.Target = 0f;
			Invalidate();
		}
		Cursor c = h != null ? Cursors.Hand : Cursors.Default;
		if (Cursor != c) Cursor = c;
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = null;
		_hotA.Target = 0f;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		string h = HitKey(e.Location);
		switch (h)
		{
			case null: break;
			case "search": SearchClicked?.Invoke(); break;
			case "heart": SupportClicked?.Invoke(); break;
			case "update": UpdateClicked?.Invoke(); break;
			case "reboot": RebootClicked?.Invoke(PointToScreen(new Point(_rbR.X, _rbR.Bottom + Theme.S(4f)))); break;
			case "logo": Navigate?.Invoke("home"); break;
			case "<": Step(-1); break;
			case ">": Step(1); break;
			default:
				foreach (Group g in _groups)
				{
					if (g.Key == h)
					{
						List<Item> kids = Kids(g);
						string to = g.Last ?? (kids.Count > 0 ? kids[0].Key : null);
						if (to != null) Navigate?.Invoke(to);
						base.OnMouseDown(e);
						return;
					}
				}
				Navigate?.Invoke(h);
				break;
		}
		base.OnMouseDown(e);
	}

	private float HotT(string key) => key == _hotLast ? _hotA.Value : 0f;

	private void Box(Graphics g, Rectangle r, float rad, Color fill, Color border)
	{
		using GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), rad);
		if (fill.A > 0) { using SolidBrush b = new SolidBrush(fill); g.FillPath(b, p); }
		if (border.A > 0) { using Pen pen = new Pen(border, Theme.BW); g.DrawPath(pen, p); }
	}

	private void Badge(Graphics g, int n, int x, int cy, bool neutral = false)
	{
		string t = BadgeText(n);
		Size sz = Theme.Measure(t, _fBadge);
		Rectangle r = new Rectangle(x, cy - Theme.S(8f), sz.Width + Theme.S(10f), Theme.S(16f));
		Box(g, r, Theme.S(5f), neutral ? Color.FromArgb(44, 44, 52) : Theme.Accent, Color.Empty);
		TextRenderer.DrawText(g, t, _fBadge, r, neutral ? Theme.Sub : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
	}

	private void Chevron(Graphics g, Rectangle r, int dir, Color c)
	{
		float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = Theme.S(4f);
		using Pen p = new Pen(c, Theme.S(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		g.DrawLine(p, cx + s * 0.5f * dir * -1, cy - s, cx + s * 0.5f * dir, cy);
		g.DrawLine(p, cx + s * 0.5f * dir, cy, cx + s * 0.5f * dir * -1, cy + s);
	}

	private void Star(Graphics g, float cx, float cy, float R, Color c)
	{
		PointF[] pts = new PointF[10];
		for (int k = 0; k < 10; k++)
		{
			double a = -Math.PI / 2 + k * Math.PI / 5;
			float rr = (k % 2 == 0) ? R : R * 0.45f;
			pts[k] = new PointF(cx + (float)(Math.Cos(a) * rr), cy + (float)(Math.Sin(a) * rr));
		}
		using GraphicsPath p = new GraphicsPath();
		p.AddPolygon(pts);
		using Pen pen = new Pen(c, Theme.S(1.4f)) { LineJoin = LineJoin.Round };
		g.DrawPath(pen, p);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		Theme.Smooth(g);
		Calc();
		g.Clear(Theme.Side1);
		using (SolidBrush lb = new SolidBrush(Theme.Line)) g.FillRectangle(lb, 0, Height - 1, Width, 1);
		// логотип
		int lx = Theme.S(20f), ls = Theme.S(26f), ly = (TopH - ls) / 2;
		if (Logo != null) g.DrawImage(Logo, new Rectangle(lx, ly, ls, ls));
		else Theme.DotLogo(g, lx, ly + Theme.S(4f), Theme.S(3f), Color.White);
		Size lsz = Theme.Measure("WinWeaver", _fLogo);
		TextRenderer.DrawText(g, "WinWeaver", _fLogo, new Point(lx + ls + Theme.S(10f), ly + (ls - lsz.Height) / 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		// группы
		Box(g, _grpBox, Theme.S(12f), Color.FromArgb(20, 20, 24), Theme.Border);
		Group cur = GroupOf(Selected);
		foreach (Group gr in _groups)
		{
			bool on = gr == cur;
			float t = HotT(gr.Key);
			if (on) Box(g, gr.R, Theme.S(9f), Theme.Panel2, Theme.BorderHi);
			else if (t > 0.01f) Box(g, gr.R, Theme.S(9f), Color.FromArgb((int)(255 * t), Theme.Hover), Color.Empty);
			Color fc = on ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, t);
			Size ts = Theme.Measure(L.T(gr.Text), _fGrp);
			int tx = gr.R.X + Theme.S(14f);
			TextRenderer.DrawText(g, L.T(gr.Text), _fGrp, new Point(tx, gr.R.Y + (gr.R.Height - ts.Height) / 2), fc, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int b = GroupBadge(gr);
			if (b > 0) Badge(g, b, tx + ts.Width + Theme.S(8f), gr.R.Y + gr.R.Height / 2);
		}
		// уведомления
		if (!_upR.IsEmpty) Pill(g, _upR, "update", Color.White, UpdateText);
		if (!_rbR.IsEmpty) Pill(g, _rbR, "reboot", RebootScheduled ? Color.White : Theme.Accent, RebootText);
		// поиск
		{
			float t = HotT("search");
			Box(g, _searchR, Theme.S(11f), Theme.Mix(Color.FromArgb(20, 20, 24), Color.FromArgb(26, 26, 31), t), Theme.Mix(Theme.Border, Theme.BorderHi, t));
			float cx = _searchCompact ? _searchR.X + _searchR.Width / 2f - Theme.S(1f) : _searchR.X + Theme.S(18f);
			float cy = _searchR.Y + _searchR.Height / 2f - Theme.S(1f);
			Color sc = Theme.Mix(Theme.Dim, Theme.Sub, t);
			using (Pen ip = new Pen(sc, Theme.S(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawEllipse(ip, cx - Theme.S(5f), cy - Theme.S(5f), Theme.S(9f), Theme.S(9f));
				g.DrawLine(ip, cx + Theme.S(3f), cy + Theme.S(3f), cx + Theme.S(6.5f), cy + Theme.S(6.5f));
			}
			if (!_searchCompact)
			{
				string kb = "Ctrl K";
				Size ksz = Theme.Measure(kb, _fMono);
				Rectangle kr = new Rectangle(_searchR.Right - Theme.S(10f) - ksz.Width - Theme.S(10f), _searchR.Y + (_searchR.Height - Theme.S(20f)) / 2, ksz.Width + Theme.S(10f), Theme.S(20f));
				TextRenderer.DrawText(g, L.T("Поиск и команды"), Theme.Small, new Rectangle(_searchR.X + Theme.S(32f), _searchR.Y, kr.X - _searchR.X - Theme.S(36f), _searchR.Height), sc, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Box(g, kr, Theme.S(5f), Color.FromArgb(24, 24, 28), Color.FromArgb(46, 46, 53));
				TextRenderer.DrawText(g, kb, _fMono, kr, Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			}
		}
		// поддержка
		{
			float t = HotT("heart");
			Box(g, _heartR, Theme.S(11f), Theme.Mix(Color.FromArgb(20, 20, 24), Color.FromArgb(26, 26, 31), t), Theme.Mix(Theme.Border, Theme.BorderHi, t));
			float u = Theme.S(1f);
			Theme.Heart(g, new RectangleF(_heartR.X + (_heartR.Width - 12f * u) / 2f, _heartR.Y + (_heartR.Height - 11f * u) / 2f, 12f * u, 11f * u), Theme.Mix(Theme.Sub, Theme.Accent, t));
		}
		// подвкладки
		List<Item> kids = CurKids();
		if (kids.Count < 2) return;
		using (SolidBrush lb = new SolidBrush(Theme.Line)) g.FillRectangle(lb, 0, TopH, Width, 1);
		foreach (string a in new[] { "<", ">" })
		{
			Rectangle r = a == "<" ? _prevR : _nextR;
			float t = HotT(a);
			Box(g, r, Theme.S(10f), Theme.Mix(Color.FromArgb(18, 18, 21), Color.FromArgb(26, 26, 31), t), Theme.Mix(Theme.Border, Theme.BorderHi, t));
			Chevron(g, r, a == "<" ? -1 : 1, Theme.Mix(Theme.Sub, Theme.Text, t));
		}
		Box(g, _tabBox, Theme.S(13f), Color.FromArgb(17, 17, 20), Theme.Border);
		g.SetClip(new Rectangle(_tabBox.X + 1, _tabBox.Y, _tabBox.Width - 2, _tabBox.Height + Theme.S(3f)));
		Item sel = Find(Selected);
		for (int k = 0; k < kids.Count; k++)
		{
			Item it = kids[k];
			bool on = it == sel;
			float t = HotT(it.Key);
			Color tone = Tone(it.Key);
			if (on)
			{
				Box(g, it.R, Theme.S(10f), Color.FromArgb(30, 30, 36), Color.FromArgb(44, 44, 52));
			}
			else if (t > 0.01f)
			{
				Box(g, it.R, Theme.S(10f), Color.FromArgb((int)(255 * t), Theme.Hover), Color.Empty);
			}
			int x = it.R.X + Theme.S(12f);
			int cy = it.R.Y + it.R.Height / 2;
			if (_showIcons)
			{
				Rectangle ir = new Rectangle(it.R.X + Theme.S(8f), cy - Theme.S(12f), Theme.S(24f), Theme.S(24f));
				Box(g, ir, Theme.S(7f), Color.FromArgb(on ? 56 : 30, tone), Color.Empty);
				if (it.Key == "review")
				{
					Star(g, ir.X + ir.Width / 2f, ir.Y + ir.Height / 2f + Theme.S(0.5f), Theme.S(6.5f), tone);
				}
				else if (Theme.IconSmall != null && !string.IsNullOrEmpty(it.Glyph))
				{
					TextRenderer.DrawText(g, it.Glyph, Theme.IconSmall, ir, tone, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				}
				else
				{
					Theme.Dot(g, ir.X + ir.Width / 2f, ir.Y + ir.Height / 2f, Theme.S(3.5f), tone);
				}
				x = ir.Right + Theme.S(9f);
			}
			Font f = on ? _fTabSel : _fTab;
			Size ts = Theme.Measure(L.T(it.Text), f);
			Color fc = on ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, t);
			TextRenderer.DrawText(g, L.T(it.Text), f, new Point(x, cy - ts.Height / 2), fc, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			x += ts.Width + Theme.S(8f);
			if (it.Badge > 0)
			{
				Badge(g, it.Badge, x, cy, it.Key == "history");
				x += Theme.Measure(BadgeText(it.Badge), _fBadge).Width + Theme.S(16f);
			}
			if (_showKeys)
			{
				string ks = (k + 1).ToString();
				Size kz = Theme.Measure(ks, _fKey);
				Rectangle kr = new Rectangle(x, cy - Theme.S(8f), kz.Width + Theme.S(8f), Theme.S(16f));
				Box(g, kr, Theme.S(4f), Color.Empty, Color.FromArgb(40, 40, 46));
				TextRenderer.DrawText(g, ks, _fKey, kr, Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			}
		}
		if (sel != null && !sel.R.IsEmpty)
		{
			float ix = _indX.Value, iw = _indW.Value;
			Color tone = Tone(sel.Key);
			using GraphicsPath bar = Theme.Round(new RectangleF(ix + Theme.S(12f), _tabBox.Bottom - Theme.S(1.5f), Math.Max(0, iw - Theme.S(24f)), Theme.S(2.5f)), Theme.S(1.2f));
			using SolidBrush ab = new SolidBrush(tone);
			g.FillPath(ab, bar);
		}
		g.ResetClip();
	}

	private void Pill(Graphics g, Rectangle r, string key, Color tone, string text)
	{
		float t = HotT(key);
		Box(g, r, Theme.S(10f), Theme.Mix(Color.FromArgb(20, 20, 24), Color.FromArgb(26, 26, 31), t), Theme.Mix(Theme.Border, Theme.BorderHi, t));
		Theme.Dot(g, r.X + Theme.S(15f), r.Y + r.Height / 2f, Theme.S(3.5f), tone);
		TextRenderer.DrawText(g, Theme.Up(text), _fTab, new Rectangle(r.X + Theme.S(28f), r.Y, r.Width - Theme.S(38f), r.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}
}
