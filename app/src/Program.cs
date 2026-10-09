// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

namespace WinWeaver
{
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
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
			{
				Log("UI ERROR " + e.Exception);
				if (Main0 != null)
				{
					Main0.Status("err", "Внутренняя ошибка: " + e.Exception.Message);
				}
			};
			Win.DpiAware();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			float result = 1f;
			try
			{
				using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
				{
					result = graphics.DpiX / 96f;
				}
			}
			catch
			{
			}
			string environmentVariable = Environment.GetEnvironmentVariable("WW_SCALE");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				float.TryParse(environmentVariable, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
			}
			Theme.Init(result);
			L.Init();
			WDialog.AppIcon = LoadIcon();
			if (!flag)
			{
				try
				{
					Security.HardenDataDir();
				}
				catch (Exception ex0)
				{
					Log("DATADIR ERR " + ex0.Message);
				}
			}
			ExePath = (flag || !Installer.RunningInstalled) ? Installer.RunningExe : Brand.InstalledExe;
			try
			{
				if (text == "/help" || text == "/?" || text == "-h" || text == "--help" || text == "/version" || text == "/apply" || (text == "/uninstall" && Array.Exists(args, (string a) => a.Equals("/quiet", StringComparison.OrdinalIgnoreCase))))
				{
					return Cli.Run(args, flag);
				}
				if (!flag && text == "" && Environment.GetEnvironmentVariable("WW_SHOT") == null)
				{
					int r0 = FirstRun();
					if (r0 >= 0)
					{
						return r0;
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
					EventWaitHandle show = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\WinWeaverShow");
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
									break;
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
			catch (Exception ex)
			{
				try
				{
					File.AppendAllText(Brand.LogPath, DateTime.Now.ToString("s") + "  CRASH " + ex?.ToString() + Environment.NewLine);
				}
				catch
				{
				}
				Console.Error.WriteLine("CRASH " + ex);
				WDialog.Info(null, Brand.Name + " — ошибка", "Не удалось запустить программу:\n\n" + ex.Message, DlgIcon.Error);
				return 1;
			}
		}

		public static void Log(string t)
		{
			try
			{
				string text = Brand.DataDir;
				Directory.CreateDirectory(text);
				File.AppendAllText(Path.Combine(text, "tweaker.log"), DateTime.Now.ToString("s") + "  " + t + Environment.NewLine);
			}
			catch
			{
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static IEngine CreatePsEngine(EngineUI ui, string mode)
		{
			return new PsEngine(ui, ExePath, mode);
		}

		private static Icon LoadIcon()
		{
			try
			{
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("winweaver.ico"))
				{
					if (stream != null)
					{
						return new Icon(stream);
					}
				}
			}
			catch
			{
			}
			return null;
		}

		// first start (welcome, optional install) and starting a newer download over an installed copy.
		// Returns an exit code when this process should end (the installed copy was started), otherwise -1.
		private static int FirstRun()
		{
			if (!Prefs.Welcomed)
			{
				WelcomeDialog d = new WelcomeDialog();
				d.ShowFor(null);
				Prefs.Welcomed = true;
				Prefs.Lang = (d.Lang == L.Resolve("")) ? "" : d.Lang;
				Prefs.UpdateCheck = d.Updates;
				Prefs.AutoChoice = d.Autostart ? "yes" : "no";
				if (!d.Install)
				{
					Prefs.InstallChoice = "portable";
					return -1;
				}
				try
				{
					string exe = Installer.Install();
					Prefs.InstallChoice = "installed";
					ExePath = exe;
					if (d.Autostart)
					{
						Process.Start(new ProcessStartInfo(exe, "/install-task") { UseShellExecute = false, CreateNoWindow = true }).WaitForExit(60000);
					}
					if (!Installer.RunningInstalled && Installer.Relaunch(exe))
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
				int c = WDialog.Choose(null, L.T("Обновить {0}?").Replace("{0}", Brand.Name), L.T("Установлена версия {0}, а запущена {1}. Заменить установленную копию в Program Files?").Replace("{0}", Installer.FileVersion(Brand.InstalledExe).ToString(3)).Replace("{1}", Brand.Version), "Обновить", "Запустить без обновления", "Отмена");
				if (c == 2)
				{
					return 0;
				}
				if (c == 0)
				{
					try
					{
						string exe = Installer.Install();
						if (Installer.Relaunch(exe))
						{
							return 0;
						}
					}
					catch (Exception ex)
					{
						WDialog.Info(null, "Не удалось обновить", ex.Message, DlgIcon.Warn);
					}
				}
			}
			return -1;
		}
	}
}
