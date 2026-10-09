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
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WinWeaver
{
	// restart handling: "restart needed" card in the sidebar and on the home banner,
	// restart now / later / cancel, plus a set of restart tools on the maintenance page
	internal partial class MainForm
	{
		private bool _sysReboot;

		private DateTime _rebootAt = DateTime.MinValue;

		private Timer _rbTimer;

		private bool RebootScheduled => _rebootAt > DateTime.Now;

		private void WireReboot()
		{
			_side.RebootClicked += RebootMenu;
			_rbTimer = new Timer { Interval = 15000 };
			_rbTimer.Tick += delegate
			{
				UpdateRebootUi();
			};
			_rbTimer.Start();
		}

		private void ApplyRebootInfo(object r)
		{
			Hashtable h = r as Hashtable;
			bool app = h != null && h["App"] is bool && (bool)h["App"];
			bool sys = h != null && h["Sys"] is bool && (bool)h["Sys"];
			if (h == null && r is bool)
			{
				app = (bool)r;
			}
			NeedReboot = app;
			_sysReboot = sys;
			UpdateRebootUi();
		}

		private void UpdateRebootUi()
		{
			bool need = NeedReboot || _sysReboot;
			if (RebootScheduled)
			{
				int min = Math.Max(1, (int)Math.Ceiling((_rebootAt - DateTime.Now).TotalMinutes));
				_side.SetReboot("Перезагрузка в " + _rebootAt.ToString("HH:mm"), "через " + min + " мин • нажми, чтобы изменить", scheduled: true);
			}
			else if (need)
			{
				_side.SetReboot("Нужна перезагрузка", NeedReboot ? "чтобы применить изменения" : "обновление Windows ждёт", scheduled: false);
			}
			else
			{
				_side.SetReboot(null, null, scheduled: false);
			}
			if (_hero != null && _hero.NeedReboot != (need && !RebootScheduled))
			{
				_hero.NeedReboot = need && !RebootScheduled;
				_hero.Invalidate();
			}
		}

		private static void RunWait(string file, string args)
		{
			try
			{
				using (Process p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true }))
				{
					p?.WaitForExit(5000);
				}
			}
			catch
			{
			}
		}

		// screenshot helpers
		internal void DemoMenu()
		{
			RebootMenu(PointToScreen(new Point(Theme.S(40f), Theme.S(300f))));
		}

		internal void DemoQueue(bool expand = true)
		{
			_status.SetBackground("Расшифровка BitLocker — диск C:", "расшифровано 72%  •  осталось ≈ 6 мин", 0.72f);
			_host.TryBegin(title: "Defender — обновление баз");
			_host.TryBegin(quick: true, title: "Быстрые подсказки при наведении: ВКЛ");
			_host.TryBegin(quick: true, title: "Отключить рекламу в Проводнике: ВКЛ");
			_host.TryBegin(title: "Очистка DNS — Очистить");
			if (expand)
			{
				_status.DemoExpand();
			}
		}

		private void RebootMenu(Point at)
		{
			DarkMenu m = new DarkMenu();
			m.Header("Перезагрузка", RebootScheduled ? ("Запланирована на " + _rebootAt.ToString("HH:mm")) : (NeedReboot ? "Чтобы применить изменения" : "Выбери, когда перезагрузить"));
			m.Item("Перезагрузить сейчас", "\ue777", "Через 5 секунд после подтверждения", delegate
			{
				RebootNow();
			});
			if (RebootScheduled)
			{
				m.Item("Отменить перезагрузку", "\ue711", "Компьютер не перезагрузится", delegate
				{
					RunWait("shutdown.exe", "/a");
					_rebootAt = DateTime.MinValue;
					_status.Set("ok", "Запланированная перезагрузка отменена");
					UpdateRebootUi();
				});
			}
			else
			{
				m.Item("Через 10 минут", "\ue823", "Успеешь сохранить работу, можно отменить", delegate
				{
					ScheduleReboot(10);
				});
				m.Item("Через час", "\ue823", "Можно отменить в любой момент", delegate
				{
					ScheduleReboot(60);
				});
			}
			m.Sep();
			m.Item("Только перезапустить Проводник", "\ue8b7", "Без перезагрузки — применит изменения оформления", delegate
			{
				RestartExplorer();
			});
			m.Show(at);
		}

		private void RebootNow()
		{
			if (WDialog.Ask(this, "Перезагрузить компьютер?", "Сохрани открытые документы — программы закроются. Перезагрузка начнётся через 5 секунд." + (NeedReboot ? "\n\nПосле неё вступят в силу все изменения, которые этого ждут." : ""), "Перезагрузить", "Отмена", DlgIcon.Warn))
			{
				if (RebootScheduled)
				{
					RunWait("shutdown.exe", "/a");
				}
				Shell.Open("shutdown.exe", "/r /t 5");
			}
		}

		// after an auto-tune level: restart now, in 10 minutes or later (the sidebar card keeps reminding)
		private void OfferRebootAfterLevel(string level)
		{
			if (!NeedReboot || RebootScheduled)
			{
				return;
			}
			string body = "Политики, службы и удалённые приложения окончательно применятся только после перезагрузки. Сохрани открытые документы — программы закроются.\n\nЕсли сейчас неудобно, напоминание останется в меню слева.";
			int r = WDialog.Choose(this, "Перезагрузить, чтобы завершить «" + level + "»?", body, "Перезагрузить сейчас", "Через 10 минут", "Позже", DlgIcon.Warn);
			if (r == 0)
			{
				Shell.Open("shutdown.exe", "/r /t 5");
			}
			else if (r == 1)
			{
				ScheduleReboot(10);
			}
		}

		private void ScheduleReboot(int minutes)
		{
			if (RebootScheduled)
			{
				RunWait("shutdown.exe", "/a");
			}
			RunWait("shutdown.exe", "/r /t " + minutes * 60 + " /c \"" + Brand.Name + ": перезагрузка, чтобы применить изменения\"");
			_rebootAt = DateTime.Now.AddMinutes(minutes);
			_status.Set("ok", "Компьютер перезагрузится в " + _rebootAt.ToString("HH:mm") + " — отменить можно в меню слева");
			UpdateRebootUi();
		}

		private void RestartExplorer()
		{
			if (!_host.TryBegin(title: "Перезапуск Проводника"))
			{
				return;
			}
			_status.Set("info", "Перезапускаю Проводник…");
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("$global:FDeferExplorer = $false; Restart-FExplorer");
				return (object)null;
			}, delegate
			{
				_host.End();
				_status.Set("ok", "Проводник перезапущен");
			});
		}

		private void BuildRestartTools(Page page)
		{
			page.Add(new SectionLabel("Перезагрузка и перезапуск"));
			TilesView t = page.Add(new TilesView(_host));
			t.AddUi(Glyph.Refresh, "Перезагрузить компьютер", "Сейчас, через 10 минут или через час — по выбору", "Можно отменить, пока не началась", delegate
			{
				RebootMenu(Cursor.Position);
			}, "Выбрать…");
			t.AddUi(Glyph.Folder, "Перезапустить Проводник", "Обновляет панель задач, Пуск и рабочий стол без перезагрузки", "Окна Проводника закроются", delegate
			{
				RestartExplorer();
			}, "Перезапустить");
			t.AddUi(Glyph.Close, "Выйти из системы", "Применяет настройки, которым хватает нового входа", "Программы закроются", delegate
			{
				if (WDialog.Ask(this, "Выйти из системы?", "Все программы будут закрыты — сохрани открытые документы.", "Выйти", "Отмена", DlgIcon.Warn))
				{
					Shell.Open("shutdown.exe", "/l");
				}
			}, "Выйти");
			t.AddUi(Glyph.Settings, "Загрузиться в BIOS / UEFI", "Перезагрузка сразу в настройки прошивки материнской платы", "Только для UEFI", delegate
			{
				if (WDialog.Ask(this, "Перезагрузиться в BIOS / UEFI?", "Компьютер перезагрузится и откроет настройки прошивки. Сохрани открытые документы.", "Перезагрузить", "Отмена", DlgIcon.Warn))
				{
					Shell.Open("shutdown.exe", "/r /fw /t 5");
				}
			}, "Перезагрузить");
			t.AddUi(Glyph.Repair, "Особые варианты загрузки", "Безопасный режим, восстановление, командная строка", "Меню восстановления Windows", delegate
			{
				if (WDialog.Ask(this, "Открыть особые варианты загрузки?", "Компьютер перезагрузится в меню восстановления Windows: оттуда можно включить безопасный режим или вернуть систему.", "Перезагрузить", "Отмена", DlgIcon.Warn))
				{
					Shell.Open("shutdown.exe", "/r /o /t 5");
				}
			}, "Перезагрузить");
		}
	}
}
