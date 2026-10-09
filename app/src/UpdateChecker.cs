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
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace WinWeaver
{
	// Asks GitHub for the latest release. Only reports it — nothing is downloaded or installed automatically.
	internal static class UpdateChecker
	{
		public class Result
		{
			public string Tag;

			public string Url;

			public bool Newer;

			public string Error;
		}

		public static Version Parse(string tag)
		{
			Match m = Regex.Match(tag ?? "", "(\\d+)\\.(\\d+)(?:\\.(\\d+))?");
			if (!m.Success)
			{
				return new Version(0, 0);
			}
			return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
		}

		public static Result Check()
		{
			Result r = new Result();
			string fake = Environment.GetEnvironmentVariable("WW_FAKE_UPDATE");
			if (!string.IsNullOrEmpty(fake))
			{
				r.Tag = fake;
				r.Url = Brand.Releases;
				r.Newer = Parse(fake) > Parse(Brand.Version);
				return r;
			}
			try
			{
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
				HttpWebRequest q = (HttpWebRequest)WebRequest.Create(Brand.LatestApi);
				q.UserAgent = Brand.Name + "/" + Brand.Version;
				q.Accept = "application/vnd.github+json";
				q.Timeout = 8000;
				q.ReadWriteTimeout = 8000;
				using (WebResponse resp = q.GetResponse())
				using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
				{
					string json = sr.ReadToEnd();
					Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
					Match u = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");
					if (!t.Success)
					{
						r.Error = "no tag";
						return r;
					}
					r.Tag = t.Groups[1].Value;
					// only a page of our own repository is ever opened
					r.Url = (u.Success && u.Groups[1].Value.StartsWith(Brand.GitHub + "/", StringComparison.OrdinalIgnoreCase)) ? u.Groups[1].Value : Brand.Releases;
					r.Newer = Parse(r.Tag) > Parse(Brand.Version);
				}
			}
			catch (Exception ex)
			{
				r.Error = ex.Message;
			}
			return r;
		}

		// background check; done is called on a worker thread
		public static void CheckAsync(Action<Result> done)
		{
			Thread t = new Thread((ThreadStart)delegate
			{
				Result r = Check();
				try
				{
					done(r);
				}
				catch
				{
				}
			});
			t.IsBackground = true;
			t.Start();
		}

		// automatic check: at most once a day, not for a version the user chose to skip
		public static bool Due()
		{
			if (!Prefs.UpdateCheck)
			{
				return false;
			}
			DateTime last;
			return !DateTime.TryParse(Prefs.LastUpdateCheck, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out last) || (DateTime.UtcNow - last).TotalHours >= 24.0 || Environment.GetEnvironmentVariable("WW_FAKE_UPDATE") != null;
		}
	}
}
