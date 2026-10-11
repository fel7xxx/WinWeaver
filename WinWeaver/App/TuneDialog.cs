using System;
using System.Collections.Generic;
using System.IO;

namespace WinWeaver;

internal class TuneDialog : Form, ISurface
{
	private class Tile
	{
		public string Key;

		public string Name;

		public string Sub;

		public string Mono;

		public Color C1;

		public Color C2;

		public Rectangle R;

		public Image Logo;
	}

	private class Opt
	{
		public string Key;

		public string Title;

		public string Hint;

		public bool On;

		public Rectangle R;
	}

	private static readonly Tile[] AllTiles = new Tile[6]
	{
		new Tile
		{
			Key = "firefox",
			Name = "Firefox",
			Sub = "Mozilla, без слежки",
			Mono = "F",
			C1 = Color.FromArgb(255, 149, 0),
			C2 = Color.FromArgb(230, 62, 94)
		},
		new Tile
		{
			Key = "chrome",
			Name = "Google Chrome",
			Sub = "Google",
			Mono = "C",
			C1 = Color.FromArgb(66, 133, 244),
			C2 = Color.FromArgb(52, 168, 83)
		},
		new Tile
		{
			Key = "brave",
			Name = "Brave",
			Sub = "Блокирует рекламу",
			Mono = "B",
			C1 = Color.FromArgb(255, 99, 32),
			C2 = Color.FromArgb(214, 40, 40)
		},
		new Tile
		{
			Key = "yandex",
			Name = "Яндекс Браузер",
			Sub = "Яндекс",
			Mono = "Я",
			C1 = Color.FromArgb(255, 72, 40),
			C2 = Color.FromArgb(200, 30, 30)
		},
		new Tile
		{
			Key = "operagx",
			Name = "Opera GX",
			Sub = "Для геймеров",
			Mono = "GX",
			C1 = Color.FromArgb(250, 30, 78),
			C2 = Color.FromArgb(120, 20, 60)
		},
		new Tile
		{
			Key = "keep",
			Name = "Оставить Edge",
			Sub = "Браузер не меняется",
			Mono = "E",
			C1 = Color.FromArgb(40, 160, 220),
			C2 = Color.FromArgb(20, 90, 170)
		}
	};

	private readonly string _level;

	private readonly string _title;

	private readonly string _tag;

	private readonly string _glyph;

	private readonly string[] _plan;

	private readonly FButton _ok;

	private readonly FButton _cancel;

	private readonly Image _wall;

	private int _sel;

	private int _hover = -1;

	private readonly Tween _selT;

	private float _appear = 1f;

	private Rectangle _planR;

	private Rectangle _tilesLabel;

	private Rectangle _fixedLabel;

	private Rectangle _fixedR;

	private Rectangle _noteR;

	private const string BrowserNote = "Установим выбранный браузер, закрепим его на панели задач и только потом удалим Edge. Не получится установить — Edge останется. WebView2, нужный другим программам, не удаляется.";

	private readonly List<Opt> _opts = new List<Opt>();

	private int _def;

	private Rectangle _defR;

	private readonly Rectangle[] _defChips = new Rectangle[3];

	private static readonly string[] DefNames = new string[3] { "Как есть", "Тише", "Выключить" };

	private Rectangle _optLabel;

	private string _hotKey;

	public static string LastOptions = "";

	private int _planTop;

	private readonly List<Rectangle> _planRows = new List<Rectangle>();

	private Rectangle _heroR;

	private int _footTop;

	private readonly List<Rectangle> _chips = new List<Rectangle>();

	private RectangleF _fromR = RectangleF.Empty;

	private static int DefCount
	{
		get
		{
			if (!Prefs.Expert)
			{
				return 2;
			}
			return 3;
		}
	}

	public string Options
	{
		get
		{
			List<string> list = new List<string>();
			foreach (Opt opt in _opts)
			{
				if (opt.On)
				{
					list.Add(opt.Key);
				}
			}
			return string.Join(",", list.ToArray()) + ";def=" + _def;
		}
	}

	public string Browser => AllTiles[_sel].Key;

	private int M => Theme.S(28f);

	private string[] ChipTexts
	{
		get
		{
			if (_level == "gamer")
			{
				return new string[3]
				{
					"не рекомендуется для работы",
					_plan.Length + " " + InstalledView.Plural(_plan.Length, "шаг", "шага", "шагов"),
					"нужна перезагрузка"
				};
			}
			return new string[3]
			{
				"3–10 минут",
				_plan.Length + " " + InstalledView.Plural(_plan.Length, "шаг", "шага", "шагов"),
				"откат ограничен"
			};
		}
	}

	private Color Tone
	{
		get
		{
			string level = _level;
			if (!(level == "light"))
			{
				if (level == "gamer")
				{
					return Color.FromArgb(240, 112, 132);
				}
				return Color.FromArgb(128, 170, 255);
			}
			return Color.FromArgb(111, 207, 151);
		}
	}

	private string LevelNo
	{
		get
		{
			if (!(_level == "light"))
			{
				if (!(_level == "gamer"))
				{
					return "2";
				}
				return "3";
			}
			return "1";
		}
	}

	private void InitOpts(string level)
	{
		bool flag = level == "light";
		bool on = level == "gamer";
		_opts.Add(new Opt
		{
			Key = "7zip",
			Title = "Поставить 7-Zip",
			Hint = "бесплатный архиватор для .zip, .rar, .7z",
			On = true
		});
		_opts.Add(new Opt
		{
			Key = "vc",
			Title = "Поставить библиотеки для игр",
			Hint = "Visual C++ и DirectX — без них игры не запускаются",
			On = on
		});
		_opts.Add(new Opt
		{
			Key = "ext",
			Title = "Показывать расширения файлов",
			Hint = "сразу видно, что это .exe, а не .pdf",
			On = !flag
		});
		_opts.Add(new Opt
		{
			Key = "temp",
			Title = "Удалить временные файлы",
			Hint = "кэш и мусор из папок Temp, личное не трогаем",
			On = !flag
		});
		_opts.Add(new Opt
		{
			Key = "drv",
			Title = "Не обновлять драйверы через Windows",
			Hint = "Windows не заменит твой видеодрайвер старым",
			On = on
		});
		_def = 0;
	}

	public static string[] PlanFor(string level)
	{
		if (!(level == "light"))
		{
			if (level == "gamer")
			{
				return new string[5] { "Всё из «Оптимальной», включая точку восстановления", "Максимальная схема питания без энергосбережения процессора и шины PCIe", "Приоритет и точный таймер для игр, без MPO, оптимизации во весь экран и ускорения мыши", "Античиты работают (FACEIT, Vanguard, EAC, BattlEye): защиту ядра HVCI/VBS не трогаем", "Чем жертвуем: поиск в Пуске без индекса, нет фоновых приложений и части уведомлений, без анимаций и прозрачности, без гибернации, ПК потребляет больше энергии" };
			}
			return new string[7] { "Сначала создадим точку восстановления", "Всё из «Базовой», плюс удалим OneDrive и встроенный мусор", "Тёмная тема, классическое меню, панель задач без виджетов", "Обновления не раздаются чужим ПК и не перезагружают компьютер сами", "Быстрее отклик и FPS: игровой режим, HAGS, приоритет игр; на ПК — максимальная схема питания", "Защиту, службы и мышь не трогаем — монтаж, 3D и античиты работают как раньше", "Ярлыки WinWeaver на Рабочем столе и в Пуске" };
		}
		return new string[4] { "Выключим телеметрию, Copilot и Recall, рекламу и Bing в поиске", "Уберём персональные советы, просьбы об отзывах и рекламу на экране блокировки и в Проводнике", "Запретим Windows самой шифровать диски (BitLocker)", "Программы не удаляются — кроме Edge, если выбран другой браузер" };
	}

	private TuneDialog(string level, string title, string tag, string glyph)
	{
		_level = level;
		_title = "Автонастройка «" + title + "»";
		_tag = tag;
		_glyph = glyph;
		_plan = PlanFor(level);
		InitOpts(level);
		string text = ((level == "light") ? "keep" : (Prefs.LastBrowser ?? "firefox"));
		_sel = 0;
		for (int i = 0; i < AllTiles.Length; i++)
		{
			if (AllTiles[i].Key == text)
			{
				_sel = i;
			}
		}
		_selT = new Tween(this, 1f, 0.22f);
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.Manual;
		base.ShowInTaskbar = false;
		base.AutoScaleMode = AutoScaleMode.None;
		base.KeyPreview = true;
		BackColor = Theme.Card2;
		Text = _title;
		if (WDialog.AppIcon != null)
		{
			base.Icon = WDialog.AppIcon;
		}
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		_wall = Walls.Load(Prefs.LastWallpaper, 320);
		Tile[] allTiles = AllTiles;
		foreach (Tile tile in allTiles)
		{
			if (tile.Logo == null)
			{
				tile.Logo = LoadLogo(tile.Key);
			}
		}
		_ok = new FButton
		{
			Text = "Далее",
			Kind = BtnKind.Silver,
			Font = Theme.Body,
			Glyph = "\ue72a"
		};
		_ok.AutoWidth(Theme.S(44f));
		_ok.Click += delegate
		{
			base.DialogResult = DialogResult.OK;
			Close();
		};
		_cancel = new FButton
		{
			Text = "Не сейчас",
			Kind = BtnKind.Ghost,
			Font = Theme.Body
		};
		_cancel.AutoWidth(Theme.S(44f));
		_cancel.Click += delegate
		{
			base.DialogResult = DialogResult.Cancel;
			Close();
		};
		base.Controls.Add(_ok);
		base.Controls.Add(_cancel);
		DoLayoutDlg();
	}

	private static Image LoadLogo(string key)
	{
		try
		{
			using Stream stream = typeof(TuneDialog).Assembly.GetManifestResourceStream("br_" + key + ".png");
			if (stream != null)
			{
				return new Bitmap(Image.FromStream(stream));
			}
		}
		catch
		{
		}
		return null;
	}


	private void DoLayoutDlg()
	{
		int num = Theme.S(960f);
		int m = M;
		int num2 = Theme.S(24f);
		int num3 = (num - m * 2 - num2) / 2;
		int num4 = m;
		int num5 = m + num3 + num2;
		_heroR = new Rectangle(0, 0, num, Theme.S(108f));
		_chips.Clear();
		int num6 = num - m;
		string[] chipTexts = ChipTexts;
		for (int num7 = chipTexts.Length - 1; num7 >= 0; num7--)
		{
			int num8 = Theme.Measure(chipTexts[num7], Theme.Small).Width + Theme.S(34f);
			_chips.Insert(0, new Rectangle(num6 - num8, Theme.S(24f), num8, Theme.S(24f)));
			num6 -= num8 + Theme.S(8f);
		}
		int num9 = _heroR.Bottom + Theme.S(12f);
		int num10 = (_planTop = num9);
		int width = num3 - Theme.S(36f);
		_planRows.Clear();
		num10 += Theme.S(28f);
		string[] plan = _plan;
		foreach (string s in plan)
		{
			int num11 = Math.Max(Theme.S(22f), Theme.Measure(s, Theme.Small, width, wrap: true).Height);
			_planRows.Add(new Rectangle(num4 + Theme.S(36f), num10, width, num11));
			num10 += num11 + Theme.S(6f);
		}
		_planR = new Rectangle(num4, _planTop, num3, num10 - _planTop);
		num10 += Theme.S(8f);
		_fixedLabel = new Rectangle(num4, num10, num3, Theme.S(20f));
		num10 = _fixedLabel.Bottom + Theme.S(8f);
		_fixedR = new Rectangle(num4, num10, num3, Theme.S(60f));
		int bottom = _fixedR.Bottom;
		num10 = num9;
		_tilesLabel = new Rectangle(num5, num10, num3, Theme.S(30f) + Theme.Measure("Установим выбранный браузер, закрепим его на панели задач и только потом удалим Edge. Не получится установить — Edge останется. WebView2, нужный другим программам, не удаляется.", Theme.Small, num3, wrap: true).Height + Theme.S(2f));
		num10 = _tilesLabel.Bottom + Theme.S(10f);
		int num12 = Theme.S(10f);
		int num13 = (num3 - num12) / 2;
		int num14 = Theme.S(54f);
		for (int j = 0; j < AllTiles.Length; j++)
		{
			AllTiles[j].R = new Rectangle(num5 + j % 2 * (num13 + num12), num10 + j / 2 * (num14 + num12), num13, num14);
		}
		int val = num10 + num14 * 3 + num12 * 2;
		num10 = Math.Max(bottom, val) + Theme.S(16f);
		_optLabel = new Rectangle(m, num10, num - m * 2, Theme.S(20f));
		num10 = _optLabel.Bottom + Theme.S(8f);
		int num15 = Theme.S(10f);
		int num16 = (num - m * 2 - num15 * 2) / 3;
		int num17 = Theme.S(52f);
		for (int k = 0; k < _opts.Count; k++)
		{
			_opts[k].R = new Rectangle(m + k % 3 * (num16 + num15), num10 + k / 3 * (num17 + num15), num16, num17);
		}
		int count = _opts.Count;
		_defR = new Rectangle(m + count % 3 * (num16 + num15), num10 + count / 3 * (num17 + num15), num16, num17);
		int num18 = _defR.X + Theme.S(10f);
		int num19 = _defR.Width - Theme.S(20f);
		int defCount = DefCount;
		int num20 = num19 / defCount;
		for (int l = 0; l < 3; l++)
		{
			_defChips[l] = ((l < defCount) ? new Rectangle(num18 + l * num20, _defR.Y + Theme.S(26f), (l == defCount - 1) ? (num19 - num20 * (defCount - 1)) : num20, Theme.S(22f)) : Rectangle.Empty);
		}
		num10 = (_footTop = _defR.Bottom + Theme.S(16f));
		int num21 = Theme.S(38f);
		int num22 = num21 + Theme.S(24f);
		base.Size = new Size(num, num10 + num22);
		int y = num10 + (num22 - num21) / 2;
		_cancel.SetBounds(num - m - _cancel.Width, y, _cancel.Width, num21);
		_ok.SetBounds(_cancel.Left - Theme.S(10f) - _ok.Width, y, _ok.Width, num21);
		_noteR = new Rectangle(m, num10, _ok.Left - m - Theme.S(20f), num22);
	}

	private static void DrawSwitch(Graphics g, Rectangle r, bool on, Color tone, float a)
	{
		using (GraphicsPath path = Theme.Round(r, (float)r.Height / 2f))
		{
			if (on)
			{
				Theme.FillGradient(g, path, r, Fade(Theme.Mix(tone, Color.White, 0.25f), a), Fade(tone, a), 0f);
			}
			else
			{
				using (SolidBrush brush = new SolidBrush(Fade(Theme.Control2, a)))
				{
					g.FillPath(brush, path);
				}
				using Pen pen = new Pen(Fade(Theme.BorderHi, a));
				g.DrawPath(pen, path);
			}
		}
		int num = r.Height - Theme.S(6f);
		Rectangle rect = new Rectangle(on ? (r.Right - Theme.S(3f) - num) : (r.X + Theme.S(3f)), r.Y + Theme.S(3f), num, num);
		using SolidBrush brush2 = new SolidBrush(Fade(on ? Color.White : Theme.Sub, a));
		g.FillEllipse(brush2, rect);
	}

	private void PaintOpts(Graphics g, float a)
	{
		int num = 0;
		foreach (Opt opt in _opts)
		{
			if (opt.On)
			{
				num++;
			}
		}
		Label(g, "По желанию", _optLabel, a, "включено " + num + " из " + _opts.Count);
		Color tone = Tone;
		foreach (Opt opt2 in _opts)
		{
			bool flag = _hotKey == opt2.Key;
			Rectangle r = opt2.R;
			r.Offset(0, (int)((1f - a) * (float)Theme.S(8f)));
			using (GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(12f)))
			{
				Theme.FillGradient(g, path, r, Fade(flag ? Theme.CardHot1 : Theme.Card1, a), Fade(flag ? Theme.CardHot2 : Theme.Card2, a));
				if (opt2.On)
				{
					using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb((int)(30f * a), tone), Color.FromArgb(0, tone), 0f);
					g.FillPath(brush, path);
				}
				using Pen pen = new Pen(Fade(opt2.On ? Theme.Mix(Theme.Border, tone, 0.45f) : (flag ? Theme.BorderHi : Theme.Border), a));
				g.DrawPath(pen, path);
			}
			Rectangle r2 = new Rectangle(r.Right - Theme.S(14f) - Theme.S(36f), r.Y + (r.Height - Theme.S(20f)) / 2, Theme.S(36f), Theme.S(20f));
			DrawSwitch(g, r2, opt2.On, tone, a);
			int num2 = r.X + Theme.S(16f);
			int width = r2.X - num2 - Theme.S(10f);
			TextRenderer.DrawText(g, opt2.Title, Theme.Body, new Rectangle(num2, r.Y + Theme.S(7f), width, Theme.S(20f)), Fade(Theme.Text, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, opt2.Hint, Theme.Tiny, new Rectangle(num2, r.Y + Theme.S(28f), width, Theme.S(16f)), Fade(opt2.On ? Theme.Sub : Theme.Dim, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		Rectangle defR = _defR;
		defR.Offset(0, (int)((1f - a) * (float)Theme.S(8f)));
		int y = defR.Y - _defR.Y;
		using (GraphicsPath path2 = Theme.Round(new RectangleF((float)defR.X + 0.5f, (float)defR.Y + 0.5f, defR.Width - 1, defR.Height - 1), Theme.S(12f)))
		{
			Theme.FillGradient(g, path2, defR, Fade(Theme.Card1, a), Fade(Theme.Card2, a));
			using Pen pen2 = new Pen(Fade((_def == 2) ? Theme.Mix(Theme.Border, Theme.Yellow, 0.5f) : Theme.Border, a));
			g.DrawPath(pen2, path2);
		}
		TextRenderer.DrawText(g, "Антивирус Defender", Theme.Body, new Rectangle(defR.X + Theme.S(14f), defR.Y + Theme.S(4f), defR.Width - Theme.S(28f), Theme.S(20f)), Fade(Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		Rectangle rectangle = Rectangle.Union(_defChips[0], _defChips[DefCount - 1]);
		rectangle.Offset(0, y);
		using (GraphicsPath path3 = Theme.Round(rectangle, (float)rectangle.Height / 2f))
		{
			using SolidBrush brush2 = new SolidBrush(Fade(Color.FromArgb(22, 22, 27), a));
			g.FillPath(brush2, path3);
		}
		for (int i = 0; i < DefCount; i++)
		{
			Rectangle rectangle2 = _defChips[i];
			rectangle2.Offset(0, y);
			bool flag2 = i == _def;
			bool flag3 = _hotKey == "def" + i;
			Rectangle rectangle3 = Rectangle.Inflate(rectangle2, -Theme.S(2f), -Theme.S(2f));
			using (GraphicsPath path4 = Theme.Round(rectangle3, (float)rectangle3.Height / 2f))
			{
				if (flag2)
				{
					if (i == 2)
					{
						Theme.FillGradient(g, path4, rectangle3, Fade(Theme.Mix(Theme.Yellow, Color.White, 0.2f), a), Fade(Theme.Mix(Theme.Yellow, Theme.Card2, 0.25f), a));
					}
					else
					{
						Theme.FillGradient(g, path4, rectangle3, Fade(Theme.Silver1, a), Fade(Theme.Silver2, a));
					}
				}
				else if (flag3)
				{
					using SolidBrush brush3 = new SolidBrush(Fade(Theme.Control1, a));
					g.FillPath(brush3, path4);
				}
			}
			Color c = (flag2 ? Theme.OnSilver : (flag3 ? Theme.Text : Theme.Sub));
			TextRenderer.DrawText(g, DefNames[i], Theme.Tiny, rectangle2, Fade(c, a), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		if (!Win.RoundCorners(base.Handle))
		{
			using (GraphicsPath path = Theme.Round(new RectangleF(0f, 0f, base.Width, base.Height), Theme.S(14f)))
			{
				base.Region = new Region(path);
			}
		}
	}

	protected override void OnLoad(EventArgs e)
	{
		if (Anim.Enabled)
		{
			_appear = 0f;
			try
			{
				base.Opacity = 0.0;
			}
			catch
			{
			}
		}
		base.OnLoad(e);
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		if (!Anim.Enabled)
		{
			return;
		}
		_appear = 0f;
		Anim.Start(this, 420, delegate(float t)
		{
			_appear = t;
			try
			{
				base.Opacity = Math.Min(1.0, (double)t * 2.2);
			}
			catch
			{
			}
			Invalidate();
		}, delegate
		{
			_appear = 1f;
			try
			{
				base.Opacity = 1.0;
			}
			catch
			{
			}
			Invalidate();
		}, "tune-appear");
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			base.DialogResult = DialogResult.Cancel;
			Close();
		}
		else if (e.KeyCode == Keys.Return)
		{
			base.DialogResult = DialogResult.OK;
			Close();
		}
		else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Left || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
		{
			int num = ((e.KeyCode == Keys.Right) ? 1 : ((e.KeyCode == Keys.Left) ? (-1) : ((e.KeyCode == Keys.Down) ? 2 : (-2))));
			Choose((_sel + num + AllTiles.Length) % AllTiles.Length);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override bool ProcessDialogKey(Keys keyData)
	{
		if (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down)
		{
			OnKeyDown(new KeyEventArgs(keyData));
			return true;
		}
		return base.ProcessDialogKey(keyData);
	}

	private void Choose(int i)
	{
		if (i != _sel)
		{
			_fromR = SelRect();
			_sel = i;
			_selT.Snap(0f);
			_selT.Target = 1f;
			Invalidate();
		}
	}

	private int Hit(Point p)
	{
		for (int i = 0; i < AllTiles.Length; i++)
		{
			if (AllTiles[i].R.Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	private string HitOpt(Point p)
	{
		foreach (Opt opt in _opts)
		{
			if (opt.R.Contains(p))
			{
				return opt.Key;
			}
		}
		for (int i = 0; i < DefCount; i++)
		{
			if (_defChips[i].Contains(p))
			{
				return "def" + i;
			}
		}
		return null;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = Hit(e.Location);
		string text = HitOpt(e.Location);
		if (num != _hover || text != _hotKey)
		{
			_hover = num;
			_hotKey = text;
			Cursor = ((num >= 0 || text != null) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = -1;
		_hotKey = null;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		int num = Hit(e.Location);
		if (num >= 0)
		{
			Choose(num);
		}
		string text = HitOpt(e.Location);
		if (text != null && e.Button == MouseButtons.Left)
		{
			if (text.StartsWith("def"))
			{
				_def = int.Parse(text.Substring(3));
			}
			else
			{
				foreach (Opt opt in _opts)
				{
					if (opt.Key == text)
					{
						opt.On = !opt.On;
					}
				}
			}
			Invalidate();
		}
		base.OnMouseClick(e);
	}

	private float A(int k)
	{
		return Anim.EaseOut(Anim.Clamp(_appear * 1.6f - (float)k * 0.08f));
	}

	private static Color Fade(Color c, float a)
	{
		return Theme.Mix(Theme.Card2, c, a);
	}

	private void Label(Graphics g, string text, Rectangle r, float a, string extra = null)
	{
		string text2 = text.ToUpperInvariant();
		Size size = Theme.Measure(text2, Theme.H2);
		TextRenderer.DrawText(g, text2, Theme.H2, new Point(r.X, r.Y), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix);
		int num = r.X + size.Width + Theme.S(10f);
		if (r.Width <= 0)
		{
			return;
		}
		int num2 = r.Right;
		if (!string.IsNullOrEmpty(extra))
		{
			int num3 = Theme.Measure(extra, Theme.Tiny).Width + Theme.S(8f);
			TextRenderer.DrawText(g, extra, Theme.Tiny, new Rectangle(r.Right - num3, r.Y, num3, size.Height), Fade(Theme.Dim, a), TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			num2 -= num3 + Theme.S(10f);
		}
		if (num2 > num)
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(num - 1, 0, num2 - num + 2, 1), Fade(Theme.BorderHi, a), Color.FromArgb(0, Theme.Border), 0f))
			{
				g.FillRectangle(brush, num, r.Y + size.Height / 2, num2 - num, 1);
			}
		}
	}

	public void PaintSurface(Graphics g)
	{
		PaintBg(g);
	}

	private void PaintBg(Graphics g)
	{
		Theme.Smooth(g);
		int width = base.Width;
		int height = base.Height;
		Color tone = Tone;
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, -1, width, height + 2), Color.FromArgb(34, 34, 40), Color.FromArgb(21, 21, 25), 90f))
		{
			g.FillRectangle(brush, 0, 0, width, height);
		}
		Rectangle heroR = _heroR;
		if (_wall != null)
		{
			float num = Math.Max((float)heroR.Width / (float)_wall.Width, (float)heroR.Height / (float)_wall.Height);
			float num2 = (float)_wall.Width * num;
			float num3 = (float)_wall.Height * num;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			Region clip = g.Clip;
			g.SetClip(heroR, CombineMode.Intersect);
			using (ImageAttributes imageAttributes = new ImageAttributes())
			{
				imageAttributes.SetColorMatrix(new ColorMatrix
				{
					Matrix33 = 0.55f
				});
				g.DrawImage(_wall, new Rectangle(heroR.X + (int)(((float)heroR.Width - num2) / 2f), heroR.Y + (int)(((float)heroR.Height - num3) * 0.4f), (int)num2, (int)num3), 0, 0, _wall.Width, _wall.Height, GraphicsUnit.Pixel, imageAttributes);
			}
			g.Clip = clip;
		}
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(heroR.X - 1, heroR.Y, heroR.Width + 2, heroR.Height), Color.FromArgb(235, 24, 24, 29), Color.FromArgb(120, 24, 24, 29), 0f))
		{
			g.FillRectangle(brush2, heroR);
		}
		using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(heroR.X, heroR.Y - 1, heroR.Width, heroR.Height + 2), Color.FromArgb(0, 32, 32, 38), Color.FromArgb(255, 33, 33, 39), 90f))
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.55f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(0, 33, 33, 39),
				Color.FromArgb(60, 33, 33, 39),
				Color.FromArgb(255, 33, 33, 39)
			};
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, heroR);
		}
		int num4 = Theme.S(210f);
		int num5 = M + Theme.S(32f);
		int num6 = Theme.S(54f);
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(num5 - num4, num6 - num4, num4 * 2, num4 * 2);
			using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(58, tone);
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, tone) };
			pathGradientBrush.FocusScales = new PointF(0.05f, 0.05f);
			g.FillPath(pathGradientBrush, graphicsPath);
		}
		using (LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush(new Rectangle(0, 0, width, 2), Color.Transparent, Color.Transparent, 0f))
		{
			ColorBlend colorBlend2 = new ColorBlend();
			colorBlend2.Positions = new float[3] { 0f, 0.35f, 1f };
			colorBlend2.Colors = new Color[3]
			{
				Color.FromArgb(0, tone),
				Color.FromArgb(220, Theme.Mix(tone, Color.White, 0.35f)),
				Color.FromArgb(0, tone)
			};
			linearGradientBrush2.InterpolationColors = colorBlend2;
			g.FillRectangle(linearGradientBrush2, 0, 0, width, Math.Max(1, Theme.S(1.5f)));
		}
		if (_footTop > 0)
		{
			using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(80, 8, 8, 10)))
			{
				g.FillRectangle(brush3, 0, _footTop, width, height - _footTop);
			}
			using Pen pen = new Pen(Color.FromArgb(20, 255, 255, 255));
			g.DrawLine(pen, 0, _footTop, width, _footTop);
		}
		using Pen pen2 = new Pen(Theme.BorderHi);
		g.DrawRectangle(pen2, 0, 0, width - 1, height - 1);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		PaintBg(graphics);
		Theme.Smooth(graphics);
		Color tone = Tone;
		int m = M;
		float num = A(0);
		int num2 = (int)((1f - num) * (float)Theme.S(8f));
		Rectangle rectangle = new Rectangle(m, Theme.S(26f) + num2, Theme.S(56f), Theme.S(56f));
		using (GraphicsPath path = Theme.Round(rectangle, (float)rectangle.Width * 0.28f))
		{
			Theme.FillGradient(graphics, path, rectangle, Fade(Theme.Mix(Color.FromArgb(70, 70, 82), tone, 0.35f), num), Fade(Theme.Mix(Color.FromArgb(32, 32, 38), tone, 0.18f), num));
			using Pen pen = new Pen(Fade(Theme.Mix(Theme.BorderHi, tone, 0.6f), num), Math.Max(1f, Theme.S(1.2f)));
			graphics.DrawPath(pen, path);
		}
		Theme.DrawIcon(graphics, _glyph, Theme.IconBig ?? Theme.Icon, rectangle, Fade(Theme.Mix(tone, Color.White, 0.55f), num));
		int num3 = rectangle.Right + Theme.S(18f);
		string text = "АВТОНАСТРОЙКА  •  УРОВЕНЬ " + LevelNo + " ИЗ 3";
		TextRenderer.DrawText(graphics, text, DlgChrome.KickerF, new Point(num3, Theme.S(24f) + num2), Fade(Theme.Mix(tone, Color.White, 0.35f), num), TextFormatFlags.NoPrefix);
		string text2 = _title.Replace("Автонастройка «", "").TrimEnd('»');
		using (Font font = Theme.F(21f, semi: true))
		{
			if (num > 0.5f)
			{
				Theme.GradientText(graphics, text2, font, new PointF(num3 - Theme.S(1f), Theme.S(39f) + num2));
			}
		}
		TextRenderer.DrawText(graphics, _tag, Theme.Body, new Point(num3, Theme.S(74f) + num2), Fade(Theme.Sub, num), TextFormatFlags.NoPrefix);
		string[] chipTexts = ChipTexts;
		for (int i = 0; i < _chips.Count && i < chipTexts.Length; i++)
		{
			Rectangle rectangle2 = _chips[i];
			rectangle2.Offset(0, num2);
			bool flag = _level == "gamer" && i == 0;
			using (GraphicsPath path2 = Theme.Round(new RectangleF((float)rectangle2.X + 0.5f, (float)rectangle2.Y + 0.5f, rectangle2.Width - 1, rectangle2.Height - 1), (float)rectangle2.Height / 2f))
			{
				using (SolidBrush brush = new SolidBrush(flag ? Color.FromArgb((int)(60f * num), Theme.Yellow) : Color.FromArgb((int)(150f * num), 20, 20, 24)))
				{
					graphics.FillPath(brush, path2);
				}
				using Pen pen2 = new Pen(flag ? Color.FromArgb((int)(150f * num), Theme.Yellow) : Color.FromArgb((int)(60f * num), 255, 255, 255));
				graphics.DrawPath(pen2, path2);
			}
			Color baseColor = ((_level == "gamer" && (i == 0 || i == 2)) ? Theme.Yellow : ((i == 2) ? Theme.Green : Theme.Mix(tone, Color.White, 0.3f)));
			int num4 = Theme.S(6f);
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(255f * num), baseColor)))
			{
				graphics.FillEllipse(brush2, rectangle2.X + Theme.S(12f), rectangle2.Y + (rectangle2.Height - num4) / 2, num4, num4);
			}
			TextRenderer.DrawText(graphics, chipTexts[i], Theme.Small, new Rectangle(rectangle2.X + Theme.S(24f), rectangle2.Y, rectangle2.Width - Theme.S(30f), rectangle2.Height), Fade(flag ? Theme.Mix(Theme.Yellow, Color.White, 0.3f) : Theme.Text, num), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
		float a = A(1);
		Label(graphics, "Что изменится", new Rectangle(_planR.X, _planTop, _planR.Width, 0), a);
		int height = Theme.Measure("Ag", Theme.Small).Height;
		int num5 = Theme.S(22f);
		int num6 = _planR.X + num5 / 2;
		if (_planRows.Count > 1)
		{
			int num7 = _planRows[0].Y + num5 / 2;
			int num8 = _planRows[_planRows.Count - 1].Y + num5 / 2;
			using LinearGradientBrush brush3 = new LinearGradientBrush(new Rectangle(num6 - 1, num7 - 1, 3, num8 - num7 + 2), Fade(Theme.Mix(Theme.BorderHi, tone, 0.5f), a), Fade(Theme.Border, a), 90f);
			graphics.FillRectangle(brush3, num6 - Theme.S(1f) / 2, num7, Math.Max(1, Theme.S(1.5f)), num8 - num7);
		}
		for (int j = 0; j < _planRows.Count; j++)
		{
			float num9 = A(1 + j);
			Rectangle rectangle3 = _planRows[j];
			int num10 = (int)((1f - num9) * (float)Theme.S(6f));
			bool flag2 = _plan[j].StartsWith("Чем жертвуем");
			Rectangle rectangle4 = new Rectangle(_planR.X, rectangle3.Y + num10, num5, num5);
			using (SolidBrush brush4 = new SolidBrush(Fade(Color.FromArgb(30, 30, 36), num9)))
			{
				graphics.FillEllipse(brush4, rectangle4);
			}
			using (Pen pen3 = new Pen(Fade(flag2 ? Theme.Yellow : Theme.Mix(Theme.BorderHi, tone, 0.55f), num9), Math.Max(1f, Theme.S(1.2f))))
			{
				graphics.DrawEllipse(pen3, (float)rectangle4.X + 0.5f, (float)rectangle4.Y + 0.5f, rectangle4.Width - 1, rectangle4.Height - 1);
			}
			TextRenderer.DrawText(graphics, flag2 ? "!" : (j + 1).ToString(), Theme.Tiny, rectangle4, Fade(flag2 ? Theme.Yellow : Theme.Mix(tone, Color.White, 0.5f), num9), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(r: new Rectangle(rectangle3.X, rectangle3.Y + num10 + Math.Max(0, (num5 - height) / 2), rectangle3.Width, rectangle3.Height), dc: graphics, text: _plan[j], font: Theme.Small, fore: Fade(flag2 ? Theme.Yellow : Theme.Text, num9), flags: TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		}
		float a2 = A(3);
		Label(graphics, "Какой браузер поставить вместо Edge", _tilesLabel, a2);
		TextRenderer.DrawText(graphics, "Установим выбранный браузер, закрепим его на панели задач и только потом удалим Edge. Не получится установить — Edge останется. WebView2, нужный другим программам, не удаляется.", Theme.Small, new Rectangle(_tilesLabel.X, _tilesLabel.Y + Theme.S(28f), _tilesLabel.Width, _tilesLabel.Height - Theme.S(28f)), Fade(Theme.Dim, a2), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		PaintTiles(graphics);
		float a3 = A(6);
		Label(graphics, "Стиль WeavOS — по желанию, на следующем шаге", _fixedLabel, a3);
		PaintFixed(graphics, a3);
		PaintOpts(graphics, A(7));
		float a4 = A(8);
		string text3 = "Браузер: " + AllTiles[_sel].Name + "  •  Defender: " + ((_def == 0) ? "без изменений" : DefNames[_def].ToLowerInvariant()) + "  •  займёт 3–10 минут";
		string text4 = ((_def == 2) ? "Defender выключится, только если сначала отключить «Защиту от подделки» — мы откроем её настройки. Без антивируса ПК уязвимее." : ((_def == 1) ? "«Тише»: защита работает, но файлы не отправляются в Microsoft и уведомлений меньше." : "Доступный откат — в «Истории». Удаление программ и очистка могут быть необратимы."));
		Rectangle rectangle5 = new Rectangle(_noteR.X, _noteR.Y + (_noteR.Height - Theme.S(30f)) / 2, Theme.S(30f), Theme.S(30f));
		using (SolidBrush brush5 = new SolidBrush(Fade(Color.FromArgb(42, 42, 50), a4)))
		{
			graphics.FillEllipse(brush5, rectangle5);
		}
		Theme.DrawIcon(graphics, (_def == 2) ? "\ue7ba" : "\ue777", Theme.IconSmall, rectangle5, Fade((_def == 2) ? Theme.Yellow : Theme.Silver2, a4));
		int num11 = rectangle5.Right + Theme.S(12f);
		int width = _noteR.Right - num11;
		int num12 = _noteR.Y + _noteR.Height / 2;
		TextRenderer.DrawText(graphics, text3, Theme.Small, new Rectangle(num11, num12 - Theme.S(19f), width, Theme.S(18f)), Fade(Theme.Text, a4), TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(graphics, text4, Theme.Tiny, new Rectangle(num11, num12 + Theme.S(1f), width, Theme.S(18f)), Fade((_def == 2) ? Theme.Yellow : Theme.Sub, a4), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}

	private RectangleF SelRect()
	{
		Rectangle r = AllTiles[_sel].R;
		float num = Anim.Clamp(_selT.Value);
		if (_fromR.IsEmpty || num >= 0.999f)
		{
			return r;
		}
		return new RectangleF(_fromR.X + ((float)r.X - _fromR.X) * num, _fromR.Y + ((float)r.Y - _fromR.Y) * num, _fromR.Width + ((float)r.Width - _fromR.Width) * num, _fromR.Height + ((float)r.Height - _fromR.Height) * num);
	}

	private void PaintTiles(Graphics g)
	{
		float num = A(4);
		RectangleF rect = SelRect();
		Color c = AllTiles[_sel].C1;
		for (int num2 = 3; num2 >= 1; num2--)
		{
			using GraphicsPath path = Theme.Round(RectangleF.Inflate(rect, Theme.S(2.5f) * num2, Theme.S(2.5f) * num2), Theme.S(11f) + Theme.S(2.5f) * num2);
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)((float)(14 - num2 * 3) * num), c));
			g.FillPath(brush, path);
		}
		for (int i = 0; i < AllTiles.Length; i++)
		{
			Tile tile = AllTiles[i];
			float num3 = A(3 + i / 3);
			Rectangle r = tile.R;
			bool flag = i == _sel;
			bool flag2 = i == _hover && !flag;
			r.Offset(0, (int)((1f - num3) * (float)Theme.S(8f)) - (flag2 ? Theme.S(1f) : 0));
			using (GraphicsPath path2 = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(11f)))
			{
				Theme.FillGradient(g, path2, r, Fade(flag2 ? Theme.CardHot1 : Theme.Card1, num3), Fade(flag2 ? Theme.CardHot2 : Theme.Card2, num3));
				if (flag)
				{
					using LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb((int)(46f * num3), tile.C1), Color.FromArgb((int)(8f * num3), tile.C2), 0f);
					g.FillPath(brush2, path2);
				}
				using Pen pen = new Pen(Fade(flag2 ? Theme.BorderHi : Theme.Border, num3));
				g.DrawPath(pen, path2);
			}
			int num4 = Theme.S(36f);
			Rectangle rectangle = new Rectangle(r.X + Theme.S(13f), r.Y + (r.Height - num4) / 2, num4, num4);
			if (tile.Logo != null)
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				float matrix = num3 * ((flag || flag2) ? 1f : 0.82f);
				using ImageAttributes imageAttributes = new ImageAttributes();
				imageAttributes.SetColorMatrix(new ColorMatrix
				{
					Matrix33 = matrix
				});
				g.DrawImage(tile.Logo, rectangle, 0, 0, tile.Logo.Width, tile.Logo.Height, GraphicsUnit.Pixel, imageAttributes);
			}
			else
			{
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					graphicsPath.AddEllipse(rectangle);
					Theme.FillGradient(g, graphicsPath, rectangle, Fade(tile.C1, num3), Fade(tile.C2, num3), 45f);
				}
				TextRenderer.DrawText(g, tile.Mono, (tile.Mono.Length > 1) ? Theme.FC(9f, semi: true) : Theme.FC(12f, semi: true), rectangle, Fade(Color.White, num3), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			int num5 = rectangle.Right + Theme.S(12f);
			int num6 = r.Right - num5 - Theme.S(36f);
			int height = Theme.Measure("Ag", Theme.Title).Height;
			int height2 = Theme.Measure("Ag", Theme.Tiny).Height;
			int num7 = r.Y + (r.Height - height - height2 - Theme.S(2f)) / 2;
			TextRenderer.DrawText(g, tile.Name, Theme.Title, new Rectangle(num5, num7, num6, height + 2), Fade(Theme.Text, num3), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, tile.Sub, Theme.Tiny, new Rectangle(num5, num7 + height + Theme.S(2f), num6 + Theme.S(24f), height2 + 2), Fade(flag ? Theme.Mix(Theme.Sub, Color.White, 0.25f) : Theme.Sub, num3), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			int num8 = Theme.S(20f);
			Rectangle rectangle2 = new Rectangle(r.Right - Theme.S(14f) - num8, r.Y + (r.Height - num8) / 2, num8, num8);
			if (flag)
			{
				using (GraphicsPath graphicsPath2 = new GraphicsPath())
				{
					graphicsPath2.AddEllipse(rectangle2);
					Theme.FillGradient(g, graphicsPath2, rectangle2, Theme.Silver1, Theme.Silver2);
				}
				using Pen pen2 = new Pen(Theme.OnSilver, Math.Max(1.5f, Theme.S(2f)))
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				};
				float num9 = rectangle2.X;
				float num10 = rectangle2.Y;
				float num11 = num8;
				g.DrawLines(pen2, new PointF[3]
				{
					new PointF(num9 + num11 * 0.28f, num10 + num11 * 0.52f),
					new PointF(num9 + num11 * 0.44f, num10 + num11 * 0.68f),
					new PointF(num9 + num11 * 0.73f, num10 + num11 * 0.36f)
				});
			}
			else
			{
				using Pen pen3 = new Pen(Fade(flag2 ? Theme.Sub : Theme.BorderHi, num3), 1.3f);
				g.DrawEllipse(pen3, rectangle2);
			}
		}
		RectangleF r2 = SelRect();
		r2.Inflate(0.5f, 0.5f);
		using GraphicsPath path3 = Theme.Round(r2, Theme.S(11.5f));
		using LinearGradientBrush brush3 = new LinearGradientBrush(new RectangleF(r2.X, r2.Y - 1f, r2.Width, r2.Height + 2f), Color.FromArgb((int)(230f * num), Theme.Mix(Theme.Silver1, c, 0.25f)), Color.FromArgb((int)(110f * num), Theme.Silver2), 90f);
		using Pen pen4 = new Pen(brush3, Theme.BW);
		g.DrawPath(pen4, path3);
	}

	private void FixedTile(Graphics g, Rectangle r, float a)
	{
		using GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(12f));
		Theme.FillGradient(g, path, r, Fade(Theme.Card1, a), Fade(Theme.Card2, a));
		using Pen pen = new Pen(Fade(Theme.Border, a));
		g.DrawPath(pen, path);
	}

	private void PaintFixed(Graphics g, float a)
	{
		Rectangle fixedR = _fixedR;
		fixedR.Offset(0, (int)((1f - a) * (float)Theme.S(8f)));
		int num = Theme.S(10f);
		int num2 = (fixedR.Width - num) / 2;
		Rectangle r = new Rectangle(fixedR.X, fixedR.Y, num2, fixedR.Height);
		Rectangle r2 = new Rectangle(fixedR.X + num2 + num, fixedR.Y, fixedR.Width - num2 - num, fixedR.Height);
		FixedTile(g, r, a);
		FixedTile(g, r2, a);
		int num3 = Theme.S(10f);
		int num4 = fixedR.Height - num3 * 2;
		int width = num4 * 14 / 10;
		Rectangle rectangle = new Rectangle(r.X + num3, r.Y + num3, width, num4);
		using (GraphicsPath path = Theme.Round(rectangle, Theme.S(7f)))
		{
			if (_wall != null)
			{
				Region clip = g.Clip;
				g.SetClip(path, CombineMode.Intersect);
				float num5 = Math.Max((float)rectangle.Width / (float)_wall.Width, (float)rectangle.Height / (float)_wall.Height);
				float num6 = (float)_wall.Width * num5;
				float num7 = (float)_wall.Height * num5;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(_wall, (float)rectangle.X + ((float)rectangle.Width - num6) / 2f, (float)rectangle.Y + ((float)rectangle.Height - num7) / 2f, num6, num7);
				using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(255f * (1f - a)), Theme.Card2)))
				{
					g.FillRectangle(brush, rectangle);
				}
				g.Clip = clip;
			}
			using Pen pen = new Pen(Color.FromArgb((int)(80f * a), 255, 255, 255));
			g.DrawPath(pen, path);
		}
		int height = Theme.Measure("Ag", Theme.Body).Height;
		int height2 = Theme.Measure("Ag", Theme.Tiny).Height;
		int num8 = r.Y + (r.Height - height - Theme.S(2f) - height2) / 2;
		int num9 = rectangle.Right + Theme.S(12f);
		TextRenderer.DrawText(g, "Обои WeavOS", Theme.Body, new Rectangle(num9, num8, r.Right - num9 - num3, height + 2), Fade(Theme.Text, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, "Рабочий стол и блокировка", Theme.Tiny, new Rectangle(num9, num8 + height + Theme.S(2f), r.Right - num9 - num3, height2 + 2), Fade(Theme.Sub, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		Rectangle rectangle2 = new Rectangle(r2.X + num3, r2.Y + num3, width, num4);
		using (GraphicsPath path2 = Theme.Round(rectangle2, Theme.S(7f)))
		{
			Theme.FillGradient(g, path2, rectangle2, Fade(Color.FromArgb(46, 46, 54), a), Fade(Color.FromArgb(30, 30, 36), a));
			using Pen pen2 = new Pen(Color.FromArgb((int)(80f * a), 255, 255, 255));
			g.DrawPath(pen2, path2);
		}
		for (int i = 0; i < 3; i++)
		{
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(90f * a), 255, 255, 255));
			g.FillEllipse(brush2, rectangle2.X + Theme.S(6f) + i * Theme.S(6f), rectangle2.Y + Theme.S(6f), Theme.S(3.5f), Theme.S(3.5f));
		}
		using (Font font = Theme.F(9.5f, semi: true, FontStyle.Bold))
		{
			if (a > 0.6f)
			{
				Size size = TextRenderer.MeasureText("WeavOS", font, Size.Empty, TextFormatFlags.NoPadding);
				Theme.GradientText(g, "WeavOS", font, new PointF((float)rectangle2.X + (float)(rectangle2.Width - size.Width) / 2f, (float)rectangle2.Y + (float)(rectangle2.Height - size.Height) / 2f + (float)Theme.S(3f)));
			}
		}
		int num10 = rectangle2.Right + Theme.S(12f);
		TextRenderer.DrawText(g, "Название «WeavOS»", Theme.Body, new Rectangle(num10, num8, r2.Right - num10 - num3, height + 2), Fade(Theme.Text, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, "В свойствах системы", Theme.Tiny, new Rectangle(num10, num8 + height + Theme.S(2f), r2.Right - num10 - num3, height2 + 2), Fade(Theme.Sub, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}

	public static string Ask(Form owner, string level, string title, string tag, string glyph)
	{
		using TuneDialog tuneDialog = new TuneDialog(level, title, tag, glyph);
		Dimmer dimmer = null;
		if (owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized)
		{
			tuneDialog.Location = new Point(owner.Left + (owner.Width - tuneDialog.Width) / 2, Math.Max(owner.Top, owner.Top + (owner.Height - tuneDialog.Height) / 2));
			dimmer = new Dimmer(owner);
			dimmer.Show(owner);
		}
		else
		{
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			tuneDialog.Location = new Point(workingArea.Left + (workingArea.Width - tuneDialog.Width) / 2, workingArea.Top + (workingArea.Height - tuneDialog.Height) / 2);
		}
		try
		{
			IWin32Window win32Window = dimmer;
			if (tuneDialog.ShowDialog(win32Window ?? owner) != DialogResult.OK)
			{
				return null;
			}
			if (level != "light" || tuneDialog.Browser != "keep")
			{
				Prefs.LastBrowser = tuneDialog.Browser;
			}
			LastOptions = tuneDialog.Options;
			return tuneDialog.Browser;
		}
		finally
		{
			if (dimmer != null)
			{
				dimmer.Close();
				dimmer.Dispose();
			}
			owner?.Activate();
		}
	}
}
