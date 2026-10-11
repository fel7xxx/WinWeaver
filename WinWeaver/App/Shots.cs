#if HEADLESS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using AT = Avalonia.Threading;

namespace WinWeaver;

/// <summary>
/// Режим проверки без дисплея (Linux/CI): WinWeaver --shots &lt;папка&gt; [страницы через запятую].
/// Рендерит главное окно с демо-движком в PNG через Avalonia.Headless + Skia.
/// </summary>
internal static class Shots
{
	private static int _errors;
	private static void Pump(int ms)
	{
		// Настоящий цикл диспетчера (таймеры, Background-задачи) + тики рендера headless-платформы.
		var render = new AT.DispatcherTimer(TimeSpan.FromMilliseconds(16), AT.DispatcherPriority.Render, (s, e) => AvaloniaHeadlessPlatform.ForceRenderTimerTick());
		render.Start();
		using (var cts = new CancellationTokenSource(ms))
		{
			AT.Dispatcher.UIThread.MainLoop(cts.Token);
		}
		render.Stop();
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		AT.Dispatcher.UIThread.RunJobs();
	}

	private static void Save(Form f, string path)
	{
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		using var frame = f.Window.CaptureRenderedFrame();
		if (frame == null) throw new InvalidOperationException("No rendered frame: " + path);
		frame.Save(path);
		Console.WriteLine("shot " + path);
	}

	private static void Anim_Tick()
	{
		typeof(Anim).GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { null, EventArgs.Empty });
	}

	private static IEnumerable<Control> AllOf(Control c)
	{
		foreach (Control k in c.Controls)
		{
			yield return k;
			foreach (Control x in AllOf(k)) yield return x;
		}
	}

	private static void Dump(Control c, int depth)
	{
		if (depth > 2 || c == null) return;
		var v = c.View;
		Console.WriteLine(new string(' ', depth * 2) + c.GetType().Name + " vis=" + c.Visible + " b=" + c.Bounds + " view=" + v.Bounds + " isv=" + v.IsVisible + " arr=" + v.IsArrangeValid + " meas=" + v.IsMeasureValid + " parentView=" + (v.Parent?.GetType().Name ?? "null") + " kids=" + c.Controls.Count);
		Control p = c.Parent;
		if (depth == 0)
		{
			foreach (var av in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(c.Parent.View)) Console.WriteLine("  F> " + ((av as ControlView)?.Owner?.GetType().Name ?? av.GetType().Name) + " " + av.Bounds + " " + av.IsVisible);
			while (p != null) { Console.WriteLine("  ^ " + p.GetType().Name + " vis=" + p.Visible + " b=" + p.Bounds + " view=" + p.View.Bounds + " kidsAv=" + Avalonia.VisualTree.VisualExtensions.GetVisualChildren(p.View).Count()); p = p.Parent; }
		}
		foreach (Control k in c.Controls) Dump(k, depth + 1);
	}

	public static int Run(string dir)
	{
		_errors = 0;
		Directory.CreateDirectory(dir);
		AppBuilder.Configure<AvaloniaApp>()
			.UseSkia()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
			.SetupWithoutStarting();
		AT.Dispatcher.UIThread.UnhandledException += (s, e) =>
		{
			e.Handled = true;
			_errors++;
			Console.Error.WriteLine("UI ERROR " + e.Exception);
		};
		if (double.TryParse(Environment.GetEnvironmentVariable("WW_BMP_SCALE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double bs))
		{
			Bitmap.RenderScale = bs;
		}
		float scale = 1f;
		string requestedScale = Environment.GetEnvironmentVariable("WW_SCALE");
		if (!string.IsNullOrEmpty(requestedScale) &&
			(!float.TryParse(requestedScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale) || scale < 0.75f || scale > 2f))
			throw new ArgumentException("WW_SCALE must be between 0.75 and 2");
		Theme.Init(scale);
		L.Init();
		string lang = Environment.GetEnvironmentVariable("WW_SHOT_LANG");
		if (!string.IsNullOrWhiteSpace(lang)) L.Set(L.Resolve(lang), notify: false);
		var f = new MainForm(new FakeEngine(), "", fake: true);
		Program.Main0 = f;
		f.Show();
		if (Environment.GetEnvironmentVariable("WW_SHOT_BOOT") == "1")
		{
			var sw0 = System.Diagnostics.Stopwatch.StartNew();
			for (int b = 0; b < 16; b++)
			{
				Pump(150);
				Save(f, Path.Combine(dir, "boot_" + b.ToString("00") + "_" + sw0.ElapsedMilliseconds + ".png"));
			}
		}
		Pump(2500);
		Save(f, Path.Combine(dir, "00_start.png"));
		if (Environment.GetEnvironmentVariable("WW_SHOT_SNAP") == "1")
		{
			Transition.DebugSnap(f.Current, Path.Combine(dir, "snap_current.png"));
			Transition.DebugSnap(f.Current.Parent, Path.Combine(dir, "snap_parent.png"));
		}
		var keys = new List<string>(f.PageKeys);
		string only = Environment.GetEnvironmentVariable("WW_SHOT_PAGES");
		if (!string.IsNullOrEmpty(only))
		{
			keys = new List<string>(only.Split(','));
		}
		int i = 1;
		foreach (string key in keys)
		{
			try
			{
				if (key.StartsWith("palette"))
				{
					f.OpenPalette(key.Length > 8 ? key.Substring(8) : "");
					Pump(800);
					Save(f, Path.Combine(dir, (i++).ToString("00") + "_palette.png"));
					continue;
				}
				f.Show(key);
				if (Environment.GetEnvironmentVariable("WW_SHOT_SEQ") == "1")
				{
					for (int k = 0; k < 6; k++)
					{
						Pump(150);
						Save(f, Path.Combine(dir, i.ToString("00") + "_" + key + "_t" + k + ".png"));
					}
				}
				Pump(1200);
				if (Environment.GetEnvironmentVariable("WW_SHOT_DEBUG") == "1")
				{
					Dump(f.Current, 0);
					Save(f, Path.Combine(dir, i.ToString("00") + "_pre.png"));
					f.Current.Invalidate(true);
					Pump(200);
					Save(f, Path.Combine(dir, i.ToString("00") + "_inv.png"));
				}
				if (Environment.GetEnvironmentVariable("WW_SHOT_BMP") == "1")
				{
					foreach (Control c in AllOf(f.Current))
					{
						if (c is InspectorPanel ip)
						{
							foreach (string fn in new[] { "_cache", "_body" })
							{
								var fi = typeof(InspectorPanel).GetField(fn, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
								(fi?.GetValue(ip) as Bitmap)?.Save(Path.Combine(dir, "bmp" + fn + ".png"));
							}
						}
					}
				}
				Save(f, Path.Combine(dir, (i++).ToString("00") + "_" + key.Replace(":", "_") + ".png"));
			}
			catch (Exception ex)
			{
				_errors++;
				Console.Error.WriteLine("PAGE " + key + " ERROR " + ex);
			}
		}
		if (Environment.GetEnvironmentVariable("WW_SHOT_PERF") == "1")
		{
			foreach (string pk in new[] { "privacy", "apps", "home" })
			{
				f.Show(pk);
				Pump(1500);
				var w = f.Window;
				w.MouseMove(new Avalonia.Point(600, 500));
				Array.Clear(Graphics.OpCount, 0, 4);
				f.Refresh(); Pump(200);
				Console.WriteLine("OPS " + pk + " gclip=" + Graphics.OpCount[0] + " imgOpacity=" + Graphics.OpCount[1] + " textOpacity=" + Graphics.OpCount[2] + " text=" + Graphics.OpCount[3]);
				Control.PaintStats = new Dictionary<string, double[]>();
				var sw = Stopwatch.StartNew();
				int frames = 0;
				for (int k = 0; k < 120; k++)
				{
					if (k % 10 == 0) w.MouseWheel(new Avalonia.Point(600, 500), new Avalonia.Vector(0, k < 60 ? -1 : 1));
					AT.Dispatcher.UIThread.RunJobs();
					var t0 = sw.Elapsed;
					Anim_Tick();
					AT.Dispatcher.UIThread.RunJobs();
					AvaloniaHeadlessPlatform.ForceRenderTimerTick();
					frames++;
				}
				Console.WriteLine("PERF " + pk + ": " + (sw.Elapsed.TotalMilliseconds / frames).ToString("0.0") + " ms/frame");
				foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Control.PaintStats, x => x.Value[1]), 12))
					Console.WriteLine("   " + kv.Key + " n=" + kv.Value[0] + " ms=" + kv.Value[1].ToString("0.0"));
				Control.PaintStats = null;
			}
		}
		if (Environment.GetEnvironmentVariable("WW_SHOT_INPUT") == "1")
		{
			var w = f.Window;
			void Click(double x, double y)
			{
				w.MouseMove(new Avalonia.Point(x, y));
				w.MouseDown(new Avalonia.Point(x, y), Avalonia.Input.MouseButton.Left);
				w.MouseUp(new Avalonia.Point(x, y), Avalonia.Input.MouseButton.Left);
				Pump(700);
			}
			Click(90, 198);
			Save(f, Path.Combine(dir, "i1_click_privacy.png"));
			w.MouseMove(new Avalonia.Point(600, 500));
			for (int k = 0; k < 4; k++)
			{
				w.MouseWheel(new Avalonia.Point(600, 500), new Avalonia.Vector(0, -1));
				Pump(60);
			}
			Pump(700);
			Save(f, Path.Combine(dir, "i2_wheel.png"));
			Click(90, 236);
			Click(605, 408);
			Save(f, Path.Combine(dir, "i3_toggle.png"));
			w.MouseMove(new Avalonia.Point(260, 410));
			Pump(400);
			Save(f, Path.Combine(dir, "i4_hover.png"));
		}
		if (Environment.GetEnvironmentVariable("WW_SHOT_DIALOGS") == "1")
		{
			f.Show("home");
			Pump(800);
			Dialog(f, dir, "d1_info", () => WDialog.Info(f, "Готово", "Точка восстановления создана.\n\nМожно откатить изменения в разделе «Восстановление».", DlgIcon.Info));
			Dialog(f, dir, "d2_ask", () => WDialog.Ask(f, "Нужна перезагрузка", "Часть изменений вступит в силу только после перезагрузки компьютера.\n\nПерезагрузить сейчас?", "Перезагрузить", "Позже", DlgIcon.Warn));
			Dialog(f, dir, "d3_tune", () => TuneDialog.Ask(f, "optimal", "Оптимальная", "Чистая и быстрая Windows на каждый день", "\ue945"));
			Dialog(f, dir, "d4_check", () => CheckDialog.Ask(f, "Удалить «Discord»?", "Программа будет удалена вместе с остатками.", new CheckDialog.Row[]
			{
				new CheckDialog.Row { Text = "Удалить папки и файлы", Hint = "AppData, ProgramData", Checked = true },
				new CheckDialog.Row { Text = "Удалить записи реестра", Hint = "HKCU\\Software\\Discord", Checked = false },
			}, "Удалить"));
			Dialog(f, dir, "d5_pause", () => PauseDialog.Ask(f));
			Dialog(f, dir, "d6_support", () => { using var d = new SupportDialog(); d.ShowFor(f); });
			Dialog(f, dir, "d7_bug", () => { using var d = new BugReportDialog(); d.ShowFor(f); });
			Dialog(f, dir, "d8_welcome", () => new WelcomeDialog().ShowFor(null));
		}
		return _errors == 0 ? 0 : 1;
	}

	private static void Dialog(Form main, string dir, string name, Action open)
	{
		var t = new AT.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
		t.Tick += (s, e) =>
		{
			t.Stop();
			int n = 0;
			foreach (Form x in new List<Form>(Application.OpenForms))
			{
				if (x == main || x is ToastWindow)
				{
					continue;
				}
				if (x is Dimmer)
				{
					continue;
				}
				AvaloniaHeadlessPlatform.ForceRenderTimerTick();
				Save(x, Path.Combine(dir, name + (n++ == 0 ? "" : "_" + n) + ".png"));
				x.Close();
			}
		};
		t.Start();
		var render = new AT.DispatcherTimer(TimeSpan.FromMilliseconds(16), AT.DispatcherPriority.Render, (s, e) => AvaloniaHeadlessPlatform.ForceRenderTimerTick());
		render.Start();
		try
		{
			open();
		}
		catch (Exception ex)
		{
			_errors++;
			Console.Error.WriteLine("DIALOG " + name + " ERROR " + ex);
		}
		render.Stop();
		Pump(500);
	}
}
#endif
