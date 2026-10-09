// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	// "Configure level" dialog (step 1 of 2): what will change, which browser replaces Edge, optional extras.
	// The WeavOS look, computer name and password are chosen on the next step (PersonalizeDialog).
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

		private static readonly Tile[] AllTiles = new Tile[6]
		{
			new Tile { Key = "firefox", Name = "Firefox", Sub = "Mozilla, без слежки", Mono = "F", C1 = Color.FromArgb(255, 149, 0), C2 = Color.FromArgb(230, 62, 94) },
			new Tile { Key = "chrome", Name = "Google Chrome", Sub = "Google", Mono = "C", C1 = Color.FromArgb(66, 133, 244), C2 = Color.FromArgb(52, 168, 83) },
			new Tile { Key = "brave", Name = "Brave", Sub = "Блокирует рекламу", Mono = "B", C1 = Color.FromArgb(255, 99, 32), C2 = Color.FromArgb(214, 40, 40) },
			new Tile { Key = "yandex", Name = "Яндекс Браузер", Sub = "Яндекс", Mono = "Я", C1 = Color.FromArgb(255, 72, 40), C2 = Color.FromArgb(200, 30, 30) },
			new Tile { Key = "operagx", Name = "Opera GX", Sub = "Для геймеров", Mono = "GX", C1 = Color.FromArgb(250, 30, 78), C2 = Color.FromArgb(120, 20, 60) },
			new Tile { Key = "keep", Name = "Оставить Edge", Sub = "Браузер не меняется", Mono = "E", C1 = Color.FromArgb(40, 160, 220), C2 = Color.FromArgb(20, 90, 170) }
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

		private class Opt
		{
			public string Key;
			public string Title;
			public string Hint;
			public bool On;
			public Rectangle R;
		}

		private readonly List<Opt> _opts = new List<Opt>();

		private int _def;

		private Rectangle _defR;

		private readonly Rectangle[] _defChips = new Rectangle[3];

		private static readonly string[] DefNames = new string[3] { "Как есть", "Тише", "Выключить" };

		// "turn Defender off" is offered only in expert mode
		private static int DefCount => Prefs.Expert ? 3 : 2;

		private Rectangle _optLabel;

		private string _hotKey;

		// "7zip,vc,ext;def=1" — read by Invoke-FLevel
		public string Options
		{
			get
			{
				List<string> on = new List<string>();
				foreach (Opt o in _opts)
				{
					if (o.On)
					{
						on.Add(o.Key);
					}
				}
				return string.Join(",", on.ToArray()) + ";def=" + _def;
			}
		}

		public static string LastOptions = "";

		private void InitOpts(string level)
		{
			bool light = level == "light";
			bool gamer = level == "gamer";
			_opts.Add(new Opt { Key = "7zip", Title = "Поставить 7-Zip", Hint = "бесплатный архиватор для .zip, .rar, .7z", On = true });
			_opts.Add(new Opt { Key = "vc", Title = "Поставить библиотеки для игр", Hint = "Visual C++ и DirectX — без них игры не запускаются", On = gamer });
			_opts.Add(new Opt { Key = "ext", Title = "Показывать расширения файлов", Hint = "сразу видно, что это .exe, а не .pdf", On = !light });
			_opts.Add(new Opt { Key = "temp", Title = "Удалить временные файлы", Hint = "кэш и мусор из папок Temp, личное не трогаем", On = !light });
			_opts.Add(new Opt { Key = "drv", Title = "Не обновлять драйверы через Windows", Hint = "Windows не заменит твой видеодрайвер старым", On = gamer });
			_def = 0;
		}

		private int _planTop;

		private readonly List<Rectangle> _planRows = new List<Rectangle>();

		public string Browser => AllTiles[_sel].Key;

		public static string[] PlanFor(string level)
		{
			switch (level)
			{
			case "light":
				return new string[4] { "Выключим телеметрию, Copilot и Recall, рекламу и Bing в поиске", "Уберём персональные советы, просьбы об отзывах и рекламу на экране блокировки и в Проводнике", "Запретим Windows самой шифровать диски (BitLocker)", "Программы не удаляются — кроме Edge, если выбран другой браузер" };
			case "gamer":
				return new string[5] { "Всё из «Оптимальной», включая точку восстановления", "Максимальная схема питания без энергосбережения процессора и шины PCIe", "Приоритет и точный таймер для игр, без MPO, оптимизации во весь экран и ускорения мыши", "Античиты работают (FACEIT, Vanguard, EAC, BattlEye): защиту ядра HVCI/VBS не трогаем", "Чем жертвуем: поиск в Пуске без индекса, нет фоновых приложений и части уведомлений, без анимаций и прозрачности, без гибернации, ПК потребляет больше энергии" };
			default:
				return new string[7] { "Сначала создадим точку восстановления", "Всё из «Базовой», плюс удалим OneDrive и встроенный мусор", "Тёмная тема, классическое меню, панель задач без виджетов", "Обновления не раздаются чужим ПК и не перезагружают компьютер сами", "Быстрее отклик и FPS: игровой режим, HAGS, приоритет игр; на ПК — максимальная схема питания", "Защиту, службы и мышь не трогаем — монтаж, 3D и античиты работают как раньше", "Ярлыки WinWeaver на Рабочем столе и в Пуске" };
			}
		}

		private TuneDialog(string level, string title, string tag, string glyph)
		{
			_level = level;
			_title = "Автонастройка «" + title + "»";
			_tag = tag;
			_glyph = glyph;
			_plan = PlanFor(level);
			InitOpts(level);
			string def = (level == "light") ? "keep" : (Prefs.LastBrowser ?? "firefox");
			_sel = 0;
			for (int i = 0; i < AllTiles.Length; i++)
			{
				if (AllTiles[i].Key == def)
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
			foreach (Tile t in AllTiles)
			{
				if (t.Logo == null)
				{
					t.Logo = LoadLogo(t.Key);
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

		// official browser logos are embedded as br_<key>.png
		private static Image LoadLogo(string key)
		{
			try
			{
				using (System.IO.Stream st = typeof(TuneDialog).Assembly.GetManifestResourceStream("br_" + key + ".png"))
				{
					if (st != null)
					{
						return new Bitmap(Image.FromStream(st));
					}
				}
			}
			catch
			{
			}
			return null;
		}

		// the dialog is pinned to the center of the main window: no dragging, no Alt+Space → Move
		protected override void WndProc(ref Message m)
		{
			const int WM_NCLBUTTONDOWN = 0xA1;
			const int WM_SYSCOMMAND = 0x112;
			const int WM_NCHITTEST = 0x84;
			const int HTCAPTION = 2;
			const int HTCLIENT = 1;
			if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == 0xF010)
			{
				return;
			}
			if (m.Msg == WM_NCLBUTTONDOWN && (int)m.WParam == HTCAPTION)
			{
				return;
			}
			base.WndProc(ref m);
			if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCAPTION)
			{
				m.Result = (IntPtr)HTCLIENT;
			}
		}

		private int M => Theme.S(28f);

		private Rectangle _heroR;

		private int _footTop;

		private readonly List<Rectangle> _chips = new List<Rectangle>();

		private string[] ChipTexts => (_level == "gamer") ? new string[3]
		{
			"не рекомендуется для работы",
			_plan.Length + " " + InstalledView.Plural(_plan.Length, "шаг", "шага", "шагов"),
			"нужна перезагрузка"
		} : new string[3]
		{
			"3–10 минут",
			_plan.Length + " " + InstalledView.Plural(_plan.Length, "шаг", "шага", "шагов"),
			"можно откатить"
		};

		// level accent: green for the light level, cool blue for the everyday one, warm red for the maximum
		private Color Tone
		{
			get
			{
				switch (_level)
				{
				case "light":
					return Color.FromArgb(111, 207, 151);
				case "gamer":
					return Color.FromArgb(240, 112, 132);
				default:
					return Color.FromArgb(128, 170, 255);
				}
			}
		}

		private string LevelNo => (_level == "light") ? "1" : ((_level == "gamer") ? "3" : "2");

		private void DoLayoutDlg()
		{
			int w = Theme.S(980f);
			int m = M;
			int gapC = Theme.S(30f);
			int colW = (w - m * 2 - gapC) / 2;
			int lx = m;
			int rx = m + colW + gapC;
			_heroR = new Rectangle(0, 0, w, Theme.S(136f));
			_chips.Clear();
			int cxr = w - m;
			string[] ct = ChipTexts;
			for (int i = ct.Length - 1; i >= 0; i--)
			{
				int cw0 = Theme.Measure(ct[i], Theme.Small).Width + Theme.S(34f);
				_chips.Insert(0, new Rectangle(cxr - cw0, Theme.S(28f), cw0, Theme.S(26f)));
				cxr -= cw0 + Theme.S(8f);
			}
			int top = _heroR.Bottom + Theme.S(22f);
			int y = top;
			_planTop = y;
			int cw = colW - Theme.S(40f);
			_planRows.Clear();
			y += Theme.S(30f);
			foreach (string p in _plan)
			{
				int h = Math.Max(Theme.S(24f), Theme.Measure(p, Theme.Small, cw, wrap: true).Height);
				_planRows.Add(new Rectangle(lx + Theme.S(40f), y, cw, h));
				y += h + Theme.S(10f);
			}
			_planR = new Rectangle(lx, _planTop, colW, y - _planTop);
			y += Theme.S(6f);
			_fixedLabel = new Rectangle(lx, y, colW, Theme.S(20f));
			y = _fixedLabel.Bottom + Theme.S(10f);
			_fixedR = new Rectangle(lx, y, colW, Theme.S(64f));
			int leftBottom = _fixedR.Bottom;
			y = top;
			_tilesLabel = new Rectangle(rx, y, colW, Theme.S(30f) + Theme.Measure(BrowserNote, Theme.Small, colW, wrap: true).Height + Theme.S(2f));
			y = _tilesLabel.Bottom + Theme.S(10f);
			int gap = Theme.S(10f);
			int tw = (colW - gap) / 2;
			int th = Theme.S(60f);
			for (int i = 0; i < AllTiles.Length; i++)
			{
				AllTiles[i].R = new Rectangle(rx + i % 2 * (tw + gap), y + i / 2 * (th + gap), tw, th);
			}
			int rightBottom = y + th * 3 + gap * 2;
			y = Math.Max(leftBottom, rightBottom) + Theme.S(24f);
			_optLabel = new Rectangle(m, y, w - m * 2, Theme.S(20f));
			y = _optLabel.Bottom + Theme.S(10f);
			int og = Theme.S(10f);
			int ow = (w - m * 2 - og * 2) / 3;
			int oh = Theme.S(58f);
			for (int i = 0; i < _opts.Count; i++)
			{
				_opts[i].R = new Rectangle(m + i % 3 * (ow + og), y + i / 3 * (oh + og), ow, oh);
			}
			int di = _opts.Count;
			_defR = new Rectangle(m + di % 3 * (ow + og), y + di / 3 * (oh + og), ow, oh);
			int sx = _defR.X + Theme.S(10f);
			int segW = _defR.Width - Theme.S(20f);
			int nc = DefCount;
			int chipW = segW / nc;
			for (int i = 0; i < 3; i++)
			{
				_defChips[i] = (i < nc) ? new Rectangle(sx + i * chipW, _defR.Y + Theme.S(29f), (i == nc - 1) ? (segW - chipW * (nc - 1)) : chipW, Theme.S(22f)) : Rectangle.Empty;
			}
			y = _defR.Bottom + Theme.S(22f);
			_footTop = y;
			int bh = Theme.S(40f);
			int fh = bh + Theme.S(32f);
			base.Size = new Size(w, y + fh);
			int by = y + (fh - bh) / 2;
			_cancel.SetBounds(w - m - _cancel.Width, by, _cancel.Width, bh);
			_ok.SetBounds(_cancel.Left - Theme.S(10f) - _ok.Width, by, _ok.Width, bh);
			_noteR = new Rectangle(m, y, _ok.Left - m - Theme.S(20f), fh);
		}

		// small switch used by the optional items
		private static void DrawSwitch(Graphics g, Rectangle r, bool on, Color tone, float a)
		{
			using (GraphicsPath p = Theme.Round(r, r.Height / 2f))
			{
				if (on)
				{
					Theme.FillGradient(g, p, r, Fade(Theme.Mix(tone, Color.White, 0.25f), a), Fade(tone, a), 0f);
				}
				else
				{
					using (SolidBrush b = new SolidBrush(Fade(Theme.Control2, a)))
					{
						g.FillPath(b, p);
					}
					using (Pen pen = new Pen(Fade(Theme.BorderHi, a)))
					{
						g.DrawPath(pen, p);
					}
				}
			}
			int k = r.Height - Theme.S(6f);
			Rectangle knob = new Rectangle(on ? (r.Right - Theme.S(3f) - k) : (r.X + Theme.S(3f)), r.Y + Theme.S(3f), k, k);
			using (SolidBrush kb = new SolidBrush(Fade(on ? Color.White : Theme.Sub, a)))
			{
				g.FillEllipse(kb, knob);
			}
		}

		private void PaintOpts(Graphics g, float a)
		{
			int on = 0;
			foreach (Opt o in _opts)
			{
				if (o.On)
				{
					on++;
				}
			}
			Label(g, "По желанию", _optLabel, a, "включено " + on + " из " + _opts.Count);
			Color tone = Tone;
			foreach (Opt o in _opts)
			{
				bool hot = _hotKey == o.Key;
				Rectangle r = o.R;
				r.Offset(0, (int)((1f - a) * Theme.S(8f)));
				using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(12f)))
				{
					Theme.FillGradient(g, p, r, Fade(hot ? Theme.CardHot1 : Theme.Card1, a), Fade(hot ? Theme.CardHot2 : Theme.Card2, a));
					if (o.On)
					{
						using (LinearGradientBrush wb = new LinearGradientBrush(new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb((int)(30 * a), tone), Color.FromArgb(0, tone), 0f))
						{
							g.FillPath(wb, p);
						}
					}
					using (Pen pen = new Pen(Fade(o.On ? Theme.Mix(Theme.Border, tone, 0.45f) : (hot ? Theme.BorderHi : Theme.Border), a)))
					{
						g.DrawPath(pen, p);
					}
				}
				Rectangle sw = new Rectangle(r.Right - Theme.S(14f) - Theme.S(36f), r.Y + (r.Height - Theme.S(20f)) / 2, Theme.S(36f), Theme.S(20f));
				DrawSwitch(g, sw, o.On, tone, a);
				int tx = r.X + Theme.S(16f);
				int tw = sw.X - tx - Theme.S(10f);
				TextRenderer.DrawText(g, o.Title, Theme.Body, new Rectangle(tx, r.Y + Theme.S(10f), tw, Theme.S(20f)), Fade(Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				TextRenderer.DrawText(g, o.Hint, Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(32f), tw, Theme.S(16f)), Fade(o.On ? Theme.Sub : Theme.Dim, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			Rectangle d = _defR;
			d.Offset(0, (int)((1f - a) * Theme.S(8f)));
			int dy = d.Y - _defR.Y;
			using (GraphicsPath p = Theme.Round(new RectangleF(d.X + 0.5f, d.Y + 0.5f, d.Width - 1, d.Height - 1), Theme.S(12f)))
			{
				Theme.FillGradient(g, p, d, Fade(Theme.Card1, a), Fade(Theme.Card2, a));
				using (Pen pen = new Pen(Fade((_def == 2) ? Theme.Mix(Theme.Border, Theme.Yellow, 0.5f) : Theme.Border, a)))
				{
					g.DrawPath(pen, p);
				}
			}
			TextRenderer.DrawText(g, "Антивирус Defender", Theme.Body, new Rectangle(d.X + Theme.S(14f), d.Y + Theme.S(6f), d.Width - Theme.S(28f), Theme.S(20f)), Fade(Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			Rectangle track = Rectangle.Union(_defChips[0], _defChips[DefCount - 1]);
			track.Offset(0, dy);
			using (GraphicsPath tp = Theme.Round(track, track.Height / 2f))
			{
				using (SolidBrush tb = new SolidBrush(Fade(Color.FromArgb(22, 22, 27), a)))
				{
					g.FillPath(tb, tp);
				}
			}
			for (int i = 0; i < DefCount; i++)
			{
				Rectangle c = _defChips[i];
				c.Offset(0, dy);
				bool sel = i == _def;
				bool hot = _hotKey == "def" + i;
				Rectangle cr = Rectangle.Inflate(c, -Theme.S(2f), -Theme.S(2f));
				using (GraphicsPath cp = Theme.Round(cr, cr.Height / 2f))
				{
					if (sel)
					{
						if (i == 2)
						{
							Theme.FillGradient(g, cp, cr, Fade(Theme.Mix(Theme.Yellow, Color.White, 0.2f), a), Fade(Theme.Mix(Theme.Yellow, Theme.Card2, 0.25f), a));
						}
						else
						{
							Theme.FillGradient(g, cp, cr, Fade(Theme.Silver1, a), Fade(Theme.Silver2, a));
						}
					}
					else if (hot)
					{
						using (SolidBrush sb = new SolidBrush(Fade(Theme.Control1, a)))
						{
							g.FillPath(sb, cp);
						}
					}
				}
				Color tc = sel ? Theme.OnSilver : (hot ? Theme.Text : Theme.Sub);
				TextRenderer.DrawText(g, DefNames[i], Theme.Tiny, c, Fade(tc, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			if (!Anim.Enabled)
			{
				return;
			}
			_appear = 0f;
			try
			{
				base.Opacity = 0.0;
			}
			catch
			{
			}
			Anim.Start(this, 420, delegate(float t)
			{
				_appear = t;
				try
				{
					base.Opacity = Math.Min(1.0, t * 2.2);
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
				int d = (e.KeyCode == Keys.Right) ? 1 : ((e.KeyCode == Keys.Left) ? (-1) : ((e.KeyCode == Keys.Down) ? 2 : (-2)));
				Choose((_sel + d + AllTiles.Length) % AllTiles.Length);
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
				// the frame glides straight from where it is now to the new tile (no detour through the tiles in between)
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
			foreach (Opt o in _opts)
			{
				if (o.R.Contains(p))
				{
					return o.Key;
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
			int h = Hit(e.Location);
			string ho = HitOpt(e.Location);
			if (h != _hover || ho != _hotKey)
			{
				_hover = h;
				_hotKey = ho;
				Cursor = (h >= 0 || ho != null) ? Cursors.Hand : Cursors.Default;
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
			int h = Hit(e.Location);
			if (h >= 0)
			{
				Choose(h);
			}
			string ho = HitOpt(e.Location);
			if (ho != null && e.Button == MouseButtons.Left)
			{
				if (ho.StartsWith("def"))
				{
					_def = int.Parse(ho.Substring(3));
				}
				else
				{
					foreach (Opt o in _opts)
					{
						if (o.Key == ho)
						{
							o.On = !o.On;
						}
					}
				}
				Invalidate();
			}
			base.OnMouseClick(e);
		}

		// staggered appearance: element k fades/rises a bit later than element k-1
		private float A(int k)
		{
			return Anim.EaseOut(Anim.Clamp(_appear * 1.6f - k * 0.08f));
		}

		private static Color Fade(Color c, float a)
		{
			return Theme.Mix(Theme.Card2, c, a);
		}

		// section caption: small caps + thin rule to the right (+ optional counter)
		private void Label(Graphics g, string text, Rectangle r, float a, string extra = null)
		{
			string t = text.ToUpperInvariant();
			Size ts = Theme.Measure(t, Theme.H2);
			TextRenderer.DrawText(g, t, Theme.H2, new Point(r.X, r.Y), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix);
			int x = r.X + ts.Width + Theme.S(10f);
			if (r.Width > 0)
			{
				int right = r.Right;
				if (!string.IsNullOrEmpty(extra))
				{
					int ew = Theme.Measure(extra, Theme.Tiny).Width + Theme.S(8f);
					TextRenderer.DrawText(g, extra, Theme.Tiny, new Rectangle(r.Right - ew, r.Y, ew, ts.Height), Fade(Theme.Dim, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
					right -= ew + Theme.S(10f);
				}
				if (right > x)
				{
					using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(x - 1, 0, right - x + 2, 1), Fade(Theme.BorderHi, a), Color.FromArgb(0, Theme.Border), 0f))
					{
						g.FillRectangle(lb, x, r.Y + ts.Height / 2, right - x, 1);
					}
				}
			}
		}

		public void PaintSurface(Graphics g)
		{
			PaintBg(g);
		}

		// banner with the WeavOS wallpaper, tone glow, footer band
		private void PaintBg(Graphics g)
		{
			Theme.Smooth(g);
			int w = base.Width;
			int h = base.Height;
			Color tone = Tone;
			using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, -1, w, h + 2), Color.FromArgb(34, 34, 40), Color.FromArgb(21, 21, 25), 90f))
			{
				g.FillRectangle(bg, 0, 0, w, h);
			}
			Rectangle hr = _heroR;
			if (_wall != null)
			{
				float k = Math.Max(hr.Width / (float)_wall.Width, hr.Height / (float)_wall.Height);
				float iw = _wall.Width * k;
				float ih = _wall.Height * k;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				Region old = g.Clip;
				g.SetClip(hr, CombineMode.Intersect);
				using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
				{
					ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.55f });
					g.DrawImage(_wall, new Rectangle(hr.X + (int)((hr.Width - iw) / 2f), hr.Y + (int)((hr.Height - ih) * 0.4f), (int)iw, (int)ih), 0, 0, _wall.Width, _wall.Height, GraphicsUnit.Pixel, ia);
				}
				g.Clip = old;
			}
			using (LinearGradientBrush vl = new LinearGradientBrush(new Rectangle(hr.X - 1, hr.Y, hr.Width + 2, hr.Height), Color.FromArgb(235, 24, 24, 29), Color.FromArgb(120, 24, 24, 29), 0f))
			{
				g.FillRectangle(vl, hr);
			}
			using (LinearGradientBrush vb = new LinearGradientBrush(new Rectangle(hr.X, hr.Y - 1, hr.Width, hr.Height + 2), Color.FromArgb(0, 32, 32, 38), Color.FromArgb(255, 33, 33, 39), 90f))
			{
				ColorBlend cb = new ColorBlend();
				cb.Positions = new float[3] { 0f, 0.55f, 1f };
				cb.Colors = new Color[3] { Color.FromArgb(0, 33, 33, 39), Color.FromArgb(60, 33, 33, 39), Color.FromArgb(255, 33, 33, 39) };
				vb.InterpolationColors = cb;
				g.FillRectangle(vb, hr);
			}
			int gr = Theme.S(210f);
			int gx = M + Theme.S(32f);
			int gy = Theme.S(66f);
			using (GraphicsPath gp = new GraphicsPath())
			{
				gp.AddEllipse(gx - gr, gy - gr, gr * 2, gr * 2);
				using (PathGradientBrush pb = new PathGradientBrush(gp))
				{
					pb.CenterColor = Color.FromArgb(58, tone);
					pb.SurroundColors = new Color[1] { Color.FromArgb(0, tone) };
					pb.FocusScales = new PointF(0.05f, 0.05f);
					g.FillPath(pb, gp);
				}
			}
			using (LinearGradientBrush top = new LinearGradientBrush(new Rectangle(0, 0, w, 2), Color.Transparent, Color.Transparent, 0f))
			{
				ColorBlend cb = new ColorBlend();
				cb.Positions = new float[3] { 0f, 0.35f, 1f };
				cb.Colors = new Color[3] { Color.FromArgb(0, tone), Color.FromArgb(220, Theme.Mix(tone, Color.White, 0.35f)), Color.FromArgb(0, tone) };
				top.InterpolationColors = cb;
				g.FillRectangle(top, 0, 0, w, Math.Max(1, Theme.S(1.5f)));
			}
			if (_footTop > 0)
			{
				using (SolidBrush fb = new SolidBrush(Color.FromArgb(80, 8, 8, 10)))
				{
					g.FillRectangle(fb, 0, _footTop, w, h - _footTop);
				}
				using (Pen lp = new Pen(Color.FromArgb(20, 255, 255, 255)))
				{
					g.DrawLine(lp, 0, _footTop, w, _footTop);
				}
			}
			using (Pen pen = new Pen(Theme.BorderHi))
			{
				g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			PaintBg(g);
			Theme.Smooth(g);
			Color tone = Tone;
			int m = M;
			float a0 = A(0);
			int lift = (int)((1f - a0) * Theme.S(8f));
			Rectangle icon = new Rectangle(m, Theme.S(34f) + lift, Theme.S(64f), Theme.S(64f));
			using (GraphicsPath ip = Theme.Round(icon, icon.Width * 0.28f))
			{
				Theme.FillGradient(g, ip, icon, Fade(Theme.Mix(Color.FromArgb(70, 70, 82), tone, 0.35f), a0), Fade(Theme.Mix(Color.FromArgb(32, 32, 38), tone, 0.18f), a0));
				using (Pen ipen = new Pen(Fade(Theme.Mix(Theme.BorderHi, tone, 0.6f), a0), Math.Max(1f, Theme.S(1.2f))))
				{
					g.DrawPath(ipen, ip);
				}
			}
			Theme.DrawIcon(g, _glyph, Theme.IconBig ?? Theme.Icon, icon, Fade(Theme.Mix(tone, Color.White, 0.55f), a0));
			int tx = icon.Right + Theme.S(18f);
			string kicker = "АВТОНАСТРОЙКА  •  УРОВЕНЬ " + LevelNo + " ИЗ 3";
			TextRenderer.DrawText(g, kicker, DlgChrome.KickerF, new Point(tx, Theme.S(36f) + lift), Fade(Theme.Mix(tone, Color.White, 0.35f), a0), TextFormatFlags.NoPrefix);
			string name = _title.Replace("Автонастройка «", "").TrimEnd('»');
			using (Font big = Theme.F(21f, semi: true))
			{
				if (a0 > 0.5f)
				{
					Theme.GradientText(g, name, big, new PointF(tx - Theme.S(1f), Theme.S(52f) + lift));
				}
			}
			TextRenderer.DrawText(g, _tag, Theme.Body, new Point(tx, Theme.S(88f) + lift), Fade(Theme.Sub, a0), TextFormatFlags.NoPrefix);
			string[] ct = ChipTexts;
			for (int i = 0; i < _chips.Count && i < ct.Length; i++)
			{
				Rectangle c = _chips[i];
				c.Offset(0, lift);
				bool warnChip = _level == "gamer" && i == 0;
				using (GraphicsPath cp = Theme.Round(new RectangleF(c.X + 0.5f, c.Y + 0.5f, c.Width - 1, c.Height - 1), c.Height / 2f))
				{
					using (SolidBrush cb = new SolidBrush(warnChip ? Color.FromArgb((int)(60 * a0), Theme.Yellow) : Color.FromArgb((int)(150 * a0), 20, 20, 24)))
					{
						g.FillPath(cb, cp);
					}
					using (Pen cpen = new Pen(warnChip ? Color.FromArgb((int)(150 * a0), Theme.Yellow) : Color.FromArgb((int)(60 * a0), 255, 255, 255)))
					{
						g.DrawPath(cpen, cp);
					}
				}
				Color dc = (_level == "gamer" && (i == 0 || i == 2)) ? Theme.Yellow : ((i == 2) ? Theme.Green : Theme.Mix(tone, Color.White, 0.3f));
				int d = Theme.S(6f);
				using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(255 * a0), dc)))
				{
					g.FillEllipse(db, c.X + Theme.S(12f), c.Y + (c.Height - d) / 2, d, d);
				}
				TextRenderer.DrawText(g, ct[i], Theme.Small, new Rectangle(c.X + Theme.S(24f), c.Y, c.Width - Theme.S(30f), c.Height), Fade(warnChip ? Theme.Mix(Theme.Yellow, Color.White, 0.3f) : Theme.Text, a0), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
			float a1 = A(1);
			Label(g, "Что изменится", new Rectangle(_planR.X, _planTop, _planR.Width, 0), a1);
			int lh = Theme.Measure("Ag", Theme.Small).Height;
			int ns = Theme.S(24f);
			int lineX = _planR.X + ns / 2;
			if (_planRows.Count > 1)
			{
				int y1 = _planRows[0].Y + ns / 2;
				int y2 = _planRows[_planRows.Count - 1].Y + ns / 2;
				using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(lineX - 1, y1 - 1, 3, y2 - y1 + 2), Fade(Theme.Mix(Theme.BorderHi, tone, 0.5f), a1), Fade(Theme.Border, a1), 90f))
				{
					g.FillRectangle(lb, lineX - Theme.S(1f) / 2, y1, Math.Max(1, Theme.S(1.5f)), y2 - y1);
				}
			}
			for (int i = 0; i < _planRows.Count; i++)
			{
				float ai = A(1 + i);
				Rectangle r = _planRows[i];
				int dy = (int)((1f - ai) * Theme.S(6f));
				bool cost = _plan[i].StartsWith("Чем жертвуем");
				Rectangle nr = new Rectangle(_planR.X, r.Y + dy, ns, ns);
				using (SolidBrush nb = new SolidBrush(Fade(Color.FromArgb(30, 30, 36), ai)))
				{
					g.FillEllipse(nb, nr);
				}
				using (Pen np = new Pen(Fade(cost ? Theme.Yellow : Theme.Mix(Theme.BorderHi, tone, 0.55f), ai), Math.Max(1f, Theme.S(1.2f))))
				{
					g.DrawEllipse(np, nr.X + 0.5f, nr.Y + 0.5f, nr.Width - 1, nr.Height - 1);
				}
				TextRenderer.DrawText(g, cost ? "!" : (i + 1).ToString(), Theme.Tiny, nr, Fade(cost ? Theme.Yellow : Theme.Mix(tone, Color.White, 0.5f), ai), TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				Rectangle trr = new Rectangle(r.X, r.Y + dy + Math.Max(0, (ns - lh) / 2), r.Width, r.Height);
				TextRenderer.DrawText(g, _plan[i], Theme.Small, trr, Fade(cost ? Theme.Yellow : Theme.Text, ai), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			}
			float a2 = A(3);
			Label(g, "Какой браузер поставить вместо Edge", _tilesLabel, a2);
			TextRenderer.DrawText(g, BrowserNote, Theme.Small, new Rectangle(_tilesLabel.X, _tilesLabel.Y + Theme.S(28f), _tilesLabel.Width, _tilesLabel.Height - Theme.S(28f)), Fade(Theme.Dim, a2), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			PaintTiles(g);
			float a3 = A(6);
			Label(g, "Стиль " + Brand.OsName + " — по желанию, на следующем шаге", _fixedLabel, a3);
			PaintFixed(g, a3);
			PaintOpts(g, A(7));
			float a4 = A(8);
			string sum = "Браузер: " + AllTiles[_sel].Name + "  •  Defender: " + ((_def == 0) ? "без изменений" : DefNames[_def].ToLowerInvariant()) + "  •  займёт 3–10 минут";
			string note = (_def == 2) ? "Defender выключится, только если сначала отключить «Защиту от подделки» — мы откроем её настройки. Без антивируса ПК уязвимее." : ((_def == 1) ? "«Тише»: защита работает, но файлы не отправляются в Microsoft и уведомлений меньше." : "Перед изменениями создаётся точка восстановления, а каждый шаг можно отменить в «Истории».");
			Rectangle hi = new Rectangle(_noteR.X, _noteR.Y + (_noteR.Height - Theme.S(30f)) / 2, Theme.S(30f), Theme.S(30f));
			using (SolidBrush hb = new SolidBrush(Fade(Color.FromArgb(42, 42, 50), a4)))
			{
				g.FillEllipse(hb, hi);
			}
			Theme.DrawIcon(g, (_def == 2) ? Glyph.Warn : Glyph.Restore, Theme.IconSmall, hi, Fade((_def == 2) ? Theme.Yellow : Theme.Silver2, a4));
			int nx = hi.Right + Theme.S(12f);
			int nw = _noteR.Right - nx;
			int mid = _noteR.Y + _noteR.Height / 2;
			TextRenderer.DrawText(g, sum, Theme.Small, new Rectangle(nx, mid - Theme.S(19f), nw, Theme.S(18f)), Fade(Theme.Text, a4), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.Bottom);
			TextRenderer.DrawText(g, note, Theme.Tiny, new Rectangle(nx, mid + Theme.S(1f), nw, Theme.S(18f)), Fade((_def == 2) ? Theme.Yellow : Theme.Sub, a4), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		private RectangleF _fromR = RectangleF.Empty;

		private RectangleF SelRect()
		{
			Rectangle to = AllTiles[_sel].R;
			float t = Anim.Clamp(_selT.Value);
			if (_fromR.IsEmpty || t >= 0.999f)
			{
				return to;
			}
			return new RectangleF(_fromR.X + (to.X - _fromR.X) * t, _fromR.Y + (to.Y - _fromR.Y) * t, _fromR.Width + (to.Width - _fromR.Width) * t, _fromR.Height + (to.Height - _fromR.Height) * t);
		}

		private void PaintTiles(Graphics g)
		{
			float selA = A(4);
			RectangleF sr = SelRect();
			Color brand = AllTiles[_sel].C1;
			for (int k = 3; k >= 1; k--)
			{
				RectangleF gr = RectangleF.Inflate(sr, Theme.S(2.5f) * k, Theme.S(2.5f) * k);
				using (GraphicsPath gp = Theme.Round(gr, Theme.S(11f) + Theme.S(2.5f) * k))
				{
					using (SolidBrush gb = new SolidBrush(Color.FromArgb((int)((14 - k * 3) * selA), brand)))
					{
						g.FillPath(gb, gp);
					}
				}
			}
			for (int i = 0; i < AllTiles.Length; i++)
			{
				Tile t = AllTiles[i];
				float a = A(3 + i / 3);
				Rectangle r = t.R;
				bool sel = i == _sel;
				bool hov = i == _hover && !sel;
				r.Offset(0, (int)((1f - a) * Theme.S(8f)) - (hov ? Theme.S(1f) : 0));
				using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(11f)))
				{
					Theme.FillGradient(g, p, r, Fade(hov ? Theme.CardHot1 : Theme.Card1, a), Fade(hov ? Theme.CardHot2 : Theme.Card2, a));
					if (sel)
					{
						using (LinearGradientBrush tb = new LinearGradientBrush(new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb((int)(46 * a), t.C1), Color.FromArgb((int)(8 * a), t.C2), 0f))
						{
							g.FillPath(tb, p);
						}
					}
					using (Pen pen = new Pen(Fade(hov ? Theme.BorderHi : Theme.Border, a)))
					{
						g.DrawPath(pen, p);
					}
				}
				int cs = Theme.S(36f);
				Rectangle c = new Rectangle(r.X + Theme.S(13f), r.Y + (r.Height - cs) / 2, cs, cs);
				if (t.Logo != null)
				{
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					float la = a * ((sel || hov) ? 1f : 0.82f);
					using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
					{
						ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = la });
						g.DrawImage(t.Logo, c, 0, 0, t.Logo.Width, t.Logo.Height, GraphicsUnit.Pixel, ia);
					}
				}
				else
				{
					using (GraphicsPath cp = new GraphicsPath())
					{
						cp.AddEllipse(c);
						Theme.FillGradient(g, cp, c, Fade(t.C1, a), Fade(t.C2, a), 45f);
					}
					TextRenderer.DrawText(g, t.Mono, (t.Mono.Length > 1) ? Theme.F(9f, semi: true) : Theme.F(12f, semi: true), c, Fade(Color.White, a), TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
				int x = c.Right + Theme.S(12f);
				int w = r.Right - x - Theme.S(36f);
				int nh = Theme.Measure("Ag", Theme.Title).Height;
				int sh = Theme.Measure("Ag", Theme.Tiny).Height;
				int ty = r.Y + (r.Height - nh - sh - Theme.S(2f)) / 2;
				TextRenderer.DrawText(g, t.Name, Theme.Title, new Rectangle(x, ty, w, nh + 2), Fade(Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				TextRenderer.DrawText(g, t.Sub, Theme.Tiny, new Rectangle(x, ty + nh + Theme.S(2f), w + Theme.S(24f), sh + 2), Fade(sel ? Theme.Mix(Theme.Sub, Color.White, 0.25f) : Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				int rs = Theme.S(20f);
				Rectangle radio = new Rectangle(r.Right - Theme.S(14f) - rs, r.Y + (r.Height - rs) / 2, rs, rs);
				if (sel)
				{
					using (GraphicsPath rp = new GraphicsPath())
					{
						rp.AddEllipse(radio);
						Theme.FillGradient(g, rp, radio, Theme.Silver1, Theme.Silver2);
					}
					using (Pen ck = new Pen(Theme.OnSilver, Math.Max(1.5f, Theme.S(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
					{
						float cx = radio.X;
						float cy = radio.Y;
						float s = rs;
						g.DrawLines(ck, new PointF[3]
						{
							new PointF(cx + s * 0.28f, cy + s * 0.52f),
							new PointF(cx + s * 0.44f, cy + s * 0.68f),
							new PointF(cx + s * 0.73f, cy + s * 0.36f)
						});
					}
				}
				else
				{
					using (Pen rp2 = new Pen(Fade(hov ? Theme.Sub : Theme.BorderHi, a), 1.3f))
					{
						g.DrawEllipse(rp2, radio);
					}
				}
			}
			RectangleF fr = SelRect();
			fr.Inflate(0.5f, 0.5f);
			using (GraphicsPath sp = Theme.Round(fr, Theme.S(11.5f)))
			{
				using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(fr.X, fr.Y - 1f, fr.Width, fr.Height + 2f), Color.FromArgb((int)(230 * selA), Theme.Mix(Theme.Silver1, brand, 0.25f)), Color.FromArgb((int)(110 * selA), Theme.Silver2), 90f))
				{
					using (Pen pen2 = new Pen(b, Math.Max(1.5f, Theme.S(1.5f))))
					{
						g.DrawPath(pen2, sp);
					}
				}
			}
		}

		private void FixedTile(Graphics g, Rectangle r, float a)
		{
			using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(12f)))
			{
				Theme.FillGradient(g, p, r, Fade(Theme.Card1, a), Fade(Theme.Card2, a));
				using (Pen pen = new Pen(Fade(Theme.Border, a)))
				{
					g.DrawPath(pen, p);
				}
			}
		}

		// two tiles side by side: wallpaper preview and "About this PC" mock
		private void PaintFixed(Graphics g, float a)
		{
			Rectangle r = _fixedR;
			r.Offset(0, (int)((1f - a) * Theme.S(8f)));
			int gap = Theme.S(10f);
			int w1 = (r.Width - gap) / 2;
			Rectangle t1 = new Rectangle(r.X, r.Y, w1, r.Height);
			Rectangle t2 = new Rectangle(r.X + w1 + gap, r.Y, r.Width - w1 - gap, r.Height);
			FixedTile(g, t1, a);
			FixedTile(g, t2, a);
			int pad = Theme.S(10f);
			int th = r.Height - pad * 2;
			int tw = th * 14 / 10;
			Rectangle thumb = new Rectangle(t1.X + pad, t1.Y + pad, tw, th);
			using (GraphicsPath tp = Theme.Round(thumb, Theme.S(7f)))
			{
				if (_wall != null)
				{
					Region old = g.Clip;
					g.SetClip(tp, CombineMode.Intersect);
					float k = Math.Max(thumb.Width / (float)_wall.Width, thumb.Height / (float)_wall.Height);
					float iw = _wall.Width * k;
					float ih = _wall.Height * k;
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					g.DrawImage(_wall, thumb.X + (thumb.Width - iw) / 2f, thumb.Y + (thumb.Height - ih) / 2f, iw, ih);
					using (SolidBrush veil = new SolidBrush(Color.FromArgb((int)(255 * (1f - a)), Theme.Card2)))
					{
						g.FillRectangle(veil, thumb);
					}
					g.Clip = old;
				}
				using (Pen tpen = new Pen(Color.FromArgb((int)(80 * a), 255, 255, 255)))
				{
					g.DrawPath(tpen, tp);
				}
			}
			int titleH = Theme.Measure("Ag", Theme.Body).Height;
			int subH = Theme.Measure("Ag", Theme.Tiny).Height;
			int ty = t1.Y + (t1.Height - titleH - Theme.S(2f) - subH) / 2;
			int x1 = thumb.Right + Theme.S(12f);
			TextRenderer.DrawText(g, "Обои WeavOS", Theme.Body, new Rectangle(x1, ty, t1.Right - x1 - pad, titleH + 2), Fade(Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, "Рабочий стол и блокировка", Theme.Tiny, new Rectangle(x1, ty + titleH + Theme.S(2f), t1.Right - x1 - pad, subH + 2), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			Rectangle mock = new Rectangle(t2.X + pad, t2.Y + pad, tw, th);
			using (GraphicsPath mp = Theme.Round(mock, Theme.S(7f)))
			{
				Theme.FillGradient(g, mp, mock, Fade(Color.FromArgb(46, 46, 54), a), Fade(Color.FromArgb(30, 30, 36), a));
				using (Pen mpen = new Pen(Color.FromArgb((int)(80 * a), 255, 255, 255)))
				{
					g.DrawPath(mpen, mp);
				}
			}
			for (int i = 0; i < 3; i++)
			{
				using (SolidBrush db = new SolidBrush(Color.FromArgb((int)(90 * a), 255, 255, 255)))
				{
					g.FillEllipse(db, mock.X + Theme.S(6f) + i * Theme.S(6f), mock.Y + Theme.S(6f), Theme.S(3.5f), Theme.S(3.5f));
				}
			}
			using (Font f = Theme.F(9.5f, semi: true, FontStyle.Bold))
			{
				if (a > 0.6f)
				{
					Size ns = TextRenderer.MeasureText("WeavOS", f, Size.Empty, TextFormatFlags.NoPadding);
					Theme.GradientText(g, "WeavOS", f, new PointF(mock.X + (mock.Width - ns.Width) / 2f, mock.Y + (mock.Height - ns.Height) / 2f + Theme.S(3f)));
				}
			}
			int x2 = mock.Right + Theme.S(12f);
			TextRenderer.DrawText(g, "Название «WeavOS»", Theme.Body, new Rectangle(x2, ty, t2.Right - x2 - pad, titleH + 2), Fade(Theme.Text, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, "В свойствах системы", Theme.Tiny, new Rectangle(x2, ty + titleH + Theme.S(2f), t2.Right - x2 - pad, subH + 2), Fade(Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		public static string Ask(Form owner, string level, string title, string tag, string glyph)
		{
			using (TuneDialog d = new TuneDialog(level, title, tag, glyph))
			{
				Dimmer dimmer = null;
				if (owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized)
				{
					d.Location = new Point(owner.Left + (owner.Width - d.Width) / 2, Math.Max(owner.Top, owner.Top + (owner.Height - d.Height) / 2));
					dimmer = new Dimmer(owner);
					dimmer.Show(owner);
				}
				else
				{
					Rectangle wa = Screen.PrimaryScreen.WorkingArea;
					d.Location = new Point(wa.Left + (wa.Width - d.Width) / 2, wa.Top + (wa.Height - d.Height) / 2);
				}
				try
				{
					if (d.ShowDialog((IWin32Window)dimmer ?? owner) != DialogResult.OK)
					{
						return null;
					}
					if (level != "light" || d.Browser != "keep")
					{
						Prefs.LastBrowser = d.Browser;
					}
					LastOptions = d.Options;
					return d.Browser;
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
	}
}
