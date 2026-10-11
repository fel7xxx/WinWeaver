using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class LevelCard : Card
{
	public readonly string Key;

	public readonly string Title;

	public readonly string Tagline;

	public readonly string GlyphText;

	public readonly string[] Points;

	public readonly bool Recommended;

	public string Caution;

	public readonly FButton Btn;

	public bool Current;

	private Rectangle _titleR;

	private Rectangle _tagR;

	private readonly List<Rectangle> _pointR = new List<Rectangle>();

	private int _iconBox;

	private bool _running;

	private float _phase;

	private string _step = "";

	private int _i;

	private int _n;

	private readonly Tween _prog;

	private readonly Tween _runT;

	private Timer _shimmer;

	private Rectangle _meterR;

	private int _divY;

	private static readonly Font TitleBig = Theme.F(12f, semi: true);

	public bool Running => _running;

	private int Depth
	{
		get
		{
			if (!(Key == "light"))
			{
				if (!(Key == "gamer"))
				{
					return 2;
				}
				return 3;
			}
			return 1;
		}
	}

	private string DepthText
	{
		get
		{
			if (Depth != 1)
			{
				if (Depth != 3)
				{
					return "Сбалансированно";
				}
				return "Максимум";
			}
			return "Бережно";
		}
	}

	private Color Accent
	{
		get
		{
			if (!Current || _running)
			{
				return Theme.Silver1;
			}
			return Theme.Green;
		}
	}

	public event Action<LevelCard> Activated;

	/// <summary>Состояние изменилось (прогресс, «применена») — для внешнего вида AutoTuneCard.</summary>
	public event Action Changed;

	public string StepText => _step;

	public int StepI => _i;

	public int StepN => _n;

	public float ProgressValue => _prog.Value;

	public void Activate()
	{
		Fire();
	}

	public LevelCard(string key, string glyph, string title, string tagline, string[] points, bool recommended)
	{
		Key = key;
		GlyphText = glyph;
		Title = title;
		Tagline = tagline;
		Points = points ?? new string[0];
		Recommended = recommended;
		HoverFx = true;
		Cursor = Cursors.Hand;
		_prog = new Tween(this, 0f, 0.15f).Timed(450);
		_runT = new Tween(this);
		Btn = new FButton
		{
			Text = "Настроить…",
			Kind = ((!recommended) ? BtnKind.Ghost : BtnKind.Silver),
			Font = Theme.Body,
			Glyph = "\ue945"
		};
		Btn.Click += delegate
		{
			Fire();
		};
		base.Controls.Add(Btn);
	}

	private void Fire()
	{
		if (!_running)
		{
			this.Activated?.Invoke(this);
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			Fire();
		}
		base.OnMouseClick(e);
	}

	public void StartProgress()
	{
		_running = true;
		_i = 0;
		_n = 0;
		_step = "Подготовка…";
		_prog.Snap(0f);
		_runT.Target = 1f;
		Btn.Visible = false;
		Cursor = Cursors.AppStarting;
		if (_shimmer == null)
		{
			_shimmer = new Timer
			{
				Interval = 33
			};
			_shimmer.Tick += delegate
			{
				_phase = (float)(Anim.Now % 1670.0 / 1670.0);
				Invalidate();
			};
		}
		if (Anim.Enabled)
		{
			_shimmer.Start();
		}
		Invalidate();
		Changed?.Invoke();
	}

	public void SetProgress(int i, int n, string step)
	{
		if (_running)
		{
			_i = i;
			_n = n;
			if (!string.IsNullOrEmpty(step))
			{
				_step = step;
			}
			_prog.Target = ((n > 0) ? Math.Max(0.02f, ((float)i - 0.5f) / (float)n) : 0f);
			Invalidate();
			Changed?.Invoke();
		}
	}

	public void EndProgress()
	{
		_running = false;
		_shimmer?.Stop();
		_runT.Snap(0f);
		Btn.Visible = true;
		Cursor = Cursors.Hand;
		Invalidate();
		Changed?.Invoke();
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		_iconBox = Theme.S(46f);
		int width = Math.Max(Theme.S(60f), w - pad * 2);
		int y2 = pad + _iconBox + Theme.S(14f);
		int height = Theme.Measure(Theme.Up(Title), TitleBig, width, wrap: true).Height;
		_titleR = new Rectangle(pad, y2, width, height);
		int height2 = Math.Max(Theme.Measure(Tagline, Theme.Small, width, wrap: true).Height, Theme.Measure("A\nA", Theme.Small, width, wrap: true).Height);
		_tagR = new Rectangle(pad, _titleR.Bottom + Theme.S(3f), width, height2);
		y2 = _tagR.Bottom + Theme.S(18f);
		int num = pad + Theme.S(28f);
		int width2 = Math.Max(Theme.S(60f), w - num - pad);
		_pointR.Clear();
		string[] points = Points;
		for (int i = 0; i < points.Length; i++)
		{
			int height3 = Theme.Measure(points[i], Theme.Small, width2, wrap: true).Height;
			_pointR.Add(new Rectangle(num, y2, width2, height3));
			y2 += Math.Max(height3, Theme.S(18f)) + Theme.S(10f);
		}
		_divY = y2 + Theme.S(4f);
		_meterR = new Rectangle(pad, _divY + Theme.S(14f), width, Theme.S(16f));
		int num2 = Theme.S(40f);
		int num3 = _meterR.Bottom + Theme.S(16f) + num2 + pad;
		SetBounds(x, y, w, num3);
		PlaceButton();
		Invalidate();
		return num3;
	}

	private void PlaceButton()
	{
		int pad = base.Pad;
		int num = Theme.S(40f);
		Btn.SetBounds(pad, base.Height - pad - num, Math.Max(Theme.S(80f), base.Width - pad * 2), num);
		_meterR = new Rectangle(pad, base.Height - pad - num - Theme.S(16f) - Theme.S(16f), Math.Max(Theme.S(60f), base.Width - pad * 2), Theme.S(16f));
		_divY = _meterR.Y - Theme.S(14f);
	}

	protected override void OnSizeChanged(EventArgs e)
	{
		base.OnSizeChanged(e);
		PlaceButton();
	}

	public override object Fetch(IEngine e)
	{
		return e.Str(e.Eval("Get-FLevelId"));
	}

	public override void Apply(object v)
	{
		bool flag = string.Equals(v as string, Key, StringComparison.OrdinalIgnoreCase);
		if (flag != Current)
		{
			Current = flag;
			Invalidate();
			Changed?.Invoke();
		}
	}

	public override void PaintSurface(Graphics g)
	{
		base.PaintSurface(g);
		float value = HotT.Value;
		Theme.Smooth(g);
		if (!Current && !_running && !Recommended && !(value > 0.01f))
		{
			return;
		}
		using GraphicsPath path = Theme.Round(new RectangleF(1f, 1f, (float)base.Width - 2.5f, (float)base.Height - 2.5f), Theme.S(21f));
		if (Current || _running)
		{
			using LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, base.Width, base.Height), Color.FromArgb(24, Theme.Accent), Color.FromArgb(0, Theme.Accent), 90f);
			g.FillPath(lg, path);
		}
		using Pen pen = new Pen((Current || _running) ? Theme.Accent : Theme.Mix(Recommended ? Theme.BorderHi : Theme.Border, Color.FromArgb(72, 72, 82), value), Theme.BW);
		g.DrawPath(pen, path);
	}

	private static void Check(Graphics g, Rectangle c, Color col, bool plus)
	{
		using (Pen pen = new Pen(plus ? Theme.Border : Theme.BorderHi, Theme.BW))
		{
			g.DrawEllipse(pen, c);
		}
		using Pen pen2 = new Pen(col, Math.Max(1.4f, Theme.S(1.6f)))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		if (plus)
		{
			float num = (float)c.Width * 0.24f;
			PointF pointF = new PointF((float)c.X + (float)c.Width / 2f, (float)c.Y + (float)c.Height / 2f);
			g.DrawLine(pen2, pointF.X - num, pointF.Y, pointF.X + num, pointF.Y);
			g.DrawLine(pen2, pointF.X, pointF.Y - num, pointF.X, pointF.Y + num);
		}
		else
		{
			g.DrawLines(pen2, new PointF[3]
			{
				new PointF((float)c.X + (float)c.Width * 0.28f, (float)c.Y + (float)c.Height * 0.53f),
				new PointF((float)c.X + (float)c.Width * 0.44f, (float)c.Y + (float)c.Height * 0.68f),
				new PointF((float)c.X + (float)c.Width * 0.73f, (float)c.Y + (float)c.Height * 0.35f)
			});
		}
	}

	protected override void PaintContent(Graphics g)
	{
		int pad = base.Pad;
		float value = HotT.Value;
		Theme.Smooth(g);
		Rectangle r = new Rectangle(pad, pad - (int)(value * (float)Theme.S(2f)), _iconBox, _iconBox);
		Theme.IconBox(g, r, GlyphText, Recommended || Current, value);
		string text = (_running ? "Идёт настройка" : (Current ? "Применена" : (Recommended ? "Рекомендуем" : Caution)));
		if (text != null)
		{
			int num = Theme.PillW(text, Theme.Tiny);
			Rectangle r2 = new Rectangle(base.Width - pad - num, pad + (_iconBox - Theme.S(22f)) / 2, num, Theme.S(22f));
			Theme.Pill(g, r2, text, Theme.Tiny, (text == Caution && !Current && !_running) ? Theme.IceHi : ((Current || _running) ? Theme.Green : Theme.Sub));
		}
		TextRenderer.DrawText(g, Theme.Up(Title), TitleBig, _titleR, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		TextRenderer.DrawText(g, Tagline, Theme.Small, _tagR, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		using (Pen pen = new Pen(Color.FromArgb(26, 255, 255, 255)))
		{
			g.DrawLine(pen, pad, _divY, base.Width - pad, _divY);
		}
		float num2 = Theme.S(5.5f);
		float num3 = Theme.S(4f);
		int num4 = 15;
		float num5 = (float)_meterR.Right - ((float)num4 * num2 + (float)(num4 - 1) * num3);
		string text2 = Theme.Up("Глубина");
		int num6 = Theme.Measure(text2, Theme.Tiny).Width + Theme.S(6f);
		TextRenderer.DrawText(g, text2, Theme.Tiny, new Rectangle(_meterR.X, _meterR.Y, num6 + Theme.S(4f), _meterR.Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, Theme.Up(DepthText), Theme.Tiny, new Rectangle(_meterR.X + num6, _meterR.Y, Math.Max(10, (int)num5 - _meterR.X - num6 - Theme.S(8f)), _meterR.Height), Theme.Mix(Theme.Sub, Theme.Text, value), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		Theme.DotBar(g, num5, (float)_meterR.Y + (float)_meterR.Height / 2f, num4, Depth * 5, num2, num3);
		int height = Theme.Measure("Ag", Theme.Small).Height;
		for (int i = 0; i < _pointR.Count && i < Points.Length; i++)
		{
			Rectangle r3 = _pointR[i];
			bool flag = i == 0 && Key != "light";
			int num7 = Theme.S(18f);
			Rectangle c = new Rectangle(pad, r3.Top + (height - num7) / 2, num7, num7);
			Check(g, c, flag ? Theme.Dim : Color.White, flag);
			TextRenderer.DrawText(g, Points[i], Theme.Small, r3, flag ? Theme.Sub : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		}
		if (_running)
		{
			int num8 = Theme.S(40f);
			Rectangle rectangle = new Rectangle(pad, base.Height - pad - num8, base.Width - pad * 2, num8);
			string text3 = ((_n > 0) ? ("Шаг " + _i + " из " + _n) : "Запуск…");
			string text4 = ((_n > 0) ? ((int)Math.Round(_prog.Value * 100f) + "%") : "");
			TextRenderer.DrawText(g, text3, Theme.Tiny, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, text4, Theme.Tiny, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, Theme.S(16f)), Theme.Silver1, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
			Theme.Progress(g, new RectangleF(rectangle.X, rectangle.Y + Theme.S(19f), rectangle.Width, Theme.S(6f)), _prog.Value, _phase);
			TextRenderer.DrawText(g, _step, Theme.Tiny, new Rectangle(rectangle.X, rectangle.Y + Theme.S(27f), rectangle.Width, Theme.S(16f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
	}
}
