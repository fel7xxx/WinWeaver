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
using System.Windows.Forms;

namespace WinWeaver
{
	// Second step of the auto-tune: the WeavOS look (optional, on by default), computer name, sign-in password
	// and the list of steps that will run (each one can be unticked).
	internal class PersonalizeDialog : PanelDialog
	{
		private readonly TileChoice _walls;

		private readonly FCheck _wallOn;

		private readonly FCheck _nameOn;

		private readonly FField _dev;

		private readonly FField _pw1;

		private readonly FField _pw2;

		private readonly string _baseOpts;

		private readonly Action<string, Action<string[]>> _fetchPlan;

		private int _labWall;

		private int _labPc;

		private int _labPw;

		private int _noteY;

		private string _error;

		private List<int> _skip = new List<int>();

		private int _planCount;

		public string Options
		{
			get
			{
				string[] parts = _baseOpts.Split(new char[1] { ';' }, 2);
				List<string> op = new List<string>();
				foreach (string s in parts[0].Split(','))
				{
					if (s.Trim().Length > 0)
					{
						op.Add(s.Trim());
					}
				}
				if (!_wallOn.Checked)
				{
					op.Add("nowall");
				}
				if (!_nameOn.Checked)
				{
					op.Add("noname");
				}
				string r = string.Join(",", op.ToArray()) + ";" + ((parts.Length > 1) ? parts[1] : "def=0") + ";wp=" + _walls.Selected;
				if (_skip.Count > 0)
				{
					r += ";skip=" + string.Join(".", _skip.ConvertAll((int i) => i.ToString()).ToArray());
				}
				return r;
			}
		}

		public string DeviceName
		{
			get
			{
				string d = _dev.Value.Trim();
				return string.Equals(d, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ? "" : d;
			}
		}

		public string Password => _pw1.Value;

		private PersonalizeDialog(string levelTitle, string baseOpts, Action<string, Action<string[]>> fetchPlan)
			: base("Стиль " + Brand.OsName + " и компьютер", "Шаг 2 из 2 • «" + levelTitle + "». Всё на этой странице — по желанию: сними галочки, если хочешь оставить как есть.", "Начать настройку", "Отмена", DlgIcon.Question, 760f)
		{
			_baseOpts = baseOpts ?? "";
			_fetchPlan = fetchPlan;
			int y = 0;
			int w = InnerW;
			_labWall = y;
			_wallOn = new FCheck("Поставить обои " + Brand.OsName + " на рабочий стол и экран блокировки");
			_wallOn.Location = new Point(M, y + Theme.S(20f));
			_wallOn.Click += delegate { _walls.Enabled = _wallOn.Checked; _walls.Invalidate(); ResetSkip(); };
			Content.Controls.Add(_wallOn);
			y = _wallOn.Bottom + Theme.S(8f);
			_walls = new TileChoice { PictureH = Theme.S(84f) };
			for (int i = 0; i < Walls.Count; i++)
			{
				_walls.Add(Walls.Names[i], "4K • 3840 × 2160", Walls.Load(i, 480));
			}
			_walls.Selected = Walls.Clamp(Prefs.LastWallpaper);
			_walls.SetBounds(M, y, w, _walls.Measure(w));
			Content.Controls.Add(_walls);
			y = _walls.Bottom + Theme.S(10f);
			_nameOn = new FCheck("Показывать «" + Brand.OsName + "» как название системы в «О компьютере»");
			_nameOn.Location = new Point(M, y);
			_nameOn.Click += delegate { ResetSkip(); };
			Content.Controls.Add(_nameOn);
			y = _nameOn.Bottom + Theme.S(18f);
			_labPc = y;
			y += Theme.S(22f);
			_dev = new FField("Имя компьютера", password: false, 15) { Value = (Environment.GetEnvironmentVariable("WW_SHOT") != null) ? "WEAVOS-PC" : Environment.MachineName };
			_dev.SetBounds(M, y, w, Theme.S(40f));
			Content.Controls.Add(_dev);
			y = _dev.Bottom + Theme.S(18f);
			_labPw = y;
			y += Theme.S(22f);
			int half = (w - Theme.S(10f)) / 2;
			_pw1 = new FField("Новый пароль", password: true, 127);
			_pw1.SetBounds(M, y, half, Theme.S(40f));
			_pw2 = new FField("Повтори пароль", password: true, 127);
			_pw2.SetBounds(M + half + Theme.S(10f), y, w - half - Theme.S(10f), Theme.S(40f));
			Content.Controls.Add(_pw1);
			Content.Controls.Add(_pw2);
			y = _pw1.Bottom + Theme.S(10f);
			_noteY = y;
			y += Theme.S(36f);
			if (_fetchPlan != null)
			{
				AddExtra("Выбрать шаги…", delegate { PickSteps(); });
			}
			Finish(y);
		}

		private void ResetSkip()
		{
			if (_skip.Count > 0)
			{
				_skip.Clear();
				if (Extra != null)
				{
					Extra.Text = "Выбрать шаги…";
				}
			}
		}

		private void PickSteps()
		{
			if (Extra != null)
			{
				Extra.Enabled = false;
			}
			string opts = Options;
			int at = opts.IndexOf(";skip=", StringComparison.Ordinal);
			if (at >= 0)
			{
				opts = opts.Substring(0, at);
			}
			_fetchPlan(opts, delegate (string[] names)
			{
				if (Extra != null)
				{
					Extra.Enabled = true;
				}
				if (names == null || names.Length == 0 || IsDisposed)
				{
					return;
				}
				CheckDialog.Row[] rows = new CheckDialog.Row[names.Length];
				for (int i = 0; i < names.Length; i++)
				{
					rows[i] = new CheckDialog.Row { Text = names[i], Checked = !_skip.Contains(i) };
				}
				bool[] r = CheckDialog.Ask(this, "Шаги автонастройки", "Выполнятся по порядку сверху вниз. Сними галочку с того, что делать не нужно — остальное пройдёт как обычно.", rows, "Готово");
				if (r == null)
				{
					return;
				}
				_skip = new List<int>();
				for (int j = 0; j < r.Length; j++)
				{
					if (!r[j])
					{
						_skip.Add(j);
					}
				}
				_planCount = names.Length;
				if (Extra != null)
				{
					Extra.Text = (_skip.Count == 0) ? "Выбрать шаги…" : L.T("Шаги: {0} из {1}").Replace("{0}", (names.Length - _skip.Count).ToString()).Replace("{1}", names.Length.ToString());
					Extra.AutoWidth(Theme.S(30f));
					Extra.Width = Math.Max(Theme.S(112f), Extra.Width);
				}
			});
		}

		protected override bool Validate()
		{
			_error = null;
			string d = _dev.Value.Trim();
			if (d.Length > 0 && !string.Equals(d, Environment.MachineName, StringComparison.OrdinalIgnoreCase) && !ValidName(d))
			{
				_error = "Имя компьютера: до 15 символов — латинские буквы, цифры и дефис, не только цифры";
			}
			else if (_pw1.Value != _pw2.Value)
			{
				_error = "Пароли не совпадают";
			}
			if (_error != null)
			{
				Content.Invalidate();
				return false;
			}
			Prefs.LastWallpaper = _walls.Selected;
			return true;
		}

		// same rules as Test-FDeviceName in engine.ps1 (NetBIOS name)
		public static bool ValidName(string n)
		{
			if (n.Length < 1 || n.Length > 15 || n.StartsWith("-") || n.EndsWith("-"))
			{
				return false;
			}
			bool letter = false;
			foreach (char c in n)
			{
				bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-';
				if (!ok)
				{
					return false;
				}
				if (!(c >= '0' && c <= '9'))
				{
					letter = true;
				}
			}
			return letter;
		}

		protected override void PaintContent(Graphics g)
		{
			Label(g, "Обои", _labWall);
			Label(g, "Имя компьютера", _labPc, "латиница, цифры и дефис, до 15 символов • вступит в силу после перезагрузки");
			Label(g, "Пароль для входа в Windows", _labPw, "необязательно • оставь пустым, чтобы не менять");
			string note = _error ?? "Пароль меняется только у локальной учётной записи (у учётной записи Microsoft — на сайте Microsoft). Программа его не сохраняет и не пишет в журнал.";
			TextRenderer.DrawText(g, note, Theme.Tiny, new Rectangle(M, _noteY, InnerW, Theme.S(34f)), (_error != null) ? Theme.Red : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		}

		// returns false when cancelled
		public static bool Ask(IWin32Window owner, string levelTitle, ref string opts, out string dev, out string pw, Action<string, Action<string[]>> fetchPlan)
		{
			dev = "";
			pw = "";
			using (PersonalizeDialog d = new PersonalizeDialog(levelTitle, opts, fetchPlan))
			{
				if (d.ShowFor(owner) != DialogResult.Yes)
				{
					return false;
				}
				opts = d.Options;
				dev = d.DeviceName;
				pw = d.Password;
				return true;
			}
		}
	}
}
