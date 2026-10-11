using Microsoft.Win32;

namespace WinWeaver;

internal static class Prefs
{
	public static bool RememberPage { get => Read("RememberPage") == "1"; set => Write("RememberPage", value ? "1" : "0"); }
	public static string LastPage { get => Read("LastPage") ?? "home"; set => Write("LastPage", value); }
	public static bool RebootReminder { get => Read("RebootReminder") != "0"; set => Write("RebootReminder", value ? "1" : "0"); }
	public static bool LargerText { get => Read("LargerText") == "1"; set => Write("LargerText", value ? "1" : "0"); }
	public static string AutoChoice
	{
		get
		{
			return Read("AutostartChoice");
		}
		set
		{
			Write("AutostartChoice", value);
		}
	}

	public static bool HideAddr
	{
		get
		{
			return Read("HideAddresses") == "1";
		}
		set
		{
			Write("HideAddresses", value ? "1" : "0");
		}
	}

	public static bool NoAnim
	{
		get
		{
			return Read("NoAnimations") == "1";
		}
		set
		{
			Write("NoAnimations", value ? "1" : "0");
		}
	}

	public static string LastBrowser
	{
		get
		{
			return Read("LastBrowser");
		}
		set
		{
			Write("LastBrowser", value);
		}
	}

	public static int LastWallpaper
	{
		get
		{
			if (!int.TryParse(Read("LastWallpaper"), out var result))
			{
				return 0;
			}
			return result;
		}
		set
		{
			Write("LastWallpaper", value.ToString());
		}
	}

	public static bool Expert
	{
		get
		{
			return Read("ExpertMode") == "1";
		}
		set
		{
			Write("ExpertMode", value ? "1" : "0");
		}
	}

	public static string Lang
	{
		get
		{
			return Read("Language") ?? "";
		}
		set
		{
			Write("Language", value);
		}
	}

	public static bool UpdateCheck
	{
		get
		{
			return Read("UpdateCheck") != "0";
		}
		set
		{
			Write("UpdateCheck", value ? "1" : "0");
		}
	}

	public static string LastUpdateCheck
	{
		get
		{
			return Read("LastUpdateCheck");
		}
		set
		{
			Write("LastUpdateCheck", value);
		}
	}

	public static string SkipVersion
	{
		get
		{
			return Read("SkipVersion");
		}
		set
		{
			Write("SkipVersion", value);
		}
	}

	public static bool Welcomed
	{
		get
		{
			return Read("Welcomed") == "1";
		}
		set
		{
			Write("Welcomed", value ? "1" : "0");
		}
	}

	public static string InstallChoice
	{
		get
		{
			return Read("InstallChoice");
		}
		set
		{
			Write("InstallChoice", value);
		}
	}

	private static string Read(string name)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\WinWeaver");
			return (registryKey == null) ? null : (registryKey.GetValue(name) as string);
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
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\WinWeaver");
			registryKey.SetValue(name, value ?? "", RegistryValueKind.String);
		}
		catch
		{
		}
	}
}
