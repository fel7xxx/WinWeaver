using System;
using System.Collections;

namespace WinWeaver;

internal class Hero : Control, IBlock
{
	private class Chip
	{
		public string Key;

		public string Text;

		public Color Color;

		public Rectangle R;
	}

	private Image _img;

	public string Line1 = "WinWeaver";

	public string Line2 = "Windows 11";

	public string Line3 = "";

	public bool NeedReboot;

	private string _cpu = "";

	private string _gpu = "";

	private string _ram = "";

	private string _disk = "";

	private readonly Tween _ramBar;

	private readonly Tween _diskBar;

	private readonly Tween _appear;

	private float _shine = -1f;

	private bool _loaded;

	private readonly Chip[] _chips = new Chip[3]
	{
		new Chip
		{
			Key = "level",
			Text = "…",
			Color = Theme.Sub
		},
		new Chip
		{
			Key = "privacy",
			Text = "…",
			Color = Theme.Sub
		},
		new Chip
		{
			Key = "history",
			Text = "…",
			Color = Theme.Sub
		}
	};

	private string _hot;

	private int _ok = -1;

	private int _total;

	private readonly Tween _ring;

	private Rectangle _fixR;

	private Rectangle _viewR;

	private Rectangle _scoreR;

	private Rectangle _rebootR;

	private string _greet = "";

	public bool Fixing;

	private int _bad = -1;

	public static readonly string[] LevelNames = new string[3] { "light", "optimal", "gamer" };

	private Bitmap _halftone;

	private bool Wide => base.Width >= Theme.S(760f);

	public event Action<string> ChipClicked;

	public void SetScore(int ok, int total, int bad = -1)
	{
		_ok = ok;
		_total = total;
		_bad = bad;
		_ring.Target = ((total > 0) ? ((float)ok / (float)total) : 0f);
		_rF.Target = _ring.Target;
		Invalidate();
	}

	public static string LevelName(string id)
	{
		return id switch
		{
			"light" => "Базовая", 
			"optimal" => "Оптимальная", 
			"gamer" => "Максимальная", 
			_ => null, 
		};
	}

	public Hero()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		_ramBar = new Tween(this, 0f, 0.12f).Timed(800);
		_diskBar = new Tween(this, 0f, 0.12f).Timed(800);
		_appear = new Tween(this, 0f, 0.14f);
		_ring = new Tween(this, 0f, 0.08f).Timed(900);
		_rO = new Tween(this, 0f, 0.08f).Timed(1100).WithEase(Anim.EaseOut);
		_rP = new Tween(this, 0f, 0.08f).Timed(1300).WithEase(Anim.EaseOut);
		_rF = new Tween(this, 0f, 0.08f).Timed(1500).WithEase(Anim.EaseOut);
		_img = LoadWallpaper(1600);
	}

	public static Image LoadWallpaper(int maxW)
	{
		return Walls.Banner(Prefs.LastWallpaper, maxW) ?? Walls.Load(Prefs.LastWallpaper, maxW);
	}

	private static Image Shrink(Image src, int maxW)
	{
		int num = Math.Min(src.Width, maxW);
		int height = Math.Max(1, src.Height * num / src.Width);
		Bitmap bitmap = new Bitmap(num, height);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.DrawImage(src, 0, 0, num, height);
		return bitmap;
	}

	public int DoLayout(int x, int y, int w)
	{
		int num = HeroH + Theme.S(14f) + TileH;
		SetBounds(x, y, w, num);
		return num;
	}

	private int HeroH => Theme.S(300f);

	private int TileH => Theme.S(108f);

	private int _idx = -1;

	private int _priv = -1;

	private int _look = -1;

	private int _recBad = -1;

	private string _topTab;

	private readonly Tween _rO;

	private readonly Tween _rP;

	private readonly Tween _rF;

	private Rectangle _recR;

	private Rectangle _histR;

	private Rectangle _tagPrivR;

	private Rectangle _tagFpsR;

	private Rectangle _tagLookR;

	/// <summary>Индекс системы и проценты по разделам (по рекомендованным настройкам).</summary>
	public void SetIndex(int overall, int privacy, int look, int bad, string topTab)
	{
		_idx = overall;
		_priv = privacy;
		_look = look;
		_recBad = bad;
		_topTab = topTab;
		_rO.Target = Math.Max(0, overall) / 100f;
		_rP.Target = Math.Max(0, privacy) / 100f;
		Invalidate();
	}

	public object Fetch(IEngine e)
	{
		return new object[4]
		{
			e.Eval("Get-FSysInfo"),
			e.Str(e.Eval("Get-FLevelId")),
			0,
			e.As(e.Eval("Get-FHistCount"), 0)
		};
	}

	public void Apply(object v)
	{
		if (!(v is object[] array) || array.Length < 4)
		{
			return;
		}
		if (array[0] is Hashtable hashtable)
		{
			Line2 = S(hashtable["OS"], "Windows 11") + ((hashtable["Build"] != null) ? ("  •  сборка " + hashtable["Build"]) : "");
			int hour = DateTime.Now.Hour;
			string text = ((hour >= 5 && hour < 12) ? "Доброе утро" : ((hour >= 12 && hour < 18) ? "Добрый день" : ((hour >= 18 && hour < 23) ? "Добрый вечер" : "Доброй ночи")));
			_greet = text + ((hashtable["User"] != null) ? (", " + hashtable["User"]) : "");
			Line3 = (S(hashtable["PC"], "") + ((hashtable["Uptime"] != null) ? ("  •  работает " + hashtable["Uptime"]) : "")).Trim(' ', '•');
			_cpu = S(hashtable["CPU"], "—");
			_gpu = S(hashtable["GPU"], "—");
			_ram = S(hashtable["RAM"], "—");
			_disk = (S(hashtable["Drive"], "") + " " + S(hashtable["DiskFree"], "")).Trim();
			if (_disk == "")
			{
				_disk = "—";
			}
			_ramBar.Target = Pct(hashtable["RAMUsed"]);
			_diskBar.Target = Pct(hashtable["DiskUsed"]);
		}
		string text2 = LevelName(array[1] as string);
		_chips[0].Text = ((text2 != null) ? text2 : "не выбран");
		_chips[0].Color = ((text2 != null) ? Theme.Green : Theme.Yellow);
		int num = ((array[2] is int) ? ((int)array[2]) : (-1));
		_chips[1].Text = ((num >= 0) ? ("Слежка выкл.: " + num + "/4") : "Приватность");
		_chips[1].Color = ((num >= 4) ? Theme.Green : ((num >= 2) ? Theme.Silver1 : Theme.Yellow));
		int num2 = ((array[3] is int) ? ((int)array[3]) : 0);
		_chips[2].Text = ((num2 > 0) ? ("Изменений: " + num2) : "Нет изменений");
		_chips[2].Color = Theme.Silver1;
		if (!_loaded)
		{
			_loaded = true;
			_appear.Target = 1f;
			PlayShine();
		}
		Invalidate();
	}

	public void PlayShine()
	{
		if (Anim.Enabled)
		{
			Anim.Start(this, 1400, delegate(float t)
			{
				_shine = t;
				Invalidate();
			}, delegate
			{
				_shine = -1f;
				Invalidate();
			}, "hero-shine");
		}
	}

	private static float Pct(object o)
	{
		try
		{
			return Math.Max(0f, Math.Min(1f, Convert.ToSingle(o) / 100f));
		}
		catch
		{
			return 0f;
		}
	}

	private static string S(object o, string d)
	{
		if (o != null && !(o.ToString() == ""))
		{
			return o.ToString();
		}
		return d;
	}


	private static readonly Color Ink = Color.FromArgb(10, 10, 12);

	private void Box(Graphics g, RectangleF r, float rad, Color fill, Color border)
	{
		using GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), rad);
		if (fill.A > 0)
		{
			using SolidBrush b = new SolidBrush(fill);
			g.FillPath(b, p);
		}
		if (border.A > 0)
		{
			using Pen pen = new Pen(border, Theme.BW);
			g.DrawPath(pen, p);
		}
	}

	private static void Glow(Graphics g, float cx, float cy, float rx, float ry, Color c, int alpha)
	{
		using GraphicsPath p = new GraphicsPath();
		p.AddEllipse(cx - rx, cy - ry, rx * 2f, ry * 2f);
		using PathGradientBrush b = new PathGradientBrush(p)
		{
			CenterColor = Color.FromArgb(alpha, c),
			SurroundColors = new Color[1] { Color.FromArgb(0, c) }
		};
		g.FillPath(b, p);
	}

	private bool Ready => _idx >= 0;

	private int OrbD => Math.Min(Theme.S(270f), HeroH - Theme.S(20f));

	private bool ShowOrb => base.Width >= Theme.S(760f);

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		Theme.Smooth(g);
		float ap = Anim.EaseOut(_appear.Value);
		int W = base.Width;
		// фон: мягкое свечение и точечная сетка
		using (GraphicsPath clip = Theme.Round(new RectangleF(0f, 0f, W, HeroH), Theme.S(24f)))
		{
			using (SolidBrush cb = new SolidBrush(Color.FromArgb(15, 15, 18)))
			{
				g.FillPath(cb, clip);
			}
			g.SetClip(clip);
			Glow(g, W * 0.18f, -HeroH * 0.1f, W * 0.55f, HeroH * 1.05f, Theme.Accent, 46);
			if (ShowOrb)
			{
				Glow(g, W - OrbD / 2f - Theme.S(56f), HeroH / 2f, OrbD * 0.95f, OrbD * 0.95f, Theme.Blue, 26);
			}
			PaintHalftone(g);
			g.ResetClip();
			using GraphicsPath bp = Theme.Round(new RectangleF(0.5f, 0.5f, W - 1f, HeroH - 1f), Theme.S(24f));
			using Pen bpen = new Pen(Theme.Border, Theme.BW);
			g.DrawPath(bpen, bp);
		}
		int x0 = Theme.S(32f);
		int dy = (int)((1f - ap) * Theme.S(12f));
		int orbL = ShowOrb ? (W - OrbD - Theme.S(96f)) : (W - Theme.S(32f));
		int textR = Math.Min(orbL - Theme.S(20f), x0 + Theme.S(640f));
		// приветствие
		string greet = (_greet == "") ? "Добро пожаловать" : _greet;
		string eyebrow = greet + ((Line3 != "") ? ("  ·  " + Line3.Split('•')[0].Trim()) : "") + "  ·  " + Line2.Split('•')[0].Trim();
		Size es = Theme.Measure(eyebrow, Theme.Small);
		Rectangle er = new Rectangle(x0, Theme.S(26f) + dy, Math.Min(textR - x0, es.Width + Theme.S(36f)), Theme.S(28f));
		Box(g, er, er.Height / 2f, Color.FromArgb((int)(10 * ap), 255, 255, 255), Theme.Mix(Theme.Bg, Color.FromArgb(48, 48, 56), ap));
		Theme.Dot(g, er.X + Theme.S(14f), er.Y + er.Height / 2f, Theme.S(3.5f), Theme.Green);
		TextRenderer.DrawText(g, eyebrow, Theme.Small, new Rectangle(er.X + Theme.S(26f), er.Y, er.Width - Theme.S(32f), er.Height), Theme.Mix(Theme.Bg, Theme.Sub, ap), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		// перезагрузка
		_rebootR = Rectangle.Empty;
		if (NeedReboot)
		{
			string rs = "Перезагрузить, чтобы применить";
			int rw = Theme.Measure(rs, Theme.Small).Width + Theme.S(40f);
			if (er.Right + Theme.S(10f) + rw <= textR)
			{
				_rebootR = new Rectangle(er.Right + Theme.S(10f), er.Y, rw, er.Height);
				Box(g, _rebootR, er.Height / 2f, Color.FromArgb(_hot == "reboot" ? 40 : 22, Theme.Accent), Color.FromArgb(_hot == "reboot" ? 200 : 120, Theme.Accent));
				Theme.Dot(g, _rebootR.X + Theme.S(14f), _rebootR.Y + _rebootR.Height / 2f, Theme.S(3.5f), Theme.Accent);
				TextRenderer.DrawText(g, rs, Theme.Small, new Rectangle(_rebootR.X + Theme.S(26f), _rebootR.Y, rw - Theme.S(30f), _rebootR.Height), Theme.IceHi, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			}
		}
		// заголовок
		float big = (textR - x0 >= Theme.S(520f)) ? 31f : 25f;
		using (Font fH = Theme.F(big, semi: true))
		{
			int lh = Theme.Measure("Ag", fH).Height;
			int hy = er.Bottom + Theme.S(20f);
			HeadLine(g, "Твоя Windows", fH, x0 - Theme.S(2f), hy, Theme.Text, Theme.Text, ap);
			string pre = "в форме на ";
			string pct = Ready ? (_idx + "%") : "…";
			HeadLine(g, pre, fH, x0 - Theme.S(2f), hy + lh, Theme.Text, Theme.Text, ap);
			int pw = Theme.Measure(pre, fH).Width;
			HeadLine(g, pct, fH, x0 - Theme.S(2f) + pw, hy + lh, Theme.Accent, Theme.Orange, ap);
			// лид
			int ly = hy + lh * 2 + Theme.S(14f);
			string lead;
			if (!Ready)
			{
				lead = "Проверяю настройки системы — это займёт пару секунд.";
			}
			else if (_recBad > 0)
			{
				lead = string.Format(L.T("Предложений для проверки: {0}."), _recBad);
				if (_topTab != null) lead += " " + string.Format(L.T("Больше всего можно улучшить в разделе «{0}»."), L.T(_topTab));
			}
			else
			{
				lead = "Всё настроено так, как советует WinWeaver. Можно выдохнуть.";
			}
			int lw = Math.Min(textR - x0, Theme.S(560f));
			int lhh = Theme.Measure(lead, Theme.Body, lw, wrap: true).Height;
			TextRenderer.DrawText(g, lead, Theme.Body, new Rectangle(x0, ly, lw, lhh + 2), Theme.Mix(Theme.Bg, Theme.Sub, ap), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
			// кнопки
			int by = Math.Max(ly + lhh + Theme.S(20f), HeroH - Theme.S(72f));
			int bh = Theme.S(42f);
			string t1 = (Ready && _recBad == 0) ? "Рекомендованные" : "Довести до 100%";
			using Font fB = Theme.F(9.5f, semi: true);
			int b1w = Theme.Measure(t1, fB).Width + Theme.S(40f) + ((Ready && _recBad > 0) ? (Theme.Measure(_recBad.ToString(), fB).Width + Theme.S(18f)) : 0);
			_recR = new Rectangle(x0, by, b1w, bh);
			bool h1 = _hot == "rec";
			Box(g, _recR, Theme.S(12f), Ready ? (h1 ? Theme.IceHi : Theme.Accent) : Color.FromArgb(40, 40, 46), Color.Empty);
			int tx = _recR.X + Theme.S(20f);
			Size t1s = Theme.Measure(t1, fB);
			TextRenderer.DrawText(g, t1, fB, new Point(tx, _recR.Y + (bh - t1s.Height) / 2), Color.White, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			if (Ready && _recBad > 0)
			{
				string n = _recBad.ToString();
				Size ns = Theme.Measure(n, fB);
				Rectangle nr = new Rectangle(tx + t1s.Width + Theme.S(10f), _recR.Y + (bh - Theme.S(22f)) / 2, ns.Width + Theme.S(12f), Theme.S(22f));
				Box(g, nr, Theme.S(6f), Color.FromArgb(50, 0, 0, 0), Color.Empty);
				TextRenderer.DrawText(g, n, fB, nr, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			string t2 = Fixing ? "Применяю…" : "Что мешает FPS";
			int b2w = Theme.Measure(t2, fB).Width + Theme.S(40f);
			_viewR = new Rectangle(_recR.Right + Theme.S(10f), by, b2w, bh);
			bool h2 = _hot == "view";
			if (_viewR.Right <= textR)
			{
				Box(g, _viewR, Theme.S(12f), Color.FromArgb(h2 ? 22 : 10, 255, 255, 255), h2 ? Theme.BorderHi : Color.FromArgb(48, 48, 56));
				TextRenderer.DrawText(g, t2, fB, _viewR, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				string note = "доступный откат — в истории";
				Size nsz = Theme.Measure(note, Theme.Tiny);
				if (_viewR.Right + Theme.S(16f) + nsz.Width <= textR)
				{
					TextRenderer.DrawText(g, note, Theme.Tiny, new Point(_viewR.Right + Theme.S(16f), by + (bh - nsz.Height) / 2), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				}
			}
			else
			{
				_viewR = Rectangle.Empty;
			}
		}
		_fixR = Rectangle.Empty;
		_scoreR = Rectangle.Empty;
		if (ShowOrb)
		{
			PaintOrb(g, ap, W - OrbD - Theme.S(56f), (HeroH - OrbD) / 2);
		}
		else
		{
			_tagPrivR = _tagFpsR = _tagLookR = Rectangle.Empty;
		}
		PaintTiles(g, ap, HeroH + Theme.S(14f));
	}

	private void HeadLine(Graphics g, string text, Font f, int x, int y, Color c1, Color c2, float ap)
	{
		text = L.T(text);
		using GraphicsPath p = new GraphicsPath();
		float em = f.SizeInPoints * g.DpiY / 72f;
		p.AddString(text, f.FontFamily, (int)f.Style, em, new PointF(x, y), StringFormat.GenericTypographic);
		RectangleF b = p.GetBounds();
		if (b.Width <= 0f)
		{
			return;
		}
		using LinearGradientBrush br = new LinearGradientBrush(new RectangleF(b.X - 1f, b.Y, b.Width + 2f, b.Height), Theme.Mix(Theme.Bg, c1, ap), Theme.Mix(Theme.Bg, c2, ap), 0f);
		g.FillPath(br, p);
	}

	private void Arc(Graphics g, float cx, float cy, float r, float v, float thick, Color c, Color track)
	{
		RectangleF rr = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
		using (Pen tp = new Pen(track, thick))
		{
			g.DrawEllipse(tp, rr);
		}
		if (v > 0.003f)
		{
			using Pen p = new Pen(c, thick)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			g.DrawArc(p, rr, -90f, 360f * Math.Min(1f, v));
		}
	}

	private void PaintOrb(Graphics g, float ap, int ox, int oy)
	{
		float d = OrbD;
		float cx = ox + d / 2f;
		float cy = oy + d / 2f;
		float r0 = d / 2f - Theme.S(14f);
		using (Pen ring = new Pen(Color.FromArgb(22, 255, 255, 255), Theme.BW))
		{
			g.DrawEllipse(ring, cx - d / 2f, cy - d / 2f, d, d);
		}
		Color track = Color.FromArgb(18, 255, 255, 255);
		Arc(g, cx, cy, r0, _rO.Value * ap, Theme.S(9f), Theme.Accent, track);
		Arc(g, cx, cy, r0 - Theme.S(20f), _rP.Value * ap, Theme.S(6f), Theme.Green, track);
		Arc(g, cx, cy, r0 - Theme.S(36f), _rF.Value * ap, Theme.S(6f), Theme.Yellow, track);
		using (Font fN = Theme.F(40f, semi: true))
		{
			string n = Ready ? ((int)Math.Round(_rO.Value * 100f)).ToString() : "…";
			Size ns = Theme.Measure(n, fN);
			Size ss = Theme.Measure("/100", Theme.Body);
			int tw = ns.Width + (Ready ? ss.Width : 0);
			int nx = (int)(cx - tw / 2f);
			int ny = (int)(cy - ns.Height / 2f - Theme.S(8f));
			TextRenderer.DrawText(g, n, fN, new Point(nx, ny), Theme.Mix(Theme.Bg, Color.White, ap), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			if (Ready)
			{
				TextRenderer.DrawText(g, "/100", Theme.Body, new Point(nx + ns.Width + Theme.S(2f), ny + Theme.S(12f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			string lbl = "ИНДЕКС СИСТЕМЫ";
			Size ls = Theme.Measure(lbl, Theme.Tiny);
			TextRenderer.DrawText(g, lbl, Theme.Tiny, new Point((int)(cx - ls.Width / 2f), ny + ns.Height + Theme.S(4f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		_tagPrivR = Tag(g, "privacy", "Приватность", _priv, Theme.Green, (int)(ox - Theme.S(26f)), (int)(oy + d * 0.12f), ap);
		_tagFpsR = Tag(g, "view", "FPS", (_ok >= 0) ? _ok : (-1), Theme.Yellow, (int)(ox + d - Theme.S(30f)), (int)(oy + d * 0.40f), ap);
		_tagLookR = Tag(g, "ui", "Оформление", _look, Theme.Blue, (int)(ox - Theme.S(10f)), (int)(oy + d * 0.80f), ap);
	}

	private Rectangle Tag(Graphics g, string key, string text, int v, Color c, int x, int y, float ap)
	{
		string val = (v >= 0) ? v.ToString() : "…";
		using Font fv = Theme.F(8.5f, semi: true);
		int w = Theme.S(30f) + Theme.Measure(text, Theme.Small).Width + Theme.S(8f) + Theme.Measure(val, fv).Width + Theme.S(12f);
		x = Math.Min(x, base.Width - w - Theme.S(4f));
		Rectangle r = new Rectangle(x, y, w, Theme.S(30f));
		bool hot = _hot == "tag_" + key;
		Box(g, r, Theme.S(10f), Theme.Mix(Theme.Bg, hot ? Color.FromArgb(30, 30, 36) : Color.FromArgb(20, 20, 24), ap), Theme.Mix(Theme.Bg, hot ? Theme.BorderHi : Color.FromArgb(44, 44, 52), ap));
		Theme.Dot(g, r.X + Theme.S(15f), r.Y + r.Height / 2f, Theme.S(3.5f), c);
		Size ts = Theme.Measure(text, Theme.Small);
		TextRenderer.DrawText(g, text, Theme.Small, new Point(r.X + Theme.S(26f), r.Y + (r.Height - ts.Height) / 2), Theme.Mix(Theme.Bg, Theme.Text, ap), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		Size vs = Theme.Measure(val, fv);
		TextRenderer.DrawText(g, val, fv, new Point(r.Right - Theme.S(12f) - vs.Width, r.Y + (r.Height - vs.Height) / 2), Theme.Mix(Theme.Bg, Color.White, ap), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		return r;
	}

	private void PaintTiles(Graphics g, float ap, int y)
	{
		int W = base.Width;
		int gap = Theme.S(12f);
		int n = 4;
		int tw = (W - gap * (n - 1)) / n;
		string lvl = _chips[0].Text;
		string[] heads = new string[4] { "Процессор · видеокарта", "Память", "Диск", "Уровень автонастройки" };
		_histR = Rectangle.Empty;
		for (int i = 0; i < n; i++)
		{
			Rectangle r = new Rectangle(i * (tw + gap), y + (int)((1f - ap) * Theme.S(10f)), (i == n - 1) ? (W - i * (tw + gap)) : tw, TileH);
			bool hot = i == 3 && _hot == "history";
			Box(g, r, Theme.S(16f), Theme.Mix(Theme.Bg, hot ? Theme.CardHot1 : Theme.Card1, ap), Theme.Mix(Theme.Bg, hot ? Theme.BorderHi : Theme.Border, ap));
			int px = r.X + Theme.S(18f);
			int pw = r.Width - Theme.S(36f);
			TextRenderer.DrawText(g, Theme.Up(heads[i]).ToUpperInvariant(), Theme.Tiny, new Rectangle(px, r.Y + Theme.S(16f), pw, Theme.S(16f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			using Font fBig = Theme.F(13.5f, semi: true);
			int vy = r.Y + Theme.S(38f);
			switch (i)
			{
			case 0:
				TextRenderer.DrawText(g, Theme.FitOrShort(_cpu == "" ? "…" : ShortCpu(_cpu), Theme.Body, pw), Theme.Body, new Rectangle(px, vy, pw, Theme.S(22f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				TextRenderer.DrawText(g, _gpu == "" ? "…" : _gpu, Theme.Small, new Rectangle(px, vy + Theme.S(24f), pw, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				break;
			case 1:
			case 2:
			{
				string v = (i == 1) ? _ram : _disk;
				TextRenderer.DrawText(g, (v == "") ? "…" : v, fBig, new Rectangle(px, vy - Theme.S(2f), pw, Theme.S(28f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				float pv = (i == 1) ? _ramBar.Value : _diskBar.Value;
				string ps = "занято " + (int)Math.Round(pv * 100f) + "%";
				Size pss = Theme.Measure(ps, Theme.Tiny);
				int byy = r.Bottom - Theme.S(22f);
				RectangleF bar = new RectangleF(px, byy, pw - pss.Width - Theme.S(12f), Theme.S(6f));
				Box(g, bar, Theme.S(3f), Color.FromArgb(28, 28, 33), Color.Empty);
				if (pv > 0.01f)
				{
					RectangleF fill = new RectangleF(bar.X, bar.Y, Math.Max(bar.Height, bar.Width * pv), bar.Height);
					using GraphicsPath fp = Theme.Round(fill, Theme.S(3f));
					using LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(fill.X - 1f, fill.Y, fill.Width + 2f, fill.Height), (i == 1) ? Theme.Blue : Theme.Cyan, (pv > 0.85f) ? Theme.Accent : ((i == 1) ? Color.FromArgb(150, 160, 255) : Theme.Green), 0f);
					g.FillPath(lb, fp);
				}
				TextRenderer.DrawText(g, ps, Theme.Tiny, new Point(r.Right - Theme.S(18f) - pss.Width, byy + Theme.S(3f) - pss.Height / 2), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				break;
			}
			default:
			{
				TextRenderer.DrawText(g, lvl, fBig, new Rectangle(px, vy - Theme.S(2f), pw, Theme.S(28f)), (_chips[0].Color == Theme.Green) ? Theme.Text : Theme.Yellow, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				string hs = _chips[2].Text + "  ·  история →";
				_histR = r;
				TextRenderer.DrawText(g, hs, Theme.Small, new Rectangle(px, r.Bottom - Theme.S(32f), pw, Theme.S(20f)), hot ? Theme.Text : Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				break;
			}
			}
		}
	}

	private static string ShortCpu(string s)
	{
		return s.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "").Replace(" Processor", "").Replace("  ", " ").Trim();
	}

	private static string Plural(int n, string one, string few, string many)
	{
		int a = n % 100;
		int b = n % 10;
		if (a >= 11 && a <= 14)
		{
			return many;
		}
		if (b == 1)
		{
			return one;
		}
		if (b >= 2 && b <= 4)
		{
			return few;
		}
		return many;
	}

	private void PaintHalftone(Graphics g)
	{
		if (_halftone == null || _halftone.Width != Math.Max(1, base.Width) || _halftone.Height != Math.Max(1, HeroH))
		{
			_halftone?.Dispose();
			_halftone = new Bitmap(Math.Max(1, base.Width), Math.Max(1, HeroH), PixelFormat.Format32bppPArgb);
			using Graphics graphics = Graphics.FromImage(_halftone);
			graphics.Clear(Color.Transparent);
			Theme.Smooth(graphics);
			float step = Theme.S(22f);
			float ds = Theme.S(1.6f);
			using SolidBrush sb = new SolidBrush(Color.White);
			for (float yy = step / 2f; yy < HeroH; yy += step)
			{
				int a = (int)(16f * (1f - yy / HeroH));
				if (a < 2)
				{
					continue;
				}
				sb.Color = Color.FromArgb(a, 255, 255, 255);
				for (float xx = step / 2f; xx < base.Width; xx += step)
				{
					graphics.FillEllipse(sb, xx - ds / 2f, yy - ds / 2f, ds, ds);
				}
			}
		}
		g.DrawImageUnscaled(_halftone, 0, 0);
	}

	private string HitChip(Point pt)
	{
		if (!_rebootR.IsEmpty && _rebootR.Contains(pt))
		{
			return "reboot";
		}
		if (!_recR.IsEmpty && _recR.Contains(pt) && Ready)
		{
			return "rec";
		}
		if (!_viewR.IsEmpty && _viewR.Contains(pt) && !Fixing)
		{
			return "view";
		}
		if (!_tagPrivR.IsEmpty && _tagPrivR.Contains(pt))
		{
			return "tag_privacy";
		}
		if (!_tagFpsR.IsEmpty && _tagFpsR.Contains(pt))
		{
			return "tag_view";
		}
		if (!_tagLookR.IsEmpty && _tagLookR.Contains(pt))
		{
			return "tag_ui";
		}
		if (!_histR.IsEmpty && _histR.Contains(pt))
		{
			return "history";
		}
		return null;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		string text = HitChip(e.Location);
		if (text != _hot)
		{
			_hot = text;
			Cursor = ((text != null) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = null;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		string text = HitChip(e.Location);
		if (text != null && e.Button == MouseButtons.Left)
		{
			this.ChipClicked?.Invoke(text.StartsWith("tag_") ? text.Substring(4) : text);
		}
		base.OnMouseClick(e);
	}
}
