using System;
using System.Collections;
using System.Globalization;

namespace WinWeaver;

internal class UpdPauseView : Card
{
	private const int Forever = 0;

	private const int Custom = -2;

	private static readonly int[] Days = new int[8] { 7, 14, 35, 91, 182, 365, 0, -2 };

	private static readonly string[] Names = new string[8] { "1 неделя", "2 недели", "5 недель", "3 месяца", "Полгода", "1 год", "Навсегда", "Свой срок…" };

	private const int N = 8;

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

	public static int ForeverDays => Math.Max(1, (int)Math.Floor((new DateTime(2077, 12, 31, 0, 0, 0) - DateTime.Now).TotalDays));

	private static CultureInfo Ru => L.Culture;

	private bool IsForever
	{
		get
		{
			if (_paused)
			{
				return _until.Year >= 2077;
			}
			return false;
		}
	}

	public event Action CustomClicked;

	public event Action<int> PauseChosen;

	public event Action ResumeClicked;

	private static int RealDays(int d)
	{
		if (d != 0)
		{
			return d;
		}
		return ForeverDays;
	}

	public UpdPauseView()
	{
		_fill = new Tween(this, 0f, 0.16f).Timed(800);
		_appear = new Tween(this, 0f, 0.14f);
		_resume = new FButton
		{
			Text = "Возобновить",
			Kind = BtnKind.Silver,
			Font = Theme.Body,
			Glyph = "\ue72c"
		};
		_resume.AutoWidth(Theme.S(30f));
		_resume.Visible = false;
		_resume.Click += delegate
		{
			if (!Busy)
			{
				this.ResumeClicked?.Invoke();
			}
		};
		base.Controls.Add(_resume);
	}

	public void Choose(int days)
	{
		if (!Busy && days > 0)
		{
			this.PauseChosen?.Invoke(days);
		}
	}

	public void SetPending(int days)
	{
		_pending = days;
		Invalidate();
	}

	private static DateTime ParseD(object o, DateTime def)
	{
		if (o != null && DateTime.TryParse(o.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return result;
		}
		return def;
	}

	public override object Fetch(IEngine e)
	{
		return e.Eval("Get-FUpdPause");
	}

	public override void Apply(object v)
	{
		if (v is Hashtable hashtable)
		{
			_now = ParseD(hashtable["Now"], DateTime.Now);
			_paused = hashtable["Paused"] is bool && (bool)hashtable["Paused"];
			_start = ParseD(hashtable["Start"], _now);
			_until = ParseD(hashtable["Until"], _now);
			_pending = -1;
			bool loaded = _loaded;
			_loaded = true;
			_resume.Visible = _paused;
			float target = 0f;
			if (_paused)
			{
				double num = Math.Max(1.0, (_until - _start).TotalMinutes);
				target = (float)Math.Max(0.02, Math.Min(1.0, (_until - _now).TotalMinutes / num));
			}
			if (!loaded)
			{
				_fill.Snap(0f);
				_appear.Target = 1f;
			}
			_fill.Target = target;
			LayoutAgain();
		}
	}

	private void LayoutAgain()
	{
		if (base.Parent is Canvas)
		{
			((Canvas)base.Parent).Page.RelayoutSoon();
		}
		Invalidate();
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(48f));
		_resume.Location = new Point(w - pad - _resume.Width, _headR.Y + (_headR.Height - _resume.Height) / 2);
		int y2 = _headR.Bottom + Theme.S(18f);
		_barR = new Rectangle(pad, y2, w - pad * 2, Theme.S(58f));
		y2 = _barR.Bottom + Theme.S(16f);
		int num = Theme.S(8f);
		int num2 = ((w - pad * 2 >= Theme.S(960f)) ? 8 : 4);
		int num3 = (w - pad * 2 - num * (num2 - 1)) / num2;
		int num4 = Theme.S(56f);
		for (int i = 0; i < 8; i++)
		{
			int num5 = i % num2;
			int num6 = i / num2;
			_chipR[i] = new Rectangle(pad + num5 * (num3 + num), y2 + num6 * (num4 + num), (num5 == num2 - 1) ? (w - pad * 2 - (num3 + num) * (num2 - 1)) : num3, num4);
		}
		y2 = _chipR[7].Bottom + Theme.S(12f);
		_noteR = new Rectangle(pad, y2, w - pad * 2, Theme.S(20f));
		int num7 = _noteR.Bottom + pad - Theme.S(4f);
		SetBounds(x, y, w, num7);
		return num7;
	}

	private int CurrentChip()
	{
		if (!_paused)
		{
			return -1;
		}
		return CurrentChipIdx((_until - _start).TotalDays);
	}

	private int CurrentChipFor(int days)
	{
		int num = CurrentChipIdx(days);
		if (num < 0)
		{
			return -2;
		}
		return Days[num];
	}

	private int CurrentChipIdx(double total)
	{
		if (_paused && _until.Year >= 2077 && total == (_until - _start).TotalDays)
		{
			return Array.IndexOf(Days, 0);
		}
		for (int i = 0; i < 8; i++)
		{
			if (Days[i] > 0 && Math.Abs(total - (double)Days[i]) < 0.6)
			{
				return i;
			}
		}
		if (total >= (double)(ForeverDays - 1))
		{
			return Array.IndexOf(Days, 0);
		}
		return Array.IndexOf(Days, -2);
	}

	private new static string Left(TimeSpan t)
	{
		if (t.TotalHours < 1.0)
		{
			return "меньше часа";
		}
		if (t.TotalDays < 1.0)
		{
			return (int)t.TotalHours + " ч";
		}
		int num = (int)t.TotalDays;
		int hours = t.Hours;
		if (num >= 730)
		{
			int n = num / 365;
			return "≈ " + n + " " + InstalledView.Plural(n, "год", "года", "лет");
		}
		if (num >= 62)
		{
			int n2 = (int)Math.Round((double)num / 30.44);
			return "≈ " + n2 + " " + InstalledView.Plural(n2, "месяц", "месяца", "месяцев");
		}
		return num + " " + InstalledView.Plural(num, "день", "дня", "дней") + ((hours > 0 && num < 7) ? (" " + hours + " ч") : "");
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		int num = Theme.S(44f);
		Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - num) / 2, num, num), "\ue916", _paused);
		int num2 = _headR.X + num + Theme.S(14f);
		int num3 = (_paused ? _resume.Left : _headR.Right) - Theme.S(12f);
		string text = ((!_loaded) ? "Пауза обновлений" : (_paused ? "Обновления на паузе" : "Пауза обновлений"));
		string text2 = ((!_loaded) ? "Проверяю…" : ((!_paused) ? "Обновления приходят как обычно. Выбери срок ниже — пауза включится сразу" : (IsForever ? "Обновления выключены «навсегда» — до 2077 года" : ("Ничего не скачивается до " + _until.ToString((_until.Year != _now.Year) ? "d MMMM yyyy" : "dddd, d MMMM", Ru) + ", " + _until.ToString("HH:mm")))));
		int height = Theme.Measure("Ag", Theme.Title).Height;
		int num4 = _headR.Y + (_headR.Height - height - Theme.S(22f)) / 2;
		TextRenderer.DrawText(g, text, Theme.Title, new Rectangle(num2, num4, num3 - num2, height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, text2, Theme.Small, new Rectangle(num2, num4 + height + Theme.S(4f), num3 - num2, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		Rectangle barR = _barR;
		using (GraphicsPath path = Theme.Round(barR, Theme.S(12f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		int num5 = Theme.S(16f);
		Rectangle rectangle = new Rectangle(barR.X + num5, barR.Bottom - Theme.S(18f), barR.Width - num5 * 2, Theme.S(6f));
		using (GraphicsPath path2 = Theme.Round(rectangle, (float)rectangle.Height / 2f))
		{
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(34, 255, 255, 255));
			g.FillPath(brush2, path2);
		}
		float value = _fill.Value;
		if (value > 0.005f)
		{
			RectangleF r = new RectangleF(rectangle.X, rectangle.Y, Math.Max(rectangle.Height, (float)rectangle.Width * value), rectangle.Height);
			using GraphicsPath path3 = Theme.Round(r, (float)rectangle.Height / 2f);
			Theme.FillGradient(g, path3, r, Theme.Yellow, Theme.Mix(Theme.Yellow, Theme.Silver1, 0.5f), 0f);
		}
		int y = barR.Y + Theme.S(10f);
		if (!_loaded)
		{
			TextRenderer.DrawText(g, "…", Theme.Body, new Point(rectangle.X, y), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		else if (_paused)
		{
			TimeSpan t = _until - _now;
			using (Font font = Theme.F(12.5f, semi: true))
			{
				string text3 = (IsForever ? "Пауза до 2077 года" : ("Осталось " + Left(t)));
				TextRenderer.DrawText(g, text3, font, new Point(rectangle.X, y), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			string text4 = ((_until.Year != _start.Year) ? "d MMM yyyy" : "d MMM");
			string text5 = "с " + _start.ToString(text4, Ru) + "  →  до " + _until.ToString(text4, Ru);
			TextRenderer.DrawText(g, text5, Theme.Small, new Rectangle(rectangle.X, y, rectangle.Width, Theme.S(22f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
		}
		else
		{
			TextRenderer.DrawText(g, "Пауза не стоит", Theme.Body, new Point(rectangle.X, y), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, "от недели до «навсегда»", Theme.Small, new Rectangle(rectangle.X, y, rectangle.Width, Theme.S(22f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
		}
		int num6 = CurrentChip();
		for (int i = 0; i < 8; i++)
		{
			Rectangle rectangle2 = _chipR[i];
			int val = RealDays(Days[i]);
			DateTime dateTime = DateTime.Now.AddDays(Math.Max(0, val));
			bool flag = i == num6;
			bool flag2 = _pending >= 0 && ((Days[i] == -2) ? (CurrentChipFor(_pending) == -2) : (RealDays(Days[i]) == _pending));
			bool flag3 = i == _hot && !Busy;
			using (GraphicsPath path4 = Theme.Round(rectangle2, Theme.S(10f)))
			{
				using (SolidBrush brush3 = new SolidBrush((flag || flag2) ? Color.FromArgb(40, Theme.Yellow) : (flag3 ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(12, 255, 255, 255))))
				{
					g.FillPath(brush3, path4);
				}
				using Pen pen2 = new Pen((flag || flag2) ? Color.FromArgb(150, Theme.Yellow) : (flag3 ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(34, 255, 255, 255)));
				g.DrawPath(pen2, path4);
			}
			Color fore = ((Busy && !flag2) ? Theme.Dim : Theme.Text);
			TextRenderer.DrawText(g, Names[i], Theme.Body, new Rectangle(rectangle2.X, rectangle2.Y + Theme.S(9f), rectangle2.Width, Theme.S(20f)), fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			string text6 = ((!flag2) ? ((Days[i] != -2) ? ((!flag) ? ((Days[i] != 0) ? ("до " + dateTime.ToString((dateTime.Year != DateTime.Now.Year) ? "d MMM yyyy" : "d MMM", Ru)) : "до конца 2077 года") : "сейчас") : (flag ? ("сейчас • до " + _until.ToString("d MMM yyyy", Ru)) : "любой срок")) : "включаю…");
			TextRenderer.DrawText(g, text6, Theme.Tiny, new Rectangle(rectangle2.X, rectangle2.Y + Theme.S(31f), rectangle2.Width, Theme.S(18f)), (flag || flag2) ? Theme.Yellow : Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		TextRenderer.DrawText(g, "Пауза останавливает и исправления безопасности. Когда срок выйдет, Windows сама скачает всё, что накопилось.", Theme.Tiny, _noteR, Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		for (int i = 0; i < 8; i++)
		{
			if (_chipR[i].Contains(e.Location))
			{
				num = i;
			}
		}
		Cursor = ((num >= 0 && !Busy) ? Cursors.Hand : Cursors.Default);
		if (num != _hot)
		{
			_hot = num;
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
		for (int i = 0; i < 8; i++)
		{
			if (_chipR[i].Contains(e.Location))
			{
				if (Days[i] == -2)
				{
					this.CustomClicked?.Invoke();
				}
				else
				{
					this.PauseChosen?.Invoke(RealDays(Days[i]));
				}
				break;
			}
		}
	}
}
