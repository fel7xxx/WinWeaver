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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace WinWeaver
{
	// pages added in 1.1: privacy overview, protection & updates heroes, programs, fixes, maintenance, settings
	internal partial class MainForm
	{
		private static string Q(string s)
		{
			return "'" + (s ?? "").Replace("'", "''") + "'";
		}

		private static string[] Days = new string[4] { "Нет", "7 дней", "14 дней", "30 дней" };

		// ---------- privacy overview (above the tabs) ----------
		private void BuildPrivacyHero(Page page)
		{
			StatusHero hero = page.Add(new StatusHero(_host, Glyph.Lock, "Get-FPrivInfo", "Проверяю приватность…", new string[4] { "СЛЕЖКА", "РЕКЛАМА", "ЗАЩИТА", "ОБНОВЛЕНИЯ" }, true));
			hero.TileClicked += delegate(int i)
			{
				page.SelectTab(i);
			};
		}

		// ---------- "Защита" tab ----------
		private void BuildProtect(Page page)
		{
			page.Add(new StatusHero(_host, Glyph.Shield, "Get-FSecInfo", "Проверяю защиту…", new string[6] { "АНТИВИРУС", "БРАНДМАУЭР", "SMARTSCREEN", "UAC", "ЗАГРУЗКА", "BITLOCKER" }, false, U("Безопасность Windows", delegate
			{
				Shell.Open("windowsdefender:");
			}, BtnKind.Silver, Glyph.Shield), U("Брандмауэр", delegate
			{
				Shell.Open("firewall.cpl");
			})));
		}

		private void BuildDefender(Page page)
		{
			DefenderView dv = page.Add(new DefenderView());
			Action<string, string, string, string> runT = null;
			Action<string, string, string> run = delegate(string title, string ps, string undo)
			{
				runT(title, ps, undo, title);
			};
			runT = delegate(string title, string ps, string undo, string job)
			{
				if (!_host.TryBegin(title: job))
				{
					return;
				}
				_host.Worker.Run(delegate(IEngine e)
				{
					if (title == null)
					{
						e.Eval(ps);
					}
					else
					{
						e.Eval("Invoke-FTracked $fa1 $fa2 -Undo $fa3 -Cat 'protect'", new Hashtable { { "fa1", title }, { "fa2", ps }, { "fa3", undo ?? "" } });
					}
					return (object)null;
				}, delegate
				{
					_host.End();
					page.Refresh(_host.Worker);
					UpdateBadge();
				});
			};
			dv.ModeClicked += delegate(int m)
			{
				if (m == 0)
				{
					run("Microsoft Defender включён", "Set-DefenderOn", "Set-DefenderOff $false");
				}
				else if (m == 1)
				{
					if (WDialog.Ask(this, "Выключить защиту в реальном времени?", "Defender перестанет проверять файлы при открытии и запуске — меньше нагрузки в играх. Проверять вручную можно по-прежнему.\n\nЕсли включена «Защита от подделки», сначала откроются настройки, чтобы её выключить.", "Выключить", "Отмена", DlgIcon.Warn))
					{
						run("Защита в реальном времени Defender выключена", "Set-DefenderOff $false", "Set-DefenderOn");
					}
				}
				else if (WDialog.Ask(this, "Полностью выключить Defender?", "Защита в реальном времени выключится сразу, а службы Defender — через перезагрузку в безопасный режим (WinWeaver сделает всё сам и вернёт обычный режим).\n\nПеред этим выключи «Защиту от подделки» в настройках Defender.", "Выключить", "Отмена", DlgIcon.Warn))
				{
					run("Microsoft Defender выключен полностью", "Set-DefenderOff $true", "Set-DefenderOn");
				}
			};
			dv.ToolClicked += delegate(string k)
			{
				if (k == "settings")
				{
					Shell.Open("windowsdefender://threatsettings");
				}
				else if (k == "sig")
				{
					runT(null, "Update-FDefSig", null, "Defender — обновление баз");
				}
				else if (k == "scan")
				{
					runT(null, "Start-FDefScan", null, "Defender — быстрая проверка");
				}
			};
		}

		private Action _blKick;

		private void BuildBitLocker(Page page)
		{
			BitLockerView bl = page.Add(new BitLockerView(_host));
			// decryption runs for a long time in Windows itself: show it on the task toast as background work
			bl.Progress += delegate(string title, string sub, float p, bool active)
			{
				if (active)
				{
					_status.SetBackground(title, sub, p);
				}
				else
				{
					_status.ClearBackground("ok", "BitLocker: расшифровка завершена");
				}
			};
			_status.BgClicked += delegate
			{
				if (_status.BgTitle == WarmTitle)
				{
					return;
				}
				Show("privacy");
				page.SelectTab(2, animate: false);
			};
			_blKick = delegate
			{
				_host.Worker.Run((IEngine e) => bl.Fetch(e), bl.Apply);
			};
			bl.DecryptClicked += delegate
			{
				if (!Prefs.Expert)
				{
					WDialog.Info(this, "Нужен экспертный режим", "Расшифровка снимает защиту данных: если ноутбук украдут, файлы можно будет прочитать.\n\nЕсли ты понимаешь, что делаешь, включи «Экспертный режим» в «Настройках».", DlgIcon.Warn);
					return;
				}
				if (!WDialog.Ask(this, "Расшифровать диски?", "Расшифровка идёт в фоне и может занять от нескольких минут до часа. Компьютером можно пользоваться — прогресс будет виден здесь же.", "Расшифровать", "Отмена", DlgIcon.Warn) || !_host.TryBegin(title: "Запуск расшифровки BitLocker"))
				{
					return;
				}
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked 'Запущена расшифровка дисков BitLocker' 'Set-BLDecrypt' -Cat 'protect' -NoUndo -Note 'Зашифровать снова можно в «Параметры → Конфиденциальность → Шифрование»'");
					return (object)null;
				}, delegate
				{
					_host.End();
					page.Refresh(_host.Worker);
					_blKick?.Invoke();
				});
			};
			bl.BlockClicked += delegate
			{
				if (!_host.TryBegin(title: "Запрет автошифрования BitLocker"))
				{
					return;
				}
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked 'Запрещено автошифрование BitLocker' \"Set-FReg 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\BitLocker' 'PreventDeviceEncryption' 1; Show-Ok 'Автошифрование запрещено'\" -Cat 'protect'");
					return (object)null;
				}, delegate
				{
					_host.End();
					page.Refresh(_host.Worker);
				});
			};
		}

		private void BuildProtectHardening(Page page)
		{
			page.Add(new SectionLabel("Усиление защиты"));
			Tw(page, "SmartScreen для приложений и файлов", "Предупреждение перед запуском незнакомых скачанных программ", "smartscreen", "protect").Info("Включает проверку SmartScreen: перед запуском редкой или неподписанной программы из интернета Windows покажет предупреждение.", "Защищает от поддельных установщиков и вирусов, которые маскируются под нужные программы.", "Иногда предупреждает о безобидных малоизвестных утилитах — их можно запустить через «Подробнее → Выполнить в любом случае».", true, "Оставь включённым — это один из самых полезных барьеров.");
			Tw(page, "Блокировать нежелательные программы (PUA)", "Defender не даст поставить тулбары, майнеры и «оптимизаторы»", "pua", "protect").Info("Включает в Microsoft Defender блокировку потенциально нежелательных приложений: рекламных установщиков, майнеров, «ускорителей ПК» с подвохом.", "Меньше мусора, который тихо ставится вместе с бесплатными программами.", "Изредка блокирует кряки и сомнительные утилиты, которые ты ставишь осознанно.", true, "Включить. Работает, только если Defender — основной антивирус.");
			Tw(page, "Отключить автозапуск со съёмных дисков", "Флешки и диски не запускают программы сами", "autorun", "protect").Info("Запрещает автозапуск и автовоспроизведение для всех типов дисков.", "Защищает от вирусов, которые распространяются через флешки.", "При подключении флешки не будет всплывать окно выбора действия — открой её в Проводнике.", true, "Включить.");
			Tw(page, "Отключить удалённого помощника", "Никто не сможет подключиться к ПК через «Удалённый помощник»", "remoteassist", "protect").Info("Выключает приглашения «Удалённого помощника» Windows.", "Закрывает способ, которым пользуются телефонные мошенники «из службы поддержки».", "Если тебе помогает близкий через «Удалённого помощника» — включи обратно. AnyDesk и TeamViewer работают независимо.", true, "Выключить.");
			Tw(page, "Отключить устаревший протокол SMBv1", "Старый протокол общих папок, через который распространялся WannaCry", "smb1", "protect").Info("Выключает сервер SMB версии 1 — протокол общих папок из 1990-х.", "Закрывает уязвимости, через которые распространялись WannaCry и NotPetya.", "Не откроются общие папки на очень старых NAS и роутерах (до ~2012 года).", true, "Выключить.");
			Tw(page, "Не отправлять образцы файлов в Microsoft", "Defender спросит, прежде чем отправить подозрительный файл", "samples", "protect").Info("Defender перестаёт автоматически отправлять подозрительные файлы в облако Microsoft для анализа.", "Личные документы не уйдут в облако, даже если покажутся Defender подозрительными.", "Новые угрозы распознаются чуть медленнее.", null, "По желанию: для приватности — включить, для максимальной защиты — оставить как есть.");
			Ch(page, "Контроль учётных записей (UAC)", "Как спрашивать разрешение на изменения в системе", new string[4] { "Всегда", "Как обычно", "Без затемнения", "Не спрашивать" }, "Get-FTweakIdx 'uac'", "Set-FTweakIdx 'uac' $v", "protect").Expert().Info("Настраивает окно «Разрешить этому приложению вносить изменения?». «Не спрашивать» — программы получают права администратора молча.", "Затемнение экрана не даёт вирусу нажать «Да» за тебя.", "«Не спрашивать» и «Без затемнения» заметно снижают защиту.", 1, "«Как обычно» — стандарт Windows и лучший баланс.").Opts("Спрашивает даже при изменении настроек Windows самим пользователем. Максимальная защита, но окно появляется часто.", "Стандарт Windows: спрашивает, когда программа хочет права администратора, экран затемняется.", "Спрашивает так же, но без затемнения — вредоносная программа теоретически может нажать «Да» за тебя.", "Программы получают права администратора молча. Защита от тихих изменений системы выключена.");
		}

		// ---------- "Обновления" tab ----------
		private void BuildUpdates(Page page)
		{
			StatusHero hero = page.Add(new StatusHero(_host, Glyph.Sync, "Get-FUpdInfo", "Проверяю Центр обновления…", new string[4] { "ВЕРСИЯ", "УСТАНОВКА", "ПАУЗА", "ПОСЛЕДНЕЕ" }, false, U("Проверить обновления", delegate
			{
				Shell.Open("ms-settings:windowsupdate-action");
			}, BtnKind.Silver, Glyph.Sync), U("Журнал обновлений", delegate
			{
				Shell.Open("ms-settings:windowsupdate-history");
			})));
			UpdPauseView pv = page.Add(new UpdPauseView());
			pv.CustomClicked += delegate
			{
				int d = PauseDialog.Ask(this);
				if (d > 0)
				{
					pv.Choose(d);
				}
			};
			pv.PauseChosen += delegate(int days)
			{
				if (!_host.TryBegin(title: "Пауза обновлений"))
				{
					return;
				}
				pv.Busy = true;
				pv.SetPending(days);
				DateTime ud = DateTime.Now.AddDays(days);
				string until = (ud.Year >= 2077) ? "2077 года («навсегда»)" : ud.ToString("dd.MM.yyyy");
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked $fa1 $fa2 -Undo 'Set-FUpdPause 0' -Cat 'update' -KeepReg", new Hashtable
					{
						{ "fa1", "Обновления приостановлены до " + until },
						{ "fa2", "Set-FUpdPause " + days }
					});
					return (object)null;
				}, delegate
				{
					pv.Busy = false;
					_host.End();
					page.Refresh(_host.Worker);
				});
			};
			pv.ResumeClicked += delegate
			{
				if (!_host.TryBegin(title: "Возобновление обновлений"))
				{
					return;
				}
				pv.Busy = true;
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked 'Обновления возобновлены' 'Set-FUpdPause 0' -Cat 'update' -NoUndo");
					return (object)null;
				}, delegate
				{
					pv.Busy = false;
					_host.End();
					page.Refresh(_host.Worker);
				});
			};
			page.Add(new SectionLabel("Как приходят обновления"));
		}

		private void BuildUpdatesExtra(Page page)
		{
			Ch(page, "Как ставить обновления", "Кто решает, когда скачивать и устанавливать", new string[3] { "Автоматически", "Скачивать, ставить по кнопке", "Только уведомлять" }, "Get-FTweakIdx 'wumode'", "Set-FTweakIdx 'wumode' $v", "update").Info("Меняет политику автообновления. «Скачивать, ставить по кнопке» — Windows загрузит обновления в фоне, а установит, когда ты нажмёшь «Установить». «Только уведомлять» — даже скачивание по твоей команде.", "Обновление не начнёт устанавливаться перед важной игрой или созвоном.", "Легко забыть про обновления безопасности — заглядывай в Центр обновления раз в пару недель.", null, "«Автоматически» подходит большинству. «Скачивать, ставить по кнопке» — если не любишь сюрпризы.").Opts("Windows сама скачивает и ставит обновления, перезагрузку планирует вне активных часов.", "Обновления скачиваются в фоне, а устанавливаются, только когда ты нажмёшь «Установить».", "Windows только сообщает о новых обновлениях — и скачивание, и установка по твоей команде.");
			Ch(page, "Отложить ежемесячные обновления", "Обновления качества приходят позже, когда их уже проверили другие", Days, "Get-FTweakIdx 'wudefer'", "Set-FTweakIdx 'wudefer' $v", "update").Info("Откладывает ежемесячные накопительные обновления на выбранный срок.", "Если Microsoft выпустит кривое обновление, его успеют отозвать до тебя.", "Исправления безопасности тоже придут позже.", null, "7 дней — разумный компромисс.").Opts("Накопительные обновления приходят в день выхода — исправления безопасности сразу.", "Неделя задержки: если обновление окажется кривым, его успеют отозвать.", "Две недели — ещё надёжнее, но и исправления безопасности приходят позже.", "Максимальная задержка. Подходит рабочим машинам, где стабильность важнее новизны.");
			Tw(page, "Не ставить драйверы через Центр обновления", "Windows не будет заменять драйверы видеокарты и устройств", "wudrivers", "update").Info("Исключает драйверы из обновлений Windows.", "Свежий драйвер NVIDIA или AMD не заменится старой версией от Microsoft.", "Драйверы новых устройств придётся ставить самому с сайта производителя.", null, "Включай, если сам следишь за драйверами видеокарты.");
			Tw(page, "Не получать новые функции первым", "Выключает «Получать последние обновления, как только они будут доступны»", "wuearly", "update").Info("Выключает ранний доступ к новым функциям Windows, которые раскатываются постепенно.", "Меньше сырых нововведений и связанных с ними багов.", "Новые функции появятся на несколько недель позже.", null, "Выключить, если важна стабильность.").Builds(22621);
			Tw(page, "Предупреждать о перезагрузке", "Уведомление, когда для обновления нужно перезагрузить ПК", "wunotify", "update").Info("Показывает уведомление, когда Windows готова перезагрузиться для установки обновлений.", "Перезагрузка не станет неожиданностью.", "Минусов почти нет (лишнее уведомление может отвлечь в игре или на презентации).", null, "Включить.");
			Tw(page, "Обновлять другие продукты Microsoft", "Office, .NET и драйверы Microsoft через Центр обновления", "wumsupd", "update").Info("Разрешает Центру обновления ставить обновления Office, .NET, Visual C++ и других продуктов Microsoft.", "Всё обновляется в одном месте.", "Чуть больше загрузок.", null, "Включи, если у тебя установлен Microsoft Office.");
		}

		// ---------- performance extras ----------
		private void PerfResp(Page page)
		{
			page.Add(new SectionLabel("Тонкая настройка отклика"));
			Ch(page, "Распределение процессорного времени", "Сколько времени процессора получает активное окно", new string[3] { "Стандарт", "Активные окна", "Игры" }, "Get-FTweakIdx 'prisep'", "Set-FTweakIdx 'prisep' $v", "game").Info("Меняет Win32PrioritySeparation — как планировщик Windows делит процессор между активным окном и фоном. «Игры» — короткие кванты с максимальным приоритетом переднего плана.", "Активная программа или игра отзывчивее, меньше микрофризов от фоновых задач.", "Фоновые задачи (рендер, архивация) выполняются чуть медленнее.", null, "«Активные окна» — для повседневной работы, «Игры» — для игрового ПК.").Opts("Стандарт Windows: баланс между активным окном и фоновыми задачами.", "Активному окну — больше процессорного времени. Отзывчивее в повседневной работе.", "Короткие кванты и максимальный приоритет переднего плана — меньше микрофризов в играх, фон чуть медленнее.");
			Tw(page, "Приоритет активным программам (MMCSS)", "Фон получает меньше процессора, когда ты чем-то занят", "sysresp", "game").Info("Снижает долю процессора, которую служба мультимедиа резервирует под фоновые задачи (SystemResponsiveness 20 → 10%).", "Чуть плавнее игры и аудио.", "Минусов практически нет (в редких случаях фоновые задачи — загрузки, стриминг, запись — работают чуть медленнее, а на некоторых системах бывает треск звука).", null, "Включить на игровом ПК.");
			Tw(page, "Быстрое закрытие зависших программ", "Windows быстрее предлагает снять зависшее окно и не ждёт при выходе", "hungapp", "game").Info("Сокращает время, через которое окно считается зависшим, и ожидание закрытия программ при выходе.", "Выход из системы и выключение быстрее, зависшие окна не держат минутами.", "Программа с несохранёнными данными может закрыться при выключении быстрее, чем успеет спросить.", null, "Включай, если не забываешь сохранять работу.");
			Tw(page, "Быстрое выключение ПК", "Службам даётся 2 секунды на завершение вместо 5", "fastoff", "game").Info("Уменьшает время ожидания служб при выключении.", "Компьютер выключается быстрее.", "Редкие службы (СУБД, виртуальные машины) могут не успеть корректно завершиться.", null, "Обычному домашнему ПК — включить.");
			Tw(page, "Отключить анимацию сворачивания окон", "Окна сворачиваются и разворачиваются мгновенно", "winanim", "game").Info("Выключает анимацию сворачивания и разворачивания окон.", "Интерфейс ощущается быстрее, особенно на слабом ПК.", "Выглядит менее плавно.", null, "По вкусу.");
			Tw(page, "Быстрый повтор клавиш", "Минимальная задержка и максимальная скорость автоповтора", "kbdrepeat", "game").Info("Ставит минимальную задержку перед автоповтором и максимальную скорость повтора клавиш.", "Удобнее удалять текст и перемещаться стрелками.", "Поначалу клавиша может «проскакивать» лишний символ.", null, "По желанию.");
			Tw(page, "Быстрые подсказки при наведении", "Всплывающие подсказки появляются через 0,1 секунды", "hovertime", "game").Info("Уменьшает задержку перед появлением подсказок и превью при наведении курсора.", "Превью окон на панели задач появляются быстрее.", "Подсказки могут всплывать чаще.", null, "По желанию.");
		}

		private void PerfGames(Page page)
		{
			page.Add(new SectionLabel("Для игр — продвинутое"));
			Tw(page, "Высокий приоритет игр (MMCSS)", "Игры получают больше процессора и видеокарты в планировщике мультимедиа", "gameprio", "game").Info("Повышает приоритет задачи «Games» в службе мультимедиа: процессор, ввод-вывод и видеокарта.", "Стабильнее время кадра в играх, которые пользуются MMCSS.", "Эффект зависит от игры — где-то незаметен.", null, "Включить на игровом ПК.");
			Tw(page, "Точный системный таймер", "Игры получают таймер 0,5 мс независимо от других программ (Windows 11)", "timerres", "game").Info("Возвращает глобальное разрешение таймера: если игра просит точный таймер, он действует для всей системы, как в Windows 10.", "Меньше задержка ввода и ровнее кадры в старых играх.", "Чуть выше энергопотребление в простое.", null, "Включить на настольном ПК. Применяется после перезагрузки.").Builds(22000);
			Tw(page, "Отключить динамический тик", "Таймер ядра работает с постоянной частотой", "dyntick", "game").Info("Выключает динамический тик таймера ядра (bcdedit disabledynamictick).", "У некоторых систем уходят микрофризы и плавает время кадра.", "Ноутбук от батареи работает немного меньше.", null, "Пробуй, только если есть микрофризы. Применяется после перезагрузки.");
			Tw(page, "Отключить MPO", "Многоплоскостное наложение — частая причина мерцания и чёрных экранов", "mpo", "game").Info("Выключает Multi-Plane Overlay в DWM.", "Пропадают мерцание, полосы и чёрные экраны в браузере и играх на некоторых видеокартах NVIDIA/AMD.", "Чуть выше нагрузка на видеокарту при видео в окне.", null, "Включай, только если видишь мерцание или чёрные вспышки.");
			Tw(page, "Запретить запись игр политикой", "Game DVR выключен полностью — даже после обновлений", "gamedvrpol", "game").Info("Запрещает фоновую запись игр групповой политикой.", "Запись не включится обратно после обновления Windows.", "Не будет записи клипов через Xbox Game Bar.", null, "Включи, если пользуешься ShadowPlay, ReLive или OBS.");
			Tw(page, "Отключить службы Xbox", "Вход Xbox Live, сохранения в облаке и геймпады Xbox по беспроводу", "xboxsvc", "game").Info("Отключает службы Xbox: авторизацию, облачные сохранения, сеть Xbox Live и аксессуары.", "Меньше фоновых служб.", "Не будут работать игры из Xbox app/Game Pass, облачные сохранения и беспроводной адаптер Xbox.", null, "Только если не пользуешься Game Pass и Xbox-приложением.");
		}

		private void PerfSys(Page page)
		{
			page.Add(new SectionLabel("Память и диск"));
			Tw(page, "Отключить лишние службы", "Карты, демо-режим, факс, удалённый реестр, родительский контроль…", "junksvc", "game").Expert().Info("Отключает службы, которые почти никому не нужны: загрузку карт, демо-режим магазина, WAP Push, сетевой доступ к медиатеке, удалённый реестр, факс, родительский контроль и телефонную связь.", "Меньше фоновых процессов и открытых портов.", "Перестанут работать офлайн-карты Windows, факс и «Семейная безопасность».", null, "Включить на домашнем ПК.");
			Tw(page, "Не выгружать ядро из памяти", "Драйверы и ядро всегда остаются в ОЗУ", "pagingexec", "game").Info("Запрещает выгружать код ядра и драйверов в файл подкачки.", "Чуть быстрее отклик после простоя.", "Требует свободной памяти — на ПК с 8 ГБ ОЗУ и меньше не нужно.", null, "Включай при 16 ГБ памяти и больше. Применяется после перезагрузки.");
			Tw(page, "Отключить сжатие памяти", "Windows не сжимает данные в ОЗУ", "memcomp", "game").Expert().Info("Выключает сжатие страниц памяти.", "Меньше нагрузка на процессор при большом объёме ОЗУ.", "При нехватке памяти система начнёт активнее использовать файл подкачки.", null, "Только при 32 ГБ ОЗУ и больше.");
			Tw(page, "Не обновлять метку доступа к файлам", "NTFS не записывает время каждого открытия файла", "lastaccess", "game").Info("Отключает обновление «Даты доступа» при каждом чтении файла.", "Меньше мелких записей на диск.", "Некоторые программы резервного копирования используют эту дату.", null, "Включить.");
			Tw(page, "Отключить короткие имена 8.3", "NTFS не создаёт имена вида PROGRA~1 для новых файлов", "shortnames", "game").Info("Запрещает создавать короткие DOS-имена для новых файлов и папок.", "Чуть быстрее работа с папками, где тысячи файлов.", "Очень старые 16-битные программы могут не найти файлы.", null, "Включить.");
			Tw(page, "Отключить виртуализацию (VBS)", "Безопасность на основе виртуализации — минус 5–15% FPS", "vbs", "game").Expert().Info("Выключает Virtualization-Based Security — основу «Целостности памяти» и Credential Guard.", "Заметный прирост FPS и отклика, особенно на процессорах до 2020 года.", "Ниже защита от вредоносных драйверов. Античиты Vanguard и FACEIT могут потребовать включить обратно.", null, "Только для игрового ПК без античитов, требующих VBS. Применяется после перезагрузки.");
			Tw(page, "Отключить защиту от Spectre и Meltdown", "Патчи процессорных уязвимостей замедляют старые CPU", "spectre", "game").Expert().Info("Выключает программные заплатки от уязвимостей Spectre и Meltdown.", "До 10–30% прироста в задачах с диском и системными вызовами на процессорах Intel до 9-го поколения.", "Компьютер становится уязвим к атакам через вредоносный код в браузере и программах.", false, "Не рекомендуем. Только для изолированного игрового ПК со старым процессором.");
		}

		// ---------- installed programs ----------
		private void BuildInstalled(Page p)
		{
			InstalledView iv = p.Add(new InstalledView());
			iv.RefreshClicked += delegate
			{
				p.Refresh(_host.Worker);
			};
			iv.UninstallClicked += delegate(InstalledView.Item it)
			{
				string body = it.Store ? "Приложение Microsoft Store будет удалено для текущего пользователя. Вернуть его можно из Store." : "Запустится деинсталлятор программы — если появится его окно, пройди шаги до конца.\n\nПосле этого WinWeaver поищет остатки и покажет их списком, прежде чем удалять.";
				bool[] r = CheckDialog.Ask(this, "Удалить «" + it.Name + "»?", body, new CheckDialog.Row[1]
				{
					new CheckDialog.Row { Text = "Найти и удалить остатки", Hint = "\nПапки в Program Files и AppData, ярлыки, ключи реестра", Checked = !it.Store }
				}, "Удалить", "Отмена", DlgIcon.Warn);
				if (r == null || !_host.TryBegin(title: "Удаление: " + it.Name))
				{
					return;
				}
				bool left = r[0];
				iv.SetBusy(it, true);
				_host.Worker.Run(delegate(IEngine e)
				{
					object res = e.Eval("$r = Uninstall-FProgram $fa1; if ($r.Ok) { Add-FHist $fa2 'apps' '' $null $fa3 $false }; $r", new Hashtable
					{
						{ "fa1", it.Id },
						{ "fa2", "Удалена программа: " + it.Name },
						{ "fa3", "Вернуть можно, установив программу заново" }
					});
					Hashtable h = res as Hashtable;
					bool ok = h != null && ((h["Ok"] is bool && (bool)h["Ok"]) || (h["Missing"] is bool && (bool)h["Missing"]));
					if (!ok || !left)
					{
						return null;
					}
					return e.Eval("Find-FLeftovers $fa1 $fa2 $fa3", new Hashtable
					{
						{ "fa1", it.Name },
						{ "fa2", it.Pub },
						{ "fa3", it.Loc }
					}) ?? new object[0];
				}, delegate(object v)
				{
					iv.SetBusy(it, false);
					_host.End();
					p.Refresh(_host.Worker);
					if (v != null)
					{
						AskLeftovers(it.Name, v, p);
					}
				});
			};
		}

		private void AskLeftovers(string name, object v, Page p)
		{
			object[] arr = v as object[];
			if (arr == null && v is Hashtable)
			{
				arr = new object[1] { v };
			}
			List<string> paths = new List<string>();
			List<CheckDialog.Row> rows = new List<CheckDialog.Row>();
			if (arr != null)
			{
				foreach (object o in arr)
				{
					Hashtable h = o as Hashtable;
					if (h == null || h["Path"] == null)
					{
						continue;
					}
					string path = h["Path"].ToString();
					string kind = (h["Kind"] ?? "").ToString();
					double size = 0;
					try
					{
						size = Convert.ToDouble(h["Size"] ?? 0, System.Globalization.CultureInfo.InvariantCulture);
					}
					catch
					{
					}
					string k = (kind == "reg") ? "Ключ реестра" : ((kind == "dir") ? "Папка" : "Файл");
					if (kind != "reg" && size > 0)
					{
						k += "  •  " + CleanView.Fmt(size);
					}
					paths.Add(path);
					rows.Add(new CheckDialog.Row { Text = path, Hint = "\n" + k, Checked = true });
				}
			}
			if (rows.Count == 0)
			{
				_status.Set("ok", "«" + name + "» удалена, остатков не найдено");
				return;
			}
			bool[] sel = CheckDialog.Ask(this, "Остатки «" + name + "»", "Найдено " + rows.Count + " " + InstalledView.Plural(rows.Count, "объект", "объекта", "объектов") + ". Сними галочки с того, что нужно оставить. Удаление необратимо.", rows.ToArray(), "Удалить отмеченное", "Оставить всё", DlgIcon.Warn);
			if (sel == null || !_host.TryBegin(title: "Удаление остатков «" + name + "»"))
			{
				return;
			}
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < sel.Length; i++)
			{
				if (sel[i])
				{
					sb.Append(paths[i]).Append('\n');
				}
			}
			string list = sb.ToString();
			_host.Worker.Run(delegate(IEngine e)
			{
				if (list == "")
				{
					return null;
				}
				return e.Eval("$n = Remove-FLeftovers $fa1; Add-FHist $fa2 'apps' '' $null ('Удалено объектов: ' + $n) $false; $n", new Hashtable
				{
					{ "fa1", list },
					{ "fa2", "Удалены остатки: " + name }
				});
			}, delegate(object v2)
			{
				_host.End();
				if (v2 != null)
				{
					_status.Set("ok", "Остатки «" + name + "» удалены: " + v2);
				}
			});
		}

		// ---------- startup: "⋯" menu ----------
		private void WireStartup(StartupView sv, Page p)
		{
			sv.MenuClicked += delegate(StartupView.Item it, Point at)
			{
				DarkMenu m = new DarkMenu();
				m.Header(it.Name, it.On ? "Запускается вместе с Windows" : "Сейчас не запускается");
				m.Item(it.On ? "Выключить" : "Включить", "\ue7e8", it.On ? "Не запускать при входе в систему" : "Запускать при входе в систему", delegate
				{
					it.Toggle.SimClick();
				});
				ToolStripMenuItem d = m.Sub("Отложить запуск", "\ue823", "Запуск через N секунд после входа");
				int[] secs = new int[5] { 0, 30, 60, 120, 300 };
				string[] names = new string[5] { "Без задержки", "На 30 секунд", "На 1 минуту", "На 2 минуты", "На 5 минут" };
				for (int i = 0; i < secs.Length; i++)
				{
					int s = secs[i];
					string nm = names[i];
					DarkMenu.Item(d.DropDownItems, nm, delegate
					{
						StartupDo(p, (s == 0) ? ("Автозагрузка: " + it.Name + " — без задержки") : ("Автозагрузка: " + it.Name + " — запуск через " + nm.Substring(3)), "Set-FStartupDelay " + Q(it.Id) + " " + s, "Set-FStartupDelay " + Q(it.Id) + " " + it.Delay, false);
					}, false, it.Delay == s);
				}
				m.Sep();
				m.Item("Открыть расположение файла", "\ue8b7", null, delegate
				{
					if (!string.IsNullOrEmpty(it.Path) && System.IO.File.Exists(it.Path))
					{
						Shell.Open("explorer.exe", "/select,\"" + it.Path + "\"");
					}
					else
					{
						_status.Set("warn", "Файл программы не найден");
					}
				}).Enabled = it.Exists && !string.IsNullOrEmpty(it.Path);
				m.Item("Найти в интернете", "\ue721", "Что это за программа и нужна ли она", delegate
				{
					string q = it.Name + " " + (string.IsNullOrEmpty(it.Path) ? "" : System.IO.Path.GetFileName(it.Path)) + " автозагрузка";
					Shell.Open("https://www.google.com/search?q=" + Uri.EscapeDataString(q.Trim()));
				});
				m.Sep();
				m.Item("Удалить из автозагрузки", "\ue74d", "Программа останется, вернуть можно в «Истории»", delegate
				{
					if (WDialog.Ask(this, "Удалить «" + it.Name + "» из автозагрузки?", "Запись автозагрузки будет удалена, сама программа останется. Вернуть запись можно в «Истории».", "Удалить", "Отмена", DlgIcon.Warn))
					{
						StartupDo(p, "Автозагрузка: удалена запись «" + it.Name + "»", "Remove-FStartup " + Q(it.Id), "Restore-FStartup " + Q(it.Id), true);
					}
				}, danger: true);
				m.Show(at);
			};
			sv.AddButton("Выключить лишние", BtnKind.Silver, delegate
			{
				List<StartupView.Item> list = sv.Items.FindAll((StartupView.Item i) => i.On && i.Advice == 1);
				if (list.Count == 0)
				{
					_status.Set("ok", "Лишних программ в автозагрузке нет");
					return;
				}
				CheckDialog.Row[] rows = new CheckDialog.Row[list.Count];
				for (int i = 0; i < list.Count; i++)
				{
					rows[i] = new CheckDialog.Row { Text = list[i].Name, Hint = "\n" + (list[i].Company != "" ? list[i].Company : list[i].Source), Checked = true };
				}
				bool[] sel = CheckDialog.Ask(this, "Выключить лишнее в автозагрузке?", "Эти программы не нужны сразу после входа. Они останутся установленными — просто не будут стартовать сами.", rows, "Выключить", "Отмена");
				if (sel == null || !_host.TryBegin(title: "Очистка автозагрузки"))
				{
					return;
				}
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Start-FGroup 'Автозагрузка: выключены лишние программы'");
					try
					{
						for (int i = 0; i < list.Count; i++)
						{
							if (!sel[i])
							{
								continue;
							}
							e.Eval("Invoke-FTracked $fa1 'Set-FStartup $id $false' -Undo $fa2 -Cat 'apps'", new Hashtable
							{
								{ "fa1", "Автозагрузка: " + list[i].Name + " — выключена" },
								{ "fa2", "Set-FStartup " + Q(list[i].Id) + " $true" },
								{ "id", list[i].Id }
							});
						}
					}
					finally
					{
						e.Eval("Stop-FGroup");
					}
					return (object)null;
				}, delegate
				{
					_host.End();
					p.Refresh(_host.Worker);
				});
			});
		}

		private void StartupDo(Page p, string title, string ps, string undo, bool keepReg)
		{
			if (!_host.TryBegin(title: title))
			{
				return;
			}
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("Invoke-FTracked $fa1 $fa2 -Undo $fa3 -Cat 'apps' -KeepReg:$fa4", new Hashtable
				{
					{ "fa1", title },
					{ "fa2", ps },
					{ "fa3", undo },
					{ "fa4", keepReg }
				});
				return (object)null;
			}, delegate
			{
				_host.End();
				p.Refresh(_host.Worker);
			});
		}

		// ---------- one-click fixes ----------
		private Func<IEngine, object> Fix(string id, string hist)
		{
			return delegate(IEngine e)
			{
				e.Eval("Invoke-FTracked $fa1 $fa2 -Cat 'fix' -NoUndo", new Hashtable
				{
					{ "fa1", hist },
					{ "fa2", "Invoke-FFix " + Q(id) }
				});
				return (object)null;
			};
		}

		private void BuildFixes()
		{
			Page page = NewPage("fixes", Glyph.Repair, "Исправления", "Исправления", "Типичные проблемы Windows — решаются одной кнопкой");
			page.Add(new SectionLabel("Сеть и интернет"));
			TilesView t = page.Add(new TilesView(_host));
			t.Add(Glyph.Globe, "Нет интернета", "Сброс Winsock, TCP/IP, кэша DNS и перезапуск адаптеров", "Сеть пропадёт на несколько секунд", Fix("net", "Исправление: сброс сети")).Confirm = "Сетевые адаптеры перезапустятся, соединение пропадёт на 5–10 секунд. Для полного эффекта потом лучше перезагрузить ПК.";
			t.Add(Glyph.Network, "Сайты не открываются", "Очистка кэша DNS и перезапуск DNS-клиента", "Помогает, когда «сайт не найден»", Fix("dns", "Исправление: очищен кэш DNS"));
			t.AddUi(Glyph.Settings, "Ничего не помогло", "Полный сброс сети Windows: адаптеры удаляются и ставятся заново", "Откроются Параметры → «Сброс сети»", delegate
			{
				Shell.Open("ms-settings:network-status");
			});
			t.Add(Glyph.Clock, "Неверное время", "Синхронизация часов с сервером времени Windows", "Из-за неверного времени ломаются сайты и игры", Fix("time", "Исправление: синхронизировано время"));
			page.Add(new SectionLabel("Система и Проводник"));
			t = page.Add(new TilesView(_host));
			t.Add(Glyph.Home, "Не открывается Пуск или поиск", "Перерегистрация Пуска, поиска и оболочки", "Проводник перезапустится", Fix("start", "Исправление: Пуск и поиск"));
			t.Add(Glyph.Apps, "Пропали или сломались значки", "Пересоздание кэша значков", "Проводник перезапустится", Fix("icons", "Исправление: кэш значков"));
			t.Add(Glyph.Picture, "Не видно превью картинок", "Очистка и пересоздание кэша эскизов", "Проводник перезапустится", Fix("thumbs", "Исправление: кэш эскизов"));
			t.Add(Glyph.Folder, "Папки открываются не так", "Сброс запомненных видов и размеров папок", "Вид всех папок вернётся к стандартному", Fix("folders", "Исправление: сброшены виды папок"));
			t.Add(Glyph.Refresh, "Завис Проводник", "Перезапуск Проводника, панели задач и рабочего стола", "Открытые окна папок закроются", Fix("explorer", "Исправление: перезапущен Проводник"), "Перезапустить");
			t.Add(Glyph.Brush, "Шрифты отображаются криво", "Перестроение кэша шрифтов", "Полный эффект после перезагрузки", Fix("fonts", "Исправление: кэш шрифтов"));
			t.Add(Glyph.Apps, "Значки в трее пропали", "Сброс области уведомлений", "Проводник перезапустится", Fix("tray", "Исправление: область уведомлений"));
			t.Add(Glyph.Check, "Не работает буфер обмена", "Очистка буфера и перезапуск его службы", "Скопированное сейчас пропадёт", Fix("clipboard", "Исправление: буфер обмена"));
			t.Add(Glyph.Search, "Поиск не находит файлы", "Перестроение индекса поиска Windows", "Индекс соберётся заново за 10–60 минут", Fix("searchidx", "Исправление: индекс поиска"));
			t.Add(Glyph.Cpu, "Ошибки WMI", "Проверка и восстановление репозитория WMI", "Помогает, если не открываются сведения о системе", Fix("wmi", "Исправление: WMI"));
			page.Add(new SectionLabel("Устройства"));
			t = page.Add(new TilesView(_host));
			t.Add(Glyph.Speaker, "Нет звука", "Перезапуск звуковых служб Windows", "Звук пропадёт на пару секунд", Fix("audio", "Исправление: звук"));
			t.Add(Glyph.Print, "Принтер не печатает", "Очистка очереди печати и перезапуск диспетчера", "Задания в очереди будут удалены", Fix("print", "Исправление: очередь печати"));
			t.Add(Glyph.Bluetooth, "Bluetooth не работает", "Перезапуск служб Bluetooth", "Наушники переподключатся", Fix("bt", "Исправление: Bluetooth"));
			t.Add(Glyph.Usb, "Устройство не определяется", "Повторный поиск оборудования", "Как «Обновить конфигурацию» в диспетчере устройств", Fix("usb", "Исправление: поиск устройств"));
			page.Add(new SectionLabel("Windows и Store"));
			t = page.Add(new TilesView(_host));
			t.Add(Glyph.Sync, "Обновления не ставятся", "Сброс компонентов Центра обновления и его кэша", "Займёт 1–3 минуты", Fix("wu", "Исправление: Центр обновления")).Confirm = "Службы Центра обновления остановятся, кэш загрузок будет очищен, компоненты перерегистрируются. Обновления скачаются заново.";
			t.Add(Glyph.Store, "Не работает Microsoft Store", "Сброс кэша Store и перерегистрация приложения", "Store откроется сам", Fix("store", "Исправление: Microsoft Store"));
			t.Add(Glyph.Shield, "Не открывается «Безопасность Windows»", "Перерегистрация приложения и перезапуск службы", "Defender не выключается", Fix("sechealth", "Исправление: Безопасность Windows"));
			t.AddUi(Glyph.Repair, "Повреждены системные файлы", "SFC + DISM: проверка и восстановление Windows", "10–30 минут, отдельное окно", delegate
			{
				Shell.Console("sfc.exe /scannow; dism.exe /Online /Cleanup-Image /RestoreHealth");
			}, "Запустить");
		}

		// ---------- maintenance ----------
		private void BuildMaintenance(Page page)
		{
			CleanView cv = page.Add(new CleanView());
			cv.RescanClicked += delegate
			{
				cv.SetScanning();
				page.Refresh(_host.Worker);
			};
			cv.CleanClicked += delegate
			{
				List<string> ids = new List<string>();
				StringBuilder sb = new StringBuilder();
				foreach (CleanView.Cat c in cv.Cats)
				{
					if (c.On && c.Size != 0)
					{
						ids.Add(c.Id);
						sb.Append("•  ").Append(c.Name).Append("  —  ").Append(CleanView.Fmt(c.Size)).Append('\n');
					}
				}
				if (ids.Count == 0)
				{
					_status.Set("ok", "Отмеченные категории уже пусты");
					return;
				}
				bool recycle = ids.Contains("recycle");
				if (!WDialog.Ask(this, "Очистить " + CleanView.Fmt(cv.Selected) + "?", sb.ToString() + (recycle ? "\nКорзина будет очищена безвозвратно." : "\nЛичные файлы не затрагиваются."), "Очистить", "Отмена") || !_host.TryBegin(title: "Очистка диска — " + CleanView.Fmt(cv.Selected)))
				{
					return;
				}
				string arg = string.Join(",", ids.ToArray());
				cv.SetScanning();
				_host.Worker.Run(delegate(IEngine e)
				{
					object r = e.Eval("$b = Clear-FItems $fa1; Add-FHist 'Очистка диска' 'clean' '' $null ('Освобождено: ' + (Format-FSize $b)) $false; $b", new Hashtable { { "fa1", arg } });
					return r;
				}, delegate(object r)
				{
					_host.End();
					double b = 0;
					try
					{
						b = Convert.ToDouble(r ?? 0, System.Globalization.CultureInfo.InvariantCulture);
					}
					catch
					{
					}
					cv.LastResult = "Освобождено " + CleanView.Fmt(b);
					_status.Set("ok", cv.LastResult);
					page.Refresh(_host.Worker);
				});
			};
			page.Add(new SectionLabel("Место на диске"));
			Tw(page, "Контроль памяти", "Windows сама удаляет временные файлы и старое содержимое корзины", "storagesense", "clean").Info("Включает «Контроль памяти»: Windows периодически чистит временные файлы и корзину, когда место заканчивается.", "Диск не забивается мусором без твоего участия.", "Файлы в корзине старше 30 дней удаляются автоматически.", null, "Включить, если не хранишь нужное в корзине.");
			Tw(page, "Отключить зарезервированное хранилище", "Вернуть ~7 ГБ, которые Windows держит под обновления", "reserved", "clean").Info("Выключает «зарезервированное хранилище» — место, которое Windows заранее занимает под обновления.", "Освобождается около 7 ГБ на системном диске.", "При почти полном диске крупное обновление может не установиться.", null, "Включи, если на диске C: мало места.");
			page.Add(new SectionLabel("Проверка и обслуживание"));
			TilesView t = page.Add(new TilesView(_host));
			t.AddUi(Glyph.Repair, "Проверка системных файлов", "SFC + DISM RestoreHealth находят и чинят повреждённые файлы Windows", "10–30 минут, отдельное окно", delegate
			{
				Shell.Console("sfc.exe /scannow; dism.exe /Online /Cleanup-Image /RestoreHealth");
			}, "Запустить");
			t.AddUi(Glyph.Disk, "Очистка компонентов", "DISM удаляет старые версии обновлений из WinSxS", "5–20 минут, отдельное окно", delegate
			{
				Shell.Console("dism.exe /Online /Cleanup-Image /StartComponentCleanup");
			}, "Запустить");
			t.AddUi(Glyph.Check, "Проверка диска", "chkdsk ищет ошибки файловой системы без перезагрузки", "1–5 минут, отдельное окно", delegate
			{
				Shell.Console("chkdsk.exe C: /scan");
			}, "Проверить");
			t.AddUi(Glyph.Disk, "Оптимизация дисков", "TRIM для SSD и дефрагментация HDD", "Откроется утилита Windows", delegate
			{
				Shell.Open("dfrgui.exe");
			});
			t.AddUi(Glyph.Disk, "Очистка диска Windows", "Классическая утилита cleanmgr", "Откроется утилита Windows", delegate
			{
				Shell.Open("cleanmgr.exe");
			});
			t.AddUi(Glyph.Settings, "Параметры хранилища", "Что занимает место и настройки «Контроля памяти»", "Откроются Параметры", delegate
			{
				Shell.Open("ms-settings:storagesense");
			});
			BuildRestartTools(page);
		}
	}

	// settings hero: logo, name, version, author, links
	internal class AboutCard : Card
	{
		private readonly Image _logo;

		private readonly List<FButton> _btns = new List<FButton>();

		private readonly Font _big = Theme.F(24f, semi: true, FontStyle.Bold);

		public AboutCard(Image logo, MainForm owner)
		{
			_logo = logo;
			Add("GitHub", BtnKind.Silver, delegate
			{
				Shell.Open(Brand.GitHub);
			});
			Add("Сообщить об ошибке", BtnKind.Ghost, delegate
			{
				owner.ShowBugReport();
			});
			Add("Поддержать", BtnKind.Ghost, delegate
			{
				owner.ShowSupport();
			});
		}

		private void Add(string text, BtnKind k, EventHandler click)
		{
			FButton b = new FButton { Text = text, Kind = k, Font = Theme.Body };
			b.AutoWidth(Theme.S(30f));
			b.Click += click;
			_btns.Add(b);
			Controls.Add(b);
		}

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Theme.S(26f);
			int h = Theme.S(150f);
			int bx = pad + Theme.S(96f);
			int by = h - pad - _btns[0].Height;
			bool below = w < Theme.S(720f);
			if (below)
			{
				by = h - Theme.S(8f);
				h += _btns[0].Height + Theme.S(8f);
			}
			else
			{
				bx = w - pad;
				for (int i = _btns.Count - 1; i >= 0; i--)
				{
					bx -= _btns[i].Width;
					_btns[i].Location = new Point(bx, (h - _btns[i].Height) / 2);
					bx -= Theme.S(8f);
				}
			}
			if (below)
			{
				foreach (FButton b in _btns)
				{
					b.Location = new Point(bx, by);
					bx += b.Width + Theme.S(8f);
				}
			}
			SetBounds(x, y, w, h);
			return h;
		}

		public override void PaintSurface(Graphics g)
		{
			base.PaintSurface(g);
			Theme.Smooth(g);
			Rectangle r = ClientRectangle;
			using (GraphicsPath p = Theme.Round(new Rectangle(0, 0, r.Width - 1, r.Height - 1), Theme.S(12f)))
			{
				using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(22, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
				{
					g.FillPath(b, p);
				}
			}
		}

		protected override void PaintContent(Graphics g)
		{
			int pad = Theme.S(26f);
			int ls = Theme.S(72f);
			int top = Theme.S(150f);
			Rectangle lr = new Rectangle(pad, (top - ls) / 2, ls, ls);
			if (_logo != null)
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(_logo, lr);
			}
			else
			{
				Theme.IconBox(g, lr, Glyph.Settings, bright: true, 0f, Theme.IconBig);
			}
			int tx = lr.Right + Theme.S(22f);
			Size ns = Theme.Measure(Brand.Short, _big);
			int ty = top / 2 - ns.Height / 2 - Theme.S(14f);
			Theme.GradientText(g, Brand.Short, _big, new PointF(tx, ty));
			string ver = "v" + Brand.Version;
			int vw = Theme.PillW(ver, Theme.Tiny);
			Theme.Pill(g, new Rectangle(tx + ns.Width + Theme.S(10f), ty + (ns.Height - Theme.S(20f)) / 2 + Theme.S(2f), vw, Theme.S(20f)), ver, Theme.Tiny, Theme.Silver2, 22);
			TextRenderer.DrawText(g, "Твикер Windows 11 (22H2 – 26H2) без окон PowerShell и батников", Theme.Body, new Point(tx, ty + ns.Height + Theme.S(4f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, L.T("Разработчик: {0}").Replace("{0}", Brand.Developer) + "  •  © 2026  •  GPL-3.0", Theme.Small, new Point(tx, ty + ns.Height + Theme.S(28f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
	}
}
