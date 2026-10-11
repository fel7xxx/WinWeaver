using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WinWeaver;

internal static class Program
{
	public static MainForm Main0;

	public static string ExePath;

	[STAThread]
	private static int Main(string[] args)
	{
		string text = ((args.Length != 0) ? args[0].Trim().ToLowerInvariant() : "");
		bool flag = Array.Exists(args, (string a) => a.Equals("/fake", StringComparison.OrdinalIgnoreCase)) || Environment.GetEnvironmentVariable("WW_FAKE") == "1";
		if (text == "/fake")
		{
			text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
		}
#if !PSENGINE
		// Сборка без PowerShell-хоста работает только в демо-режиме.
		flag = true;
#endif
#if HEADLESS
		int shotsAt = Array.FindIndex(args, (string a) => a == "--shots");
		if (shotsAt >= 0)
		{
			return Shots.Run(args.Length > shotsAt + 1 ? args[shotsAt + 1] : "shots");
		}
#endif
		AvaloniaApp.Start();
		Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
		{
			Log("UI ERROR " + e.Exception);
			if (Main0 != null)
			{
				Main0.Status("err", "Внутренняя ошибка: " + e.Exception.Message);
			}
		};
		// Avalonia работает в независимых от DPI единицах и масштабирует сама,
		// поэтому собственный коэффициент Theme.S по умолчанию равен 1.
		float result = 1f;
		string environmentVariable = Environment.GetEnvironmentVariable("WW_SCALE");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			float.TryParse(environmentVariable, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
		}
		Theme.Init(result);
		L.Init();
		WDialog.AppIcon = LoadIcon();
		Application.AppIcon = WDialog.AppIcon;
		if (!flag)
		{
			try
			{
				Security.HardenDataDir();
			}
			catch (Exception ex)
			{
				Log("DATADIR ERR " + ex.Message);
				WDialog.Info(null, "Ошибка безопасности", ex.Message, DlgIcon.Error);
				return 1;
			}
		}
		ExePath = ((flag || !Installer.RunningInstalled) ? Installer.RunningExe : Brand.InstalledExe);
		try
		{
			switch (text)
			{
			case "/uninstall":
				if (!Array.Exists(args, (string a) => a.Equals("/quiet", StringComparison.OrdinalIgnoreCase)))
				{
					break;
				}
				goto case "/help";
			case "/help":
			case "/?":
			case "-h":
			case "--help":
			case "/version":
			case "/apply":
				return Cli.Run(args, flag);
			}
			if (!flag && text == "" && Environment.GetEnvironmentVariable("WW_SHOT") == null)
			{
				int num = FirstRun();
				if (num >= 0)
				{
					return num;
				}
			}
			switch (text)
			{
			case "/install-task":
			{
				IEngine engine = CreatePsEngine(new EngineUI(null, null), text);
				engine.Start();
				return (!engine.As(engine.Eval("Install-FPanelTask"), fallback: false)) ? 1 : 0;
			}
			case "def-off-safe":
			case "def-on-safe":
				Application.Run(new SafeForm(text, flag ? new FakeEngine() : null));
				return 0;
			case "/autostart":
				if (Prefs.AutoChoice == "no")
				{
					return 0;
				}
				break;
			}
			bool createdNew;
			using (Mutex obj2 = new Mutex(initiallyOwned: true, "Local\\WinWeaver", out createdNew))
			{
				EventWaitHandle show = CreateShowEvent();
				try
				{
					if (!createdNew)
					{
						show.Set();
						return 0;
					}
					MainForm f = new MainForm(flag ? new FakeEngine() : null, text, flag);
					Main0 = f;
					Thread thread = new Thread((ThreadStart)delegate
					{
						while (true)
						{
							show.WaitOne();
							try
							{
								f.BeginInvoke((Action)delegate
								{
									if (f.WindowState == FormWindowState.Minimized)
									{
										f.WindowState = FormWindowState.Normal;
									}
									f.Activate();
								});
							}
							catch
							{
								if (f.IsDisposed)
								{
									break;
								}
							}
						}
					});
					thread.IsBackground = true;
					thread.Start();
					string environmentVariable2 = Environment.GetEnvironmentVariable("WW_SHOT");
					if (!string.IsNullOrEmpty(environmentVariable2))
					{
						ShotRunner.Attach(f, environmentVariable2);
					}
					Application.Run(f);
					GC.KeepAlive(obj2);
				}
				finally
				{
					if (show != null)
					{
						((IDisposable)show).Dispose();
					}
				}
			}
			return 0;
		}
		catch (Exception ex2)
		{
			try
			{
				File.AppendAllText(Brand.LogPath, DateTime.Now.ToString("s") + "  CRASH " + ex2?.ToString() + Environment.NewLine);
			}
			catch
			{
			}
			Console.Error.WriteLine("CRASH " + ex2);
			WDialog.Info(null, "WinWeaver — ошибка", "Не удалось запустить программу:\n\n" + ex2.Message, DlgIcon.Error);
			return 1;
		}
	}

	private static EventWaitHandle CreateShowEvent()
	{
		try
		{
			return new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\WinWeaverShow");
		}
		catch (PlatformNotSupportedException)
		{
			// Именованные события есть только в Windows.
			return new EventWaitHandle(initialState: false, EventResetMode.AutoReset);
		}
	}

	public static void Log(string t)
	{
		try
		{
			string dataDir = Brand.DataDir;
			Directory.CreateDirectory(dataDir);
			File.AppendAllText(Path.Combine(dataDir, "tweaker.log"), DateTime.Now.ToString("s") + "  " + t + Environment.NewLine);
		}
		catch
		{
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static IEngine CreatePsEngine(EngineUI ui, string mode)
	{
#if PSENGINE
		return new PsEngine(ui, ExePath, mode);
#else
		// Сборка net8.0 не содержит хоста Windows PowerShell 5.1 —
		// используется демонстрационный движок (как с ключом /fake).
		Log("PS engine недоступен в этой сборке — FakeEngine");
		return new FakeEngine();
#endif
	}

	private static Icon LoadIcon()
	{
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("winweaver.ico");
			if (stream != null)
			{
				return new Icon(stream);
			}
		}
		catch
		{
		}
		return null;
	}

	private static int FirstRun()
	{
		if (!Prefs.Welcomed)
		{
			WelcomeDialog welcomeDialog = new WelcomeDialog();
			welcomeDialog.ShowFor(null);
			Prefs.Welcomed = true;
			Prefs.Lang = ((welcomeDialog.Lang == L.Resolve("")) ? "" : welcomeDialog.Lang);
			Prefs.UpdateCheck = welcomeDialog.Updates;
			Prefs.AutoChoice = (welcomeDialog.Autostart ? "yes" : "no");
			if (!welcomeDialog.Install)
			{
				Prefs.InstallChoice = "portable";
				return -1;
			}
			try
			{
				string text = Installer.Install();
				Prefs.InstallChoice = "installed";
				ExePath = text;
				if (welcomeDialog.Autostart)
				{
					Process.Start(new ProcessStartInfo(text, "/install-task")
					{
						UseShellExecute = false,
						CreateNoWindow = true
					}).WaitForExit(60000);
				}
				if (!Installer.RunningInstalled && Installer.Relaunch(text))
				{
					return 0;
				}
			}
			catch (Exception ex)
			{
				Log("INSTALL ERR " + ex);
				WDialog.Info(null, "Не удалось установить", ex.Message + "\n\nПрограмма запустится без установки.", DlgIcon.Warn);
			}
			return -1;
		}
		if (Installer.InstalledIsOlder)
		{
			switch (WDialog.Choose(null, L.T("Обновить {0}?").Replace("{0}", "WinWeaver"), L.T("Установлена версия {0}, а запущена {1}. Заменить установленную копию в Program Files?").Replace("{0}", Installer.FileVersion(Brand.InstalledExe).ToString(3)).Replace("{1}", "1.0.1"), "Обновить", "Запустить без обновления", "Отмена"))
			{
			case 2:
				return 0;
			case 0:
				try
				{
					if (Installer.Relaunch(Installer.Install()))
					{
						return 0;
					}
				}
				catch (Exception ex2)
				{
					WDialog.Info(null, "Не удалось обновить", ex2.Message, DlgIcon.Warn);
				}
				break;
			}
		}
		return -1;
	}
}
