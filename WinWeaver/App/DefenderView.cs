using System;
using System.Collections;

namespace WinWeaver;

internal class DefenderView : Card
{
	private static readonly string[] ModeNames = new string[3] { "Включён", "Без реального времени", "Выключен полностью" };

	private static readonly string[] ModeHints = new string[3] { "полная защита Windows", "сканирует по запросу, не мешает играм", "службы остановлены, без антивируса" };

	private bool _loaded;

	private int _mode;

	private bool _rtp;

	private bool _tamper;

	private bool _cloud;

	private bool _pua;

	private int _sigAge = -1;

	private int _scanAge = -1;

	private string _note = "";

	private string _other = "";

	private Rectangle _headR;

	private readonly Rectangle[] _modeR = new Rectangle[3];

	private readonly Rectangle[] _tileR = new Rectangle[4];

	private Rectangle _noteR;

	private int _hot = -1;

	private readonly FButton _sig;

	private readonly FButton _scan;

	private readonly FButton _settings;

	private int ModeCount
	{
		get
		{
			if (!Prefs.Expert && _mode != 2)
			{
				return 2;
			}
			return 3;
		}
	}

	public event Action<int> ModeClicked;

	public event Action<string> ToolClicked;

	public DefenderView()
	{
		_sig = Btn("Обновить базы", "sig", "\ue895");
		_scan = Btn("Быстрая проверка", "scan", "\ue721");
		_settings = Btn("Настройки защиты", "settings", null);
	}

	private FButton Btn(string text, string key, string glyph)
	{
		FButton fButton = new FButton
		{
			Text = text,
			Kind = BtnKind.Ghost,
			Font = Theme.Body,
			Glyph = glyph
		};
		fButton.AutoWidth(Theme.S(30f));
		fButton.Click += delegate
		{
			this.ToolClicked?.Invoke(key);
		};
		base.Controls.Add(fButton);
		return fButton;
	}

	public override object Fetch(IEngine e)
	{
		return e.Eval("Get-FDefInfo");
	}

	private static int Int(object o, int d)
	{
		try
		{
			return (o == null) ? d : Convert.ToInt32(o);
		}
		catch
		{
			return d;
		}
	}

	public override void Apply(object v)
	{
		if (v is Hashtable hashtable)
		{
			int modeCount = ModeCount;
			_mode = Math.Max(0, Math.Min(2, Int(hashtable["Mode"], 0)));
			_rtp = hashtable["Rtp"] is bool && (bool)hashtable["Rtp"];
			_tamper = hashtable["Tamper"] is bool && (bool)hashtable["Tamper"];
			_cloud = hashtable["Cloud"] is bool && (bool)hashtable["Cloud"];
			_pua = hashtable["Pua"] is bool && (bool)hashtable["Pua"];
			_sigAge = Int(hashtable["SigAge"], -1);
			_scanAge = Int(hashtable["ScanAge"], -1);
			_note = (hashtable["Note"] ?? "").ToString();
			_other = (hashtable["Other"] ?? "").ToString();
			bool num = _noteR.Height > 0;
			_loaded = true;
			_sig.Enabled = _mode != 2;
			_scan.Enabled = _mode != 2;
			Skeleton.Track(this, loading: false);
			if ((num != (Note() != "") || modeCount != ModeCount) && base.Parent is Canvas)
			{
				((Canvas)base.Parent).Page.RelayoutSoon();
			}
			Invalidate();
		}
	}

	private string Note()
	{
		if (!_loaded)
		{
			return "";
		}
		if (_note != "")
		{
			return _note;
		}
		if (_other != "")
		{
			return "Основной антивирус — " + _other + ". Defender работает в пассивном режиме.";
		}
		if (_tamper && _mode == 0)
		{
			return "Чтобы выключить Defender, сначала отключи «Защиту от подделки» — кнопка «Настройки защиты».";
		}
		return "";
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(48f));
		int num = _headR.Bottom + Theme.S(16f);
		int num2 = Theme.S(8f);
		int modeCount = ModeCount;
		int num3 = (w - pad * 2 - num2 * (modeCount - 1)) / modeCount;
		for (int i = 0; i < 3; i++)
		{
			_modeR[i] = ((i < modeCount) ? new Rectangle(pad + i * (num3 + num2), num, (i == modeCount - 1) ? (w - pad * 2 - (num3 + num2) * (modeCount - 1)) : num3, Theme.S(54f)) : Rectangle.Empty);
		}
		num += Theme.S(54f) + Theme.S(12f);
		int num4 = (w - pad * 2 - num2 * 3) / 4;
		for (int j = 0; j < 4; j++)
		{
			_tileR[j] = new Rectangle(pad + j * (num4 + num2), num, num4, Theme.S(58f));
		}
		num += Theme.S(58f) + Theme.S(12f);
		if (Note() != "")
		{
			_noteR = new Rectangle(pad, num, w - pad * 2, Theme.S(20f));
			num += Theme.S(20f) + Theme.S(10f);
		}
		else
		{
			_noteR = Rectangle.Empty;
		}
		_sig.Location = new Point(pad, num);
		_scan.Location = new Point(_sig.Right + num2, num);
		_settings.Location = new Point(_scan.Right + num2, num);
		num += _sig.Height;
		int num5 = num + pad;
		SetBounds(x, y, w, num5);
		return num5;
	}

	private static string Days(int d, string never)
	{
		if (d < 0 || d > 3650)
		{
			return never;
		}
		switch (d)
		{
		case 0:
			return "сегодня";
		case 1:
			return "вчера";
		default:
		{
			int num = d % 10;
			int num2 = d % 100;
			string text = ((num == 1 && num2 != 11) ? "день" : ((num >= 2 && num <= 4 && (num2 < 12 || num2 > 14)) ? "дня" : "дней"));
			return d + " " + text + " назад";
		}
		}
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		Color color = ((!_loaded) ? Theme.Sub : ((_mode == 0) ? Theme.Green : ((_mode == 1) ? Theme.Yellow : Theme.Red)));
		int num = Theme.S(44f);
		Rectangle rectangle = new Rectangle(_headR.X, _headR.Y + (_headR.Height - num) / 2, num, num);
		using (GraphicsPath path = Theme.Round(rectangle, (float)num * 0.27f))
		{
			Theme.FillGradient(g, path, rectangle, Theme.Mix(Color.FromArgb(58, 58, 67), color, 0.22f), Theme.Mix(Color.FromArgb(40, 40, 47), color, 0.08f));
			using Pen pen = new Pen(Color.FromArgb(110, color));
			g.DrawPath(pen, path);
		}
		Theme.DrawIcon(g, "\uea18", Theme.Icon, rectangle, Theme.Mix(color, Color.White, 0.3f));
		int num2 = rectangle.Right + Theme.S(14f);
		int height = Theme.Measure("Ag", Theme.Title).Height;
		int num3 = _headR.Y + (_headR.Height - height - Theme.S(22f)) / 2;
		TextRenderer.DrawText(g, "Microsoft Defender", Theme.Title, new Point(num2, num3), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		string text = ((!_loaded) ? "Проверяю защиту…" : ((_mode == 0) ? ("Компьютер защищён • базы обновлены " + Days(_sigAge, "давно")) : ((_mode == 1) ? "Защита в реальном времени выключена — файлы не проверяются при открытии" : "Defender выключен полностью — компьютер без антивируса")));
		string text2 = ((!_loaded) ? "" : ((_mode == 0) ? "Защищает" : ((_mode == 1) ? "Частично" : "Выключен")));
		int num4 = ((!(text2 == "")) ? Theme.PillW(text2, Theme.Tiny) : 0);
		TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(num2, num3 + height + Theme.S(4f), _headR.Right - num2 - num4 - Theme.S(12f), Theme.S(20f)), (_mode == 0 || !_loaded) ? Theme.Sub : color, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		if (num4 > 0)
		{
			Theme.Pill(g, new Rectangle(_headR.Right - num4, _headR.Y + (_headR.Height - Theme.S(22f)) / 2, num4, Theme.S(22f)), text2, Theme.Tiny, color, 26);
		}
		if (!_loaded)
		{
			Skeleton.Row(g, new Rectangle(_modeR[0].X, _modeR[0].Y, _modeR[ModeCount - 1].Right - _modeR[0].X, _modeR[0].Height), this);
			Skeleton.Row(g, new Rectangle(_tileR[0].X, _tileR[0].Y, _tileR[3].Right - _tileR[0].X, _tileR[0].Height), this);
			return;
		}
		for (int i = 0; i < ModeCount; i++)
		{
			Rectangle rectangle2 = _modeR[i];
			bool flag = i == _mode;
			bool flag2 = i == _hot && !flag;
			using (GraphicsPath path2 = Theme.Round(new RectangleF((float)rectangle2.X + 0.5f, (float)rectangle2.Y + 0.5f, rectangle2.Width - 1, rectangle2.Height - 1), Theme.S(12f)))
			{
				if (flag)
				{
					Theme.FillGradient(g, path2, rectangle2, Theme.Silver1, Theme.Silver2);
				}
				else
				{
					Theme.FillGradient(g, path2, rectangle2, flag2 ? Theme.CardHot1 : Theme.Card1, flag2 ? Theme.CardHot2 : Theme.Card2);
					using Pen pen2 = new Pen(flag2 ? Theme.BorderHi : Theme.Border);
					g.DrawPath(pen2, path2);
				}
			}
			Color color2 = i switch
			{
				1 => Theme.Yellow, 
				0 => Theme.Green, 
				_ => Theme.Red, 
			};
			int num5 = Theme.S(8f);
			using (SolidBrush brush = new SolidBrush(flag ? Theme.Mix(color2, Color.Black, 0.25f) : color2))
			{
				g.FillEllipse(brush, rectangle2.X + Theme.S(14f), rectangle2.Y + Theme.S(15f), num5, num5);
			}
			int num6 = rectangle2.X + Theme.S(30f);
			TextRenderer.DrawText(g, ModeNames[i], Theme.Body, new Rectangle(num6, rectangle2.Y + Theme.S(8f), rectangle2.Right - num6 - Theme.S(10f), Theme.S(22f)), flag ? Theme.OnSilver : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, ModeHints[i], Theme.Tiny, new Rectangle(num6, rectangle2.Y + Theme.S(30f), rectangle2.Right - num6 - Theme.S(10f), Theme.S(18f)), flag ? Color.FromArgb(80, 80, 90) : Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		Tile(g, _tileR[0], "Реальное время", _rtp ? "Включено" : "Выключено", _rtp);
		Tile(g, _tileR[1], "Защита от подделки", _tamper ? "Включена" : "Выключена", _tamper);
		Tile(g, _tileR[2], "Облачная защита", _cloud ? "Включена" : "Выключена", _cloud);
		Tile(g, _tileR[3], "Последняя проверка", Days(_scanAge, "не проводилась"), _scanAge >= 0 && _scanAge <= 7);
		string text3 = Note();
		if (text3 != "" && _noteR.Height > 0)
		{
			Rectangle r = new Rectangle(_noteR.X, _noteR.Y + Theme.S(1f), Theme.S(18f), Theme.S(18f));
			if (Theme.IconSmall != null)
			{
				Theme.DrawIcon(g, "\ue946", Theme.IconSmall, r, Theme.Yellow);
			}
			TextRenderer.DrawText(g, text3, Theme.Small, new Rectangle(r.Right + Theme.S(8f), _noteR.Y, _noteR.Width - Theme.S(26f), _noteR.Height), Theme.Yellow, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	private void Tile(Graphics g, Rectangle r, string name, string val, bool good)
	{
		using (GraphicsPath path = Theme.Round(r, Theme.S(12f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		int num = r.X + Theme.S(14f);
		TextRenderer.DrawText(g, name, Theme.Tiny, new Rectangle(num, r.Y + Theme.S(10f), r.Width - Theme.S(24f), Theme.S(16f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		int num2 = Theme.S(7f);
		int num3 = r.Y + Theme.S(30f);
		using (SolidBrush brush2 = new SolidBrush(good ? Theme.Green : Theme.Dim))
		{
			g.FillEllipse(brush2, num, num3 + Theme.S(6f), num2, num2);
		}
		TextRenderer.DrawText(g, val, Theme.Body, new Rectangle(num + num2 + Theme.S(8f), num3, r.Right - num - num2 - Theme.S(18f), Theme.S(20f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}

	private int HitMode(Point p)
	{
		if (!_loaded)
		{
			return -1;
		}
		for (int i = 0; i < ModeCount; i++)
		{
			if (_modeR[i].Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		int num = HitMode(e.Location);
		if (num != _hot)
		{
			_hot = num;
			Cursor = ((num >= 0 && num != _mode) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (_hot != -1)
		{
			_hot = -1;
			Cursor = Cursors.Default;
			Invalidate();
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		int num = HitMode(e.Location);
		if (e.Button == MouseButtons.Left && num >= 0 && num != _mode)
		{
			this.ModeClicked?.Invoke(num);
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Skeleton.Track(this, !_loaded);
	}
}
