// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

// WinWeaver — Windows 11 setup tool
// Copyright © 2026 fel7xxx. Licensed under GPL-3.0-or-later, see LICENSE.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinWeaver
{
	// First start: language, install into Program Files or run portable, autostart and update notices
	internal class WelcomeDialog : PanelDialog
	{
		private readonly TileChoice _lang;

		private readonly TileChoice _inst;

		private readonly FCheck _auto;

		private readonly FCheck _upd;

		private readonly int _labLang;

		private readonly int _labInst;

		private readonly int _labMore;

		private readonly List<string> _codes = new List<string>();

		private readonly Image _banner;

		public string Lang => _codes[_lang.Selected];

		public bool Install => _inst.Selected == 0;

		public bool Autostart => _auto.Checked && Install;

		public bool Updates => _upd.Checked;

		public WelcomeDialog()
			: base("Добро пожаловать в " + Brand.Name, "Настройка Windows 11 в пару кликов: приватность, оформление, скорость и обслуживание. Перед изменениями создаётся точка восстановления, а каждый шаг можно отменить в «Истории».", "Начать", null, DlgIcon.Info, 720f)
		{
			int w = InnerW;
			_banner = Walls.Banner(0, 1280) ?? Walls.Load(0, 1280);
			int y = Theme.S(150f);
			_labLang = y;
			y += Theme.S(22f);
			_lang = new TileChoice();
			foreach (string c in L.Available())
			{
				_codes.Add(c);
				_lang.Add(L.NameOf(c), (c == "ru") ? "интерфейс на русском" : ((c == "en") ? "English interface" : c), null, null);
			}
			_lang.Selected = Math.Max(0, _codes.IndexOf(L.Lang));
			_lang.Changed += delegate (int i)
			{
				L.Set(_codes[i]);
				Invalidate(invalidateChildren: true);
			};
			_lang.SetBounds(M, y, w, _lang.Measure(w));
			Content.Controls.Add(_lang);
			y = _lang.Bottom + Theme.S(20f);
			_labInst = y;
			y += Theme.S(22f);
			_inst = new TileChoice { hintLines = 2 };
			_inst.Add("Установить", "в Program Files: ярлык в Пуске, удаление через «Установленные приложения»");
			_inst.Add("Без установки", "запускать этот файл как есть, ничего не копируя");
			_inst.Selected = 0;
			_inst.Changed += delegate
			{
				_auto.Enabled = _inst.Selected == 0;
				_auto.Invalidate();
				Content.Invalidate();
			};
			_inst.SetBounds(M, y, w, _inst.Measure(w));
			Content.Controls.Add(_inst);
			y = _inst.Bottom + Theme.S(20f);
			_labMore = y;
			y += Theme.S(22f);
			_auto = new FCheck("Открывать " + Brand.Name + " при входе в Windows") { Checked = false };
			_auto.Location = new Point(M, y);
			Content.Controls.Add(_auto);
			y = _auto.Bottom + Theme.S(4f);
			_upd = new FCheck("Сообщать о новых версиях на GitHub (только уведомление, ничего не скачивается)") { Checked = true };
			_upd.Location = new Point(M, y);
			Content.Controls.Add(_upd);
			y = _upd.Bottom + Theme.S(8f);
			Finish(y);
		}

		protected override void PaintContent(Graphics g)
		{
			Rectangle br = new Rectangle(M, 0, InnerW, Theme.S(130f));
			Theme.Smooth(g);
			using (System.Drawing.Drawing2D.GraphicsPath p = Theme.Round(br, Theme.S(14f)))
			{
				if (_banner != null)
				{
					Region old = g.Clip;
					g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect);
					float k = Math.Max(br.Width / (float)_banner.Width, br.Height / (float)_banner.Height);
					float iw = _banner.Width * k;
					float ih = _banner.Height * k;
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					// the wallpaper has the WeavOS mark in the middle: shift it out of the banner
					g.DrawImage(_banner, br.X + (br.Width - iw) / 2f, br.Y - ih * 0.62f + br.Height / 2f, iw, ih);
					using (System.Drawing.Drawing2D.LinearGradientBrush veil = new System.Drawing.Drawing2D.LinearGradientBrush(br, Color.FromArgb(150, 12, 13, 16), Color.FromArgb(20, 12, 13, 16), 0f))
					{
						g.FillRectangle(veil, br);
					}
					g.Clip = old;
					old.Dispose();
				}
				using (Pen pen = new Pen(Color.FromArgb(60, Theme.Ice)))
				{
					g.DrawPath(pen, p);
				}
			}
			using (Font big = Theme.F(22f, semi: true, FontStyle.Bold))
			{
				Size ns = Theme.Measure(Brand.Name, big);
				Theme.GradientText(g, Brand.Name, big, new PointF(br.X + Theme.S(26f), br.Y + (br.Height - ns.Height) / 2f - Theme.S(10f)));
				TextRenderer.DrawText(g, L.T("настройка Windows 11") + "  •  v" + Brand.Version + "  •  " + Brand.Developer, Theme.Small, new Point(br.X + Theme.S(28f), br.Y + (br.Height + ns.Height) / 2 - Theme.S(6f)), Theme.Mix(Theme.Sub, Theme.IceHi, 0.3f), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			Label(g, "Язык", _labLang, "можно сменить в «Настройках»");
			Label(g, "Установка", _labInst);
			Label(g, "Запуск", _labMore, (_inst.Selected == 0) ? null : "автозапуск — только для установленной версии");
		}
	}

	// Tips: Boosty and crypto addresses
	internal class SupportDialog : PanelDialog
	{
		private readonly int[] _rowY = new int[3];

		private static readonly string[] Titles = { "Boosty", "TON", "Bitcoin (BTC)" };

		private static readonly string[] Values = { "boosty.to/fel7xxx/donate", Brand.Ton, Brand.Btc };

		private string _copied;

		public SupportDialog()
			: base("Поддержать " + Brand.Name, Brand.Name + " бесплатный и с открытым кодом. Если программа пригодилась, можно оставить чаевые разработчику — это помогает выпускать обновления. Спасибо!", "Закрыть", null, DlgIcon.Info, 640f)
		{
			int y = 0;
			int rh = Theme.S(62f);
			for (int i = 0; i < 3; i++)
			{
				_rowY[i] = y;
				int k = i;
				FButton b = new FButton { Text = (i == 0) ? "Открыть" : "Копировать", Kind = (i == 0) ? BtnKind.Silver : BtnKind.Ghost, Font = Theme.Body };
				b.AutoWidth(Theme.S(30f));
				b.SetBounds(M + InnerW - b.Width - Theme.S(12f), y + (rh - DlgChrome.BtnH) / 2, b.Width, DlgChrome.BtnH);
				b.Click += delegate
				{
					if (k == 0)
					{
						Shell.Open(Brand.Boosty);
						return;
					}
					try
					{
						Clipboard.SetText(Values[k]);
						_copied = Titles[k];
					}
					catch
					{
					}
					Content.Invalidate();
				};
				Content.Controls.Add(b);
				y += rh + Theme.S(10f);
			}
			y += Theme.S(18f);
			Finish(y);
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			int rh = Theme.S(62f);
			for (int i = 0; i < 3; i++)
			{
				Rectangle r = new Rectangle(M, _rowY[i], InnerW, rh);
				using (System.Drawing.Drawing2D.GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(12f)))
				{
					Theme.FillGradient(g, p, r, Theme.Card1, Theme.Card2);
					using (Pen pen = new Pen((i == 0) ? Theme.Mix(Theme.Border, Theme.Ice, 0.45f) : Theme.Border))
					{
						g.DrawPath(pen, p);
					}
				}
				Rectangle ic = new Rectangle(r.X + Theme.S(14f), r.Y + (rh - Theme.S(26f)) / 2, Theme.S(26f), Theme.S(26f));
				if (i == 0)
				{
					Theme.Heart(g, ic, Theme.Ice);
				}
				else
				{
					using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.Card1, Theme.Ice, 0.25f)))
					{
						g.FillEllipse(b, ic);
					}
					TextRenderer.DrawText(g, (i == 1) ? "T" : "₿", Theme.Title, ic, Theme.IceHi, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				}
				int tx = ic.Right + Theme.S(14f);
				int tw = r.Width - (tx - r.X) - Theme.S(150f);
				TextRenderer.DrawText(g, Titles[i], Theme.Title, new Rectangle(tx, r.Y + Theme.S(11f), tw, Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				TextRenderer.DrawText(g, Values[i], Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(34f), tw, Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
			}
			string note = (_copied != null) ? L.T("Адрес {0} скопирован в буфер обмена").Replace("{0}", _copied) : "Проверь адрес после вставки: переводы в криптовалюте нельзя отменить.";
			TextRenderer.DrawText(g, note, Theme.Tiny, new Rectangle(M, _rowY[2] + rh + Theme.S(8f), InnerW, Theme.S(18f)), (_copied != null) ? Theme.Green : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}
	}

	// "Report a problem": diagnostics preview (personal names masked), copy, open a prefilled GitHub issue
	internal class BugReportDialog : PanelDialog
	{
		private readonly TextBox _box;

		private readonly string _diag;

		private bool _copied;

		private readonly int _boxY;

		private readonly int _noteY;

		public BugReportDialog()
			: base("Сообщить об ошибке", "Откроется форма на GitHub (нужен бесплатный аккаунт). Сведения ниже скопируются в буфер — вставь их в поле «Журнал». Имя пользователя и компьютера в них заменены.", "Открыть форму", "Закрыть", DlgIcon.Info, 760f)
		{
			_diag = Diag.Text();
			int y = 0;
			_boxY = y;
			Panel frame = new Panel { BackColor = Color.FromArgb(22, 23, 27) };
			frame.SetBounds(M, y, InnerW, Theme.S(220f));
			_box = new TextBox
			{
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				BorderStyle = BorderStyle.None,
				BackColor = Color.FromArgb(22, 23, 27),
				ForeColor = Theme.Sub,
				Font = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
				Text = _diag.Replace("\r\n", "\n").Replace("\n", "\r\n"),
				WordWrap = false,
				TabStop = false
			};
			_box.HandleCreated += delegate
			{
				if (Win.IsWindows)
				{
					try
					{
						SetWindowTheme(_box.Handle, "DarkMode_Explorer", null);
					}
					catch
					{
					}
				}
			};
			_box.SetBounds(Theme.S(10f), Theme.S(8f), InnerW - Theme.S(20f), Theme.S(204f));
			frame.Controls.Add(_box);
			Content.Controls.Add(frame);
			y = frame.Bottom + Theme.S(12f);
			int bx = M;
			foreach (string[] it in new string[3][]
			{
				new string[2] { "Копировать сведения", "copy" },
				new string[2] { "Предложить идею", "idea" },
				new string[2] { "Обсуждения", "talk" }
			})
			{
				string k = it[1];
				FButton b = new FButton { Text = it[0], Kind = BtnKind.Ghost, Font = Theme.Small };
				b.AutoWidth(Theme.S(24f));
				b.SetBounds(bx, y, b.Width, Theme.S(32f));
				b.Click += delegate
				{
					if (k == "copy")
					{
						Copy();
					}
					else
					{
						Shell.Open((k == "idea") ? Brand.NewIdea : Brand.Discussions);
					}
				};
				Content.Controls.Add(b);
				bx += b.Width + Theme.S(8f);
			}
			y += Theme.S(40f);
			_noteY = y;
			y += Theme.S(20f);
			Finish(y);
		}

		[System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr h, string app, string idList);

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			_box.SelectionStart = 0;
			_box.SelectionLength = 0;
			Yes.Focus();
		}

		private void Copy()
		{
			try
			{
				Clipboard.SetText(_diag);
				_copied = true;
			}
			catch
			{
			}
			Content.Invalidate();
		}

		protected override bool Validate()
		{
			Copy();
			Shell.Open(Brand.NewBug + "&version=" + Uri.EscapeDataString(Brand.Version) + "&windows=" + Uri.EscapeDataString(Diag.WindowsLine()));
			return true;
		}

		protected override void PaintContent(Graphics g)
		{
			TextRenderer.DrawText(g, _copied ? "Сведения скопированы — вставь их в форму на GitHub (Ctrl + V)" : "Пароли и личные файлы в журнал не попадают.", Theme.Tiny, new Rectangle(M, _noteY, InnerW, Theme.S(18f)), _copied ? Theme.Green : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
	}

	// text for bug reports
	internal static class Diag
	{
		public static string WindowsLine()
		{
			string name = "Windows";
			string disp = "";
			string ubr = "";
			try
			{
				using (RegistryKey k = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"))
				{
					if (k != null)
					{
						name = Convert.ToString(k.GetValue("ProductName") ?? "Windows");
						disp = Convert.ToString(k.GetValue("DisplayVersion") ?? "");
						ubr = Convert.ToString(k.GetValue("UBR") ?? "");
					}
				}
			}
			catch
			{
			}
			// ProductName still says "Windows 10" on Windows 11
			if (Win.Build >= 22000)
			{
				name = name.Replace("Windows 10", "Windows 11");
			}
			return (name + " " + disp + " (" + Win.Build + (ubr.Length > 0 ? ("." + ubr) : "") + ")").Replace("  ", " ");
		}

		public static string Mask(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return s;
			}
			string user = Environment.UserName;
			string pc = Environment.MachineName;
			string prof = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(prof) && prof.Length > 3)
			{
				s = Regex.Replace(s, Regex.Escape(prof), "%USERPROFILE%", RegexOptions.IgnoreCase);
			}
			if (!string.IsNullOrEmpty(user) && user.Length > 1)
			{
				s = Regex.Replace(s, "\\b" + Regex.Escape(user) + "\\b", "<user>", RegexOptions.IgnoreCase);
			}
			if (!string.IsNullOrEmpty(pc) && pc.Length > 1)
			{
				s = Regex.Replace(s, "\\b" + Regex.Escape(pc) + "\\b", "<pc>", RegexOptions.IgnoreCase);
			}
			return s;
		}

		public static string Text(int lines = 80)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(Brand.Name + " " + Brand.Version + " (" + L.T(Installer.RunningInstalled ? "установлен" : "без установки") + ")");
			sb.AppendLine(WindowsLine());
			sb.AppendLine("Language: " + L.Lang + " • expert: " + (Prefs.Expert ? "yes" : "no") + " • scale: " + (int)Math.Round(Theme.Scale * 100f) + "%");
			sb.AppendLine("---- tweaker.log (last lines) ----");
			try
			{
				if (File.Exists(Brand.LogPath))
				{
					string[] all = File.ReadAllLines(Brand.LogPath);
					for (int i = Math.Max(0, all.Length - lines); i < all.Length; i++)
					{
						sb.AppendLine(Mask(all[i]));
					}
				}
				else
				{
					sb.AppendLine("(log is empty)");
				}
			}
			catch (Exception ex)
			{
				sb.AppendLine("(log not read: " + ex.Message + ")");
			}
			return sb.ToString();
		}
	}
}
