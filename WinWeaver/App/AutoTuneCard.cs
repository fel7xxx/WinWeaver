using System;

namespace WinWeaver;

/// <summary>
/// Автонастройка на «Главной»: три уровня на одной шкале, подробности выбранного
/// и одна кнопка «Применить». Данные и прогресс берутся из LevelCard.
/// </summary>
internal class AutoTuneCard : Card
{
	private readonly LevelCard[] _lv;

	private int _sel = -1;

	private int _hot = -1;

	private bool _btnHot;

	private readonly Rectangle[] _tileR = new Rectangle[3];

	private Rectangle _btnR;

	private Rectangle _trackR;

	private int _detailY;

	private readonly Tween _knob;

	private Timer _tick;

	private float _phase;

	private static readonly string[] Risks = new string[3] { "нет", "низкий", "средний" };

	private static readonly string[] Who = new string[3] { "Работа, учёба, «просто чтобы не следили»", "Большинству — дома и для игр", "Игровой ПК, где важен каждый кадр" };

	private static readonly Color[] Tones = new Color[3] { Theme.Blue, Theme.Accent, Theme.Orange };

	public AutoTuneCard(LevelCard[] levels)
	{
		_lv = levels;
		_knob = new Tween(this, 1f, 0.2f);
		foreach (LevelCard l in _lv)
		{
			l.Changed += OnLevelChanged;
		}
		Cursor = Cursors.Default;
	}

	private int Running
	{
		get
		{
			for (int i = 0; i < _lv.Length; i++)
			{
				if (_lv[i].Running)
				{
					return i;
				}
			}
			return -1;
		}
	}

	private int CurrentIdx
	{
		get
		{
			for (int i = 0; i < _lv.Length; i++)
			{
				if (_lv[i].Current)
				{
					return i;
				}
			}
			return -1;
		}
	}

	private int Sel
	{
		get
		{
			if (Running >= 0)
			{
				return Running;
			}
			if (_sel >= 0)
			{
				return _sel;
			}
			if (CurrentIdx >= 0)
			{
				return CurrentIdx;
			}
			for (int i = 0; i < _lv.Length; i++)
			{
				if (_lv[i].Recommended)
				{
					return i;
				}
			}
			return 0;
		}
	}

	private void OnLevelChanged()
	{
		bool run = Running >= 0;
		if (run && _tick == null && Anim.Enabled)
		{
			_tick = new Timer
			{
				Interval = 33
			};
			_tick.Tick += delegate
			{
				_phase = (float)(Anim.Now % 1670.0 / 1670.0);
				Invalidate();
				if (Running < 0)
				{
					_tick.Stop();
					_tick.Dispose();
					_tick = null;
				}
			};
			_tick.Start();
		}
		_knob.Target = Sel;
		Invalidate();
	}

	public override object Fetch(IEngine e)
	{
		return e.Str(e.Eval("Get-FLevelId"));
	}

	public override void Apply(object v)
	{
		foreach (LevelCard l in _lv)
		{
			l.Apply(v);
		}
		if (_sel < 0)
		{
			_knob.Snap(Sel);
		}
		Invalidate();
	}

	private int TileH => Theme.S(92f);

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad + Theme.S(4f);
		int top = pad + Theme.S(52f);
		int gap = Theme.S(12f);
		int tw = (w - pad * 2 - gap * 2) / 3;
		for (int i = 0; i < 3; i++)
		{
			_tileR[i] = new Rectangle(pad + i * (tw + gap), top, (i == 2) ? (w - pad - (pad + 2 * (tw + gap))) : tw, TileH);
		}
		_trackR = new Rectangle(pad, _tileR[0].Bottom + Theme.S(18f), w - pad * 2, Theme.S(12f));
		_detailY = _trackR.Bottom + Theme.S(26f);
		int ph = PointsH(w);
		int h = _detailY + Math.Max(ph, Theme.S(118f)) + pad;
		SetBounds(x, y, w, h);
		return h;
	}

	private int PointsW(int w)
	{
		int pad = base.Pad + Theme.S(4f);
		return (int)((w - pad * 2) * 0.58f) - Theme.S(28f);
	}

	private int PointsH(int w)
	{
		int h = Theme.Measure("Ag", Theme.Tiny).Height + Theme.S(12f);
		LevelCard l = _lv[Math.Max(0, Sel)];
		foreach (string p in l.Points)
		{
			h += Math.Max(Theme.S(20f), Theme.Measure(p, Theme.Small, PointsW(w), wrap: true).Height) + Theme.S(10f);
		}
		return h;
	}

	private void Box(Graphics g, RectangleF r, float rad, Color fill, Color border, float bw = -1f)
	{
		using GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f), rad);
		if (fill.A > 0)
		{
			using SolidBrush b = new SolidBrush(fill);
			g.FillPath(b, p);
		}
		if (border.A > 0)
		{
			using Pen pen = new Pen(border, (bw > 0f) ? bw : Theme.BW);
			g.DrawPath(pen, p);
		}
	}

	private void Pill(Graphics g, int right, int y, string text, Color c)
	{
		Size s = Theme.Measure(text, Theme.Tiny);
		Rectangle r = new Rectangle(right - s.Width - Theme.S(16f), y, s.Width + Theme.S(16f), Theme.S(20f));
		Box(g, r, r.Height / 2f, Color.FromArgb(26, c), Color.FromArgb(110, c));
		TextRenderer.DrawText(g, text, Theme.Tiny, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		int pad = base.Pad + Theme.S(4f);
		int W = base.Width;
		int sel = Sel;
		int cur = CurrentIdx;
		int run = Running;
		// заголовок
		using (Font fT = Theme.F(12f, semi: true))
		{
			TextRenderer.DrawText(g, "Автонастройка", fT, new Point(pad, pad - Theme.S(2f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		TextRenderer.DrawText(g, "Выбери глубину настройки. Проверь список изменений: удаление программ и очистка могут быть необратимы.", Theme.Small, new Rectangle(pad, pad + Theme.S(22f), W - pad * 2, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		// плитки уровней
		bool showTag = _tileR[0].Width >= Theme.S(200f);
		for (int i = 0; i < 3; i++)
		{
			LevelCard l = _lv[i];
			Rectangle r = _tileR[i];
			bool on = i == sel;
			bool hot = i == _hot && !on;
			Color tone = Tones[i];
			Box(g, r, Theme.S(14f), on ? Color.FromArgb(22, tone) : (hot ? Theme.CardHot1 : Color.FromArgb(16, 16, 19)), on ? Color.FromArgb(200, tone) : (hot ? Theme.BorderHi : Theme.Border), on ? Theme.S(1.5f) : (-1f));
			Rectangle ib = new Rectangle(r.X + Theme.S(16f), r.Y + Theme.S(16f), Theme.S(34f), Theme.S(34f));
			Box(g, ib, Theme.S(10f), Color.FromArgb(on ? 60 : 30, tone), Color.Empty);
			if (Theme.IconSmall != null)
			{
				TextRenderer.DrawText(g, l.GlyphText, Theme.IconSmall, ib, tone, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			else
			{
				for (int k = 0; k <= i; k++)
				{
					Theme.Dot(g, ib.X + ib.Width / 2f + (k - i / 2f) * Theme.S(8f), ib.Y + ib.Height / 2f, Theme.S(2.6f), tone);
				}
			}
			using (Font fN = Theme.F(10.5f, semi: true))
			{
				TextRenderer.DrawText(g, l.Title, fN, new Point(ib.Right + Theme.S(12f), ib.Y - Theme.S(1f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			string badge = (i == run) ? "идёт настройка" : ((i == cur) ? "сейчас" : (l.Recommended ? "советуем" : null));
			if (badge != null)
			{
				Pill(g, r.Right - Theme.S(12f), r.Y + Theme.S(12f), badge, (i == cur || i == run) ? Theme.Green : Theme.Sub);
			}
			string sub = showTag ? l.Tagline : (i == 0 ? "бережно" : (i == 1 ? "сбалансированно" : "максимум"));
			TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(ib.Right + Theme.S(12f), ib.Y + Theme.S(19f), r.Right - ib.Right - Theme.S(24f), Theme.S(18f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, Who[i], Theme.Tiny, new Rectangle(r.X + Theme.S(16f), r.Bottom - Theme.S(26f), r.Width - Theme.S(32f), Theme.S(16f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		// шкала глубины
		float y = _trackR.Y + _trackR.Height / 2f;
		float x0 = _tileR[0].X + _tileR[0].Width / 2f;
		float x2 = _tileR[2].X + _tileR[2].Width / 2f;
		float kx = x0 + (x2 - x0) * (_knob.Value / 2f);
		using (Pen tp = new Pen(Color.FromArgb(34, 34, 40), Theme.S(4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			g.DrawLine(tp, x0, y, x2, y);
		}
		using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(x0 - 1f, y - 2f, Math.Max(2f, kx - x0) + 2f, 4f), Theme.Blue, Tones[Math.Max(0, sel)], 0f))
		using (Pen fp = new Pen(lb, Theme.S(4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
		{
			if (kx > x0 + 1f)
			{
				g.DrawLine(fp, x0, y, kx, y);
			}
		}
		for (int i = 0; i < 3; i++)
		{
			float cx = _tileR[i].X + _tileR[i].Width / 2f;
			bool reached = i <= sel;
			using SolidBrush b = new SolidBrush(reached ? Tones[i] : Color.FromArgb(52, 52, 60));
			g.FillEllipse(b, cx - Theme.S(5f), y - Theme.S(5f), Theme.S(10f), Theme.S(10f));
		}
		using (SolidBrush kb = new SolidBrush(Color.White))
		{
			g.FillEllipse(kb, kx - Theme.S(8f), y - Theme.S(8f), Theme.S(16f), Theme.S(16f));
		}
		using (SolidBrush kb2 = new SolidBrush(Tones[Math.Max(0, sel)]))
		{
			g.FillEllipse(kb2, kx - Theme.S(4f), y - Theme.S(4f), Theme.S(8f), Theme.S(8f));
		}
		string[] depth = new string[3] { "Бережно", "Сбалансированно", "Максимум" };
		for (int i = 0; i < 3; i++)
		{
			Size ds = Theme.Measure(depth[i], Theme.Tiny);
			float cx = _tileR[i].X + _tileR[i].Width / 2f;
			int tx = (i == 0) ? (int)(cx - ds.Width / 2f) : (int)(cx - ds.Width / 2f);
			TextRenderer.DrawText(g, depth[i], Theme.Tiny, new Point(tx, (int)y + Theme.S(12f)), (i == sel) ? Theme.Text : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		// подробности выбранного
		LevelCard s = _lv[Math.Max(0, sel)];
		using (Pen lp = new Pen(Color.FromArgb(26, 255, 255, 255)))
		{
			g.DrawLine(lp, pad, _detailY - Theme.S(4f), W - pad, _detailY - Theme.S(4f));
		}
		int py = _detailY + Theme.S(12f);
		TextRenderer.DrawText(g, ("Что сделает «" + s.Title + "»").ToUpperInvariant(), Theme.Tiny, new Point(pad, py), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		py += Theme.Measure("Ag", Theme.Tiny).Height + Theme.S(12f);
		int pw = PointsW(W);
		for (int i = 0; i < s.Points.Length; i++)
		{
			bool plus = i == 0 && sel > 0;
			int ph = Math.Max(Theme.S(20f), Theme.Measure(s.Points[i], Theme.Small, pw, wrap: true).Height);
			Rectangle c = new Rectangle(pad, py + Theme.S(1f), Theme.S(18f), Theme.S(18f));
			Box(g, c, c.Height / 2f, plus ? Color.Empty : Color.FromArgb(40, Tones[Math.Max(0, sel)]), plus ? Theme.Border : Color.Empty);
			using (Pen ck = new Pen(plus ? Theme.Dim : Tones[Math.Max(0, sel)], Theme.S(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				if (plus)
				{
					float m = c.Width * 0.25f;
					g.DrawLine(ck, c.X + c.Width / 2f - m, c.Y + c.Height / 2f, c.X + c.Width / 2f + m, c.Y + c.Height / 2f);
					g.DrawLine(ck, c.X + c.Width / 2f, c.Y + c.Height / 2f - m, c.X + c.Width / 2f, c.Y + c.Height / 2f + m);
				}
				else
				{
					g.DrawLines(ck, new PointF[3]
					{
						new PointF(c.X + c.Width * 0.28f, c.Y + c.Height * 0.53f),
						new PointF(c.X + c.Width * 0.44f, c.Y + c.Height * 0.68f),
						new PointF(c.X + c.Width * 0.73f, c.Y + c.Height * 0.35f)
					});
				}
			}
			TextRenderer.DrawText(g, s.Points[i], Theme.Small, new Rectangle(pad + Theme.S(28f), py, pw, ph + 2), plus ? Theme.Sub : Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
			py += ph + Theme.S(10f);
		}
		// правая колонка: риск, кнопка/прогресс
		int rx = pad + (int)((W - pad * 2) * 0.62f);
		int rw = W - pad - rx;
		int ry = _detailY + Theme.S(12f);
		string[] keys = new string[2] { "Риск", "Подходит" };
		string[] vals = new string[2] { Risks[Math.Max(0, sel)], Who[Math.Max(0, sel)] };
		for (int i = 0; i < 2; i++)
		{
			TextRenderer.DrawText(g, keys[i].ToUpperInvariant(), Theme.Tiny, new Point(rx, ry), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, vals[i], Theme.Small, new Rectangle(rx + Theme.S(84f), ry - Theme.S(2f), rw - Theme.S(84f), Theme.S(18f)), (i == 0 && sel == 2) ? Theme.Yellow : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			ry += Theme.S(24f);
		}
		int bh = Theme.S(44f);
		_btnR = new Rectangle(rx, Math.Max(ry + Theme.S(8f), base.Height - pad - bh), rw, bh);
		if (run >= 0)
		{
			LevelCard rl = _lv[run];
			Rectangle r = _btnR;
			string t3 = (rl.StepN > 0) ? ("Шаг " + rl.StepI + " из " + rl.StepN) : "Запуск…";
			string t4 = (rl.StepN > 0) ? ((int)Math.Round(rl.ProgressValue * 100f) + "%") : "";
			TextRenderer.DrawText(g, t3, Theme.Tiny, new Rectangle(r.X, r.Y, r.Width, Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, t4, Theme.Tiny, new Rectangle(r.X, r.Y, r.Width, Theme.S(16f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
			Theme.Progress(g, new RectangleF(r.X, r.Y + Theme.S(19f), r.Width, Theme.S(6f)), rl.ProgressValue, _phase);
			TextRenderer.DrawText(g, rl.StepText, Theme.Tiny, new Rectangle(r.X, r.Y + Theme.S(28f), r.Width, Theme.S(16f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		else
		{
			bool again = sel == cur;
			string bt = again ? "Применить заново" : ("Применить «" + s.Title + "»");
			Color fill = again ? Color.FromArgb(_btnHot ? 34 : 22, 255, 255, 255) : (_btnHot ? Theme.IceHi : Theme.Accent);
			Box(g, _btnR, Theme.S(12f), fill, again ? Theme.BorderHi : Color.Empty);
			using Font fB = Theme.F(9.5f, semi: true);
			TextRenderer.DrawText(g, Theme.FitOrShort(bt, fB, _btnR.Width - Theme.S(20f)), fB, _btnR, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
	}

	private int HitTile(Point p)
	{
		for (int i = 0; i < 3; i++)
		{
			if (_tileR[i].Contains(p))
			{
				return i;
			}
		}
		if (_trackR.Height > 0 && p.Y >= _trackR.Y - Theme.S(10f) && p.Y <= _trackR.Bottom + Theme.S(24f))
		{
			int best = -1;
			float bd = float.MaxValue;
			for (int i = 0; i < 3; i++)
			{
				float d = Math.Abs(p.X - (_tileR[i].X + _tileR[i].Width / 2f));
				if (d < bd)
				{
					bd = d;
					best = i;
				}
			}
			return best;
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int h = HitTile(e.Location);
		bool bh = _btnR.Contains(e.Location) && Running < 0;
		if (h != _hot || bh != _btnHot)
		{
			_hot = h;
			_btnHot = bh;
			Invalidate();
		}
		Cursor c = (h >= 0 || bh) ? Cursors.Hand : Cursors.Default;
		if (Cursor != c)
		{
			Cursor = c;
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = -1;
		_btnHot = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && Running < 0)
		{
			int h = HitTile(e.Location);
			if (h >= 0)
			{
				_sel = h;
				_knob.Target = h;
				Page p = Parent as Page ?? Parent?.Parent as Page;
				int oldH = base.Height;
				DoLayout(base.Left, base.Top, base.Width);
				if (base.Height != oldH)
				{
					(FindPage())?.RelayoutSoon();
				}
				Invalidate();
			}
			else if (_btnR.Contains(e.Location))
			{
				_lv[Sel].Activate();
			}
		}
		base.OnMouseClick(e);
	}

	private Page FindPage()
	{
		Control c = Parent;
		while (c != null && !(c is Page))
		{
			c = c.Parent;
		}
		return c as Page;
	}
}
