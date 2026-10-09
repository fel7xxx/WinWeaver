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
using System.Text.RegularExpressions;
using System.Threading;

namespace WinWeaver
{
	internal partial class FakeEngine : IEngine
	{
		private readonly Dictionary<string, bool> _b = new Dictionary<string, bool>();

		private readonly Dictionary<string, int> _i = new Dictionary<string, int>();

		public bool Ready { get; private set; }

		public EngineUI Ui;

		private string _level = Environment.GetEnvironmentVariable("WW_FAKE_LEVEL") ?? "optimal";

		private readonly List<Hashtable> _hist = new List<Hashtable>();

		private readonly Random _rnd = new Random(7);

		private readonly List<object> _rp = new List<object>();

		private object[] RestoreList()
		{
			if (_rp.Count == 0 && Environment.GetEnvironmentVariable("WW_FAKE_NORP") != "1" && _hid >= 0)
			{
				DateTime n = DateTime.Now;
				_rp.Add(new Hashtable { { "Seq", 5 }, { "Desc", "WinWeaver 2026-10-09 01:02" }, { "Iso", n.AddHours(-3).ToString("s") }, { "Type", 12 } });
				_rp.Add(new Hashtable { { "Seq", 4 }, { "Desc", "Комплект средств для развертывания и оценки Windows" }, { "Iso", n.AddDays(-1).AddHours(-2).ToString("s") }, { "Type", 0 } });
				_rp.Add(new Hashtable { { "Seq", 3 }, { "Desc", "Driver Booster : AMD GPIO Controller" }, { "Iso", n.AddDays(-4).ToString("s") }, { "Type", 10 } });
				_rp.Add(new Hashtable { { "Seq", 2 }, { "Desc", "O&O ShutUp10" }, { "Iso", n.AddDays(-4).AddHours(-1).ToString("s") }, { "Type", 12 } });
				_rp.Add(new Hashtable { { "Seq", 1 }, { "Desc", "Установщик модулей Windows" }, { "Iso", n.AddDays(-4).AddHours(-5).ToString("s") }, { "Type", 12 } });
			}
			return _rp.ToArray();
		}

		private int _hid;

		private void Msg(string k, string t)
		{
			if (Ui != null)
			{
				Ui.Msg(k, t);
			}
		}

		private Hashtable H(string title, string cat, bool can, string group, string gtitle, DateTime t, string note = "", bool done = false)
		{
			Hashtable h = new Hashtable
			{
				{ "Id", "h" + (++_hid) },
				{ "Time", t.ToString("s") },
				{ "Title", title },
				{ "Cat", cat },
				{ "Group", group ?? "" },
				{ "GroupTitle", gtitle ?? "" },
				{ "Can", can },
				{ "Done", done },
				{ "Note", note }
			};
			_hist.Add(h);
			return h;
		}

		private void Seed()
		{
			if (Environment.GetEnvironmentVariable("WW_FAKE_EMPTY") == "1")
			{
				return;
			}
			DateTime t = DateTime.Now.AddDays(-1.0).Date.AddHours(19.0).AddMinutes(12.0);
			string g = "gseed";
			string gt = "Автонастройка «Оптимальная»";
			H("Создана точка восстановления", "restore", can: false, g, gt, t, "К ней можно откатить всю систему");
			H("Телеметрия выключена", "privacy", can: true, g, gt, t.AddSeconds(20.0));
			H("Copilot и Recall выключены", "privacy", can: true, g, gt, t.AddSeconds(25.0));
			H("Реклама и рекомендации выключены", "privacy", can: true, g, gt, t.AddSeconds(31.0));
			H("Удалены встроенные приложения: 14", "apps", can: false, g, gt, t.AddSeconds(80.0), "Вернуть можно из Microsoft Store");
			H("Тёмная тема", "ui", can: true, g, gt, t.AddSeconds(95.0));
			H("Обои WeavOS на рабочем столе и экране блокировки", "ui", can: true, g, gt, t.AddSeconds(99.0));
			H("Название системы — WeavOS", "ui", can: true, g, gt, t.AddSeconds(100.0));
			H("Установлен Firefox", "apps", can: false, g, gt, t.AddSeconds(160.0), "Удалить можно в «Параметры → Приложения»");
			H("Удалён Microsoft Edge", "apps", can: false, g, gt, t.AddSeconds(190.0), "WebView2 оставлен");
			DateTime n = DateTime.Now;
			H("DNS-сервер: Cloudflare", "net", can: true, null, null, n.AddMinutes(-42.0));
			H("Показывать расширения файлов", "ui", can: true, null, null, n.AddMinutes(-17.0), "", done: true);
			H("Фоновая запись игр (Game DVR) (отменено)", "game", can: true, null, null, n.AddMinutes(-6.0));
		}

		private object HistOut()
		{
			object[] a = new object[_hist.Count];
			for (int i = 0; i < _hist.Count; i++)
			{
				a[i] = _hist[_hist.Count - 1 - i].Clone();
			}
			return a;
		}

		private static string V(Hashtable vars, string k)
		{
			if (vars == null || vars[k] == null)
			{
				return "";
			}
			return vars[k].ToString();
		}

		private object Special(string s, Hashtable vars)
		{
			if (s.StartsWith("Invoke-FSetting"))
			{
				Hashtable fv = new Hashtable { { "v", vars["fa4"] } };
				Flip(V(vars, "fa3"), fv);
				H(V(vars, "fa1"), V(vars, "fa5"), can: true, null, null, DateTime.Now);
				return null;
			}
			if (s.StartsWith("Invoke-FTracked"))
			{
				Flip(V(vars, "fa2"), vars);
				Match pm = Regex.Match(V(vars, "fa2") ?? "", "Set-FUpdPause (\\d+)");
				if (pm.Success)
				{
					int pd = int.Parse(pm.Groups[1].Value);
					_pauseUntil = (pd > 0) ? DateTime.Now.AddDays(pd) : DateTime.MinValue;
					_pauseStart = DateTime.Now;
				}
				bool noUndo = vars != null && vars["fa5"] is bool && (bool)vars["fa5"];
				H(V(vars, "fa1"), (vars != null && vars.ContainsKey("fa4")) ? V(vars, "fa4") : "apps", !noUndo, null, null, DateTime.Now, V(vars, "fa7"));
				Msg("ok", "Готово: " + V(vars, "fa1"));
				return null;
			}
			if (s.Contains("Undo-FHistory"))
			{
				Thread.Sleep(400);
				foreach (Hashtable h in _hist)
				{
					if ((string)h["Id"] == V(vars, "fa1"))
					{
						h["Done"] = true;
						Msg("ok", "Откачено: " + h["Title"]);
					}
				}
				return true;
			}
			if (s.Contains("Undo-FHistGroup"))
			{
				int k = 0;
				foreach (Hashtable h2 in _hist)
				{
					if ((string)h2["Group"] == V(vars, "fa1") && (bool)h2["Can"] && !(bool)h2["Done"])
					{
						Thread.Sleep(150);
						h2["Done"] = true;
						k++;
					}
				}
				Msg("ok", "Откачено изменений: " + k);
				return k;
			}
			if (s.Contains("Invoke-FLevel"))
			{
				string lvl = V(vars, "lvl");
				string br = V(vars, "br");
				string name = (lvl == "light") ? "Базовая" : ((lvl == "gamer") ? "Максимальная" : "Оптимальная");
				string[] steps = new string[8] { "Точка восстановления", "Телеметрия", "Copilot и Recall", "Реклама и рекомендации", "Тёмная тема", "Обои WeavOS", "Название WeavOS", (br == "keep") ? "Готово" : "Установка браузера" };
				string g = "g" + (++_hid);
				int delay = Environment.GetEnvironmentVariable("WW_FAKE_SLOW") == "1" ? 1500 : 350;
				for (int i = 0; i < steps.Length; i++)
				{
					Msg("ok", "Автонастройка «" + name + "» [" + (i + 1) + "/" + steps.Length + "]: " + steps[i] + "…");
					Thread.Sleep(delay);
					H(steps[i], (i < 4) ? "privacy" : "ui", i > 0, g, "Автонастройка «" + name + "»", DateTime.Now);
				}
				_level = lvl;
				Msg("ok", "Автонастройка «" + name + "» выполнена");
				return true;
			}
			return null;
		}

		public string LastError => "";

		public void Start()
		{
			Seed();
			Ready = true;
		}

		private bool B(string k, bool d)
		{
			if (!_b.ContainsKey(k))
			{
				return d;
			}
			return _b[k];
		}

		private int I(string k, int d)
		{
			if (!_i.ContainsKey(k))
			{
				return d;
			}
			return _i[k];
		}

		public void Stop()
		{
		}

		public object Eval(string script, Hashtable vars = null)
		{
			script = script.Trim();
			Thread.Sleep(15);
			Match match = Regex.Match(script, "^\\((Get-\\w+)\\)\\s*-(eq|like)\\s*'([^']*)'$");
			if (match.Success)
			{
				string text = (Route(match.Groups[1].Value, vars) ?? "").ToString();
				string value = match.Groups[3].Value;
				if (match.Groups[2].Value == "eq")
				{
					return text == value;
				}
				return Regex.IsMatch(text, "^" + Regex.Escape(value).Replace("\\*", ".*") + "$");
			}
			if (script.StartsWith("Invoke-FSetting") || script.StartsWith("Invoke-FTracked") || script.Contains("Undo-FHist") || script.Contains("Invoke-FLevel"))
			{
				return Special(script, vars);
			}
			return Route(script, vars);
		}

		private object Route(string s, Hashtable vars)
		{
			if (s == "Test-FBuildChanged")
			{
				return Environment.GetEnvironmentVariable("WW_FAKE_DRIFT") == "1";
			}
			if (s == "Get-FDrift")
			{
				return new object[3]
				{
					new Hashtable { { "Id", "h1" }, { "Title", "Телеметрия и сбор данных" }, { "Cat", "privacy" } },
					new Hashtable { { "Id", "h2" }, { "Title", "Реклама в меню «Пуск»" }, { "Cat", "privacy" } },
					new Hashtable { { "Id", "h3" }, { "Title", "Bing в поиске" }, { "Cat", "privacy" } }
				};
			}
			if (s.StartsWith("Uninstall-FWinWeaver") || s.StartsWith("[void](Repair-FDrift"))
			{
				Thread.Sleep(600);
				Msg("ok", s.StartsWith("Uninstall") ? "WinWeaver удалён из системы" : "Возвращено настроек: 3");
				return true;
			}
			if (s.StartsWith("Get-FPlanNames"))
			{
				List<string> pl = new List<string> { "Точка восстановления", "Телеметрия и сбор данных", "Copilot и Recall", "Реклама и рекомендации", "Bing в поиске", "Экран блокировки без рекламы", "Запрет автошифрования BitLocker", "Тёмная тема", "Классическое меню", "Панель задач", "OneDrive", "Встроенный мусор", "Обновления Windows", "Игровой режим и HAGS", "Схема питания", "7-Zip", "Расширения файлов", "Временные файлы" };
				string o = V(vars, "opts") ?? "";
				if (!o.Contains("nowall"))
				{
					pl.Add("Обои WeavOS");
				}
				if (!o.Contains("noname"))
				{
					pl.Add("Название WeavOS");
				}
				if (V(vars, "br") != "keep")
				{
					pl.Add("Браузер вместо Edge");
				}
				return pl.ToArray();
			}
			bool handled;
			object ex = Extra(s, vars, out handled);
			if (handled)
			{
				return ex;
			}
			switch (s)
			{
			case "Get-FRebootInfo":
				return new Hashtable { { "App", B("reboot", Environment.GetEnvironmentVariable("WW_FAKE_REBOOT") == "1") }, { "Sys", false } };
			case "[bool]$global:NeedReboot":
				return B("reboot", Environment.GetEnvironmentVariable("WW_FAKE_REBOOT") == "1");
			case "Get-FSysInfo":
				Thread.Sleep(300);
				return new Hashtable
				{
					{ "OS", "Windows 11 Pro 25H2" },
					{ "Build", "26200.6584" },
					{ "CPU", "AMD Ryzen 7 7800X3D 8-Core Processor" },
					{ "Cores", "8 ядер / 16 потоков" },
					{ "GPU", "NVIDIA GeForce RTX 4070 SUPER" },
					{ "RAM", "32 ГБ" },
					{ "RAMUsed", 41 },
					{ "Disk", "612 ГБ свободно из 931 ГБ" },
					{ "DiskFree", "612 ГБ свободно" },
					{ "DiskSize", "931 ГБ" },
					{ "Drive", "C:" },
					{ "DiskUsed", 34 },
					{ "PC", "WEAVOS-PC" },
					{ "User", "Antigravity" },
					{ "Uptime", "2 ч 17 мин" }
				};
			case "Get-FApps":
			{
				string[][] apps = new string[][]
				{
					new string[] { "Microsoft.Copilot", "Copilot", "ИИ-помощник Microsoft в отдельном окне", "1", "1" },
					new string[] { "Clipchamp.Clipchamp", "Clipchamp", "Онлайн-видеоредактор с подпиской", "1", "1" },
					new string[] { "MSTeams", "Microsoft Teams", "Мессенджер для работы", "1", "0" },
					new string[] { "Microsoft.BingWeather", "Погода", "Прогноз погоды от MSN", "0", "1" },
					new string[] { "Microsoft.OutlookForWindows", "Outlook (новый)", "Почта — веб-приложение Outlook", "0", "1" },
					new string[] { "Microsoft.MicrosoftSolitaireCollection", "Solitaire", "Пасьянсы с рекламой", "1", "0" },
					new string[] { "MicrosoftCorporationII.QuickAssist", "Быстрая помощь", "Удалённое управление ПК — частый инструмент мошенников", "1", "1" },
					new string[] { "Microsoft.YourPhone", "Связь с телефоном", "Звонки, SMS и фото с Android/iPhone", "0", "1" }
				};
				object[] r = new object[apps.Length];
				for (int i = 0; i < apps.Length; i++)
				{
					r[i] = new Hashtable { { "Id", apps[i][0] }, { "Name", apps[i][1] }, { "Desc", apps[i][2] }, { "Rec", int.Parse(apps[i][3]) }, { "Installed", apps[i][4] == "1" && !B("app_" + apps[i][0], false) } };
				}
				return r;
			}
			case "Get-FStartup":
				return new object[9]
				{
					new Hashtable
					{
						{ "Id", "HKCU|OneDrive" }, { "Name", "OneDrive" }, { "Path", @"C:\Users\u\AppData\Local\Microsoft\OneDrive\OneDrive.exe" }, { "Company", "Microsoft Corporation" }, { "Desc", "Microsoft OneDrive" }, { "Exists", true },
						{ "Command", "\"C:\\Users\\u\\AppData\\Local\\Microsoft\\OneDrive\\OneDrive.exe\" /background" }, { "Source", "Пользователь" }, { "Enabled", true }
					},
					new Hashtable
					{
						{ "Id", "HKCU|Spotify" }, { "Name", "Spotify" }, { "Path", @"C:\Users\u\AppData\Roaming\Spotify\Spotify.exe" }, { "Company", "Spotify Ltd" }, { "Desc", "Spotify" }, { "Exists", true },
						{ "Command", "\"C:\\Users\\u\\AppData\\Roaming\\Spotify\\Spotify.exe\" --autostart --minimized" }, { "Source", "Пользователь" }, { "Enabled", true }
					},
					new Hashtable
					{
						{ "Id", "HKLM|EpicGamesLauncher" }, { "Name", "EpicGamesLauncher" }, { "Path", @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe" }, { "Company", "Epic Games, Inc." }, { "Desc", "Epic Games Launcher" }, { "Exists", true },
						{ "Command", "EpicGamesLauncher.exe -silent" }, { "Source", "Все пользователи" }, { "Enabled", false }
					},
					new Hashtable
					{
						{ "Id", "HKCU|OldUpdater" }, { "Name", "GameLauncherHelper" }, { "Path", @"C:\Program Files\OldGame\helper.exe" }, { "Company", "" }, { "Desc", "" }, { "Exists", false },
						{ "Command", "\"C:\\Program Files\\OldGame\\helper.exe\" /tray" }, { "Source", "Пользователь" }, { "Enabled", true }
					},
					new Hashtable
					{
						{ "Id", "HKCU|Discord" },
						{ "Path", @"C:\Users\u\AppData\Local\Discord\app-1.0.9187\Discord.exe" },
						{ "Company", "Discord Inc." },
						{ "Desc", "Discord" },
						{ "Exists", true },
						{ "Name", "Discord" },
						{ "Command", "\"C:\\Users\\u\\AppData\\Local\\Discord\\Update.exe\" --processStart Discord.exe" },
						{ "Source", "Пользователь" },
						{ "Enabled", true }
					},
					new Hashtable
					{
						{ "Id", "HKCU|Steam" },
						{ "Path", @"C:\Program Files (x86)\Steam\steam.exe" },
						{ "Company", "Valve Corporation" },
						{ "Desc", "Steam Client Bootstrapper" },
						{ "Exists", true },
						{ "Name", "Steam" },
						{ "Command", "\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent" },
						{ "Source", "Пользователь" },
						{ "Enabled", false }
					},
					new Hashtable
					{
						{ "Id", "HKLM|SecurityHealth" },
						{ "Path", @"C:\Windows\System32\SecurityHealthSystray.exe" },
						{ "Company", "Microsoft Corporation" },
						{ "Desc", "Windows Security notification icon" },
						{ "Exists", true },
						{ "Name", "SecurityHealth" },
						{ "Command", "%windir%\\system32\\SecurityHealthSystray.exe" },
						{ "Source", "Все пользователи" },
						{ "Enabled", false }
					},
					new Hashtable
					{
						{ "Id", "HKLM|RtkAudUService" },
						{ "Path", @"C:\Windows\System32\RtkAudUService64.exe" },
						{ "Company", "Realtek Semiconductor" },
						{ "Desc", "Realtek HD Audio Universal Service" },
						{ "Exists", true },
						{ "Name", "RtkAudUService" },
						{ "Command", "\"C:\\Windows\\System32\\RtkAudUService64.exe\" -background" },
						{ "Source", "Все пользователи" },
						{ "Enabled", true }
					},
					new Hashtable
					{
						{ "Id", "UFOLD|Spotify.lnk" },
						{ "Path", @"C:\Users\u\AppData\Roaming\Spotify\Spotify.exe" },
						{ "Company", "Spotify AB" },
						{ "Desc", "Spotify" },
						{ "Exists", true },
						{ "Name", "Spotify" },
						{ "Command", "C:\\Users\\u\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\Spotify.lnk" },
						{ "Source", "Папка автозагрузки" },
						{ "Enabled", true }
					}
				};
			case "Get-FRestoreList":
				return RestoreList();
			case "Get-FRestoreInfo":
				return new Hashtable
				{
					{ "On", true },
					{ "Used", 2254857830.0 * Math.Max(1, _rp.Count) / 4.0 },
					{ "Max", 12884901888.0 },
					{ "Cap", 257698037760.0 },
					{ "Drive", "C:" },
					{ "Auto", I("twi_rpauto", 0) }
				};
			case "Enable-FRestore":
				Msg("ok", "Защита системы включена для диска C:");
				return true;
			case "New-FRestorePointNow":
				Thread.Sleep(600);
				RestoreList();
				_rp.Insert(0, new Hashtable
				{
					{ "Seq", (int)((Hashtable)_rp[0])["Seq"] + 1 },
					{ "Desc", "WinWeaver " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") },
					{ "Iso", DateTime.Now.ToString("s") },
					{ "Type", 12 }
				});
				Msg("ok", "Точка восстановления создана");
				return true;
			case "[void](Remove-FOldRestorePoints)":
			{
				int n = _rp.Count - 1;
				_rp.RemoveRange(1, Math.Max(0, n));
				Msg("ok", "Удалено старых точек: " + n + ". Последняя осталась");
				return n;
			}
			case "Get-FNetInfo":
				return new Hashtable
				{
					{ "Ok", true },
					{ "Phys", true },
					{ "Vpn", Environment.GetEnvironmentVariable("WW_FAKE_VPN") == "1" },
					{ "VpnName", "happ-tun" },
					{ "VpnDesc", "Happ Tunnel" },
					{ "VpnIp", "172.19.0.1" },
					{ "VpnDns", "172.19.0.2" },
					{ "Name", "Wi-Fi" },
					{ "Desc", "Intel(R) Wi-Fi 6E AX211 160MHz" },
					{ "Wifi", Environment.GetEnvironmentVariable("WW_FAKE_ETH") != "1" },
					{ "Speed", "1.2 Gbps" },
					{ "Ip", "192.168.1.34" },
					{ "Gw", "192.168.1.1" },
					{ "Dns", (I("dns", 1) == 1) ? "1.1.1.1, 1.0.0.1" : ((I("dns", 1) == 4) ? "94.140.14.14, 94.140.15.15" : ((I("dns", 1) == 2) ? "8.8.8.8, 8.8.4.4" : ((I("dns", 1) == 3) ? "9.9.9.9, 149.112.112.112" : "192.168.1.1"))) },
					{ "Ssid", "Keenetic-5G" },
					{ "Signal", 82 },
					{ "Mac", "3C-21-9C-11-22-33" },
					{ "Dhcp", "Enabled" }
				};
			case "Test-FDnsSpeed":
				Thread.Sleep(900);
				return new Hashtable
				{
					{ "1", 9 + _rnd.Next(6) },
					{ "2", 18 + _rnd.Next(8) },
					{ "3", 27 + _rnd.Next(10) },
					{ "4", 34 + _rnd.Next(12) }
				};
			case "Get-FLevelId":
				return _level;
			case "Get-FHistory":
				return HistOut();
			case "Get-FHistCount":
			{
				int n = 0;
				foreach (Hashtable h in _hist)
				{
					if (!(bool)h["Done"])
					{
						n++;
					}
				}
				return n;
			}
			case "Clear-FHistory":
				_hist.Clear();
				Msg("ok", "История изменений очищена");
				return null;
			case "Get-FSearchIdx":
				return I("search", 0);
			case "Get-FPowerIdx":
				return I("power", 0);
			case "Get-FDnsText":
				return "Ethernet: 1.1.1.1, 1.0.0.1";
			case "Get-FDns":
				return I("dns", 1);
			default:
			{
				if (s.StartsWith("Get-FPing"))
				{
					Thread.Sleep(60);
					Hashtable ph = new Hashtable();
					foreach (string hst in V(vars, "fa1").Split(','))
					{
						int bas = hst.StartsWith("192.") ? 2 : (hst == "1.1.1.1" ? 11 : 17);
						if (Environment.GetEnvironmentVariable("WW_FAKE_VPN") == "1" && !hst.StartsWith("192."))
						{
							ph[hst] = 0;
							continue;
						}
						ph[hst] = (_rnd.Next(60) == 0) ? (-1) : (bas + _rnd.Next(hst.StartsWith("192.") ? 2 : 7));
					}
					return ph;
				}
				if (s.StartsWith("[void](Remove-FRestorePoint"))
				{
					RestoreList();
					int seq = (vars != null && vars["seq"] is int) ? (int)vars["seq"] : -1;
					_rp.RemoveAll((object o) => (int)((Hashtable)o)["Seq"] == seq);
					Msg("ok", "Точка №" + seq + " удалена");
					return true;
				}
				Match tw = Regex.Match(s, "^Get-FTweak(Idx)? '(\\w+)'$");
				if (tw.Success)
				{
					string id = tw.Groups[2].Value;
					if (tw.Groups[1].Success && tw.Groups[1].Value == "Idx")
					{
						return I("twi_" + id, (id == "tcpauto" || id == "rpauto") ? 0 : 1);
					}
					return B("tw_" + id, Array.IndexOf(new string[] { "tailored", "feedback", "scoobe", "startdelay", "windowed", "dop2p", "widgets", "launchpc", "rss", "rpfreq" }, id) >= 0);
				}
			}
				if (s.StartsWith("'Отключено функций"))
				{
					return "Отключено функций слежки: 3 из 4  •  Телеметрия: ВЫКЛ, Copilot: ВЫКЛ, реклама: ВЫКЛ, Bing: ВКЛ";
				}
				if (s.StartsWith("'Питание"))
				{
					return "Питание: Максимальная производительность  •  Игровой режим: ВКЛ  •  Game DVR: ВЫКЛ  •  Ускорение мыши: ВЫКЛ";
				}
				if (s.StartsWith("'Defender"))
				{
					return "Defender: ВКЛ  •  BitLocker: диски не зашифрованы, автошифрование запрещено";
				}
				if (s.StartsWith("'Edge"))
				{
					return "Edge: удалён  •  OneDrive: удалён  •  обновляется до новых версий";
				}
				if (s.Contains("Get-Telemetry"))
				{
					if (!B("tel", d: true))
					{
						return "ВКЛ";
					}
					return "ВЫКЛ";
				}
				if (s.Contains("Get-AI"))
				{
					if (!B("ai", d: true))
					{
						return "ВКЛ";
					}
					return "ВЫКЛ";
				}
				if (s.Contains("Get-Ads"))
				{
					if (!B("ads", d: true))
					{
						return "ВКЛ";
					}
					return "ВЫКЛ";
				}
				if (s.Contains("Get-Bing"))
				{
					if (!B("bing", d: false))
					{
						return "ВКЛ";
					}
					return "ВЫКЛ";
				}
				if (s.Contains("Get-Theme"))
				{
					if (!B("theme", d: true))
					{
						return "Светлая";
					}
					return "Тёмная";
				}
				if (s.Contains("Get-Transp"))
				{
					if (!B("transp", d: true))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-Menu"))
				{
					if (!B("menu", d: false))
					{
						return "Windows 11";
					}
					return "Классическое (Win10)";
				}
				if (s.Contains("Get-TaskView"))
				{
					if (!B("tv", d: false))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-EndTask"))
				{
					if (!B("et", d: true))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-Align"))
				{
					if (!B("align", d: true))
					{
						return "По центру";
					}
					return "Слева";
				}
				if (s.Contains("Get-Ext"))
				{
					if (!B("ext", d: false))
					{
						return "Скрывать";
					}
					return "Показывать";
				}
				if (s.Contains("Get-Hidden"))
				{
					if (!B("hid", d: false))
					{
						return "Скрывать";
					}
					return "Показывать";
				}
				if (s.Contains("Get-GameMode"))
				{
					if (!B("gm", d: true))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-Dvr"))
				{
					if (!B("dvr", d: false))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-MouseAcc"))
				{
					if (!B("macc", d: false))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-Hags"))
				{
					if (!B("hags", d: true))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-HvciWanted"))
				{
					return B("hvci", d: false);
				}
				if (s.Contains("Get-FastBoot"))
				{
					if (!B("fb", d: false))
					{
						return "ВЫКЛ";
					}
					return "ВКЛ";
				}
				if (s.Contains("Get-FAutoChoice"))
				{
					if (!B("auto", d: true))
					{
						return "no";
					}
					return "yes";
				}
				if (s.Contains("Get-Power"))
				{
					return (new string[3] { "Максимальная производительность", "Высокая производительность", "Сбалансированная" })[I("power", 0)];
				}
				if (s.Contains("Get-Search"))
				{
					return (new string[3] { "Значок", "Поле поиска", "Скрыт" })[I("search", 0)];
				}
				if (s.Contains("Get-Defender"))
				{
					return "ВКЛ";
				}
				if (s.Contains("Get-BL"))
				{
					return "диски не зашифрованы, автошифрование запрещено";
				}
				if (s.Contains("Get-WU"))
				{
					if (!B("wu", d: false))
					{
						return "обновляется до новых версий";
					}
					return "остаётся на 25H2";
				}
				if (s.Contains("Get-FLevelNow"))
				{
					return "Последняя автонастройка: «Оптимальная», 08.10.2026 21:55";
				}
				if (s.Contains("Get-FLevelCard"))
				{
					return "Описание уровня для проверки интерфейса.\nПрименён: 08.10.2026 21:55";
				}
				if (s.Contains("Get-Edge"))
				{
					return "удалён";
				}
				if (s.Contains("Get-OneDrive"))
				{
					return "удалён";
				}
				if (s.Contains("Get-FPrivacyScore"))
				{
					int num = 0;
					if (B("tel", d: true))
					{
						num++;
					}
					if (B("ai", d: true))
					{
						num++;
					}
					if (B("ads", d: true))
					{
						num++;
					}
					if (B("bing", d: false))
					{
						num++;
					}
					return num;
				}
				if (s.StartsWith("Set-") || s.StartsWith("Invoke-") || s.StartsWith("Remove-") || s.StartsWith("New-") || s.StartsWith("Install-") || s.StartsWith("Clear-") || s.StartsWith("Update-") || s.StartsWith("Find-") || s.StartsWith("Restore-"))
				{
					Flip(s, vars);
					return null;
				}
				return null;
			}
		}

		private void Flip(string s, Hashtable vars)
		{
			bool value = false;
			int num = 0;
			bool flag = false;
			if (vars != null)
			{
				foreach (DictionaryEntry var in vars)
				{
					if (var.Value is bool)
					{
						value = (bool)var.Value;
					}
					else if (var.Value is int)
					{
						num = (int)var.Value;
						flag = true;
					}
				}
			}
			if (s.Contains("Remove-FApp"))
			{
				Match am = Regex.Match(s, "Remove-FApp '([^']+)'");
				if (am.Success)
				{
					_b["app_" + am.Groups[1].Value] = true;
				}
				return;
			}
			Match tw = Regex.Match(s, "Set-FTweak(Idx)? '(\\w+)'");
			if (tw.Success)
			{
				if (tw.Groups[1].Value == "Idx")
				{
					_i["twi_" + tw.Groups[2].Value] = flag ? num : 1;
				}
				else
				{
					_b["tw_" + tw.Groups[2].Value] = value;
				}
				return;
			}
			if (s.Contains("Set-Telemetry"))
			{
				_b["tel"] = value;
			}
			else if (s.Contains("Set-AI"))
			{
				_b["ai"] = value;
			}
			else if (s.Contains("Set-Ads"))
			{
				_b["ads"] = value;
			}
			else if (s.Contains("Set-Bing"))
			{
				_b["bing"] = value;
			}
			else if (s.Contains("Set-Theme"))
			{
				_b["theme"] = value;
			}
			else if (s.Contains("Set-Transp"))
			{
				_b["transp"] = value;
			}
			else if (s.Contains("Set-Menu"))
			{
				_b["menu"] = value;
			}
			else if (s.Contains("Set-TaskView"))
			{
				_b["tv"] = value;
			}
			else if (s.Contains("Set-EndTask"))
			{
				_b["et"] = value;
			}
			else if (s.Contains("Set-Align"))
			{
				_b["align"] = value;
			}
			else if (s.Contains("Set-Ext"))
			{
				_b["ext"] = value;
			}
			else if (s.Contains("Set-Hidden"))
			{
				_b["hid"] = value;
			}
			else if (s.Contains("Set-GameMode"))
			{
				_b["gm"] = value;
			}
			else if (s.Contains("Set-Dvr"))
			{
				_b["dvr"] = value;
			}
			else if (s.Contains("Set-MouseAcc"))
			{
				_b["macc"] = value;
			}
			else if (s.Contains("Set-Hags"))
			{
				_b["hags"] = value;
				_b["reboot"] = true;
			}
			else if (s.Contains("Set-Hvci"))
			{
				_b["hvci"] = value;
			}
			else if (s.Contains("Set-FastBoot"))
			{
				_b["fb"] = value;
			}
			else if (s.Contains("Set-WUStay"))
			{
				_b["wu"] = value;
			}
			else if (s.Contains("Set-FPowerIdx"))
			{
				_i["power"] = (flag ? num : I("power", 0));
			}
			else if (s.Contains("Set-FSearchIdx"))
			{
				_i["search"] = (flag ? num : I("search", 0));
			}
			else if (s.Contains("Set-FDns"))
			{
				_i["dns"] = (flag ? num : I("dns", 1));
			}
			else if (s.Contains("Invoke-FPrivacyAll"))
			{
				Dictionary<string, bool> b = _b;
				Dictionary<string, bool> b2 = _b;
				Dictionary<string, bool> b3 = _b;
				bool flag2 = (_b["bing"] = true);
				bool flag4 = (b3["ads"] = flag2);
				bool value2 = (b2["ai"] = flag4);
				b["tel"] = value2;
			}
		}

		public string Get(string f)
		{
			object obj = Eval(f);
			if (obj != null)
			{
				return obj.ToString();
			}
			return "";
		}

		public void Call(string f, params object[] args)
		{
			Hashtable hashtable = new Hashtable();
			for (int i = 0; i < args.Length; i++)
			{
				hashtable["a" + i] = args[i];
			}
			Eval(f, hashtable);
		}

		public T As<T>(object o, T fallback)
		{
			try
			{
				if (o == null)
				{
					return fallback;
				}
				return (T)Convert.ChangeType(o, typeof(T));
			}
			catch
			{
				return fallback;
			}
		}
	}
}
