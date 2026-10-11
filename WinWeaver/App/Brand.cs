using System;
using System.IO;

namespace WinWeaver;

internal static class Brand
{
	public const string Name = "WinWeaver";

	public const string Short = "WinWeaver";

	public const string OsName = "WeavOS";

	public const string Version = "1.0.2";

	public const string Folder = "WinWeaver";

	public const string ExeName = "WinWeaver.exe";

	public const string Developer = "fel7xxx";

	public const string Repo = "fel7xxx/WinWeaver";

	public const string GitHub = "https://github.com/fel7xxx/WinWeaver";

	public const string Releases = "https://github.com/fel7xxx/WinWeaver/releases";

	public const string LatestApi = "https://api.github.com/repos/fel7xxx/WinWeaver/releases/latest";

	public const string Issues = "https://github.com/fel7xxx/WinWeaver/issues";

	public const string NewBug = "https://github.com/fel7xxx/WinWeaver/issues/new?template=bug_report.yml";

	public const string NewIdea = "https://github.com/fel7xxx/WinWeaver/issues/new?template=feature_request.yml";

	public const string Discussions = "https://github.com/fel7xxx/WinWeaver/discussions";

	public const string Boosty = "https://boosty.to/fel7xxx/donate";

	public const string Ton = "UQB_35tg7SFtMrJVeU5W6MoB2shypmmpBcJUJKG5WfvCuOMc";

	public const string Btc = "bc1qpn0wasg3mkgdhepkpy5qjg9rmw793f85jq8f5m";

	public const string RegKey = "Software\\WinWeaver";

	public const string UninstallKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\WinWeaver";

	public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinWeaver");

	public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinWeaver");

	public static string LogPath => Path.Combine(DataDir, "tweaker.log");

	public static string InstalledExe => Path.Combine(InstallDir, "WinWeaver.exe");
}
