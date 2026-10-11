using System;

namespace WinWeaver;

internal class LoadScreen : Control
{
	private readonly Timer _t = new Timer
	{
		Interval = 16
	};

	private readonly DateTime _start = DateTime.Now;

	private double _last = Anim.Now;

	private float _ang;

	private float _p;

	private float _shown;

	private float _target;

	private string _step = "Запускаю движок…";

	private string _hint = "";

	private float _fade = 1f;

	private bool _closing;

	private Action _closed;

	public Image Logo;

	private bool _canSkip;

	private float _skipA;

	private bool _skipHot;

	private Rectangle _skipR;

	public event Action Skip;

	public void AllowSkip()
	{
		if (!_closing)
		{
			_canSkip = true;
			if (!Anim.Enabled)
			{
				_skipA = 1f;
			}
			Invalidate();
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		bool flag = _canSkip && !_closing && _skipR.Contains(e.Location);
		if (flag != _skipHot)
		{
			_skipHot = flag;
			Cursor = (flag ? Cursors.Hand : Cursors.AppStarting);
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (_skipHot)
		{
			_skipHot = false;
			Cursor = Cursors.AppStarting;
			Invalidate();
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Left && _canSkip && !_closing && _skipR.Contains(e.Location))
		{
			_canSkip = false;
			_skipHot = false;
			Cursor = Cursors.Default;
			this.Skip?.Invoke();
		}
	}

	public LoadScreen()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
		Dock = DockStyle.Fill;
		Cursor = Cursors.AppStarting;
		_t.Tick += delegate
		{
			Step();
		};
		_t.Start();
	}

	public void SetStep(string step, float p, string hint = null)
	{
		_step = step ?? "";
		_stage = Math.Max(_stage, StageOf(_step));
		_target = Math.Max(_target, Math.Min(1f, p));
		if (hint != null)
		{
			_hint = hint;
		}
		if (!Anim.Enabled)
		{
			_shown = _target;
		}
		Invalidate();
	}

	public void Finish(Action closed)
	{
		_target = 1f;
		_step = "Готово";
		_stage = 3;
		_closing = true;
		_closed = closed;
		if (!Anim.Enabled)
		{
			Close();
		}
	}

	private void Close()
	{
		_t.Stop();
		base.Parent?.Controls.Remove(this);
		_closed?.Invoke();
		Dispose();
	}

	private void Step()
	{
		double now = Anim.Now;
		float num = Math.Min(40f, (float)(now - _last));
		_last = now;
		_ang = (_ang + num * 0.4f) % 1800f;
		if (_canSkip && _skipA < 1f)
		{
			_skipA = Math.Min(1f, _skipA + num / 300f);
		}
		_shown += (_target - _shown) * Math.Min(1f, num / 180f);
		if (_closing && _shown > 0.985f)
		{
			_fade -= num / 260f;
			if (_fade <= 0f)
			{
				Close();
				return;
			}
		}
		Invalidate();
	}

	private static readonly string[] Stages = { "Запуск движка", "Проверка системы", "Оценки и рекомендации" };

	private int _stage;

	private static int StageOf(string step)
	{
		string s = (step ?? "").ToLowerInvariant();
		if (s.Contains("готово")) return 3;
		if (s.Contains("оценк") || s.Contains("раздел")) return 2;
		if (s.Contains("систем")) return 1;
		return 0;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		Theme.Smooth(g);
		float fa = Math.Max(0f, Math.Min(1f, _fade));
		// Плавное исчезновение всего экрана: фон тоже прозрачнеет.
		using (SolidBrush bg = new SolidBrush(Color.FromArgb((int)(255f * fa), Theme.Bg)))
		{
			g.FillRectangle(bg, base.ClientRectangle);
		}
		int cx = base.Width / 2;
		int cy = base.Height / 2 - Theme.S(20f);
		using (GraphicsPath glow = new GraphicsPath())
		{
			int gr = Theme.S(260f);
			glow.AddEllipse(cx - gr, cy - gr - Theme.S(40f), gr * 2, gr * 2);
			using PathGradientBrush pg = new PathGradientBrush(glow)
			{
				CenterColor = Color.FromArgb((int)(22f * fa), Theme.Accent),
				SurroundColors = new Color[1] { Color.FromArgb(0, Theme.Accent) }
			};
			g.FillPath(pg, glow);
		}
		int a = (int)(255f * fa);
		// Карточка по центру — как карточки в приложении.
		int cw = Math.Min(Theme.S(380f), base.Width - Theme.S(60f));
		int ch = Theme.S(236f);
		Rectangle card = new Rectangle(cx - cw / 2, cy - ch / 2, cw, ch);
		using (GraphicsPath cp = Theme.Round(new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1f, card.Height - 1f), Theme.S(12f)))
		{
			using (SolidBrush cb = new SolidBrush(Color.FromArgb(a, Theme.Card1))) g.FillPath(cb, cp);
			using Pen pen = new Pen(Color.FromArgb(a, Theme.Border), Theme.BW);
			g.DrawPath(pen, cp);
		}
		int pad = Theme.S(22f);
		int x = card.X + pad;
		int y = card.Y + pad;
		int ls = Theme.S(36f);
		Rectangle lr = new Rectangle(x, y, ls, ls);
		using (GraphicsPath lp = Theme.Round(lr, Theme.S(9f)))
		{
			using (SolidBrush lb = new SolidBrush(Color.FromArgb(a, 28, 28, 33))) g.FillPath(lb, lp);
			using Pen lpen = new Pen(Color.FromArgb(a, Theme.Border), Theme.BW);
			g.DrawPath(lpen, lp);
		}
		if (Logo != null && fa > 0.02f)
		{
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.DrawImage(Logo, Rectangle.Inflate(lr, -Theme.S(6f), -Theme.S(6f)));
		}
		TextFormatFlags one = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
		using (Font tf = Theme.F(13f, semi: true))
		{
			TextRenderer.DrawText(g, "WinWeaver", tf, new Rectangle(lr.Right + Theme.S(12f), y - Theme.S(1f), cw - pad * 2 - ls - Theme.S(12f), Theme.S(22f)), Color.FromArgb(a, Theme.Text), one);
		}
		TextRenderer.DrawText(g, "Подготовка к работе", Theme.Small, new Rectangle(lr.Right + Theme.S(12f), y + Theme.S(20f), cw - pad * 2 - ls - Theme.S(12f), Theme.S(18f)), Color.FromArgb(a, Theme.Sub), one);
		y = lr.Bottom + Theme.S(20f);
		// Этапы со статусами, как кружки статуса у настроек.
		int rowH = Theme.S(28f);
		for (int i = 0; i < Stages.Length; i++)
		{
			float ccx = x + Theme.S(8f);
			float ccy = y + rowH / 2f;
			float r = Theme.S(7f);
			bool done = i < _stage;
			bool cur = i == _stage;
			if (done)
			{
				using SolidBrush db = new SolidBrush(Color.FromArgb(a, Theme.Green));
				g.FillEllipse(db, ccx - r, ccy - r, r * 2f, r * 2f);
				using Pen ck = new Pen(Color.FromArgb(a, 12, 12, 14), Theme.S(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
				g.DrawLines(ck, new PointF[3]
				{
					new PointF(ccx - r * 0.42f, ccy + r * 0.02f),
					new PointF(ccx - r * 0.1f, ccy + r * 0.34f),
					new PointF(ccx + r * 0.45f, ccy - r * 0.3f)
				});
			}
			else if (cur)
			{
				using (Pen tr = new Pen(Color.FromArgb(a / 4, Theme.Accent), Theme.S(1.6f))) g.DrawEllipse(tr, ccx - r, ccy - r, r * 2f, r * 2f);
				using Pen sp = new Pen(Color.FromArgb(a, Theme.Accent), Theme.S(1.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
				float sweep = 80f + 40f * (float)Math.Sin(_ang * Math.PI / 180.0);
				g.DrawArc(sp, ccx - r, ccy - r, r * 2f, r * 2f, _ang * 1.4f % 360f, sweep);
			}
			else
			{
				using Pen hp = new Pen(Color.FromArgb(a, 70, 70, 78), Theme.S(1.5f));
				g.DrawEllipse(hp, ccx - r + 0.75f, ccy - r + 0.75f, r * 2f - 1.5f, r * 2f - 1.5f);
			}
			Color tc = done ? Theme.Sub : (cur ? Theme.Text : Theme.Dim);
			TextRenderer.DrawText(g, Stages[i], Theme.Body, new Rectangle(x + Theme.S(26f), y, cw - pad * 2 - Theme.S(26f), rowH), Color.FromArgb(a, tc), one | TextFormatFlags.VerticalCenter);
			y += rowH;
		}
		y += Theme.S(14f);
		// Полоса прогресса акцентного цвета с бегущим бликом.
		int bw = cw - pad * 2;
		RectangleF br = new RectangleF(x, y, bw, Theme.S(4f));
		using (GraphicsPath bp = Theme.Round(br, Theme.S(2f)))
		{
			using SolidBrush tb = new SolidBrush(Color.FromArgb(a, 35, 35, 40));
			g.FillPath(tb, bp);
		}
		float fw = Math.Max(Theme.S(4f), br.Width * Math.Max(0f, Math.Min(1f, _shown)));
		RectangleF fr = new RectangleF(br.X, br.Y, fw, br.Height);
		using (GraphicsPath fp = Theme.Round(fr, Theme.S(2f)))
		{
			using (SolidBrush fb = new SolidBrush(Color.FromArgb(a, Theme.Accent))) g.FillPath(fb, fp);
			float sx = fr.X + (float)((_ang * 0.35f) % 140f) / 140f * (fw + Theme.S(60f)) - Theme.S(60f);
			Region old = g.Clip;
			g.SetClip(fp, CombineMode.Intersect);
			using (LinearGradientBrush sh = new LinearGradientBrush(new RectangleF(sx, fr.Y, Theme.S(60f), fr.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(a * 90 / 255, 255, 255, 255), 0f))
			{
				g.FillRectangle(sh, sx, fr.Y, Theme.S(30f), fr.Height);
			}
			using (LinearGradientBrush sh2 = new LinearGradientBrush(new RectangleF(sx + Theme.S(30f) - 1f, fr.Y, Theme.S(31f), fr.Height), Color.FromArgb(a * 90 / 255, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
			{
				g.FillRectangle(sh2, sx + Theme.S(30f), fr.Y, Theme.S(30f), fr.Height);
			}
			g.Clip = old;
			old.Dispose();
		}
		y += Theme.S(14f);
		string pct = (int)Math.Round(_shown * 100f) + "%";
		int pw = Theme.Measure(pct, Theme.Mono).Width + Theme.S(4f);
		string hint = string.IsNullOrEmpty(_hint) ? _step : _hint;
		TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(x, y, bw - pw - Theme.S(8f), Theme.S(16f)), Color.FromArgb(a, Theme.Dim), one);
		TextRenderer.DrawText(g, pct, Theme.Mono, new Rectangle(x, y, bw, Theme.S(16f)), Color.FromArgb(a, Theme.Sub), one | TextFormatFlags.Right);
		int skA = (int)(a * Math.Max(0f, Math.Min(1f, _skipA)));
		if (_canSkip || (_closing && _skipA > 0f))
		{
			string st = "Пропустить";
			int sw = Theme.Measure(st, Theme.Small).Width + Theme.S(32f);
			int sh0 = Theme.S(30f);
			_skipR = new Rectangle(cx - sw / 2, card.Bottom + Theme.S(16f), sw, sh0);
			using (GraphicsPath kp = Theme.Round(_skipR, Theme.S(8f)))
			{
				using (SolidBrush kb = new SolidBrush(Color.FromArgb(skA, _skipHot ? Color.FromArgb(34, 34, 40) : Theme.Card1))) g.FillPath(kb, kp);
				using Pen kpen = new Pen(Color.FromArgb(skA, Theme.Border), Theme.BW);
				g.DrawPath(kpen, kp);
			}
			TextRenderer.DrawText(g, st, Theme.Small, _skipR, Color.FromArgb(skA, _skipHot ? Theme.Text : Theme.Sub), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		else
		{
			TextRenderer.DrawText(g, "остальные разделы дочитаются в фоне", Theme.Tiny, new Rectangle(0, card.Bottom + Theme.S(16f), base.Width, Theme.S(18f)), Color.FromArgb(a * 3 / 4, Theme.Dim), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_t.Dispose();
		}
		base.Dispose(disposing);
	}
}
