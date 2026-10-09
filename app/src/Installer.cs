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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace WinWeaver
{
	// Installation into Program Files (only with the user's consent on the first start),
	// the "Apps → Installed apps" entry, the Start menu shortcut and the removal of the program files.
	internal static class Installer
	{
		public static string RunningExe => Assembly.GetExecutingAssembly().Location;

		public static bool IsInstalled => File.Exists(Brand.InstalledExe);

		public static bool RunningInstalled => SamePath(RunningExe, Brand.InstalledExe);

		private static bool SamePath(string a, string b)
		{
			try
			{
				return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
			}
			catch
			{
				return false;
			}
		}

		public static Version FileVersion(string path)
		{
			try
			{
				FileVersionInfo v = FileVersionInfo.GetVersionInfo(path);
				return new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart, v.FilePrivatePart);
			}
			catch
			{
				return new Version(0, 0);
			}
		}

		// the installed copy is older than the running exe (the user started a newer download)
		public static bool InstalledIsOlder => IsInstalled && !RunningInstalled && FileVersion(Brand.InstalledExe) < FileVersion(RunningExe);

		// copies the running exe into Program Files and registers it; returns the installed path
		public static string Install()
		{
			string dir = Brand.InstallDir;
			Directory.CreateDirectory(dir);
			string dst = Brand.InstalledExe;
			if (!RunningInstalled)
			{
				string tmp = dst + ".new";
				File.Copy(RunningExe, tmp, overwrite: true);
				if (File.Exists(dst))
				{
					File.Delete(dst);
				}
				File.Move(tmp, dst);
			}
			Register(dst);
			try
			{
				StartMenuShortcut(dst);
			}
			catch (Exception ex)
			{
				Program.Log("SHORTCUT ERR " + ex.Message);
			}
			Program.Log("INSTALLED " + dst + " v" + Brand.Version);
			return dst;
		}

		private static void Register(string exe)
		{
			using (RegistryKey k = Registry.LocalMachine.CreateSubKey(Brand.UninstallKey))
			{
				k.SetValue("DisplayName", Brand.Name);
				k.SetValue("DisplayVersion", Brand.Version);
				k.SetValue("Publisher", Brand.Developer);
				k.SetValue("DisplayIcon", exe + ",0");
				k.SetValue("InstallLocation", Path.GetDirectoryName(exe));
				k.SetValue("UninstallString", "\"" + exe + "\" /uninstall");
				k.SetValue("QuietUninstallString", "\"" + exe + "\" /uninstall /quiet");
				k.SetValue("URLInfoAbout", Brand.GitHub);
				k.SetValue("HelpLink", Brand.Issues);
				k.SetValue("URLUpdateInfo", Brand.Releases);
				k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
				k.SetValue("NoModify", 1, RegistryValueKind.DWord);
				k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
				try
				{
					k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
				}
				catch
				{
				}
			}
		}

		public static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Brand.Name + ".lnk");

		private static void StartMenuShortcut(string exe)
		{
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			if (t == null)
			{
				return;
			}
			object shell = Activator.CreateInstance(t);
			object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[1] { StartMenuLink });
			Type lt = lnk.GetType();
			lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[1] { exe });
			lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[1] { exe + ",0" });
			lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[1] { Brand.Name + " — настройка Windows 11" });
			lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
		}

		// starts the installed copy and returns true (the caller exits)
		public static bool Relaunch(string exe, string args = "")
		{
			try
			{
				Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
				return true;
			}
			catch (Exception ex)
			{
				Program.Log("RELAUNCH ERR " + ex.Message);
				return false;
			}
		}

		// after the engine has cleaned the system: remove shortcuts and the ARP entry, then let a short
		// cmd script delete the program folder and the data folder once this process has exited
		public static void RemoveFilesAfterExit()
		{
			try
			{
				File.Delete(StartMenuLink);
			}
			catch
			{
			}
			try
			{
				Registry.LocalMachine.DeleteSubKeyTree(Brand.UninstallKey, throwOnMissingSubKey: false);
			}
			catch
			{
			}
			string data = Brand.DataDir;
			string inst = Brand.InstallDir;
			string script = Path.Combine(Path.GetTempPath(), "winweaver-remove-" + Guid.NewGuid().ToString("N") + ".cmd");
			string body = "@echo off\r\n" +
				"ping -n 4 127.0.0.1 >nul\r\n" +
				"rd /s /q \"" + data + "\" 2>nul\r\n" +
				(Directory.Exists(inst) ? ("rd /s /q \"" + inst + "\" 2>nul\r\n") : "") +
				"del /f /q \"%~f0\" 2>nul\r\n";
			File.WriteAllText(script, body);
			Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
		}
	}
}
