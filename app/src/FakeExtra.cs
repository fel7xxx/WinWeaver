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
using System.Threading;

namespace WinWeaver
{
	// fake data for the 1.1 pages (screenshots / UI tests without Windows)
	internal partial class FakeEngine
	{
		private bool _inStartup;

		private readonly HashSet<string> _removed = new HashSet<string>();

		private static Hashtable Tile(string l, string v, string h, string t, float p = -1f)
		{
			return new Hashtable { { "L", l }, { "V", v }, { "H", h }, { "T", t }, { "P", p } };
		}

		private DateTime _pauseUntil = DateTime.Now.AddDays(9);

		private DateTime _pauseStart = DateTime.Now.AddDays(-5);

		private double _blPct = Environment.GetEnvironmentVariable("WW_FAKE_NOBL") == "1" ? 0.0 : 63.0;

		private object Extra(string s, Hashtable vars, out bool handled)
		{
			handled = true;
			switch (s)
			{
			case "Get-FUpdPause":
				{
					Hashtable r = new Hashtable { { "Paused", _pauseUntil > DateTime.Now }, { "Max", 35 }, { "Now", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") } };
					if (_pauseUntil > DateTime.Now)
					{
						r["Start"] = _pauseStart.ToString("yyyy-MM-ddTHH:mm:ss");
						r["Until"] = _pauseUntil.ToString("yyyy-MM-ddTHH:mm:ss");
					}
					return r;
				}
			case "Get-FDefInfo":
				Thread.Sleep(150);
				return new Hashtable { { "Mode", 0 }, { "Rtp", true }, { "Tamper", true }, { "Cloud", true }, { "Pua", false }, { "SigAge", 0 }, { "ScanAge", 3 }, { "Note", "" }, { "Other", "" } };
			case "Get-FBLInfo":
				Thread.Sleep(150);
				_blPct = Math.Max(0.0, _blPct - 1.7);
				return new Hashtable
				{
					{ "Auto", true },
					{ "Vols", new object[2]
						{
							new Hashtable { { "Drive", "C:" }, { "Status", (_blPct > 0.0) ? "DecryptionInProgress" : "FullyDecrypted" }, { "Pct", _blPct }, { "Prot", "Off" }, { "Method", "XtsAes128" }, { "Type", "OperatingSystem" }, { "Size", 476.0 * 1073741824.0 } },
							new Hashtable { { "Drive", "D:" }, { "Status", "FullyDecrypted" }, { "Pct", 0.0 }, { "Prot", "Off" }, { "Method", "" }, { "Type", "Data" }, { "Size", 931.0 * 1073741824.0 } }
						}
					}
				};
			case "Get-FPrivInfo":
				Thread.Sleep(200);
				return new Hashtable
				{
					{ "Title", "Приватность почти в порядке" }, { "Sub", "Осталось выключить рекламу в Пуске и 2 параметра защиты" }, { "Tone", "warn" }, { "Score", 0.78 },
					{ "Tiles", new object[4] { Tile("СЛЕЖКА", "8 из 9", "телеметрия выключена", "ok", 0.89f), Tile("РЕКЛАМА", "3 из 5", "реклама в Пуске включена", "warn", 0.6f), Tile("ЗАЩИТА", "Defender", "реальное время включено", "ok", 1f), Tile("ОБНОВЛЕНИЯ", "Авто", "пауза не стоит", "info", -1f) } }
				};
			case "Get-FSecInfo":
				Thread.Sleep(200);
				return new Hashtable
				{
					{ "Title", "Компьютер защищён" }, { "Sub", "Microsoft Defender работает, брандмауэр включён во всех сетях" }, { "Tone", "ok" },
					{ "Tiles", new object[6] { Tile("АНТИВИРУС", "Defender", "базы: сегодня", "ok"), Tile("БРАНДМАУЭР", "Включён", "3 из 3 профилей", "ok"), Tile("SMARTSCREEN", "Предупреждать", "для приложений", "ok"), Tile("UAC", "Как обычно", "с затемнением", "ok"), Tile("ЗАГРУЗКА", "Secure Boot", "TPM 2.0", "ok"), Tile("BITLOCKER", "Выключен", "диск C: не зашифрован", "info") } }
				};
			case "Get-FUpdInfo":
				Thread.Sleep(200);
				return new Hashtable
				{
					{ "Title", "Обновления устанавливаются автоматически" }, { "Sub", "Windows 11 25H2, последнее обновление 3 дня назад" }, { "Tone", "info" },
					{ "Tiles", new object[4] { Tile("ВЕРСИЯ", "25H2", "сборка 26200.6584", "info"), Tile("УСТАНОВКА", "Авто", "по политике Windows", "info"), Tile("ПАУЗА", "Нет", "можно на любой срок", "info"), Tile("ПОСЛЕДНЕЕ", "3 дня", "KB5065789", "ok") } }
				};
			case "Get-FDiskInfo":
				return new Hashtable { { "Drive", "C:" }, { "Free", 258.4 * 1073741824.0 }, { "Size", 931.0 * 1073741824.0 } };
			case "Get-FCleanScan":
				Thread.Sleep(500);
				{
					string[] ids = new string[10] { "temp", "wintemp", "wucache", "docache", "dumps", "logs", "thumbs", "shader", "browser", "recycle" };
					double[] mb = new double[10] { 1454, 312, 2867, 640, 0, 96, 184, 1126, 870, 3481 };
					object[] r = new object[10];
					for (int i = 0; i < 10; i++)
					{
						r[i] = new Hashtable { { "Id", ids[i] }, { "Size", mb[i] * 1048576.0 } };
					}
					return r;
				}
			case "Get-FInstalled":
			{
				Thread.Sleep(300);
				string[][] a = new string[][]
				{
					new string[] { "HKLM|Steam", "Steam", "Valve Corporation", "2.10.91.91", "1.2", "2024-03-14", "C:\\Program Files (x86)\\Steam", "app" },
					new string[] { "HKLM|{7zip}", "7-Zip 24.08 (x64)", "Igor Pavlov", "24.08", "0.0055", "2024-09-02", "C:\\Program Files\\7-Zip", "app" },
					new string[] { "HKCU|Discord", "Discord", "Discord Inc.", "1.0.9187", "0.41", "2025-01-20", "C:\\Users\\u\\AppData\\Local\\Discord", "app" },
					new string[] { "HKLM|Chrome", "Google Chrome", "Google LLC", "138.0.7204.101", "0.62", "2025-06-30", "C:\\Program Files\\Google\\Chrome\\Application", "app" },
					new string[] { "HKLM|CC", "Adobe Creative Cloud", "Adobe Inc.", "6.4.0.361", "1.9", "2024-11-08", "C:\\Program Files\\Adobe\\Adobe Creative Cloud", "app" },
					new string[] { "HKLM|Cyberpunk", "Cyberpunk 2077", "CD PROJEKT RED", "2.21", "71.4", "2025-02-11", "D:\\Games\\Cyberpunk 2077", "app" },
					new string[] { "HKLM|NV", "NVIDIA App", "NVIDIA Corporation", "11.0.4.526", "0.68", "2025-05-17", "C:\\Program Files\\NVIDIA Corporation\\NVIDIA App", "app" },
					new string[] { "HKLM32|VCRedist", "Microsoft Visual C++ 2015-2022 Redistributable (x64)", "Microsoft Corporation", "14.42.34433.0", "0.022", "2025-03-01", "", "app" },
					new string[] { "HKCU|Spotify", "Spotify", "Spotify AB", "1.2.70", "0.31", "2025-07-02", "C:\\Users\\u\\AppData\\Roaming\\Spotify", "app" },
					new string[] { "HKLM|OBS", "OBS Studio", "OBS Project", "31.0.3", "0.58", "2025-04-12", "C:\\Program Files\\obs-studio", "app" },
					new string[] { "APPX|Microsoft.WindowsCalculator", "Калькулятор Windows", "Microsoft Corporation", "11.2502.2.0", "0", "2025-05-09", "", "store" },
					new string[] { "APPX|SpotifyAB.SpotifyMusic", "Spotify Music", "Spotify AB", "1.262.580.0", "0", "2025-06-21", "", "store" },
					new string[] { "APPX|Microsoft.WindowsTerminal", "Терминал Windows", "Microsoft Corporation", "1.22.11141.0", "0", "2025-04-30", "", "store" },
					new string[] { "APPX|5319275A.WhatsAppDesktop", "WhatsApp", "WhatsApp Inc.", "2.2525.3.0", "0", "2025-07-01", "", "store" }
				};
				List<object> r = new List<object>();
				foreach (string[] x in a)
				{
					if (_removed.Contains(x[0]))
					{
						continue;
					}
					r.Add(new Hashtable { { "Id", x[0] }, { "Name", x[1] }, { "Pub", x[2] }, { "Ver", x[3] }, { "Size", double.Parse(x[4], System.Globalization.CultureInfo.InvariantCulture) * 1073741824.0 }, { "Date", x[5] }, { "Loc", x[6] }, { "Icon", "" }, { "Kind", x[7] }, { "Quiet", false } });
				}
				return r.ToArray();
			}
			case "Get-FStartup":
				if (!_inStartup)
				{
					_inStartup = true;
					try
					{
						List<object> list = new List<object>((object[])Route(s, vars));
						foreach (object o in list)
						{
							Hashtable h = o as Hashtable;
							if (h != null && (string)h["Id"] == "HKCU|Spotify")
							{
								h["Delay"] = _i.ContainsKey("delay_spotify") ? _i["delay_spotify"] : 60;
							}
						}
						list.Add(new Hashtable
						{
							{ "Id", "TASK|\\GoogleUpdaterTaskUser" }, { "Name", "Google Updater" }, { "Path", @"C:\Users\u\AppData\Local\Google\GoogleUpdater\updater.exe" }, { "Company", "Google LLC" }, { "Desc", "Google Updater" }, { "Exists", true },
							{ "Command", "updater.exe --wake" }, { "Source", "Планировщик задач" }, { "Enabled", true }, { "Delay", 0 }
						});
						return list.ToArray();
					}
					finally
					{
						_inStartup = false;
					}
				}
				break;
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
				string n = (string)vars["fa1"];
				return new object[3]
				{
					new Hashtable { { "Path", "C:\\Users\\u\\AppData\\Roaming\\" + n }, { "Kind", "dir" }, { "Size", 48.0 * 1048576.0 } },
					new Hashtable { { "Path", "C:\\ProgramData\\" + n }, { "Kind", "dir" }, { "Size", 3.0 * 1048576.0 } },
					new Hashtable { { "Path", "HKCU:\\Software\\" + n }, { "Kind", "reg" }, { "Size", 0.0 } }
				};
			}
			if (s.StartsWith("$n = Remove-FLeftovers"))
			{
				return ((string)vars["fa1"]).Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
			}
			if (s.StartsWith("$b = Clear-FItems"))
			{
				Thread.Sleep(700);
				return 4.6 * 1073741824.0;
			}
			if (s.StartsWith("Invoke-FTracked") && vars != null && vars["fa2"] is string && ((string)vars["fa2"]).StartsWith("Set-FStartupDelay 'HKCU|Spotify'"))
			{
				string[] parts = ((string)vars["fa2"]).Split(' ');
				_i["delay_spotify"] = int.Parse(parts[parts.Length - 1]);
			}
			handled = false;
			return null;
		}
	}
}
