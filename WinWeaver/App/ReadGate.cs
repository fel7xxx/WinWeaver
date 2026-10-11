using System;

namespace WinWeaver;

internal class ReadGate : Control
{
	private readonly FButton _btn;

	private readonly Timer _t = new Timer
	{
		Interval = 16
	};

	private float _ang;

	private bool _busy;

	public string PageTitle = "";

	public bool Busy
	{
		get
		{
			return _busy;
		}
		set
		{
			_busy = value;
			_btn.Visible = !value;
			if (value)
			{
				_t.Start();
			}
			else
			{
				_t.Stop();
			}
			Invalidate();
		}
	}

	private int CardW => Math.Min(Theme.S(480f), base.Width - Theme.S(40f));

	private int CardH => Theme.S(220f);

	private Rectangle CardRect => new Rectangle((base.Width - CardW) / 2, Math.Max(Theme.S(24f), base.Height / 2 - CardH / 2 - Theme.S(60f)), CardW, CardH);

	private Rectangle SpinRect
	{
		get
		{
			Rectangle cardRect = CardRect;
			int num = Theme.S(28f);
			return new Rectangle(cardRect.X + (cardRect.Width - num) / 2 - 2, cardRect.Bottom - Theme.S(58f) - 2, num + 4, num + 4);
		}
	}

	public event Action ReadClicked;

	public ReadGate()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
		_btn = new FButton
		{
			Text = "Прочитать раздел",
			Kind = BtnKind.Silver,
			Font = Theme.Body,
			Glyph = "\ue72c"
		};
		_btn.AutoWidth(Theme.S(26f));
		_btn.Click += delegate
		{
			if (!_busy)
			{
				this.ReadClicked?.Invoke();
			}
		};
		base.Controls.Add(_btn);
		_t.Tick += delegate
		{
			_ang = (_ang + 7f) % 360f;
			Invalidate(SpinRect);
		};
	}

	protected override void OnLayout(LayoutEventArgs levent)
	{
		base.OnLayout(levent);
		Rectangle cardRect = CardRect;
		_btn.Location = new Point(cardRect.X + (cardRect.Width - _btn.Width) / 2, cardRect.Bottom - Theme.S(62f));
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		PerformLayout();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		graphics.Clear(Theme.Bg);
		Rectangle cardRect = CardRect;
		using (GraphicsPath path = Theme.Round(cardRect, Theme.S(14f)))
		{
			Theme.FillGradient(graphics, path, cardRect, Theme.Card1, Theme.Card2);
			using Pen pen = new Pen(Theme.Border, 1f);
			graphics.DrawPath(pen, path);
		}
		int num = cardRect.Y + Theme.S(24f);
		if (Theme.Icon != null)
		{
			Theme.DrawIcon(graphics, "\ue946", Theme.Icon, new Rectangle(cardRect.X, num, cardRect.Width, Theme.S(26f)), Theme.Sub);
		}
		num += Theme.S(34f);
		TextFormatFlags textFormatFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
		TextRenderer.DrawText(graphics, _busy ? "Читаю раздел…" : "Раздел ещё не прочитан", Theme.Title, new Rectangle(cardRect.X + Theme.S(20f), num, cardRect.Width - Theme.S(40f), Theme.S(26f)), Theme.Text, textFormatFlags | TextFormatFlags.SingleLine);
		num += Theme.S(30f);
		string text = (_busy ? "Смотрю текущие настройки в системе — обычно несколько секунд" : "Чтобы показать текущие настройки, программа прочитает их из системы. Обычно это занимает несколько секунд.");
		TextRenderer.DrawText(graphics, text, Theme.Small, new Rectangle(cardRect.X + Theme.S(28f), num, cardRect.Width - Theme.S(56f), Theme.S(40f)), Theme.Sub, textFormatFlags);
		if (_busy)
		{
			Rectangle spinRect = SpinRect;
			spinRect.Inflate(-2, -2);
			using (Pen pen2 = new Pen(Color.FromArgb(30, 255, 255, 255), Theme.S(2.5f)))
			{
				graphics.DrawEllipse(pen2, spinRect);
			}
			using Pen pen3 = new Pen(Theme.Silver1, Theme.S(2.5f))
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			graphics.DrawArc(pen3, spinRect, _ang, 100f);
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
