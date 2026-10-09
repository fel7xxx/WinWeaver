// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.IO;

namespace WinWeaver
{
	// Program name, version, author, links and all paths in one place
	internal static class Brand
	{
		public const string Name = "WinWeaver";

		public const string Short = "WinWeaver";

		// the system look applied by the auto-tune (wallpaper text, OEM name)
		public const string OsName = "WeavOS";

		public const string Version = "1.0.0";

		public const string Folder = "WinWeaver";

		public const string ExeName = "WinWeaver.exe";

		public const string Developer = "fel7xxx";

		public const string Repo = "fel7xxx/WinWeaver";

		public const string GitHub = "https://github.com/" + Repo;

		public const string Releases = GitHub + "/releases";

		public const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";

		public const string Issues = GitHub + "/issues";

		public const string NewBug = GitHub + "/issues/new?template=bug_report.yml";

		public const string NewIdea = GitHub + "/issues/new?template=feature_request.yml";

		public const string Discussions = GitHub + "/discussions";

		// tips
		public const string Boosty = "https://boosty.to/fel7xxx/donate";

		public const string Ton = "UQB_35tg7SFtMrJVeU5W6MoB2shypmmpBcJUJKG5WfvCuOMc";

		public const string Btc = "bc1qpn0wasg3mkgdhepkpy5qjg9rmw793f85jq8f5m";

		public const string RegKey = "Software\\WinWeaver";

		public const string UninstallKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\WinWeaver";

		public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Folder);

		public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Folder);

		public static string LogPath => Path.Combine(DataDir, "tweaker.log");

		public static string InstalledExe => Path.Combine(InstallDir, ExeName);
	}
}
