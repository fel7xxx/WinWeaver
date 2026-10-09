// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WinWeaver
{
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
							bool flag = toggleSwitch.On;
							toggleSwitch.SimClick();
							num++;
							log.AppendLine("  toggle " + ((toggleSwitch.Parent as SettingCard)?.Title ?? "row") + ": " + flag + " -> clicked");
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
						if (item6 is FButton fButton && fButton.Enabled && fButton.Visible && fButton.Tag is ActionDef actionDef && actionDef.Confirm == null && actionDef.Ui == null)
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
						if (text.StartsWith("tab:"))
						{
							string[] pp = text.Split(':');
							(f as MainForm)?.Show(pp[1]);
							(f as MainForm)?.Current?.SelectTab(int.Parse(pp[2]), false);
						}
						else if (text.StartsWith("read:"))
						{
							(f as MainForm)?.Show(text.Substring(5));
							(f as MainForm)?.Current?.GateRead();
						}
						else if (text.StartsWith("body:"))
						{
							string[] pp = text.Split(':');
							(f as MainForm)?.Show(pp[1]);
							(f as MainForm)?.Current?.SelectTab(int.Parse(pp[2]), false);
							Timer st2 = new Timer { Interval = 900 };
							st2.Tick += delegate
							{
								st2.Stop();
								Transition.DebugSnap(f.Current.Body, Path.Combine(dir, "body_" + pp[1] + pp[2] + ".png"));
							};
							st2.Start();
						}
						else if (text == "pausedlg")
						{
							f.BeginInvoke((Action)delegate { PauseDialog.Ask(f); });
						}
						else if (text.StartsWith("snap"))
						{
							(f as MainForm)?.Show(text.Substring(5));
							Timer st = new Timer { Interval = 500 };
							st.Tick += delegate
							{
								st.Stop();
								Transition.DebugSnap(f.Current, Path.Combine(dir, "snap_" + text.Substring(5) + ".png"));
							};
							st.Start();
						}
						else if (text == "menu")
						{
							f.BeginInvoke((Action)delegate { (f as MainForm)?.DemoMenu(); });
						}
						else if (text == "queue" || text == "queuec")
						{
							(f as MainForm)?.DemoQueue(text == "queue");
						}
						else if (text.StartsWith("toast"))
						{
							(f as MainForm)?.DemoToast(text == "toastb");
						}
						else if (text.StartsWith("dlg"))
						{
							f.BeginInvoke((Action)delegate
							{
								if (text == "dlgf")
								{
									(f as MainForm)?.DemoFps();
								}
								else if (text == "dlgr")
								{
									(f as MainForm)?.DemoRec();
								}
								else if (text == "dlgc")
								{
									CheckDialog.Ask(f, "Удалить Microsoft Teams?", "Программа будет удалена. Затем можно убрать оставшиеся файлы и записи реестра.", new CheckDialog.Row[3]
									{
										new CheckDialog.Row { Text = "Создать точку восстановления", Hint = "\nна случай, если что-то пойдёт не так", Checked = true },
										new CheckDialog.Row { Text = "Найти и удалить остатки", Hint = "\nпапки в AppData и ключи реестра", Checked = true },
										new CheckDialog.Row { Text = "Удалить данные пользователя", Hint = "\nнастройки и кэш программы", Checked = false }
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
									WDialog.Ask(f, "Автозапуск " + Brand.Name, Brand.Name + " открылся автоматически при входе в систему.\n\nОставить автозапуск? Изменить это можно в любой момент в разделе «Настройки».", "Оставить автозапуск", "Не запускать");
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
									(f as MainForm)?.Demo(what);
								}
								catch (Exception dex)
								{
									Console.Error.WriteLine("DEMO " + what + ": " + dex);
								}
							});
							Timer c3 = new Timer { Interval = 2400 };
							c3.Tick += delegate
							{
								c3.Stop();
								for (int k = Application.OpenForms.Count - 1; k >= 0; k--)
								{
									Form of = Application.OpenForms[k];
									if (!(of is MainForm) && !(of is Dimmer) && of.Modal)
									{
										of.DialogResult = DialogResult.Cancel;
										of.Close();
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
							string[] pp = text.Split('@');
							f.ShowTab(pp[0], int.Parse(pp[1]));
						}
						else if (text.StartsWith("exp:"))
						{
							int want = int.Parse(text.Substring(4));
							int k = 0;
							foreach (IBlock b in f.Current.Blocks)
							{
								SettingCard sc = b as SettingCard;
								if (sc != null && sc.Visible && sc.HasInfo)
								{
									if (k == want)
									{
										sc.SetExpanded(true, animate: false);
									}
									k++;
								}
							}
						}
						else if (text.StartsWith("scroll:"))
						{
							f.Current.ScrollBy(int.Parse(text.Substring(7)));
						}
						else
						{
							f.Show(text);
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
}
