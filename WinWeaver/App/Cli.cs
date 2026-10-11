using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinWeaver;

internal static class Cli
{
	private static bool _own;

	public static string Help => "WinWeaver 1.0.1 — " + L.T("настройка Windows 11") + "\n\n  WinWeaver.exe /apply <light|optimal|gamer> [/browser <keep|firefox|chrome|brave|yandex|operagx>]\n                [/opts \"7zip,vc,ext,temp,drv\"] [/wallpaper 1-" + Walls.Count + "] [/nowall] [/noname]\n                [/defender <asis|quiet>] [/restart]\n  WinWeaver.exe /uninstall [/quiet] [/undo]\n  WinWeaver.exe /version\n\n" + L.T("Каждый шаг записывается в «Историю» — его можно отменить в программе. Журнал:") + " " + Brand.LogPath + "\n";

	[DllImport("kernel32.dll")]
	private static extern bool AttachConsole(int pid);

	[DllImport("kernel32.dll")]
	private static extern bool AllocConsole();

	private static void OpenConsole()
	{
		if (!Win.IsWindows)
		{
			return;
		}
		try
		{
			if (!AttachConsole(-1))
			{
				_own = AllocConsole();
			}
			Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
			{
				AutoFlush = true
			});
			Console.OutputEncoding = Encoding.UTF8;
		}
		catch
		{
		}
	}

	private static string Arg(string[] args, string name)
	{
		for (int i = 0; i < args.Length - 1; i++)
		{
			if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return args[i + 1];
			}
		}
		return null;
	}

	private static bool Has(string[] args, string name)
	{
		return Array.Exists(args, (string a) => a.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	public static int Run(string[] args, bool fake)
	{
		OpenConsole();
		int result = 0;
		try
		{
			switch (args[0].ToLowerInvariant())
			{
			case "/version":
				Console.WriteLine("WinWeaver 1.0.1");
				break;
			case "/apply":
				result = Apply(args, fake);
				break;
			case "/uninstall":
				result = Uninstall(args, fake);
				break;
			default:
				Console.WriteLine(Help);
				break;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("ERROR: " + ex.Message);
			Program.Log("CLI ERROR " + ex);
			result = 1;
		}
		if (_own && !Has(args, "/quiet"))
		{
			Console.WriteLine();
			Console.WriteLine(L.T("Нажми Enter, чтобы закрыть окно"));
			try
			{
				Console.ReadLine();
			}
			catch
			{
			}
		}
		return result;
	}

	private static IEngine Engine(bool fake, string mode)
	{
		EngineUI ui = new EngineUI(delegate(string kind, string text)
		{
			Console.WriteLine(((kind == "err") ? "[!] " : ((kind == "warn") ? "[~] " : "    ")) + L.T(text ?? ""));
		}, (string title, string body, string yes, string no) => false);
		IEngine engine;
		if (!fake)
		{
			engine = Program.CreatePsEngine(ui, mode);
		}
		else
		{
			IEngine engine2 = new FakeEngine();
			engine = engine2;
		}
		engine.Start();
		return engine;
	}

	private static int Apply(string[] args, bool fake)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
		if (text != "light" && text != "optimal" && text != "gamer")
		{
			Console.WriteLine(Help);
			return 2;
		}
		string text2 = (Arg(args, "/browser") ?? ((text == "light") ? "keep" : "firefox")).ToLowerInvariant();
		string obj = Arg(args, "/opts") ?? ((text == "light") ? "7zip" : ((text == "gamer") ? "7zip,vc,ext,temp,drv" : "7zip,ext,temp"));
		List<string> list = new List<string>();
		string[] array = obj.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			string text3 = array[i].Trim().ToLowerInvariant();
			switch (text3)
			{
			case "7zip":
			case "vc":
			case "ext":
			case "temp":
			case "drv":
				list.Add(text3);
				break;
			}
		}
		if (Has(args, "/nowall"))
		{
			list.Add("nowall");
		}
		if (Has(args, "/noname"))
		{
			list.Add("noname");
		}
		int num = 0;
		string text4 = Arg(args, "/wallpaper");
		if (text4 != null && int.TryParse(text4, out var result))
		{
			num = Walls.Clamp(result - 1);
		}
		string text5 = (Arg(args, "/defender") ?? "asis").ToLowerInvariant();
		string text6 = string.Join(",", list.ToArray()) + ";def=" + ((text5 == "quiet") ? "1" : "0") + ";wp=" + num;
		Console.WriteLine(L.T("Автонастройка") + ": " + text + ", " + text2 + "  (" + text6 + ")");
		IEngine engine = Engine(fake, "/apply");
		try
		{
			object o = engine.Eval("$r = Invoke-FLevel $lvl $br $opts; [bool](@($r) | Select-Object -Last 1)", new Hashtable
			{
				{ "lvl", text },
				{ "br", text2 },
				{ "opts", text6 }
			});
			bool num2 = engine.As(o, fallback: false) || fake;
			if (num2 && Has(args, "/restart") && !fake)
			{
				Shell.Open("shutdown.exe", "/r /t 30 /c \"WinWeaver\"");
			}
			return (!num2) ? 1 : 0;
		}
		finally
		{
			engine.Stop();
		}
	}

	private static int Uninstall(string[] args, bool fake)
	{
		IEngine engine = Engine(fake, "/uninstall");
		try
		{
			engine.Eval("[void](Uninstall-FWinWeaver $fa1)", new Hashtable { 
			{
				"fa1",
				Has(args, "/undo")
			} });
		}
		finally
		{
			engine.Stop();
		}
		if (!fake)
		{
			Installer.RemoveFilesAfterExit();
		}
		return 0;
	}
}
