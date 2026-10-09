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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace WinWeaver
{
	// Interface language. Russian is the source language of the program; other languages are tables
	// "russian<TAB>translation" (embedded lang/en.tsv, or lang\<code>.tsv next to the exe for community
	// translations). Texts are translated when they are drawn, so the engine and the program logic always
	// work with the original strings. Templates with {0}, {1}… translate composed texts such as
	// "Шаг 3 из 15"; captured parts are translated recursively.
	internal static class L
	{
		public static string Lang { get; private set; } = "ru";

		public static event Action Changed;

		private static Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

		private static List<Template> _templates = new List<Template>();

		// section captions are upper-cased before drawing: "ОБНОВЛЕНИЯ" finds the translation of "Обновления"
		private static Dictionary<string, string> _upper = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly object _lock = new object();

		private class Template
		{
			public Regex Re;

			public string To;

			public int Weight;
		}

		public static bool IsRu => Lang == "ru";

		// translators: WW_LANG_MISSING=<file> collects texts that still have no translation
		private static readonly string Miss = Environment.GetEnvironmentVariable("WW_LANG_MISSING");

		// numbers and dates follow the interface language
		public static CultureInfo Culture
		{
			get
			{
				try
				{
					return CultureInfo.GetCultureInfo(IsRu ? "ru-RU" : (Lang == "en" ? "en-US" : Lang));
				}
				catch
				{
					return CultureInfo.InvariantCulture;
				}
			}
		}

		// "" = like Windows: Russian for a Russian system, English for everything else
		public static string Resolve(string pref)
		{
			if (!string.IsNullOrEmpty(pref))
			{
				return pref;
			}
			string sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
			return (sys == "ru") ? "ru" : "en";
		}

		public static void Init()
		{
			string env = Environment.GetEnvironmentVariable("WW_LANG");
			Set(Resolve(!string.IsNullOrEmpty(env) ? env : Prefs.Lang), notify: false);
		}

		public static void Set(string lang, bool notify = true)
		{
			lock (_lock)
			{
				Lang = string.IsNullOrEmpty(lang) ? "ru" : lang;
				_map = new Dictionary<string, string>(StringComparer.Ordinal);
				_templates = new List<Template>();
				_upper = new Dictionary<string, string>(StringComparer.Ordinal);
				_pieces = null;
				_upPieces = null;
				_cache.Clear();
				if (Lang != "ru")
				{
					Load(ReadEmbedded(Lang));
					Load(ReadExternal(Lang));
				}
			}
			if (notify)
			{
				Changed?.Invoke();
			}
		}

		// languages offered in Settings: built-in ones plus any lang\*.tsv next to the exe
		public static List<string> Available()
		{
			List<string> l = new List<string> { "ru", "en" };
			try
			{
				string dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "lang");
				if (Directory.Exists(dir))
				{
					foreach (string f in Directory.GetFiles(dir, "*.tsv"))
					{
						string code = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
						if (!l.Contains(code))
						{
							l.Add(code);
						}
					}
				}
			}
			catch
			{
			}
			return l;
		}

		public static string NameOf(string code)
		{
			switch (code)
			{
			case "ru":
				return "Русский";
			case "en":
				return "English";
			default:
				try
				{
					CultureInfo c = new CultureInfo(code);
					return c.TextInfo.ToTitleCase(c.NativeName);
				}
				catch
				{
					return code;
				}
			}
		}

		private static string ReadEmbedded(string lang)
		{
			try
			{
				using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lang." + lang + ".tsv"))
				{
					if (s == null)
					{
						return null;
					}
					using (StreamReader r = new StreamReader(s, Encoding.UTF8))
					{
						return r.ReadToEnd();
					}
				}
			}
			catch
			{
				return null;
			}
		}

		private static string ReadExternal(string lang)
		{
			try
			{
				string f = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "lang", lang + ".tsv");
				return File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : null;
			}
			catch
			{
				return null;
			}
		}

		private static string Unescape(string s)
		{
			if (s.IndexOf('\\') < 0)
			{
				return s;
			}
			StringBuilder b = new StringBuilder(s.Length);
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (c == '\\' && i + 1 < s.Length)
				{
					char n = s[++i];
					b.Append(n == 'n' ? '\n' : (n == 't' ? '\t' : (n == 'r' ? '\r' : n)));
				}
				else
				{
					b.Append(c);
				}
			}
			return b.ToString();
		}

		private static readonly Regex Slot = new Regex("\\{(\\d+)\\}");

		// "{0:N0}" in a table is matched like "{0}": the program has already formatted the number
		private static readonly Regex FmtSpec = new Regex("\\{(\\d+)(?:[,:][^}]*)?\\}");

		private static readonly Regex Sep = new Regex("(\\s+•\\s+)");

		// texts glued together in code ("612 ГБ" + " свободно"): known pieces, longest first
		private static List<KeyValuePair<string, string>> _pieces;

		private static List<KeyValuePair<string, string>> _upPieces;

		private static readonly char[] PieceTrim = new char[] { ' ', '\t', '•', ',', ':', '(', ')' };

		private static void Load(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}
				int tab = line.IndexOf('\t');
				if (tab <= 0)
				{
					continue;
				}
				string from = FmtSpec.Replace(Unescape(line.Substring(0, tab)), "{$1}");
				string to = FmtSpec.Replace(Unescape(line.Substring(tab + 1)), "{$1}");
				if (to.Length == 0)
				{
					continue;
				}
				if (Slot.IsMatch(from))
				{
					AddTemplate(from, to);
				}
				else
				{
					// dialogs draw long texts paragraph by paragraph: every line of a text is known by itself too
					if (from.IndexOf('\n') >= 0)
					{
						string[] fl = from.Split('\n');
						string[] tl = to.Split('\n');
						if (fl.Length == tl.Length)
						{
							for (int i = 0; i < fl.Length; i++)
							{
								if (fl[i].Trim().Length > 0 && !_map.ContainsKey(fl[i]))
								{
									_map[fl[i]] = tl[i];
								}
							}
						}
					}
					_map[from] = to;
					string up = from.ToUpperInvariant();
					if (up != from && !_upper.ContainsKey(up))
					{
						_upper[up] = to.ToUpperInvariant();
					}
				}
			}
			// longer literal parts first: the most specific template wins
			_templates.Sort((a, b) => b.Weight.CompareTo(a.Weight));
		}

		private static void AddTemplate(string from, string to)
		{
			StringBuilder re = new StringBuilder("^");
			int last = 0;
			int weight = 0;
			HashSet<int> seen = new HashSet<int>();
			foreach (Match m in Slot.Matches(from))
			{
				string lit = from.Substring(last, m.Index - last);
				weight += lit.Length;
				re.Append(Regex.Escape(lit));
				int n = int.Parse(m.Groups[1].Value);
				if (seen.Add(n))
				{
					re.Append("(?<s" + n + ">.+?)");
				}
				else
				{
					re.Append("\\k<s" + n + ">");
				}
				last = m.Index + m.Length;
			}
			string tail = from.Substring(last);
			weight += tail.Length;
			re.Append(Regex.Escape(tail)).Append("$");
			if (weight < 2)
			{
				return;
			}
			try
			{
				_templates.Add(new Template { Re = new Regex(re.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), To = to, Weight = weight });
			}
			catch
			{
			}
		}

		private static bool HasCyrillic(string s)
		{
			foreach (char c in s)
			{
				if (c >= '\u0400' && c <= '\u04FF')
				{
					return true;
				}
			}
			return false;
		}

		public static string T(string s)
		{
			if (s == null || Lang == "ru" || s.Length == 0 || !HasCyrillic(s))
			{
				return s;
			}
			lock (_lock)
			{
				string r;
				if (_cache.TryGetValue(s, out r))
				{
					return r;
				}
				r = Translate(s, 0);
				if (Miss != null && HasCyrillic(r))
				{
					try
					{
						File.AppendAllText(Miss, s.Replace("\n", "\\n") + "\n", Encoding.UTF8);
					}
					catch
					{
					}
				}
				if (_cache.Count > 20000)
				{
					_cache.Clear();
				}
				_cache[s] = r;
				return r;
			}
		}

		private static string Translate(string s, int depth)
		{
			string r;
			if (_map.TryGetValue(s, out r))
			{
				return r;
			}
			if (!HasCyrillic(s) || depth > 4)
			{
				return s;
			}
			if (_upper.TryGetValue(s, out r) && s == s.ToUpperInvariant())
			{
				return r;
			}
			// keep surrounding spaces / punctuation that the source glued on
			string core = s.Trim();
			if (core.Length != s.Length && core.Length > 0 && _map.TryGetValue(core, out r))
			{
				int lead = s.IndexOf(core, StringComparison.Ordinal);
				return s.Substring(0, lead) + r + s.Substring(lead + core.Length);
			}
			// a template first; when it leaves Russian words behind, "a  •  b  •  c" is tried part by part
			string byTemplate = null;
			bool upper = s == s.ToUpperInvariant();
			foreach (Template t in _templates)
			{
				Match m = t.Re.Match(s);
				if (!m.Success)
				{
					continue;
				}
				byTemplate = Slot.Replace(t.To, delegate(Match sm)
				{
					Group g = m.Groups["s" + sm.Groups[1].Value];
					return g.Success ? Translate(g.Value, depth + 1) : sm.Value;
				});
				if (upper)
				{
					byTemplate = byTemplate.ToUpperInvariant();
				}
				break;
			}
			if (byTemplate != null && !HasCyrillic(byTemplate))
			{
				return byTemplate;
			}
			if (s.IndexOf('\n') < 0 && Sep.IsMatch(s))
			{
				string[] parts = Sep.Split(s);
				if (parts.Length > 1)
				{
					for (int i = 0; i < parts.Length; i += 2)
					{
						parts[i] = Translate(parts[i], depth + 1);
					}
					string joined = string.Join("", parts);
					if (byTemplate == null || !HasCyrillic(joined))
					{
						return joined;
					}
				}
			}
			if (byTemplate != null)
			{
				return byTemplate;
			}
			// multi-line texts: translate line by line
			if (s.IndexOf('\n') >= 0)
			{
				string[] lines = s.Split('\n');
				for (int i = 0; i < lines.Length; i++)
				{
					lines[i] = Translate(lines[i], depth + 1);
				}
				return string.Join("\n", lines);
			}
			return Pieces(s);
		}

		private static bool IsLetter(char c)
		{
			return (c >= '\u0400' && c <= '\u04FF') || char.IsLetter(c);
		}

		private static string Pieces(string s)
		{
			if (_pieces == null)
			{
				Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
				foreach (KeyValuePair<string, string> kv in _map)
				{
					string k = kv.Key.Trim(PieceTrim);
					if (k.Length >= 2 && HasCyrillic(k) && !d.ContainsKey(k))
					{
						d[k] = kv.Value.Trim(PieceTrim);
					}
				}
				_pieces = new List<KeyValuePair<string, string>>(d);
				_pieces.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
				_upPieces = new List<KeyValuePair<string, string>>();
				foreach (KeyValuePair<string, string> kv in _pieces)
				{
					_upPieces.Add(new KeyValuePair<string, string>(kv.Key.ToUpperInvariant(), kv.Value.ToUpperInvariant()));
				}
			}
			List<KeyValuePair<string, string>> list = (s == s.ToUpperInvariant()) ? _upPieces : _pieces;
			string r = s;
			foreach (KeyValuePair<string, string> kv in list)
			{
				if (!HasCyrillic(r))
				{
					break;
				}
				int at = 0;
				while ((at = r.IndexOf(kv.Key, at, StringComparison.Ordinal)) >= 0)
				{
					int end = at + kv.Key.Length;
					bool whole = (at == 0 || !IsLetter(r[at - 1])) && (end >= r.Length || !IsLetter(r[end]));
					if (!whole)
					{
						at = end;
						continue;
					}
					r = r.Substring(0, at) + kv.Value + r.Substring(end);
					at += kv.Value.Length;
				}
			}
			return r;
		}
	}

	// Shadows System.Windows.Forms.TextRenderer inside this namespace: every text the interface draws or
	// measures goes through the translator, so layout is computed for the translated text.
	internal static class TextRenderer
	{

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color fore)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, pt, fore);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color fore, System.Windows.Forms.TextFormatFlags flags)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, pt, fore, flags);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color fore, System.Drawing.Color back)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, pt, fore, back);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color fore, System.Drawing.Color back, System.Windows.Forms.TextFormatFlags flags)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, pt, fore, back, flags);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Rectangle r, System.Drawing.Color fore)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, r, fore);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Rectangle r, System.Drawing.Color fore, System.Windows.Forms.TextFormatFlags flags)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, r, fore, flags);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Rectangle r, System.Drawing.Color fore, System.Drawing.Color back)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, r, fore, back);
		}

		public static void DrawText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Rectangle r, System.Drawing.Color fore, System.Drawing.Color back, System.Windows.Forms.TextFormatFlags flags)
		{
			System.Windows.Forms.TextRenderer.DrawText(dc, L.T(text), font, r, fore, back, flags);
		}

		public static System.Drawing.Size MeasureText(string text, System.Drawing.Font font)
		{
			return System.Windows.Forms.TextRenderer.MeasureText(L.T(text), font);
		}

		public static System.Drawing.Size MeasureText(string text, System.Drawing.Font font, System.Drawing.Size proposed)
		{
			return System.Windows.Forms.TextRenderer.MeasureText(L.T(text), font, proposed);
		}

		public static System.Drawing.Size MeasureText(string text, System.Drawing.Font font, System.Drawing.Size proposed, System.Windows.Forms.TextFormatFlags flags)
		{
			return System.Windows.Forms.TextRenderer.MeasureText(L.T(text), font, proposed, flags);
		}

		public static System.Drawing.Size MeasureText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font)
		{
			return System.Windows.Forms.TextRenderer.MeasureText(dc, L.T(text), font);
		}

		public static System.Drawing.Size MeasureText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Size proposed)
		{
			return System.Windows.Forms.TextRenderer.MeasureText(dc, L.T(text), font, proposed);
		}

		public static System.Drawing.Size MeasureText(System.Drawing.IDeviceContext dc, string text, System.Drawing.Font font, System.Drawing.Size proposed, System.Windows.Forms.TextFormatFlags flags)
		{
			return System.Windows.Forms.TextRenderer.MeasureText(dc, L.T(text), font, proposed, flags);
		}
	}
}
