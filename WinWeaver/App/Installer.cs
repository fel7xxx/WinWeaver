using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace WinWeaver;

internal static class Installer
{
	public static string RunningExe => Assembly.GetExecutingAssembly().Location;

	public static bool IsInstalled => File.Exists(Brand.InstalledExe);

	public static bool RunningInstalled => SamePath(RunningExe, Brand.InstalledExe);

	public static bool InstalledIsOlder
	{
		get
		{
			if (IsInstalled && !RunningInstalled)
			{
				return FileVersion(Brand.InstalledExe) < FileVersion(RunningExe);
			}
			return false;
		}
	}

	public static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WinWeaver.lnk");

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
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
			return new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
		}
		catch
		{
			return new Version(0, 0);
		}
	}

	public static string Install()
	{
		Directory.CreateDirectory(Brand.InstallDir);
		string installedExe = Brand.InstalledExe;
		if (!RunningInstalled)
		{
			CopyDependencies(Path.GetDirectoryName(RunningExe), Brand.InstallDir);
			string text = installedExe + ".new";
			File.Copy(RunningExe, text, overwrite: true);
			if (File.Exists(installedExe))
			{
				try
				{
					File.Delete(installedExe);
				}
				catch (Exception) when (File.Exists(installedExe))
				{
					string text2 = installedExe + ".old";
					try
					{
						if (File.Exists(text2))
						{
							File.Delete(text2);
						}
					}
					catch
					{
						text2 = installedExe + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".old";
					}
					File.Move(installedExe, text2);
				}
			}
			File.Move(text, installedExe);
		}
		Register(installedExe);
		try
		{
			StartMenuShortcut(installedExe);
		}
		catch (Exception ex2)
		{
			Program.Log("SHORTCUT ERR " + ex2.Message);
		}
		Program.Log("INSTALLED " + installedExe + " v1.0.1");
		return installedExe;
	}

	// Avalonia is a multi-file deployment. Copy runtime files, not user profiles or logs.
	internal static void CopyDependencies(string source, string destination)
	{
		if (SamePath(source, destination)) return;
		Directory.CreateDirectory(destination);
		foreach (string path in Directory.GetFiles(source))
		{
			if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
			string name = Path.GetFileName(path);
			string ext = Path.GetExtension(path).ToLowerInvariant();
			if (ext != ".dll" && ext != ".config" && ext != ".json" && ext != ".so" && ext != ".dylib") continue;
			if (ext == ".json" && !name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase)) continue;
			File.Copy(path, Path.Combine(destination, name), true);
		}
		foreach (string dir in Directory.GetDirectories(source))
		{
			if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
			string name = Path.GetFileName(dir);
			if (name.Equals("lang", StringComparison.OrdinalIgnoreCase))
			{
				string target = Path.Combine(destination, name);
				Directory.CreateDirectory(target);
				foreach (string language in Directory.GetFiles(dir, "*.tsv"))
					if ((File.GetAttributes(language) & FileAttributes.ReparsePoint) == 0)
						File.Copy(language, Path.Combine(target, Path.GetFileName(language)), true);
				continue;
			}
			if (name == "runtimes" || name == "x64" || name == "x86" || name == "arm64" || name == "native" || name == "lib" || name.StartsWith("win-", StringComparison.OrdinalIgnoreCase) || name == "net48" || name == "netstandard2.0" || name == "en" || name == "ru")
				CopyDependencies(dir, Path.Combine(destination, name));
		}
	}

	private static void Register(string exe)
	{
		using RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(Brand.UninstallKey);
		registryKey.SetValue("DisplayName", "WinWeaver");
		registryKey.SetValue("DisplayVersion", Brand.Version);
		registryKey.SetValue("Publisher", "fel7xxx");
		registryKey.SetValue("DisplayIcon", exe + ",0");
		registryKey.SetValue("InstallLocation", Path.GetDirectoryName(exe));
		registryKey.SetValue("UninstallString", "\"" + exe + "\" /uninstall");
		registryKey.SetValue("QuietUninstallString", "\"" + exe + "\" /uninstall /quiet");
		registryKey.SetValue("URLInfoAbout", "https://github.com/fel7xxx/WinWeaver");
		registryKey.SetValue("HelpLink", "https://github.com/fel7xxx/WinWeaver/issues");
		registryKey.SetValue("URLUpdateInfo", "https://github.com/fel7xxx/WinWeaver/releases");
		registryKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
		registryKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
		registryKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
		try
		{
			registryKey.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
		}
		catch
		{
		}
	}

	private static void StartMenuShortcut(string exe)
	{
		Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
		if (!(typeFromProgID == null))
		{
			object target = Activator.CreateInstance(typeFromProgID);
			object obj = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, target, new object[1] { StartMenuLink });
			Type type = obj.GetType();
			type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, obj, new object[1] { exe });
			type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, obj, new object[1] { exe + ",0" });
			type.InvokeMember("Description", BindingFlags.SetProperty, null, obj, new object[1] { "WinWeaver — настройка Windows 11" });
			type.InvokeMember("Save", BindingFlags.InvokeMethod, null, obj, null);
		}
	}

	public static bool Relaunch(string exe, string args = "")
	{
		try
		{
			Process.Start(new ProcessStartInfo(exe, args)
			{
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(exe)
			});
			return true;
		}
		catch (Exception ex)
		{
			Program.Log("RELAUNCH ERR " + ex.Message);
			return false;
		}
	}

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
			Registry.LocalMachine.DeleteSubKeyTree("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\WinWeaver", throwOnMissingSubKey: false);
		}
		catch
		{
		}
		string dataDir = Brand.DataDir;
		string installDir = Brand.InstallDir;
		string text = Path.Combine(Path.GetTempPath(), "winweaver-remove-" + Guid.NewGuid().ToString("N") + ".cmd");
		string contents = "@echo off\r\nping -n 4 127.0.0.1 >nul\r\nrd /s /q \"" + dataDir + "\" 2>nul\r\n" + (Directory.Exists(installDir) ? ("rd /s /q \"" + installDir + "\" 2>nul\r\n") : "") + "del /f /q \"%~f0\" 2>nul\r\n";
		File.WriteAllText(text, contents);
		Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + text + "\"")
		{
			CreateNoWindow = true,
			UseShellExecute = false,
			WindowStyle = ProcessWindowStyle.Hidden
		});
	}
}
