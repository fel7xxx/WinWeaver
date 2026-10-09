// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using Microsoft.Win32;

namespace WinWeaver
{
	// per-user preferences in HKCU\Software\WinWeaver
	internal static class Prefs
	{
		private static string Read(string name)
		{
			try
			{
				using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Brand.RegKey))
				{
					return (k == null) ? null : (k.GetValue(name) as string);
				}
			}
			catch
			{
				return null;
			}
		}

		private static void Write(string name, string value)
		{
			try
			{
				using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Brand.RegKey))
				{
					k.SetValue(name, value ?? "", RegistryValueKind.String);
				}
			}
			catch
			{
			}
		}

		// start with Windows: "yes" / "no" / null (not asked yet)
		public static string AutoChoice
		{
			get { return Read("AutostartChoice"); }
			set { Write("AutostartChoice", value); }
		}

		// network screen: mask local addresses / SSID (for screenshots and streams)
		public static bool HideAddr
		{
			get { return Read("HideAddresses") == "1"; }
			set { Write("HideAddresses", value ? "1" : "0"); }
		}

		// settings: turn interface animations off
		public static bool NoAnim
		{
			get { return Read("NoAnimations") == "1"; }
			set { Write("NoAnimations", value ? "1" : "0"); }
		}

		// browser chosen last time in the auto-tune dialog
		public static string LastBrowser
		{
			get { return Read("LastBrowser"); }
			set { Write("LastBrowser", value); }
		}

		// WeavOS wallpaper variant chosen last time
		public static int LastWallpaper
		{
			get { int v; return int.TryParse(Read("LastWallpaper"), out v) ? v : 0; }
			set { Write("LastWallpaper", value.ToString()); }
		}

		// expert mode: shows settings that weaken protection or can break apps
		public static bool Expert
		{
			get { return Read("ExpertMode") == "1"; }
			set { Write("ExpertMode", value ? "1" : "0"); }
		}

		// interface language: "" = like Windows, "ru", "en"
		public static string Lang
		{
			get { return Read("Language") ?? ""; }
			set { Write("Language", value); }
		}

		// check GitHub for a new version on start (only a notification, nothing is downloaded)
		public static bool UpdateCheck
		{
			get { return Read("UpdateCheck") != "0"; }
			set { Write("UpdateCheck", value ? "1" : "0"); }
		}

		public static string LastUpdateCheck
		{
			get { return Read("LastUpdateCheck"); }
			set { Write("LastUpdateCheck", value); }
		}

		// a version the user chose to skip in the update notice
		public static string SkipVersion
		{
			get { return Read("SkipVersion"); }
			set { Write("SkipVersion", value); }
		}

		// first-run welcome finished
		public static bool Welcomed
		{
			get { return Read("Welcomed") == "1"; }
			set { Write("Welcomed", value ? "1" : "0"); }
		}

		// "portable" = the user chose not to install into Program Files
		public static string InstallChoice
		{
			get { return Read("InstallChoice"); }
			set { Write("InstallChoice", value); }
		}
	}
}
