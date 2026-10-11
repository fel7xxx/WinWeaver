using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace WinWeaver;

internal static class L
{
	private class Template
	{
		public Regex Re;

		public string To;

		public int Weight;
	}

	private static Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

	private static List<Template> _templates = new List<Template>();

	private static Dictionary<string, string> _upper = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly object _lock = new object();

	private static readonly string Miss = Environment.GetEnvironmentVariable("WW_LANG_MISSING");

	private static readonly Regex Slot = new Regex("\\{(\\d+)\\}");

	private static readonly Regex FmtSpec = new Regex("\\{(\\d+)(?:[,:][^}]*)?\\}");

	private static readonly Regex Sep = new Regex("(\\s+•\\s+)");

	private static List<KeyValuePair<string, string>> _pieces;

	private static List<KeyValuePair<string, string>> _upPieces;

	private static readonly char[] PieceTrim = new char[7] { ' ', '\t', '•', ',', ':', '(', ')' };

	public static string Lang { get; private set; } = "ru";


	public static bool IsRu => Lang == "ru";

	public static CultureInfo Culture
	{
		get
		{
			try
			{
				return CultureInfo.GetCultureInfo(IsRu ? "ru-RU" : ((Lang == "en") ? "en-US" : Lang));
			}
			catch
			{
				return CultureInfo.InvariantCulture;
			}
		}
	}

	public static event Action Changed;

	public static string Resolve(string pref)
	{
		if (!string.IsNullOrEmpty(pref))
		{
			return pref;
		}
		if (!(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"))
		{
			return "en";
		}
		return "ru";
	}

	public static void Init()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("WW_LANG");
		Set(Resolve((!string.IsNullOrEmpty(environmentVariable)) ? environmentVariable : Prefs.Lang), notify: false);
	}

	public static void Set(string lang, bool notify = true)
	{
		lock (_lock)
		{
			Lang = (string.IsNullOrEmpty(lang) ? "ru" : lang);
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
			L.Changed?.Invoke();
		}
	}

	public static List<string> Available()
	{
		List<string> list = new List<string> { "ru", "en" };
		try
		{
			string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "lang");
			if (Directory.Exists(path))
			{
				string[] files = Directory.GetFiles(path, "*.tsv");
				for (int i = 0; i < files.Length; i++)
				{
					string item = Path.GetFileNameWithoutExtension(files[i]).ToLowerInvariant();
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static string NameOf(string code)
	{
		if (!(code == "ru"))
		{
			if (code == "en")
			{
				return "English";
			}
			try
			{
				CultureInfo cultureInfo = new CultureInfo(code);
				return cultureInfo.TextInfo.ToTitleCase(cultureInfo.NativeName);
			}
			catch
			{
				return code;
			}
		}
		return "Русский";
	}

	private static string ReadEmbedded(string lang)
	{
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("lang." + lang + ".tsv");
			if (stream == null)
			{
				return null;
			}
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
			return streamReader.ReadToEnd();
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
			string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "lang", lang + ".tsv");
			return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
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
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (c == '\\' && i + 1 < s.Length)
			{
				char c2 = s[++i];
				stringBuilder.Append(c2 switch
				{
					'r' => '\r', 
					't' => '\t', 
					'n' => '\n', 
					_ => c2, 
				});
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static void Load(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		string[] array = text.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].TrimEnd('\r');
			if (text2.Length == 0 || text2[0] == '#')
			{
				continue;
			}
			int num = text2.IndexOf('\t');
			if (num <= 0)
			{
				continue;
			}
			string text3 = FmtSpec.Replace(Unescape(text2.Substring(0, num)), "{$1}");
			string text4 = FmtSpec.Replace(Unescape(text2.Substring(num + 1)), "{$1}");
			if (text4.Length == 0)
			{
				continue;
			}
			if (Slot.IsMatch(text3))
			{
				AddTemplate(text3, text4);
				continue;
			}
			if (text3.IndexOf('\n') >= 0)
			{
				string[] array2 = text3.Split('\n');
				string[] array3 = text4.Split('\n');
				if (array2.Length == array3.Length)
				{
					for (int j = 0; j < array2.Length; j++)
					{
						if (array2[j].Trim().Length > 0 && !_map.ContainsKey(array2[j]))
						{
							_map[array2[j]] = array3[j];
						}
					}
				}
			}
			_map[text3] = text4;
			string text5 = text3.ToUpperInvariant();
			if (text5 != text3 && !_upper.ContainsKey(text5))
			{
				_upper[text5] = text4.ToUpperInvariant();
			}
		}
		_templates.Sort((Template a, Template b) => b.Weight.CompareTo(a.Weight));
	}

	private static void AddTemplate(string from, string to)
	{
		StringBuilder stringBuilder = new StringBuilder("^");
		int num = 0;
		int num2 = 0;
		HashSet<int> hashSet = new HashSet<int>();
		foreach (Match item2 in Slot.Matches(from))
		{
			string text = from.Substring(num, item2.Index - num);
			num2 += text.Length;
			stringBuilder.Append(Regex.Escape(text));
			int item = int.Parse(item2.Groups[1].Value);
			if (hashSet.Add(item))
			{
				stringBuilder.Append("(?<s" + item + ">.+?)");
			}
			else
			{
				stringBuilder.Append("\\k<s" + item + ">");
			}
			num = item2.Index + item2.Length;
		}
		string text2 = from.Substring(num);
		num2 += text2.Length;
		stringBuilder.Append(Regex.Escape(text2)).Append("$");
		if (num2 < 2)
		{
			return;
		}
		try
		{
			_templates.Add(new Template
			{
				Re = new Regex(stringBuilder.ToString(), RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant),
				To = to,
				Weight = num2
			});
		}
		catch
		{
		}
	}

	private static bool HasCyrillic(string s)
	{
		foreach (char c in s)
		{
			if (c >= 'Ѐ' && c <= 'ӿ')
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
			if (_cache.TryGetValue(s, out var value))
			{
				return value;
			}
			value = Translate(s, 0);
			if (Miss != null && HasCyrillic(value))
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
			_cache[s] = value;
			return value;
		}
	}

	private static string Translate(string s, int depth)
	{
		if (_map.TryGetValue(s, out var value))
		{
			return value;
		}
		if (!HasCyrillic(s) || depth > 4)
		{
			return s;
		}
		if (_upper.TryGetValue(s, out value) && s == s.ToUpperInvariant())
		{
			return value;
		}
		string text = s.Trim();
		if (text.Length != s.Length && text.Length > 0 && _map.TryGetValue(text, out value))
		{
			int num = s.IndexOf(text, StringComparison.Ordinal);
			return s.Substring(0, num) + value + s.Substring(num + text.Length);
		}
		string text2 = null;
		bool flag = s == s.ToUpperInvariant();
		foreach (Template template in _templates)
		{
			Match i = template.Re.Match(s);
			if (i.Success)
			{
				text2 = Slot.Replace(template.To, delegate(Match sm)
				{
					Group group = i.Groups["s" + sm.Groups[1].Value];
					return (!group.Success) ? sm.Value : Translate(group.Value, depth + 1);
				});
				if (flag)
				{
					text2 = text2.ToUpperInvariant();
				}
				break;
			}
		}
		if (text2 != null && !HasCyrillic(text2))
		{
			return text2;
		}
		if (s.IndexOf('\n') < 0 && Sep.IsMatch(s))
		{
			string[] array = Sep.Split(s);
			if (array.Length > 1)
			{
				for (int j = 0; j < array.Length; j += 2)
				{
					array[j] = Translate(array[j], depth + 1);
				}
				string text3 = string.Join("", array);
				if (text2 == null || !HasCyrillic(text3))
				{
					return text3;
				}
			}
		}
		if (text2 != null)
		{
			return text2;
		}
		if (s.IndexOf('\n') >= 0)
		{
			string[] array2 = s.Split('\n');
			for (int k = 0; k < array2.Length; k++)
			{
				array2[k] = Translate(array2[k], depth + 1);
			}
			return string.Join("\n", array2);
		}
		return Pieces(s);
	}

	private static bool IsLetter(char c)
	{
		if (c < 'Ѐ' || c > 'ӿ')
		{
			return char.IsLetter(c);
		}
		return true;
	}

	private static string Pieces(string s)
	{
		if (_pieces == null)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, string> item in _map)
			{
				string text = item.Key.Trim(PieceTrim);
				if (text.Length >= 2 && HasCyrillic(text) && !dictionary.ContainsKey(text))
				{
					dictionary[text] = item.Value.Trim(PieceTrim);
				}
			}
			_pieces = new List<KeyValuePair<string, string>>(dictionary);
			_pieces.Sort((KeyValuePair<string, string> a, KeyValuePair<string, string> b) => b.Key.Length.CompareTo(a.Key.Length));
			_upPieces = new List<KeyValuePair<string, string>>();
			foreach (KeyValuePair<string, string> piece in _pieces)
			{
				_upPieces.Add(new KeyValuePair<string, string>(piece.Key.ToUpperInvariant(), piece.Value.ToUpperInvariant()));
			}
		}
		List<KeyValuePair<string, string>> obj = ((s == s.ToUpperInvariant()) ? _upPieces : _pieces);
		string text2 = s;
		foreach (KeyValuePair<string, string> item2 in obj)
		{
			if (!HasCyrillic(text2))
			{
				break;
			}
			int startIndex = 0;
			while ((startIndex = text2.IndexOf(item2.Key, startIndex, StringComparison.Ordinal)) >= 0)
			{
				int num = startIndex + item2.Key.Length;
				if ((startIndex != 0 && IsLetter(text2[startIndex - 1])) || (num < text2.Length && IsLetter(text2[num])))
				{
					startIndex = num;
					continue;
				}
				text2 = text2.Substring(0, startIndex) + item2.Value + text2.Substring(num);
				startIndex += item2.Value.Length;
			}
		}
		return text2;
	}
}
