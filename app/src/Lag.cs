// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WinWeaver
{
	// Freeze log: notices every moment the UI thread was stuck for more than 70 ms and writes what ran
	// during it (named sections with their times; the rest is Windows painting/layout) to
	// "WinWeaver_lag.txt" on the Desktop. Used to find the real cause of stutters on the user's PC.
	internal static class Lag
	{
		private static readonly Stopwatch _clock = Stopwatch.StartNew();

		private static long _lastBeat;

		private static readonly List<string> _ran = new List<string>();

		private static readonly StringBuilder _buf = new StringBuilder();

		private static string _path;

		private static Timer _beat;

		private static Timer _flush;

		public static bool On;

		public static string Context = "";

		public static void Start()
		{
			if (On)
			{
				return;
			}
			On = true;
			try
			{
				_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WinWeaver_lag.txt");
				File.WriteAllText(_path, "WinWeaver — журнал подвисаний (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\r\n" + Environment.OSVersion + ", ядер: " + Environment.ProcessorCount + "\r\nФормат: время, длительность фриза, что выполнялось (мс). «прочее» = отрисовка/раскладка окна Windows.\r\n\r\n", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("LAG " + ex.Message);
				_path = null;
			}
			Console.Error.WriteLine("LAG path " + _path);
			_lastBeat = _clock.ElapsedMilliseconds;
			_beat = new Timer { Interval = 10 };
			_beat.Tick += delegate
			{
				Beat();
			};
			_beat.Start();
			_flush = new Timer { Interval = 2000 };
			_flush.Tick += delegate
			{
				Flush();
			};
			_flush.Start();
		}

		private static void Beat()
		{
			long now = _clock.ElapsedMilliseconds;
			long gap = now - _lastBeat;
			_lastBeat = now;
			int g2 = GC.CollectionCount(2);
			int g0 = GC.CollectionCount(0);
			bool gc2 = g2 != _g2;
			int gc0 = g0 - _g0;
			_g2 = g2;
			_g0 = g0;
			if (gap > 70)
			{
				long known = _top;
				_buf.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("  ФРИЗ ").Append(gap).Append(" мс  [").Append(Context).Append("]\r\n");
				foreach (string r in Merge(_ran))
				{
					_buf.Append("     ").Append(r).Append("\r\n");
				}
				_buf.Append("     прочее=").Append(Math.Max(0, gap - known)).Append("ms").Append(gc2 ? "  (была полная сборка мусора)" : (gc0 > 0 ? ("  (сборок мусора: " + gc0 + ")") : "")).Append("\r\n");
			}
			_ran.Clear();
			_top = 0;
		}

		// identical sections in one freeze are summed up: "Apply InstalledView x12=340ms"
		private static List<string> Merge(List<string> ran)
		{
			Dictionary<string, long> sum = new Dictionary<string, long>();
			Dictionary<string, int> cnt = new Dictionary<string, int>();
			List<string> order = new List<string>();
			foreach (string r in ran)
			{
				int i = r.LastIndexOf('=');
				string k = (i > 0) ? r.Substring(0, i) : r;
				long.TryParse((i > 0) ? r.Substring(i + 1).Replace("ms", "") : "0", out long v);
				if (!sum.ContainsKey(k))
				{
					sum[k] = 0;
					cnt[k] = 0;
					order.Add(k);
				}
				sum[k] += v;
				cnt[k]++;
			}

			List<string> res = new List<string>();
			foreach (string k in order)
			{
				if (sum[k] >= 3 || res.Count < 6)
				{
					res.Add(k + ((cnt[k] > 1) ? (" x" + cnt[k]) : "") + "=" + sum[k] + "ms");
				}
			}
			return res;
		}

		private static void Flush()
		{
			if (_buf.Length == 0 || _path == null)
			{
				return;
			}
			try
			{
				File.AppendAllText(_path, _buf.ToString(), Encoding.UTF8);
			}
			catch
			{
			}
			_buf.Clear();
		}

		public static void Stop()
		{
			Flush();
		}

		// time a piece of UI-thread work
		private static int _depth;

		private static int _g2;

		private static int _g0;

		private static long _top;

		public static long T0()
		{
			_depth++;
			return _clock.ElapsedMilliseconds;
		}

		public static void Done(string what, long t0)
		{
			_depth = Math.Max(0, _depth - 1);
			if (!On)
			{
				return;
			}
			long ms = _clock.ElapsedMilliseconds - t0;
			if (_depth == 0)
			{
				_top += ms;
			}
			if (ms >= 1)
			{
				_ran.Add(((_depth > 0) ? "  ↳ " : "") + what + "=" + ms + "ms");
			}
		}

		// a part of an outer section (not added to the total twice)
		public static void Part(string what, long ms)
		{
			if (On && ms >= 1)
			{
				_ran.Add("  ↳ " + what + "=" + ms + "ms");
			}
		}

		public static void Note(string what)
		{
			if (On)
			{
				_ran.Add(what + "=0ms");
			}
		}
	}
}
