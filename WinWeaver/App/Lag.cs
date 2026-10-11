using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinWeaver;

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

	private static int _depth;

	private static int _g2;

	private static int _g0;

	private static long _top;

	public static void RemoveOldDesktopLog()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WinWeaver_lag.txt");
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	public static void Start()
	{
		if (!On)
		{
			On = true;
			try
			{
				Directory.CreateDirectory(Brand.DataDir);
				_path = Path.Combine(Brand.DataDir, "WinWeaver_lag.txt");
				File.WriteAllText(_path, "WinWeaver — журнал подвисаний (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\r\n" + Environment.OSVersion?.ToString() + ", ядер: " + Environment.ProcessorCount + "\r\nФормат: время, длительность фриза, что выполнялось (мс). «прочее» = отрисовка/раскладка окна Windows.\r\n\r\n", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("LAG " + ex.Message);
				_path = null;
			}
			Console.Error.WriteLine("LAG path " + _path);
			_lastBeat = _clock.ElapsedMilliseconds;
			_beat = new Timer
			{
				Interval = 10
			};
			_beat.Tick += delegate
			{
				Beat();
			};
			_beat.Start();
			_flush = new Timer
			{
				Interval = 2000
			};
			_flush.Tick += delegate
			{
				Flush();
			};
			_flush.Start();
		}
	}

	private static void Beat()
	{
		long elapsedMilliseconds = _clock.ElapsedMilliseconds;
		long num = elapsedMilliseconds - _lastBeat;
		_lastBeat = elapsedMilliseconds;
		int num2 = GC.CollectionCount(2);
		int num3 = GC.CollectionCount(0);
		bool flag = num2 != _g2;
		int num4 = num3 - _g0;
		_g2 = num2;
		_g0 = num3;
		if (num > 70)
		{
			long top = _top;
			_buf.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("  ФРИЗ ").Append(num)
				.Append(" мс  [")
				.Append(Context)
				.Append("]\r\n");
			foreach (string item in Merge(_ran))
			{
				_buf.Append("     ").Append(item).Append("\r\n");
			}
			_buf.Append("     прочее=").Append(Math.Max(0L, num - top)).Append("ms")
				.Append(flag ? "  (была полная сборка мусора)" : ((num4 > 0) ? ("  (сборок мусора: " + num4 + ")") : ""))
				.Append("\r\n");
		}
		_ran.Clear();
		_top = 0L;
	}

	private static List<string> Merge(List<string> ran)
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>();
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		List<string> list = new List<string>();
		foreach (string item in ran)
		{
			int num = item.LastIndexOf('=');
			string text = ((num > 0) ? item.Substring(0, num) : item);
			long.TryParse((num > 0) ? item.Substring(num + 1).Replace("ms", "") : "0", out var result);
			if (!dictionary.ContainsKey(text))
			{
				dictionary[text] = 0L;
				dictionary2[text] = 0;
				list.Add(text);
			}
			dictionary[text] += result;
			dictionary2[text]++;
		}
		List<string> list2 = new List<string>();
		foreach (string item2 in list)
		{
			if (dictionary[item2] >= 3 || list2.Count < 6)
			{
				list2.Add(item2 + ((dictionary2[item2] > 1) ? (" x" + dictionary2[item2]) : "") + "=" + dictionary[item2] + "ms");
			}
		}
		return list2;
	}

	private static void Flush()
	{
		if (_buf.Length != 0 && _path != null)
		{
			try
			{
				File.AppendAllText(_path, _buf.ToString(), Encoding.UTF8);
			}
			catch
			{
			}
			_buf.Clear();
		}
	}

	public static void Stop()
	{
		Flush();
	}

	public static long T0()
	{
		_depth++;
		return _clock.ElapsedMilliseconds;
	}

	public static void Done(string what, long t0)
	{
		_depth = Math.Max(0, _depth - 1);
		if (On)
		{
			long num = _clock.ElapsedMilliseconds - t0;
			if (_depth == 0)
			{
				_top += num;
			}
			if (num >= 1)
			{
				_ran.Add(((_depth > 0) ? "  ↳ " : "") + what + "=" + num + "ms");
			}
		}
	}

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
