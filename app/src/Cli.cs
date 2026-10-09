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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinWeaver
{
	// Command line: WinWeaver.exe /apply optimal /browser firefox  (see /help)
	internal static class Cli
	{
		[DllImport("kernel32.dll")]
		private static extern bool AttachConsole(int pid);

		[DllImport("kernel32.dll")]
		private static extern bool AllocConsole();

		private static bool _own;

		private static void OpenConsole()
		{
			if (!Win.IsWindows)
			{
				return;
			}
			try
			{
				if (!AttachConsole(-1))
				{
					_own = AllocConsole();
				}
				StreamWriter w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
				Console.SetOut(w);
				Console.OutputEncoding = Encoding.UTF8;
			}
			catch
			{
			}
		}

		private static string Arg(string[] args, string name)
		{
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return args[i + 1];
				}
			}
			return null;
		}

		private static bool Has(string[] args, string name)
		{
			return Array.Exists(args, (string a) => a.Equals(name, StringComparison.OrdinalIgnoreCase));
		}

		public static string Help => Brand.Name + " " + Brand.Version + " — " + L.T("настройка Windows 11") + "\n\n" +
			"  WinWeaver.exe /apply <light|optimal|gamer> [/browser <keep|firefox|chrome|brave|yandex|operagx>]\n" +
			"                [/opts \"7zip,vc,ext,temp,drv\"] [/wallpaper 1-" + Walls.Count + "] [/nowall] [/noname]\n" +
			"                [/defender <asis|quiet>] [/restart]\n" +
			"  WinWeaver.exe /uninstall [/quiet] [/undo]\n" +
			"  WinWeaver.exe /version\n\n" +
			L.T("Каждый шаг записывается в «Историю» — его можно отменить в программе. Журнал:") + " " + Brand.LogPath + "\n";

		public static int Run(string[] args, bool fake)
		{
			OpenConsole();
			int code = 0;
			try
			{
				string cmd = args[0].ToLowerInvariant();
				if (cmd == "/version")
				{
					Console.WriteLine(Brand.Name + " " + Brand.Version);
				}
				else if (cmd == "/apply")
				{
					code = Apply(args, fake);
				}
				else if (cmd == "/uninstall")
				{
					code = Uninstall(args, fake);
				}
				else
				{
					Console.WriteLine(Help);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("ERROR: " + ex.Message);
				Program.Log("CLI ERROR " + ex);
				code = 1;
			}
			if (_own && !Has(args, "/quiet"))
			{
				Console.WriteLine();
				Console.WriteLine(L.T("Нажми Enter, чтобы закрыть окно"));
				try
				{
					Console.ReadLine();
				}
				catch
				{
				}
			}
			return code;
		}

		private static IEngine Engine(bool fake, string mode)
		{
			EngineUI ui = new EngineUI(delegate (string kind, string text)
			{
				Console.WriteLine(((kind == "err") ? "[!] " : ((kind == "warn") ? "[~] " : "    ")) + L.T(text ?? ""));
			}, (string title, string body, string yes, string no) => false);
			IEngine e = fake ? (IEngine)new FakeEngine() : Program.CreatePsEngine(ui, mode);
			e.Start();
			return e;
		}

		private static int Apply(string[] args, bool fake)
		{
			string lvl = (args.Length > 1) ? args[1].ToLowerInvariant() : "";
			if (lvl != "light" && lvl != "optimal" && lvl != "gamer")
			{
				Console.WriteLine(Help);
				return 2;
			}
			string br = (Arg(args, "/browser") ?? ((lvl == "light") ? "keep" : "firefox")).ToLowerInvariant();
			string op = Arg(args, "/opts") ?? ((lvl == "light") ? "7zip" : ((lvl == "gamer") ? "7zip,vc,ext,temp,drv" : "7zip,ext,temp"));
			List<string> o = new List<string>();
			foreach (string x in op.Split(','))
			{
				string t = x.Trim().ToLowerInvariant();
				if (t == "7zip" || t == "vc" || t == "ext" || t == "temp" || t == "drv")
				{
					o.Add(t);
				}
			}
			if (Has(args, "/nowall"))
			{
				o.Add("nowall");
			}
			if (Has(args, "/noname"))
			{
				o.Add("noname");
			}
			int wp = 0;
			string ws = Arg(args, "/wallpaper");
			if (ws != null && int.TryParse(ws, out int wn))
			{
				wp = Walls.Clamp(wn - 1);
			}
			string def = (Arg(args, "/defender") ?? "asis").ToLowerInvariant();
			string opts = string.Join(",", o.ToArray()) + ";def=" + ((def == "quiet") ? "1" : "0") + ";wp=" + wp;
			Console.WriteLine(L.T("Автонастройка") + ": " + lvl + ", " + br + "  (" + opts + ")");
			IEngine e = Engine(fake, "/apply");
			try
			{
				object r = e.Eval("$r = Invoke-FLevel $lvl $br $opts; [bool](@($r) | Select-Object -Last 1)", new Hashtable
				{
					{ "lvl", lvl },
					{ "br", br },
					{ "opts", opts }
				});
				bool ok = e.As(r, fallback: false) || fake;
				if (ok && Has(args, "/restart") && !fake)
				{
					Shell.Open("shutdown.exe", "/r /t 30 /c \"WinWeaver\"");
				}
				return ok ? 0 : 1;
			}
			finally
			{
				e.Stop();
			}
		}

		private static int Uninstall(string[] args, bool fake)
		{
			IEngine e = Engine(fake, "/uninstall");
			try
			{
				e.Eval("[void](Uninstall-FWinWeaver $fa1)", new Hashtable { { "fa1", Has(args, "/undo") } });
			}
			finally
			{
				e.Stop();
			}
			if (!fake)
			{
				Installer.RemoveFilesAfterExit();
			}
			return 0;
		}
	}
}
