// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Diagnostics;

namespace WinWeaver
{
	internal static class Shell
	{
		public static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

		public static void Open(string file, string args = null)
		{
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(file)
				{
					UseShellExecute = true
				};
				if (args != null)
				{
					processStartInfo.Arguments = args;
				}
				Process.Start(processStartInfo);
			}
			catch (Exception ex)
			{
				Program.Main0.Status("err", "Не удалось открыть " + file + ": " + ex.Message);
			}
		}

		public static void Console(string command)
		{
			Open("powershell.exe", "-NoProfile -NoExit -ExecutionPolicy Bypass -Command \"" + command + "\"");
		}
	}
}
