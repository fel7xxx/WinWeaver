using System;
using System.Diagnostics;

namespace WinWeaver;

internal static class Shell
{
	public static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

	public static void Open(string file, string args = null)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(file)
			{
				UseShellExecute = true
			};
			if (args != null)
			{
				processStartInfo.Arguments = args;
			}
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			Program.Log("OPEN ERROR " + ex);
			Program.Main0?.Status("err", "Не удалось открыть " + file + ": " + ex.Message);
		}
	}

	public static void Console(string command)
	{
		Open("powershell.exe", "-NoProfile -NoExit -ExecutionPolicy Bypass -Command \"" + command + "\"");
	}
}
