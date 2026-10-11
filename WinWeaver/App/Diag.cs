using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WinWeaver;

internal static class Diag
{
	public static string WindowsLine()
	{
		string text = "Windows";
		string text2 = "";
		string text3 = "";
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
			if (registryKey != null)
			{
				text = Convert.ToString(registryKey.GetValue("ProductName") ?? "Windows");
				text2 = Convert.ToString(registryKey.GetValue("DisplayVersion") ?? "");
				text3 = Convert.ToString(registryKey.GetValue("UBR") ?? "");
			}
		}
		catch
		{
		}
		if (Win.Build >= 22000)
		{
			text = text.Replace("Windows 10", "Windows 11");
		}
		return (text + " " + text2 + " (" + Win.Build + ((text3.Length > 0) ? ("." + text3) : "") + ")").Replace("  ", " ");
	}

	public static string Mask(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		string userName = Environment.UserName;
		string machineName = Environment.MachineName;
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (!string.IsNullOrEmpty(folderPath) && folderPath.Length > 3)
		{
			s = Regex.Replace(s, Regex.Escape(folderPath), "%USERPROFILE%", RegexOptions.IgnoreCase);
		}
		if (!string.IsNullOrEmpty(userName) && userName.Length > 1)
		{
			s = Regex.Replace(s, "\\b" + Regex.Escape(userName) + "\\b", "<user>", RegexOptions.IgnoreCase);
		}
		if (!string.IsNullOrEmpty(machineName) && machineName.Length > 1)
		{
			s = Regex.Replace(s, "\\b" + Regex.Escape(machineName) + "\\b", "<pc>", RegexOptions.IgnoreCase);
		}
		return s;
	}

	public static string Text(int lines = 80)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("WinWeaver 1.0.1 (" + L.T(Installer.RunningInstalled ? "установлен" : "без установки") + ")");
		stringBuilder.AppendLine(WindowsLine());
		stringBuilder.AppendLine("Language: " + L.Lang + " • expert: " + (Prefs.Expert ? "yes" : "no") + " • scale: " + (int)Math.Round(Theme.Scale * 100f) + "%");
		stringBuilder.AppendLine("---- tweaker.log (last lines) ----");
		try
		{
			if (File.Exists(Brand.LogPath))
			{
				string[] array = File.ReadAllLines(Brand.LogPath);
				for (int i = Math.Max(0, array.Length - lines); i < array.Length; i++)
				{
					stringBuilder.AppendLine(Mask(array[i]));
				}
			}
			else
			{
				stringBuilder.AppendLine("(log is empty)");
			}
		}
		catch (Exception ex)
		{
			stringBuilder.AppendLine("(log not read: " + ex.Message + ")");
		}
		return stringBuilder.ToString();
	}
}
