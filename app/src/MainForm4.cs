// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections;
using System.Collections.Generic;

namespace WinWeaver
{
	// FPS score on the home banner: 100 = every Windows-side setting is at its max-FPS value.
	// Weights: how much a setting usually matters for frame rate / frame pacing.
	internal partial class MainForm
	{
		private static readonly object[][] FpsMap = new object[][]
		{
			// title, weight, best value(s)
			new object[] { "Схема питания", 10, 0, 1 },
			new object[] { "Целостность памяти (HVCI)", 9, false },
			new object[] { "Отключить виртуализацию (VBS)", 8, true },
			new object[] { "Фоновая запись игр (Game DVR)", 8, false },
			new object[] { "Запретить запись игр политикой", 3, true },
			new object[] { "Аппаратное планирование GPU (HAGS)", 6, true },
			new object[] { "Игровой режим", 6, true },
			new object[] { "Оптимизация оконных игр", 5, true },
			new object[] { "Высокий приоритет игр (MMCSS)", 5, true },
			new object[] { "Распределение процессорного времени", 5, 2 },
			new object[] { "Отключить Power Throttling", 4, true },
			new object[] { "Приоритет активным программам (MMCSS)", 4, true },
			new object[] { "Отключить оптимизацию во весь экран", 3, true },
			new object[] { "Точный системный таймер", 3, true },
			new object[] { "Отключить MPO", 3, true },
			new object[] { "Запретить фоновые приложения", 3, true },
			new object[] { "Отключить динамический тик", 2, true },
			new object[] { "Ускорение мыши", 2, false },
			new object[] { "Отключить SysMain (Superfetch)", 2, true },
			new object[] { "Отключить лишние службы", 2, true },
			new object[] { "Отключить сжатие памяти", 1, true },
			new object[] { "Отключить индексирование поиска", 1, true },
			new object[] { "Не выгружать ядро из памяти", 1, true },
			new object[] { "Не обновлять метку доступа к файлам", 1, true }
		};

		// switching these off adds FPS but weakens protection against malware
		internal static bool IsFpsRisky(SettingCard c)
		{
			return c.Title != null && (c.Title.Contains("HVCI") || c.Title.Contains("VBS"));
		}

		internal void DemoFps()
		{
			FixFps();
		}

		private void ApplyFpsMap()
		{
			foreach (SettingCard c in _tracked)
			{
				foreach (object[] m in FpsMap)
				{
					if ((string)m[0] == c.Title)
					{
						object[] best = new object[m.Length - 2];
						Array.Copy(m, 2, best, 0, best.Length);
						c.Fps((int)m[1], best);
					}
				}
			}
		}

		private List<SettingCard> FpsCards()
		{
			return _tracked.FindAll((SettingCard c) => c.FpsW > 0 && !c.Unsupported);
		}

		// value to set for the max-FPS state (first allowed value)
		private static object FpsTarget(SettingCard c)
		{
			return c.FpsBest[c.FpsBest.Length > 1 && c.Seg != null ? 1 : 0];
		}

		private void FixFps()
		{
			List<SettingCard> bad = FpsCards().FindAll((SettingCard c) => c.FpsOk == false);
			if (bad.Count == 0)
			{
				return;
			}
			bad.Sort((SettingCard a, SettingCard b) => b.FpsW.CompareTo(a.FpsW));
			List<CheckDialog.Row> rows = new List<CheckDialog.Row>();
			foreach (SettingCard c in bad)
			{
				object t = FpsTarget(c);
				string tag = (c.Seg != null) ? ("→ " + c.Seg.Items[(int)t]) : (((bool)t) ? null : "выключить");
				string w = (c.FpsW >= 7) ? "сильно влияет на FPS" : ((c.FpsW >= 4) ? "заметно" : "немного");
				bool risky = IsFpsRisky(c);
				rows.Add(new CheckDialog.Row { Text = c.Title, Hint = "\n" + w + "  •  " + (string.IsNullOrEmpty(c.Desc) ? c.What : c.Desc), Tag = tag, Checked = !risky });
			}
			bool[] picked = CheckDialog.Ask(this, "Выжать максимум FPS", "Эти настройки Windows мешают играм. Снизу — то, что влияет меньше. Защита ядра (HVCI, VBS) по умолчанию не отмечена: её выключение даёт прирост, но ослабляет защиту от вирусов. Всё попадёт в «Историю» одной группой.", rows.ToArray(), "Применить", "Отмена", DlgIcon.Info);
			if (picked == null)
			{
				return;
			}
			List<SettingCard> sel = new List<SettingCard>();
			for (int i = 0; i < picked.Length && i < bad.Count; i++)
			{
				if (picked[i])
				{
					sel.Add(bad[i]);
				}
			}
			if (sel.Count == 0 || !_host.TryBegin(title: "Исправляю проблемы (" + sel.Count + ")"))
			{
				return;
			}
			_hero.Fixing = true;
			_hero.Invalidate();
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("Start-FGroup 'Максимум FPS'");
				try
				{
					foreach (SettingCard c in sel)
					{
						object t = FpsTarget(c);
						e.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
						{
							{ "fa1", c.HistTitle(t) },
							{ "fa2", c.GetScript },
							{ "fa3", c.SetScript },
							{ "fa4", t },
							{ "fa5", c.Cat }
						});
					}
				}
				finally
				{
					e.Eval("Stop-FGroup; Show-Ok ('Применены игровые настройки: ' + $fa1)", new Hashtable { { "fa1", sel.Count } });
				}
				return (object)null;
			}, delegate
			{
				_hero.Fixing = false;
				_host.End();
				CheckReboot();
				_scoreDirty = true;
				UpdateScore();
				_cur?.Refresh(_host.Worker);
			});
		}
	}
}
