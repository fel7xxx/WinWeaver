using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinWeaver;

internal static class Win
{
	private static int _build = -1;

	public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

	public static int Build
	{
		get
		{
			if (_build >= 0)
			{
				return _build;
			}
			_build = 26100;
			string environmentVariable = Environment.GetEnvironmentVariable("WW_FAKE_BUILD");
			if (!string.IsNullOrEmpty(environmentVariable) && int.TryParse(environmentVariable, out var result))
			{
				_build = result;
			}
			else if (IsWindows)
			{
				try
				{
					using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
					if (registryKey != null && int.TryParse(Convert.ToString(registryKey.GetValue("CurrentBuild")), out var result2))
					{
						_build = result2;
					}
				}
				catch
				{
					_build = Environment.OSVersion.Version.Build;
				}
			}
			return _build;
		}
	}

	public static string BuildName(int b)
	{
		if (b >= 26300)
		{
			return "26H2";
		}
		if (b >= 26200)
		{
			return "25H2";
		}
		if (b >= 26100)
		{
			return "24H2";
		}
		if (b >= 22631)
		{
			return "23H2";
		}
		if (b >= 22621)
		{
			return "22H2";
		}
		if (b >= 22000)
		{
			return "21H2";
		}
		return "Windows 10";
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(IntPtr h);

	[DllImport("user32.dll")]
	public static extern bool ShowWindow(IntPtr h, int cmd);

	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

	[DllImport("user32.dll")]
	private static extern bool SetProcessDPIAware();

	public static void DpiAware()
	{
		if (IsWindows)
		{
			try
			{
				SetProcessDPIAware();
			}
			catch
			{
			}
		}
	}

	private static int Attr(IntPtr h, int a, int v)
	{
		try
		{
			return DwmSetWindowAttribute(h, a, ref v, 4);
		}
		catch
		{
			return -1;
		}
	}

	public static void DarkTitle(IntPtr h, Color caption)
	{
		if (IsWindows)
		{
			if (Attr(h, 20, 1) != 0)
			{
				Attr(h, 19, 1);
			}
			Attr(h, 35, caption.R | (caption.G << 8) | (caption.B << 16));
			Attr(h, 36, 15789292);
			Attr(h, 34, 4209210);
		}
	}

	public static bool RoundCorners(IntPtr h)
	{
		if (!IsWindows)
		{
			return false;
		}
		if (Environment.OSVersion.Version.Build < 22000)
		{
			return false;
		}
		bool result = Attr(h, 33, 2) == 0;
		Attr(h, 34, 4734526);
		return result;
	}

	public static void DragWindow(IntPtr h)
	{
		if (!IsWindows)
		{
			return;
		}
		try
		{
			ReleaseCapture();
			SendMessage(h, 161, (IntPtr)2, IntPtr.Zero);
		}
		catch
		{
		}
	}
}
