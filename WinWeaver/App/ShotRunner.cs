using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinWeaver;

internal static class ShotRunner
{
	private static IEnumerable<Control> All(Control c)
	{
		foreach (Control x in c.Controls)
		{
			yield return x;
			foreach (Control item in All(x))
			{
				yield return item;
			}
		}
	}

	private static void SelfTest(MainForm f, string dir)
	{
		StringBuilder log = new StringBuilder();
		List<string> list = new List<string>(f.PageKeys);
		Queue<Action> steps = new Queue<Action>();
		steps.Enqueue(delegate
		{
		});
		foreach (string item in list)
		{
			string key = item;
			steps.Enqueue(delegate
			{
				f.Show(key);
				log.AppendLine("PAGE " + key);
			});
			steps.Enqueue(delegate
			{
				int num = 0;
				foreach (Control item2 in All(f.Current))
				{
					if (item2 is ToggleSwitch && item2.Visible)
					{
						ToggleSwitch toggleSwitch = (ToggleSwitch)item2;
						bool on = toggleSwitch.On;
						toggleSwitch.SimClick();
						num++;
						log.AppendLine("  toggle " + ((toggleSwitch.Parent as SettingCard)?.Title ?? "row") + ": " + on + " -> clicked");
					}
				}
				log.AppendLine("  toggles=" + num);
			});
			steps.Enqueue(delegate
			{
				foreach (Control item3 in All(f.Current))
				{
					if (item3 is ToggleSwitch)
					{
						log.AppendLine("  state " + ((item3.Parent as SettingCard)?.Title ?? "row") + " = " + ((ToggleSwitch)item3).On);
					}
				}
			});
			steps.Enqueue(delegate
			{
				foreach (Control item4 in All(f.Current))
				{
					if (item4 is Segment)
					{
						Segment segment = (Segment)item4;
						int i = (segment.Selected + 1) % segment.Items.Length;
						segment.SimSelect(i);
						log.AppendLine("  segment " + ((segment.Parent as SettingCard)?.Title ?? "row") + " -> " + i);
					}
				}
			});
			steps.Enqueue(delegate
			{
				foreach (Control item5 in All(f.Current))
				{
					if (item5 is Segment)
					{
						log.AppendLine("  segstate " + ((item5.Parent as SettingCard)?.Title ?? "row") + " = " + ((Segment)item5).Selected);
					}
				}
			});
			steps.Enqueue(delegate
			{
				foreach (Control item6 in All(f.Current))
				{
					if (item6 is FButton { Enabled: not false, Visible: not false, Tag: ActionDef { Confirm: null, Ui: null } } fButton)
					{
						log.AppendLine("  button " + fButton.Text);
						fButton.PerformClick();
						break;
					}
				}
			});
		}
		Timer tm = new Timer
		{
			Interval = 700
		};
		tm.Tick += delegate
		{
			if (steps.Count == 0)
			{
				tm.Stop();
				File.WriteAllText(Path.Combine(dir, "selftest.txt"), log.ToString());
				File.WriteAllText(Path.Combine(dir, "done"), "");
				f.Close();
				return;
			}
			try
			{
				steps.Dequeue()();
			}
			catch (Exception ex)
			{
				log.AppendLine("  EXCEPTION " + ex);
			}
		};
		tm.Start();
	}

	public static void Attach(MainForm f, string dir)
	{
		Directory.CreateDirectory(dir);
		List<string> keys = new List<string>(f.PageKeys);
		int i = -1;
		bool mark = false;
		Timer t = new Timer
		{
			Interval = 1300
		};
		string environmentVariable = Environment.GetEnvironmentVariable("WW_SHOT_PAGES");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			keys = new List<string>(environmentVariable.Split(','));
		}
		t.Tick += delegate
		{
			if (mark)
			{
				mark = false;
				File.WriteAllText(Path.Combine(dir, "ready_" + i.ToString("00") + "_" + keys[i].Replace(":", "_").Replace("@", "_")), "");
			}
			else
			{
				mark = true;
				i++;
				if (i >= keys.Count)
				{
					t.Stop();
					File.WriteAllText(Path.Combine(dir, "done"), "");
					Timer timer = new Timer();
					timer.Interval = 1500;
					timer.Tick += delegate
					{
						f.Close();
					};
					timer.Start();
				}
				else
				{
					string text = keys[i];
					if (text.StartsWith("exp:"))
					{
						string[] pp2 = text.Split(':');
						f?.Show(pp2[1]);
						int want = ((pp2.Length > 2) ? int.Parse(pp2[2]) : 0);
						int num = 0;
						{
							foreach (Control item in All(f.Current))
							{
								if (item is SettingCard { Visible: not false, HasInfo: not false } settingCard && num++ == want)
								{
									if (!settingCard.Expanded)
									{
										settingCard.SetExpanded(on: true, animate: false);
									}
									SettingCard snap = settingCard;
									Timer st3 = new Timer
									{
										Interval = 1100
									};
									st3.Tick += delegate
									{
										st3.Stop();
										Transition.DebugSnap(snap, Path.Combine(dir, "card_" + pp2[1] + want + ".png"));
									};
									st3.Start();
									break;
								}
							}
							return;
						}
					}
					if (text.StartsWith("bench:"))
					{
						f?.Show(text.Substring(6));
						Page current = f.Current;
						Stopwatch stopwatch = Stopwatch.StartNew();
						int num2 = 0;
						for (int j = 0; j < 60; j++)
						{
							current.ScrollBy((j < 30) ? Theme.S(24f) : (-Theme.S(24f)));
							current.Update();
							current.Body.Update();
							foreach (Control control in current.Controls)
							{
								control.Update();
							}
							num2++;
						}
						File.AppendAllText(Path.Combine(dir, "bench.txt"), text + " " + ((double)stopwatch.ElapsedMilliseconds / (double)num2).ToString("0.0") + " ms/frame\n");
					}
					else if (text.StartsWith("tab:"))
					{
						string[] array = text.Split(':');
						f?.Show(array[1]);
						f?.Current?.SelectTab(int.Parse(array[2]), animate: false);
					}
					else if (text.StartsWith("read:"))
					{
						f?.Show(text.Substring(5));
						f?.Current?.GateRead();
					}
					else if (text.StartsWith("body:"))
					{
						string[] pp = text.Split(':');
						f?.Show(pp[1]);
						f?.Current?.SelectTab(int.Parse(pp[2]), animate: false);
						Timer st2 = new Timer
						{
							Interval = 900
						};
						st2.Tick += delegate
						{
							st2.Stop();
							Transition.DebugSnap(f.Current.Body, Path.Combine(dir, "body_" + pp[1] + pp[2] + ".png"));
						};
						st2.Start();
					}
					else if (text == "pausedlg")
					{
						f.BeginInvoke((Action)delegate
						{
							PauseDialog.Ask(f);
						});
					}
					else if (text.StartsWith("snap"))
					{
						f?.Show(text.Substring(5));
						Timer st = new Timer
						{
							Interval = 500
						};
						st.Tick += delegate
						{
							st.Stop();
							Transition.DebugSnap(f.Current, Path.Combine(dir, "snap_" + text.Substring(5) + ".png"));
						};
						st.Start();
					}
					else if (text == "menu")
					{
						f.BeginInvoke((Action)delegate
						{
							f?.DemoMenu();
						});
					}
					else if (text == "queue" || text == "queuec")
					{
						f?.DemoQueue(text == "queue");
					}
					else if (text.StartsWith("toast"))
					{
						f?.DemoToast(text == "toastb");
					}
					else if (text.StartsWith("dlg"))
					{
						f.BeginInvoke((Action)delegate
						{
							if (text == "dlgf")
							{
								f?.DemoFps();
							}
							else if (text == "dlgr")
							{
								f?.DemoRec();
							}
							else if (text == "dlgc")
							{
								CheckDialog.Ask(f, "Удалить Microsoft Teams?", "Программа будет удалена. Затем можно убрать оставшиеся файлы и записи реестра.", new CheckDialog.Row[3]
								{
									new CheckDialog.Row
									{
										Text = "Создать точку восстановления",
										Hint = "\nна случай, если что-то пойдёт не так",
										Checked = true
									},
									new CheckDialog.Row
									{
										Text = "Найти и удалить остатки",
										Hint = "\nпапки в AppData и ключи реестра",
										Checked = true
									},
									new CheckDialog.Row
									{
										Text = "Удалить данные пользователя",
										Hint = "\nнастройки и кэш программы",
										Checked = false
									}
								}, "Удалить", "Отмена", DlgIcon.Warn);
							}
							else if (text == "dlgb")
							{
								WDialog.Choose(f, "Перезагрузить, чтобы завершить «Оптимальная»?", "Политики, службы и удалённые приложения окончательно применятся только после перезагрузки. Сохрани открытые документы — программы закроются.\n\nЕсли сейчас неудобно, напоминание останется в меню слева.", "Перезагрузить сейчас", "Через 10 минут", "Позже", DlgIcon.Warn);
							}
							else if (text == "dlgw")
							{
								WDialog.Ask(f, "Откатить систему?", "Точка №12: 14 мая, 18:40 — «Перед автонастройкой».\n\nКомпьютер перезагрузится, несохранённые данные будут потеряны.", "Откатить", "Отмена", DlgIcon.Warn);
							}
							else
							{
								WDialog.Ask(f, "Автозапуск WinWeaver", "WinWeaver открылся автоматически при входе в систему.\n\nОставить автозапуск? Изменить это можно в любой момент в разделе «Настройки».", "Оставить автозапуск", "Не запускать");
							}
						});
						Timer c = new Timer
						{
							Interval = 2200
						};
						c.Tick += delegate
						{
							c.Stop();
							foreach (Form openForm in Application.OpenForms)
							{
								if (openForm is WDialog || openForm is CheckDialog || openForm is PauseDialog)
								{
									openForm.Close();
									break;
								}
							}
						};
						c.Start();
					}
					else if (text.StartsWith("show:"))
					{
						string what = text.Substring(5);
						f.BeginInvoke((Action)delegate
						{
							try
							{
								f?.Demo(what);
							}
							catch (Exception ex)
							{
								Console.Error.WriteLine("DEMO " + what + ": " + ex);
							}
						});
						Timer c3 = new Timer
						{
							Interval = 2400
						};
						c3.Tick += delegate
						{
							c3.Stop();
							for (int num5 = Application.OpenForms.Count - 1; num5 >= 0; num5--)
							{
								Form form2 = Application.OpenForms[num5];
								if (!(form2 is MainForm) && !(form2 is Dimmer) && form2.Modal)
								{
									form2.DialogResult = DialogResult.Cancel;
									form2.Close();
									break;
								}
							}
						};
						c3.Start();
					}
					else if (text.StartsWith("tune:"))
					{
						string lv = text.Substring(5);
						f.BeginInvoke((Action)delegate
						{
							f.OpenTune(lv);
						});
						Timer c2 = new Timer
						{
							Interval = 2400
						};
						c2.Tick += delegate
						{
							c2.Stop();
							foreach (Form openForm2 in Application.OpenForms)
							{
								if (openForm2 is TuneDialog)
								{
									openForm2.DialogResult = DialogResult.Cancel;
									openForm2.Close();
									break;
								}
							}
						};
						c2.Start();
					}
					else if (text.StartsWith("run:"))
					{
						f.StartLevel(text.Substring(4), "firefox");
					}
					else if (text.Contains("@"))
					{
						string[] array2 = text.Split('@');
						f.ShowTab(array2[0], int.Parse(array2[1]));
					}
					else
					{
						if (text.StartsWith("exp:"))
						{
							int num3 = int.Parse(text.Substring(4));
							int num4 = 0;
							{
								foreach (IBlock block in f.Current.Blocks)
								{
									if (block is SettingCard { Visible: not false, HasInfo: not false } settingCard2)
									{
										if (num4 == num3)
										{
											settingCard2.SetExpanded(on: true, animate: false);
										}
										num4++;
									}
								}
								return;
							}
						}
						if (text.StartsWith("scroll:"))
						{
							f.Current.ScrollBy(int.Parse(text.Substring(7)));
						}
						else
						{
							f.Show(text);
						}
					}
				}
			}
		};
		f.Shown += delegate
		{
			if (Environment.GetEnvironmentVariable("WW_SELFTEST") == "1")
			{
				SelfTest(f, dir);
			}
			else
			{
				t.Start();
			}
		};
	}
}
