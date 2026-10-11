using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace WinWeaver;

internal class FakeEngine : IEngine
{
	private readonly Dictionary<string, bool> _b = new Dictionary<string, bool>();

	private readonly Dictionary<string, int> _i = new Dictionary<string, int>();

	public EngineUI Ui;

	private string _level = Environment.GetEnvironmentVariable("WW_FAKE_LEVEL") ?? "optimal";

	private readonly List<Hashtable> _hist = new List<Hashtable>();

	private readonly Random _rnd = new Random(7);

	private readonly List<object> _rp = new List<object>();

	private int _hid;

	private bool _inStartup;

	private readonly HashSet<string> _removed = new HashSet<string>();

	private DateTime _pauseUntil = DateTime.Now.AddDays(9.0);

	private DateTime _pauseStart = DateTime.Now.AddDays(-5.0);

	private double _blPct = ((Environment.GetEnvironmentVariable("WW_FAKE_NOBL") == "1") ? 0.0 : 63.0);

	public bool Ready { get; private set; }

	public string LastError => "";

	private object[] RestoreList()
	{
		if (_rp.Count == 0 && Environment.GetEnvironmentVariable("WW_FAKE_NORP") != "1" && _hid >= 0)
		{
			DateTime now = DateTime.Now;
			_rp.Add(new Hashtable
			{
				{ "Seq", 5 },
				{ "Desc", "WinWeaver 2026-10-09 01:02" },
				{
					"Iso",
					now.AddHours(-3.0).ToString("s")
				},
				{ "Type", 12 }
			});
			_rp.Add(new Hashtable
			{
				{ "Seq", 4 },
				{ "Desc", "Комплект средств для развертывания и оценки Windows" },
				{
					"Iso",
					now.AddDays(-1.0).AddHours(-2.0).ToString("s")
				},
				{ "Type", 0 }
			});
			_rp.Add(new Hashtable
			{
				{ "Seq", 3 },
				{ "Desc", "Driver Booster : AMD GPIO Controller" },
				{
					"Iso",
					now.AddDays(-4.0).ToString("s")
				},
				{ "Type", 10 }
			});
			_rp.Add(new Hashtable
			{
				{ "Seq", 2 },
				{ "Desc", "O&O ShutUp10" },
				{
					"Iso",
					now.AddDays(-4.0).AddHours(-1.0).ToString("s")
				},
				{ "Type", 12 }
			});
			_rp.Add(new Hashtable
			{
				{ "Seq", 1 },
				{ "Desc", "Установщик модулей Windows" },
				{
					"Iso",
					now.AddDays(-4.0).AddHours(-5.0).ToString("s")
				},
				{ "Type", 12 }
			});
		}
		return _rp.ToArray();
	}

	private void Msg(string k, string t)
	{
		if (Ui != null)
		{
			Ui.Msg(k, t);
		}
	}

	private Hashtable H(string title, string cat, bool can, string group, string gtitle, DateTime t, string note = "", bool done = false)
	{
		Hashtable hashtable = new Hashtable();
		int num = ++_hid;
		hashtable.Add("Id", "h" + num);
		hashtable.Add("Time", t.ToString("s"));
		hashtable.Add("Title", title);
		hashtable.Add("Cat", cat);
		hashtable.Add("Group", group ?? "");
		hashtable.Add("GroupTitle", gtitle ?? "");
		hashtable.Add("Can", can);
		hashtable.Add("Done", done);
		hashtable.Add("Note", note);
		Hashtable hashtable2 = hashtable;
		_hist.Add(hashtable2);
		return hashtable2;
	}

	private void Seed()
	{
		if (!(Environment.GetEnvironmentVariable("WW_FAKE_EMPTY") == "1"))
		{
			DateTime t = DateTime.Now.AddDays(-1.0).Date.AddHours(19.0).AddMinutes(12.0);
			string group = "gseed";
			string gtitle = "Автонастройка «Оптимальная»";
			H("Создана точка восстановления", "restore", can: false, group, gtitle, t, "К ней можно откатить всю систему");
			H("Телеметрия выключена", "privacy", can: true, group, gtitle, t.AddSeconds(20.0));
			H("Copilot и Recall выключены", "privacy", can: true, group, gtitle, t.AddSeconds(25.0));
			H("Реклама и рекомендации выключены", "privacy", can: true, group, gtitle, t.AddSeconds(31.0));
			H("Удалены встроенные приложения: 14", "apps", can: false, group, gtitle, t.AddSeconds(80.0), "Вернуть можно из Microsoft Store");
			H("Тёмная тема", "ui", can: true, group, gtitle, t.AddSeconds(95.0));
			H("Обои WeavOS на рабочем столе и экране блокировки", "ui", can: true, group, gtitle, t.AddSeconds(99.0));
			H("Название системы — WeavOS", "ui", can: true, group, gtitle, t.AddSeconds(100.0));
			H("Установлен Firefox", "apps", can: false, group, gtitle, t.AddSeconds(160.0), "Удалить можно в «Параметры → Приложения»");
			H("Удалён Microsoft Edge", "apps", can: false, group, gtitle, t.AddSeconds(190.0), "WebView2 оставлен");
			DateTime now = DateTime.Now;
			H("DNS-сервер: Cloudflare", "net", can: true, null, null, now.AddMinutes(-42.0));
			H("Показывать расширения файлов", "ui", can: true, null, null, now.AddMinutes(-17.0), "", done: true);
			H("Фоновая запись игр (Game DVR) (отменено)", "game", can: true, null, null, now.AddMinutes(-6.0));
		}
	}

	private object HistOut()
	{
		object[] array = new object[_hist.Count];
		for (int i = 0; i < _hist.Count; i++)
		{
			array[i] = _hist[_hist.Count - 1 - i].Clone();
		}
		return array;
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
			Hashtable vars2 = new Hashtable { 
			{
				"v",
				vars["fa4"]
			} };
			Flip(V(vars, "fa3"), vars2);
			H(V(vars, "fa1"), V(vars, "fa5"), can: true, null, null, DateTime.Now);
			return null;
		}
		if (s.StartsWith("Invoke-FTracked"))
		{
			Flip(V(vars, "fa2"), vars);
			Match match = Regex.Match(V(vars, "fa2") ?? "", "Set-FUpdPause (\\d+)");
			if (match.Success)
			{
				int num = int.Parse(match.Groups[1].Value);
				_pauseUntil = ((num > 0) ? DateTime.Now.AddDays(num) : DateTime.MinValue);
				_pauseStart = DateTime.Now;
			}
			bool flag = vars != null && vars["fa5"] is bool && (bool)vars["fa5"];
			H(V(vars, "fa1"), (vars != null && vars.ContainsKey("fa4")) ? V(vars, "fa4") : "apps", !flag, null, null, DateTime.Now, V(vars, "fa7"));
			Msg("ok", "Готово: " + V(vars, "fa1"));
			return null;
		}
		if (s.Contains("Undo-FHistory"))
		{
			Thread.Sleep(400);
			foreach (Hashtable item in _hist)
			{
				if ((string)item["Id"] == V(vars, "fa1"))
				{
					item["Done"] = true;
					Msg("ok", "Откачено: " + item["Title"]);
				}
			}
			return true;
		}
		if (s.Contains("Undo-FHistGroup"))
		{
			int num2 = 0;
			foreach (Hashtable item2 in _hist)
			{
				if ((string)item2["Group"] == V(vars, "fa1") && (bool)item2["Can"] && !(bool)item2["Done"])
				{
					Thread.Sleep(150);
					item2["Done"] = true;
					num2++;
				}
			}
			Msg("ok", "Откачено изменений: " + num2);
			return num2;
		}
		if (s.Contains("Invoke-FLevel"))
		{
			string text = V(vars, "lvl");
			string text2 = V(vars, "br");
			string text3 = ((text == "light") ? "Базовая" : ((text == "gamer") ? "Максимальная" : "Оптимальная"));
			string[] array = new string[8]
			{
				"Точка восстановления",
				"Телеметрия",
				"Copilot и Recall",
				"Реклама и рекомендации",
				"Тёмная тема",
				"Обои WeavOS",
				"Название WeavOS",
				(text2 == "keep") ? "Готово" : "Установка браузера"
			};
			string group = "g" + ++_hid;
			int millisecondsTimeout = ((Environment.GetEnvironmentVariable("WW_FAKE_SLOW") == "1") ? 1500 : 350);
			for (int i = 0; i < array.Length; i++)
			{
				Msg("ok", "Автонастройка «" + text3 + "» [" + (i + 1) + "/" + array.Length + "]: " + array[i] + "…");
				Thread.Sleep(millisecondsTimeout);
				H(array[i], (i < 4) ? "privacy" : "ui", i > 0, group, "Автонастройка «" + text3 + "»", DateTime.Now);
			}
			_level = text;
			Msg("ok", "Автонастройка «" + text3 + "» выполнена");
			return true;
		}
		return null;
	}

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
				new Hashtable
				{
					{ "Id", "h1" },
					{ "Title", "Телеметрия и сбор данных" },
					{ "Cat", "privacy" }
				},
				new Hashtable
				{
					{ "Id", "h2" },
					{ "Title", "Реклама в меню «Пуск»" },
					{ "Cat", "privacy" }
				},
				new Hashtable
				{
					{ "Id", "h3" },
					{ "Title", "Bing в поиске" },
					{ "Cat", "privacy" }
				}
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
			List<string> list = new List<string>
			{
				"Точка восстановления", "Телеметрия и сбор данных", "Copilot и Recall", "Реклама и рекомендации", "Bing в поиске", "Экран блокировки без рекламы", "Запрет автошифрования BitLocker", "Тёмная тема", "Классическое меню", "Панель задач",
				"OneDrive", "Встроенный мусор", "Обновления Windows", "Игровой режим и HAGS", "Схема питания", "7-Zip", "Расширения файлов", "Временные файлы"
			};
			string obj = V(vars, "opts") ?? "";
			if (!obj.Contains("nowall"))
			{
				list.Add("Обои WeavOS");
			}
			if (!obj.Contains("noname"))
			{
				list.Add("Название WeavOS");
			}
			if (V(vars, "br") != "keep")
			{
				list.Add("Браузер вместо Edge");
			}
			return list.ToArray();
		}
		bool handled;
		object result = Extra(s, vars, out handled);
		if (handled)
		{
			return result;
		}
		switch (s)
		{
		case "Get-FRebootInfo":
			return new Hashtable
			{
				{
					"App",
					B("reboot", Environment.GetEnvironmentVariable("WW_FAKE_REBOOT") == "1")
				},
				{ "Sys", false }
			};
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
			string[][] array4 = new string[8][]
			{
				new string[5] { "Microsoft.Copilot", "Copilot", "ИИ-помощник Microsoft в отдельном окне", "1", "1" },
				new string[5] { "Clipchamp.Clipchamp", "Clipchamp", "Онлайн-видеоредактор с подпиской", "1", "1" },
				new string[5] { "MSTeams", "Microsoft Teams", "Мессенджер для работы", "1", "0" },
				new string[5] { "Microsoft.BingWeather", "Погода", "Прогноз погоды от MSN", "0", "1" },
				new string[5] { "Microsoft.OutlookForWindows", "Outlook (новый)", "Почта — веб-приложение Outlook", "0", "1" },
				new string[5] { "Microsoft.MicrosoftSolitaireCollection", "Solitaire", "Пасьянсы с рекламой", "1", "0" },
				new string[5] { "MicrosoftCorporationII.QuickAssist", "Быстрая помощь", "Удалённое управление ПК — частый инструмент мошенников", "1", "1" },
				new string[5] { "Microsoft.YourPhone", "Связь с телефоном", "Звонки, SMS и фото с Android/iPhone", "0", "1" }
			};
			object[] array5 = new object[array4.Length];
			for (int j = 0; j < array4.Length; j++)
			{
				array5[j] = new Hashtable
				{
					{
						"Id",
						array4[j][0]
					},
					{
						"Name",
						array4[j][1]
					},
					{
						"Desc",
						array4[j][2]
					},
					{
						"Rec",
						int.Parse(array4[j][3])
					},
					{
						"Installed",
						array4[j][4] == "1" && !B("app_" + array4[j][0], d: false)
					}
				};
			}
			return array5;
		}
		case "Get-FStartup":
			return new object[9]
			{
				new Hashtable
				{
					{ "Id", "HKCU|OneDrive" },
					{ "Name", "OneDrive" },
					{ "Path", "C:\\Users\\u\\AppData\\Local\\Microsoft\\OneDrive\\OneDrive.exe" },
					{ "Company", "Microsoft Corporation" },
					{ "Desc", "Microsoft OneDrive" },
					{ "Exists", true },
					{ "Command", "\"C:\\Users\\u\\AppData\\Local\\Microsoft\\OneDrive\\OneDrive.exe\" /background" },
					{ "Source", "Пользователь" },
					{ "Enabled", true }
				},
				new Hashtable
				{
					{ "Id", "HKCU|Spotify" },
					{ "Name", "Spotify" },
					{ "Path", "C:\\Users\\u\\AppData\\Roaming\\Spotify\\Spotify.exe" },
					{ "Company", "Spotify Ltd" },
					{ "Desc", "Spotify" },
					{ "Exists", true },
					{ "Command", "\"C:\\Users\\u\\AppData\\Roaming\\Spotify\\Spotify.exe\" --autostart --minimized" },
					{ "Source", "Пользователь" },
					{ "Enabled", true }
				},
				new Hashtable
				{
					{ "Id", "HKLM|EpicGamesLauncher" },
					{ "Name", "EpicGamesLauncher" },
					{ "Path", "C:\\Program Files (x86)\\Epic Games\\Launcher\\Portal\\Binaries\\Win64\\EpicGamesLauncher.exe" },
					{ "Company", "Epic Games, Inc." },
					{ "Desc", "Epic Games Launcher" },
					{ "Exists", true },
					{ "Command", "EpicGamesLauncher.exe -silent" },
					{ "Source", "Все пользователи" },
					{ "Enabled", false }
				},
				new Hashtable
				{
					{ "Id", "HKCU|OldUpdater" },
					{ "Name", "GameLauncherHelper" },
					{ "Path", "C:\\Program Files\\OldGame\\helper.exe" },
					{ "Company", "" },
					{ "Desc", "" },
					{ "Exists", false },
					{ "Command", "\"C:\\Program Files\\OldGame\\helper.exe\" /tray" },
					{ "Source", "Пользователь" },
					{ "Enabled", true }
				},
				new Hashtable
				{
					{ "Id", "HKCU|Discord" },
					{ "Path", "C:\\Users\\u\\AppData\\Local\\Discord\\app-1.0.9187\\Discord.exe" },
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
					{ "Path", "C:\\Program Files (x86)\\Steam\\steam.exe" },
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
					{ "Path", "C:\\Windows\\System32\\SecurityHealthSystray.exe" },
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
					{ "Path", "C:\\Windows\\System32\\RtkAudUService64.exe" },
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
					{ "Path", "C:\\Users\\u\\AppData\\Roaming\\Spotify\\Spotify.exe" },
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
				{
					"Used",
					2254857830.0 * (double)Math.Max(1, _rp.Count) / 4.0
				},
				{ "Max", 12884901888.0 },
				{ "Cap", 257698037760.0 },
				{ "Drive", "C:" },
				{
					"Auto",
					I("twi_rpauto", 0)
				}
			};
		case "Enable-FRestore":
			Msg("ok", "Защита системы включена для диска C:");
			return true;
		case "New-FRestorePointNow":
			Thread.Sleep(600);
			RestoreList();
			_rp.Insert(0, new Hashtable
			{
				{
					"Seq",
					(int)((Hashtable)_rp[0])["Seq"] + 1
				},
				{
					"Desc",
					"WinWeaver " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
				},
				{
					"Iso",
					DateTime.Now.ToString("s")
				},
				{ "Type", 12 }
			});
			Msg("ok", "Точка восстановления создана");
			return true;
		case "[void](Remove-FOldRestorePoints)":
		{
			int num3 = _rp.Count - 1;
			_rp.RemoveRange(1, Math.Max(0, num3));
			Msg("ok", "Удалено старых точек: " + num3 + ". Последняя осталась");
			return num3;
		}
		case "Get-FNetInfo":
			return new Hashtable
			{
				{ "Ok", true },
				{ "Phys", true },
				{
					"Vpn",
					Environment.GetEnvironmentVariable("WW_FAKE_VPN") == "1"
				},
				{ "VpnName", "happ-tun" },
				{ "VpnDesc", "Happ Tunnel" },
				{ "VpnIp", "172.19.0.1" },
				{ "VpnDns", "172.19.0.2" },
				{ "Name", "Wi-Fi" },
				{ "Desc", "Intel(R) Wi-Fi 6E AX211 160MHz" },
				{
					"Wifi",
					Environment.GetEnvironmentVariable("WW_FAKE_ETH") != "1"
				},
				{ "Speed", "1.2 Gbps" },
				{ "Ip", "192.168.1.34" },
				{ "Gw", "192.168.1.1" },
				{
					"Dns",
					(I("dns", 1) == 1) ? "1.1.1.1, 1.0.0.1" : ((I("dns", 1) == 4) ? "94.140.14.14, 94.140.15.15" : ((I("dns", 1) == 2) ? "8.8.8.8, 8.8.4.4" : ((I("dns", 1) == 3) ? "9.9.9.9, 149.112.112.112" : "192.168.1.1")))
				},
				{ "Ssid", "Keenetic-5G" },
				{ "Signal", 82 },
				{ "Mac", "3C-21-9C-11-22-33" },
				{ "Dhcp", "Enabled" }
			};
		case "Test-FDnsSpeed":
			Thread.Sleep(900);
			return new Hashtable
			{
				{
					"1",
					9 + _rnd.Next(6)
				},
				{
					"2",
					18 + _rnd.Next(8)
				},
				{
					"3",
					27 + _rnd.Next(10)
				},
				{
					"4",
					34 + _rnd.Next(12)
				}
			};
		case "Get-FLevelId":
			return _level;
		case "Get-FHistory":
			return HistOut();
		case "Get-FHistCount":
		{
			int num4 = 0;
			foreach (Hashtable item in _hist)
			{
				if (!(bool)item["Done"])
				{
					num4++;
				}
			}
			return num4;
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
				Hashtable hashtable = new Hashtable();
				string[] array = V(vars, "fa1").Split(',');
				foreach (string text in array)
				{
					int num = (text.StartsWith("192.") ? 2 : ((text == "1.1.1.1") ? 11 : 17));
					if (Environment.GetEnvironmentVariable("WW_FAKE_VPN") == "1" && !text.StartsWith("192."))
					{
						hashtable[text] = 0;
					}
					else
					{
						hashtable[text] = ((_rnd.Next(60) == 0) ? (-1) : (num + _rnd.Next(text.StartsWith("192.") ? 2 : 7)));
					}
				}
				return hashtable;
			}
			if (s.StartsWith("[void](Remove-FRestorePoint"))
			{
				RestoreList();
				int seq = ((vars != null && vars["seq"] is int) ? ((int)vars["seq"]) : (-1));
				_rp.RemoveAll((object o) => (int)((Hashtable)o)["Seq"] == seq);
				Msg("ok", "Точка №" + seq + " удалена");
				return true;
			}
			Match match = Regex.Match(s, "^Get-FTweakPlan '(\\w+)'$");
			if (match.Success)
			{
				if (!B("tw_" + match.Groups[1].Value, d: false))
				{
					return "#ex\nHKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location|Value|Allow|Deny\nHKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System|PublishUserActivities|-|0\nsvc|lfsvc|3|4";
				}
				return "#\nHKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy|TailoredExperiences|0|1\nsvc|DiagTrack|4|2";
			}
			if (s == "Get-FPlan $list")
			{
				string[] array2 = ((vars != null) ? (vars["list"] as string[]) : null);
				string text2 = "#";
				if (array2 != null)
				{
					string[] array = array2;
					for (int i = 0; i < array.Length; i++)
					{
						string[] array3 = array[i].Split('|');
						text2 = text2 + "\n" + array3[0] + "|" + array3[1] + "|" + ((array3[2] == "-") ? "1" : "-") + "|" + array3[2];
					}
				}
				return text2;
			}
			Match match2 = Regex.Match(s, "^Get-FTweak(Idx)? '(\\w+)'$");
			if (match2.Success)
			{
				string value = match2.Groups[2].Value;
				if (match2.Groups[1].Success && match2.Groups[1].Value == "Idx")
				{
					return I("twi_" + value, (!(value == "tcpauto") && !(value == "rpauto")) ? 1 : 0);
				}
				return B("tw_" + value, Array.IndexOf(new string[10] { "tailored", "feedback", "scoobe", "startdelay", "windowed", "dop2p", "widgets", "launchpc", "rss", "rpfreq" }, value) >= 0);
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
				int num2 = 0;
				if (B("tel", d: true))
				{
					num2++;
				}
				if (B("ai", d: true))
				{
					num2++;
				}
				if (B("ads", d: true))
				{
					num2++;
				}
				if (B("bing", d: false))
				{
					num2++;
				}
				return num2;
			}
			if (s.StartsWith("Set-") || s.StartsWith("Invoke-") || s.StartsWith("Remove-") || s.StartsWith("New-") || s.StartsWith("Install-") || s.StartsWith("Clear-") || s.StartsWith("Update-") || s.StartsWith("Find-") || s.StartsWith("Restore-"))
			{
				Flip(s, vars);
				return null;
			}
			return null;
		}
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
			Match match = Regex.Match(s, "Remove-FApp '([^']+)'");
			if (match.Success)
			{
				_b["app_" + match.Groups[1].Value] = true;
			}
			return;
		}
		Match match2 = Regex.Match(s, "Set-FTweak(Idx)? '(\\w+)'");
		if (match2.Success)
		{
			if (match2.Groups[1].Value == "Idx")
			{
				_i["twi_" + match2.Groups[2].Value] = ((!flag) ? 1 : num);
			}
			else
			{
				_b["tw_" + match2.Groups[2].Value] = value;
			}
		}
		else if (s.Contains("Set-Telemetry"))
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
			bool flag3 = (_b["bing"] = true);
			bool flag4 = flag3;
			flag3 = (b3["ads"] = flag4);
			bool flag6 = flag3;
			flag3 = (b2["ai"] = flag6);
			bool value2 = flag3;
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

	private static Hashtable Tile(string l, string v, string h, string t, float p = -1f)
	{
		return new Hashtable
		{
			{ "L", l },
			{ "V", v },
			{ "H", h },
			{ "T", t },
			{ "P", p }
		};
	}

	private object Extra(string s, Hashtable vars, out bool handled)
	{
		handled = true;
		switch (s)
		{
		case "Get-FUpdPause":
		{
			Hashtable hashtable3 = new Hashtable
			{
				{
					"Paused",
					_pauseUntil > DateTime.Now
				},
				{ "Max", 35 },
				{
					"Now",
					DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
				}
			};
			if (_pauseUntil > DateTime.Now)
			{
				hashtable3["Start"] = _pauseStart.ToString("yyyy-MM-ddTHH:mm:ss");
				hashtable3["Until"] = _pauseUntil.ToString("yyyy-MM-ddTHH:mm:ss");
			}
			return hashtable3;
		}
		case "Get-FDefInfo":
			Thread.Sleep(150);
			return new Hashtable
			{
				{ "Mode", 0 },
				{ "Rtp", true },
				{ "Tamper", true },
				{ "Cloud", true },
				{ "Pua", false },
				{ "SigAge", 0 },
				{ "ScanAge", 3 },
				{ "Note", "" },
				{ "Other", "" }
			};
		case "Get-FBLInfo":
		{
			Thread.Sleep(150);
			_blPct = Math.Max(0.0, _blPct - 1.7);
			Hashtable hashtable2 = new Hashtable();
			hashtable2.Add("Auto", true);
			hashtable2.Add("Vols", new object[2]
			{
				new Hashtable
				{
					{ "Drive", "C:" },
					{
						"Status",
						(_blPct > 0.0) ? "DecryptionInProgress" : "FullyDecrypted"
					},
					{ "Pct", _blPct },
					{ "Prot", "Off" },
					{ "Method", "XtsAes128" },
					{ "Type", "OperatingSystem" },
					{ "Size", 511101108224.0 }
				},
				new Hashtable
				{
					{ "Drive", "D:" },
					{ "Status", "FullyDecrypted" },
					{ "Pct", 0.0 },
					{ "Prot", "Off" },
					{ "Method", "" },
					{ "Type", "Data" },
					{ "Size", 999653638144.0 }
				}
			});
			return hashtable2;
		}
		case "Get-FPrivInfo":
		{
			Thread.Sleep(200);
			Hashtable hashtable2 = new Hashtable();
			hashtable2.Add("Title", "Приватность почти в порядке");
			hashtable2.Add("Sub", "Осталось выключить рекламу в Пуске и 2 параметра защиты");
			hashtable2.Add("Tone", "warn");
			hashtable2.Add("Score", 0.78);
			hashtable2.Add("Tiles", new object[4]
			{
				Tile("СЛЕЖКА", "8 из 9", "телеметрия выключена", "ok", 0.89f),
				Tile("РЕКЛАМА", "3 из 5", "реклама в Пуске включена", "warn", 0.6f),
				Tile("ЗАЩИТА", "Defender", "реальное время включено", "ok", 1f),
				Tile("ОБНОВЛЕНИЯ", "Авто", "пауза не стоит", "info")
			});
			return hashtable2;
		}
		case "Get-FSecInfo":
		{
			Thread.Sleep(200);
			Hashtable hashtable2 = new Hashtable();
			hashtable2.Add("Title", "Компьютер защищён");
			hashtable2.Add("Sub", "Microsoft Defender работает, брандмауэр включён во всех сетях");
			hashtable2.Add("Tone", "ok");
			hashtable2.Add("Tiles", new object[6]
			{
				Tile("АНТИВИРУС", "Defender", "базы: сегодня", "ok"),
				Tile("БРАНДМАУЭР", "Включён", "3 из 3 профилей", "ok"),
				Tile("SMARTSCREEN", "Предупреждать", "для приложений", "ok"),
				Tile("UAC", "Как обычно", "с затемнением", "ok"),
				Tile("ЗАГРУЗКА", "Secure Boot", "TPM 2.0", "ok"),
				Tile("BITLOCKER", "Выключен", "диск C: не зашифрован", "info")
			});
			return hashtable2;
		}
		case "Get-FUpdInfo":
		{
			Thread.Sleep(200);
			Hashtable hashtable2 = new Hashtable();
			hashtable2.Add("Title", "Обновления устанавливаются автоматически");
			hashtable2.Add("Sub", "Windows 11 25H2, последнее обновление 3 дня назад");
			hashtable2.Add("Tone", "info");
			hashtable2.Add("Tiles", new object[4]
			{
				Tile("ВЕРСИЯ", "25H2", "сборка 26200.6584", "info"),
				Tile("УСТАНОВКА", "Авто", "по политике Windows", "info"),
				Tile("ПАУЗА", "Нет", "можно на любой срок", "info"),
				Tile("ПОСЛЕДНЕЕ", "3 дня", "KB5065789", "ok")
			});
			return hashtable2;
		}
		case "Get-FDiskInfo":
			return new Hashtable
			{
				{ "Drive", "C:" },
				{ "Free", 277454887321.6 },
				{ "Size", 999653638144.0 }
			};
		case "Get-FCleanScan":
		{
			Thread.Sleep(500);
			string[] array3 = new string[10] { "temp", "wintemp", "wucache", "docache", "dumps", "logs", "thumbs", "shader", "browser", "recycle" };
			double[] array4 = new double[10] { 1454.0, 312.0, 2867.0, 640.0, 0.0, 96.0, 184.0, 1126.0, 870.0, 3481.0 };
			object[] array5 = new object[10];
			for (int j = 0; j < 10; j++)
			{
				array5[j] = new Hashtable
				{
					{
						"Id",
						array3[j]
					},
					{
						"Size",
						array4[j] * 1048576.0
					}
				};
			}
			return array5;
		}
		case "Get-FInstalled":
		{
			Thread.Sleep(300);
			string[][] obj = new string[14][]
			{
				new string[8] { "HKLM|Steam", "Steam", "Valve Corporation", "2.10.91.91", "1.2", "2024-03-14", "C:\\Program Files (x86)\\Steam", "app" },
				new string[8] { "HKLM|{7zip}", "7-Zip 24.08 (x64)", "Igor Pavlov", "24.08", "0.0055", "2024-09-02", "C:\\Program Files\\7-Zip", "app" },
				new string[8] { "HKCU|Discord", "Discord", "Discord Inc.", "1.0.9187", "0.41", "2025-01-20", "C:\\Users\\u\\AppData\\Local\\Discord", "app" },
				new string[8] { "HKLM|Chrome", "Google Chrome", "Google LLC", "138.0.7204.101", "0.62", "2025-06-30", "C:\\Program Files\\Google\\Chrome\\Application", "app" },
				new string[8] { "HKLM|CC", "Adobe Creative Cloud", "Adobe Inc.", "6.4.0.361", "1.9", "2024-11-08", "C:\\Program Files\\Adobe\\Adobe Creative Cloud", "app" },
				new string[8] { "HKLM|Cyberpunk", "Cyberpunk 2077", "CD PROJEKT RED", "2.21", "71.4", "2025-02-11", "D:\\Games\\Cyberpunk 2077", "app" },
				new string[8] { "HKLM|NV", "NVIDIA App", "NVIDIA Corporation", "11.0.4.526", "0.68", "2025-05-17", "C:\\Program Files\\NVIDIA Corporation\\NVIDIA App", "app" },
				new string[8] { "HKLM32|VCRedist", "Microsoft Visual C++ 2015-2022 Redistributable (x64)", "Microsoft Corporation", "14.42.34433.0", "0.022", "2025-03-01", "", "app" },
				new string[8] { "HKCU|Spotify", "Spotify", "Spotify AB", "1.2.70", "0.31", "2025-07-02", "C:\\Users\\u\\AppData\\Roaming\\Spotify", "app" },
				new string[8] { "HKLM|OBS", "OBS Studio", "OBS Project", "31.0.3", "0.58", "2025-04-12", "C:\\Program Files\\obs-studio", "app" },
				new string[8] { "APPX|Microsoft.WindowsCalculator", "Калькулятор Windows", "Microsoft Corporation", "11.2502.2.0", "0", "2025-05-09", "", "store" },
				new string[8] { "APPX|SpotifyAB.SpotifyMusic", "Spotify Music", "Spotify AB", "1.262.580.0", "0", "2025-06-21", "", "store" },
				new string[8] { "APPX|Microsoft.WindowsTerminal", "Терминал Windows", "Microsoft Corporation", "1.22.11141.0", "0", "2025-04-30", "", "store" },
				new string[8] { "APPX|5319275A.WhatsAppDesktop", "WhatsApp", "WhatsApp Inc.", "2.2525.3.0", "0", "2025-07-01", "", "store" }
			};
			List<object> list2 = new List<object>();
			string[][] array = obj;
			foreach (string[] array2 in array)
			{
				if (!_removed.Contains(array2[0]))
				{
					list2.Add(new Hashtable
					{
						{
							"Id",
							array2[0]
						},
						{
							"Name",
							array2[1]
						},
						{
							"Pub",
							array2[2]
						},
						{
							"Ver",
							array2[3]
						},
						{
							"Size",
							double.Parse(array2[4], CultureInfo.InvariantCulture) * 1073741824.0
						},
						{
							"Date",
							array2[5]
						},
						{
							"Loc",
							array2[6]
						},
						{ "Icon", "" },
						{
							"Kind",
							array2[7]
						},
						{ "Quiet", false }
					});
				}
			}
			return list2.ToArray();
		}
		case "Get-FStartup":
			if (_inStartup)
			{
				break;
			}
			_inStartup = true;
			try
			{
				List<object> list = new List<object>((object[])Route(s, vars));
				foreach (object item in list)
				{
					if (item is Hashtable hashtable && (string)hashtable["Id"] == "HKCU|Spotify")
					{
						hashtable["Delay"] = (_i.ContainsKey("delay_spotify") ? _i["delay_spotify"] : 60);
					}
				}
				list.Add(new Hashtable
				{
					{ "Id", "TASK|\\GoogleUpdaterTaskUser" },
					{ "Name", "Google Updater" },
					{ "Path", "C:\\Users\\u\\AppData\\Local\\Google\\GoogleUpdater\\updater.exe" },
					{ "Company", "Google LLC" },
					{ "Desc", "Google Updater" },
					{ "Exists", true },
					{ "Command", "updater.exe --wake" },
					{ "Source", "Планировщик задач" },
					{ "Enabled", true },
					{ "Delay", 0 }
				});
				return list.ToArray();
			}
			finally
			{
				_inStartup = false;
			}
		}
		if (s.StartsWith("$r = Uninstall-FProgram"))
		{
			Thread.Sleep(600);
			_removed.Add((string)vars["fa1"]);
			Msg("ok", "Программа удалена");
			return new Hashtable { { "Ok", true } };
		}
		if (s.StartsWith("Find-FLeftovers"))
		{
			Thread.Sleep(400);
			string text = (string)vars["fa1"];
			return new object[3]
			{
				new Hashtable
				{
					{
						"Path",
						"C:\\Users\\u\\AppData\\Roaming\\" + text
					},
					{ "Kind", "dir" },
					{ "Size", 50331648.0 }
				},
				new Hashtable
				{
					{
						"Path",
						"C:\\ProgramData\\" + text
					},
					{ "Kind", "dir" },
					{ "Size", 3145728.0 }
				},
				new Hashtable
				{
					{
						"Path",
						"HKCU:\\Software\\" + text
					},
					{ "Kind", "reg" },
					{ "Size", 0.0 }
				}
			};
		}
		if (s.StartsWith("$n = Remove-FLeftovers"))
		{
			return ((string)vars["fa1"]).Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
		}
		if (s.StartsWith("$b = Clear-FItems"))
		{
			Thread.Sleep(700);
			return 4939212390.4;
		}
		if (s.StartsWith("Invoke-FTracked") && vars != null && vars["fa2"] is string && ((string)vars["fa2"]).StartsWith("Set-FStartupDelay 'HKCU|Spotify'"))
		{
			string[] array6 = ((string)vars["fa2"]).Split(' ');
			_i["delay_spotify"] = int.Parse(array6[array6.Length - 1]);
		}
		handled = false;
		return null;
	}
}
