// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

// WinWeaver — Windows 11 setup tool
// Copyright © 2026 fel7xxx. Licensed under GPL-3.0-or-later, see LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WinWeaver
{
	// Settings page, profile export/import, "what Windows changed back", updates, support, uninstall
	internal partial class MainForm
	{
		private bool _extrasWired;

		private UpdateChecker.Result _update;

		private void WireExtras()
		{
			if (_extrasWired)
			{
				return;
			}
			_extrasWired = true;
			_side.SupportClicked += ShowSupport;
			_side.UpdateClicked += delegate
			{
				Shell.Open((_update != null && _update.Url != null) ? _update.Url : Brand.Releases);
			};
			L.Changed += delegate
			{
				if (IsDisposed || !IsHandleCreated)
				{
					return;
				}
				BeginInvoke((Action)delegate
				{
					foreach (Page p in _pages.Values)
					{
						p.DoLayoutBlocks();
					}
					Invalidate(invalidateChildren: true);
				});
			};
		}

		// called once the start screen is gone
		private void AfterLoadedExtras()
		{
			if (Mode == "/uninstall")
			{
				BeginInvoke((Action)StartUninstall);
				return;
			}
			if (UpdateChecker.Due())
			{
				CheckUpdates(manual: false);
			}
			_host.Worker.Run((IEngine e) => e.As(e.Eval("Test-FBuildChanged"), fallback: false), delegate (object r)
			{
				if (r is bool && (bool)r)
				{
					CheckDrift(afterUpdate: true);
				}
			});
		}

		// screenshots of dialogs (ShotRunner "show:<name>")
		public void Demo(string what)
		{
			switch (what)
			{
			case "welcome":
				using (WelcomeDialog d = new WelcomeDialog())
				{
					d.ShowFor(this);
				}
				break;
			case "personalize":
			{
				string o = "7zip,ext,temp;def=0";
				PersonalizeDialog.Ask(this, "Оптимальная", ref o, out string _, out string _, null);
				break;
			}
			case "support":
				ShowSupport();
				break;
			case "bug":
				ShowBugReport();
				break;
			case "expert":
				ConfirmExpert();
				break;
			case "drift":
				CheckDrift(afterUpdate: true);
				break;
			case "uninstall":
				WDialog.Choose(this, L.T("Удалить {0}?").Replace("{0}", Brand.Name), "Будут удалены задачи планировщика, автозапуск, ярлыки, схема питания WinWeaver, папка программы и папка данных (история, журнал).\n\nМожно сначала откатить все изменения из «Истории» — система вернётся к состоянию до WinWeaver (удалённые приложения Windows придётся поставить из Microsoft Store).", "Откатить всё и удалить", "Только удалить", "Отмена", DlgIcon.Warn);
				break;
			case "update":
				_update = new UpdateChecker.Result { Tag = "v1.1.0", Url = Brand.Releases, Newer = true };
				_side.SetUpdate(L.T("Доступна версия {0}").Replace("{0}", "1.1.0"), "Открыть страницу загрузки");
				break;
			}
		}

		public void ShowSupport()
		{
			using (SupportDialog d = new SupportDialog())
			{
				d.ShowFor(this);
			}
		}

		public void ShowBugReport()
		{
			using (BugReportDialog d = new BugReportDialog())
			{
				d.ShowFor(this);
			}
		}

		// ---------- updates (notification only) ----------
		private void CheckUpdates(bool manual)
		{
			if (manual)
			{
				_status.Set("ok", "Проверяю обновления на GitHub…");
			}
			UpdateChecker.CheckAsync(delegate (UpdateChecker.Result r)
			{
				if (IsDisposed || !IsHandleCreated)
				{
					return;
				}
				BeginInvoke((Action)delegate
				{
					if (r.Error == null)
					{
						Prefs.LastUpdateCheck = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
					}
					if (r.Error != null)
					{
						if (manual)
						{
							_status.Set("warn", "Не удалось проверить обновления: нет связи с GitHub");
						}
						return;
					}
					if (!r.Newer)
					{
						if (manual)
						{
							_status.Set("ok", L.T("Установлена последняя версия {0}").Replace("{0}", Brand.Version));
						}
						return;
					}
					_update = r;
					_side.SetUpdate(L.T("Доступна версия {0}").Replace("{0}", r.Tag.TrimStart('v', 'V')), "Открыть страницу загрузки");
					if (manual || Prefs.SkipVersion != r.Tag)
					{
						int c = WDialog.Choose(this, L.T("Вышла версия {0}").Replace("{0}", r.Tag.TrimStart('v', 'V')), L.T("Сейчас установлена {0}. Новую версию можно скачать со страницы релиза на GitHub — там же список изменений.\n\nПрограмма ничего не скачивает и не обновляет сама.").Replace("{0}", Brand.Version), "Открыть страницу", "Пропустить эту версию", "Позже", DlgIcon.Info);
						if (c == 0)
						{
							Shell.Open(r.Url ?? Brand.Releases);
						}
						else if (c == 1)
						{
							Prefs.SkipVersion = r.Tag;
						}
					}
				});
			});
		}

		// ---------- settings that Windows turned back ----------
		private void CheckDrift(bool afterUpdate)
		{
			if (!afterUpdate && !_host.TryBegin(quick: true, title: "Проверка настроек"))
			{
				return;
			}
			_host.Worker.Run((IEngine e) => e.Eval("Get-FDrift"), delegate (object r)
			{
				if (!afterUpdate)
				{
					_host.End(quick: true);
				}
				List<Hashtable> items = new List<Hashtable>();
				if (r is IEnumerable en && !(r is string))
				{
					foreach (object o in en)
					{
						if (o is Hashtable h)
						{
							items.Add(h);
						}
					}
				}
				if (items.Count == 0)
				{
					if (!afterUpdate)
					{
						_status.Set("ok", "Все настройки на месте — Windows ничего не вернула");
					}
					return;
				}
				CheckDialog.Row[] rows = new CheckDialog.Row[items.Count];
				for (int i = 0; i < rows.Length; i++)
				{
					rows[i] = new CheckDialog.Row { Text = Convert.ToString(items[i]["Title"]), Checked = true };
				}
				string title = afterUpdate ? "Windows обновилась и вернула часть настроек" : "Windows вернула часть настроек";
				string body = L.T("Эти изменения были сделаны в {0}, но сейчас снова выключены или вернулись к значению Windows ({1} шт.). Отметь, что применить заново — каждое снова попадёт в «Историю».").Replace("{0}", Brand.Name).Replace("{1}", items.Count.ToString());
				bool[] pick = CheckDialog.Ask(this, title, body, rows, "Применить заново", "Не сейчас", DlgIcon.Warn);
				if (pick == null)
				{
					return;
				}
				List<string> ids = new List<string>();
				for (int i = 0; i < pick.Length; i++)
				{
					if (pick[i])
					{
						ids.Add(Convert.ToString(items[i]["Id"]));
					}
				}
				if (ids.Count == 0 || !_host.TryBegin(title: "Возврат настроек после обновления Windows"))
				{
					return;
				}
				_host.Worker.Run(delegate (IEngine e)
				{
					e.Eval("[void](Repair-FDrift $fa1)", new Hashtable { { "fa1", ids.ToArray() } });
					return (object)null;
				}, delegate
				{
					_host.End();
					_cur?.Refresh(_host.Worker);
				});
			});
		}

		// ---------- profile export / import (values only, no code) ----------
		private List<SettingCard> TrackedCards()
		{
			List<SettingCard> l = new List<SettingCard>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (Page p in _pages.Values)
			{
				if (p.Key == "review")
				{
					continue;
				}
				foreach (IBlock b in Flatten(p.Blocks))
				{
					if (b is SettingCard sc && sc.GetScript != null && !sc.Unsupported && seen.Add(sc.Title))
					{
						l.Add(sc);
					}
				}
			}
			return l;
		}

		private static IEnumerable<IBlock> Flatten(IEnumerable<IBlock> blocks)
		{
			foreach (IBlock b in blocks)
			{
				if (b is Grid g)
				{
					foreach (IBlock x in Flatten(g.Items))
					{
						yield return x;
					}
				}
				else
				{
					yield return b;
				}
			}
		}

		private static string J(string s)
		{
			StringBuilder sb = new StringBuilder("\"");
			foreach (char c in s ?? "")
			{
				if (c == '"' || c == '\\')
				{
					sb.Append('\\').Append(c);
				}
				else if (c < ' ')
				{
					sb.Append("\\u").Append(((int)c).ToString("x4"));
				}
				else
				{
					sb.Append(c);
				}
			}
			return sb.Append('"').ToString();
		}

		private static string UnJ(string s)
		{
			return Regex.Replace(s, "\\\\(u([0-9a-fA-F]{4})|.)", (Match m) => m.Groups[2].Success ? ((char)Convert.ToInt32(m.Groups[2].Value, 16)).ToString() : m.Groups[1].Value);
		}

		private void ExportProfile()
		{
			string path;
			using (SaveFileDialog d = new SaveFileDialog { Filter = L.T("Профиль WinWeaver") + " (*.json)|*.json", FileName = "WinWeaver-profile-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json" })
			{
				if (d.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				path = d.FileName;
			}
			if (!_host.TryBegin(title: "Экспорт профиля"))
			{
				return;
			}
			List<SettingCard> cards = TrackedCards();
			_host.Worker.Run(delegate (IEngine e)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("{\n  \"app\": \"WinWeaver\",\n  \"format\": 1,\n  \"version\": ").Append(J(Brand.Version)).Append(",\n  \"created\": ").Append(J(DateTime.Now.ToString("s"))).Append(",\n  \"settings\": {");
				int n = 0;
				foreach (SettingCard c in cards)
				{
					object v = null;
					try
					{
						v = c.Fetch(e);
					}
					catch
					{
					}
					string val = (v is bool) ? (((bool)v) ? "true" : "false") : ((v is int && (int)v >= 0) ? ((int)v).ToString(CultureInfo.InvariantCulture) : null);
					if (val == null)
					{
						continue;
					}
					sb.Append((n++ == 0) ? "\n" : ",\n").Append("    ").Append(J(c.Title)).Append(": ").Append(val);
				}
				sb.Append("\n  }\n}\n");
				File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
				return n;
			}, delegate (object r)
			{
				_host.End();
				_status.Set("ok", L.T("Профиль сохранён: {0} настроек").Replace("{0}", Convert.ToString(r)));
			});
		}

		private void ImportProfile()
		{
			string text;
			using (OpenFileDialog d = new OpenFileDialog { Filter = L.T("Профиль WinWeaver") + " (*.json)|*.json" })
			{
				if (d.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				try
				{
					if (new FileInfo(d.FileName).Length > 1048576)
					{
						throw new Exception("file too large");
					}
					text = File.ReadAllText(d.FileName, Encoding.UTF8);
				}
				catch (Exception ex)
				{
					WDialog.Info(this, "Не удалось открыть профиль", ex.Message, DlgIcon.Error);
					return;
				}
			}
			if (!text.Contains("\"WinWeaver\"") || text.IndexOf("\"settings\"", StringComparison.Ordinal) < 0)
			{
				WDialog.Info(this, "Это не профиль WinWeaver", "Выбери файл, сохранённый кнопкой «Сохранить профиль».", DlgIcon.Warn);
				return;
			}
			Dictionary<string, SettingCard> byTitle = new Dictionary<string, SettingCard>(StringComparer.Ordinal);
			foreach (SettingCard c in TrackedCards())
			{
				byTitle[c.Title] = c;
			}
			string body = text.Substring(text.IndexOf("\"settings\"", StringComparison.Ordinal));
			List<KeyValuePair<SettingCard, object>> wanted = new List<KeyValuePair<SettingCard, object>>();
			int skipped = 0;
			foreach (Match m in Regex.Matches(body, "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*(true|false|\\d{1,2})"))
			{
				string title = UnJ(m.Groups[1].Value);
				if (!byTitle.TryGetValue(title, out SettingCard c) || (c.IsExpert && !Prefs.Expert))
				{
					skipped++;
					continue;
				}
				string raw = m.Groups[2].Value;
				object v;
				if (c.Seg != null)
				{
					int i;
					if (!int.TryParse(raw, out i) || i < 0 || i >= c.Seg.Items.Length)
					{
						skipped++;
						continue;
					}
					v = i;
				}
				else
				{
					if (raw != "true" && raw != "false")
					{
						skipped++;
						continue;
					}
					v = raw == "true";
				}
				wanted.Add(new KeyValuePair<SettingCard, object>(c, v));
			}
			if (!_host.TryBegin(quick: true, title: "Сравнение профиля"))
			{
				return;
			}
			_host.Worker.Run(delegate (IEngine e)
			{
				List<KeyValuePair<SettingCard, object>> diff = new List<KeyValuePair<SettingCard, object>>();
				foreach (KeyValuePair<SettingCard, object> kv in wanted)
				{
					object now = null;
					try
					{
						now = kv.Key.Fetch(e);
					}
					catch
					{
					}
					if (!object.Equals(now, kv.Value))
					{
						diff.Add(kv);
					}
				}
				return diff;
			}, delegate (object r)
			{
				_host.End(quick: true);
				List<KeyValuePair<SettingCard, object>> diff = (List<KeyValuePair<SettingCard, object>>)r;
				if (diff.Count == 0)
				{
					_status.Set("ok", "Профиль совпадает с текущими настройками — менять нечего");
					return;
				}
				CheckDialog.Row[] rows = new CheckDialog.Row[diff.Count];
				for (int i = 0; i < rows.Length; i++)
				{
					SettingCard c = diff[i].Key;
					object v = diff[i].Value;
					rows[i] = new CheckDialog.Row { Text = c.Title, Tag = (c.Seg != null) ? c.Seg.Items[(int)v] : (((bool)v) ? "включить" : "выключить"), Checked = true };
				}
				string note = L.T("Из профиля будет изменено настроек: {0}. Каждое изменение попадёт в «Историю», и его можно будет отменить.").Replace("{0}", diff.Count.ToString());
				if (skipped > 0)
				{
					note += " " + L.T("Пропущено: {0} (нет в этой версии или нужен экспертный режим).").Replace("{0}", skipped.ToString());
				}
				bool[] pick = CheckDialog.Ask(this, "Применить профиль?", note, rows, "Применить");
				if (pick == null || !_host.TryBegin(title: "Импорт профиля"))
				{
					return;
				}
				_host.Worker.Run(delegate (IEngine e)
				{
					e.Eval("Start-FGroup 'Импорт профиля настроек'");
					try
					{
						for (int i = 0; i < pick.Length; i++)
						{
							if (pick[i])
							{
								diff[i].Key.SetValue(e, diff[i].Value);
							}
						}
					}
					finally
					{
						e.Eval("Stop-FGroup");
					}
					return (object)null;
				}, delegate
				{
					_host.End();
					foreach (Page p in _pages.Values)
					{
						p.ForgetData();
					}
					_cur?.Refresh(_host.Worker);
					_status.Set("ok", "Профиль применён");
				});
			});
		}

		// ---------- expert mode ----------
		private bool ConfirmExpert()
		{
			return WDialog.Ask(this, "Включить экспертный режим?", "Появятся настройки, которые ослабляют защиту или могут сломать программы: полное отключение Defender, выключение VBS/HVCI и защиты от Spectre, «Не спрашивать» в UAC, отключение служб и сжатия памяти, расшифровка BitLocker.\n\nВключай, только если понимаешь, что делает каждая из них. Любое изменение по-прежнему можно отменить в «Истории».", "Включить", "Отмена", DlgIcon.Warn);
		}

		private void ExpertChanged()
		{
			foreach (Page p in _pages.Values)
			{
				p.DoLayoutBlocks();
			}
			_status.Set(Prefs.Expert ? "warn" : "ok", Prefs.Expert ? "Экспертный режим включён — рискованные настройки показаны" : "Экспертный режим выключен — рискованные настройки скрыты");
		}

		// ---------- uninstall ----------
		public void StartUninstall()
		{
			int c = WDialog.Choose(this, L.T("Удалить {0}?").Replace("{0}", Brand.Name), "Будут удалены задачи планировщика, автозапуск, ярлыки, схема питания WinWeaver, папка программы и папка данных (история, журнал).\n\nМожно сначала откатить все изменения из «Истории» — система вернётся к состоянию до WinWeaver (удалённые приложения Windows придётся поставить из Microsoft Store).", "Откатить всё и удалить", "Только удалить", "Отмена", DlgIcon.Warn);
			if (c == 2)
			{
				if (Mode == "/uninstall")
				{
					Close();
				}
				return;
			}
			if (!_host.TryBegin(title: L.T("Удаление {0}").Replace("{0}", Brand.Name)))
			{
				return;
			}
			bool undo = c == 0;
			_host.Worker.Run((IEngine e) => e.As(e.Eval("Uninstall-FWinWeaver $fa1", new Hashtable { { "fa1", undo } }), fallback: false), delegate (object r)
			{
				_host.End();
				if (!_fake)
				{
					try
					{
						Installer.RemoveFilesAfterExit();
					}
					catch (Exception ex)
					{
						Program.Log("REMOVE ERR " + ex.Message);
					}
				}
				WDialog.Info(this, L.T("{0} удалён").Replace("{0}", Brand.Name), "Программа и её данные удалены. Чтобы всё вступило в силу, перезагрузи компьютер.\n\nСпасибо, что пользовался!", DlgIcon.Info);
				_skipCloseAsk = true;
				Close();
			});
		}

		private bool _skipCloseAsk;

		// ---------- install from the settings page (portable copy) ----------
		private void InstallFromSettings()
		{
			if (_fake)
			{
				_status.Set("ok", "Демо-режим: установка пропущена");
				return;
			}
			try
			{
				string exe = Installer.Install();
				Prefs.InstallChoice = "installed";
				if (WDialog.Ask(this, L.T("{0} установлен").Replace("{0}", Brand.Name), "Программа скопирована в Program Files, ярлык добавлен в Пуск. Перезапустить из установленной копии?", "Перезапустить", "Позже"))
				{
					_skipCloseAsk = true;
					if (Installer.Relaunch(exe))
					{
						Close();
					}
				}
			}
			catch (Exception ex)
			{
				WDialog.Info(this, "Не удалось установить", ex.Message, DlgIcon.Error);
			}
		}

		// ---------- settings page ----------
		private void BuildSettings(Page page)
		{
			page.Add(new AboutCard(_side.Logo, this));
			page.Add(new SectionLabel("Язык"));
			List<string> codes = L.Available();
			List<string> names = new List<string> { "Как в Windows" };
			foreach (string code in codes)
			{
				names.Add(L.NameOf(code));
			}
			page.Add(new SettingCard(_host, "Язык интерфейса", "«Как в Windows» — русский для русской Windows, английский для остальных", delegate (IEngine e)
			{
				string p = Prefs.Lang;
				return string.IsNullOrEmpty(p) ? 0 : (codes.IndexOf(p) + 1);
			}, delegate (IEngine e, object v)
			{
				int i = (int)v;
				string lang = (i <= 0 || i > codes.Count) ? "" : codes[i - 1];
				Prefs.Lang = lang;
				L.Set(L.Resolve(lang));
			}, names.ToArray()));
			page.Add(new SectionLabel("Запуск"));
			if (Installer.RunningInstalled || _fake)
			{
				page.Add(new SettingCard(_host, "Открывать программу при входе в систему", "Без запроса UAC — через планировщик задач", (IEngine e) => Prefs.AutoChoice == "yes", delegate (IEngine e, object v)
				{
					if ((bool)v)
					{
						e.Eval("[void](Install-FPanelTask)");
						Prefs.AutoChoice = "yes";
					}
					else
					{
						e.Eval("Unregister-ScheduledTask -TaskName 'WinWeaver' -TaskPath '\\WinWeaver\\' -Confirm:$false -ErrorAction SilentlyContinue");
						Prefs.AutoChoice = "no";
					}
					e.Eval("Show-Ok ('Автозапуск: ' + (Get-FOnOff $v))", new Hashtable { { "v", v } });
				}));
			}
			else
			{
				TilesView it = page.Add(new TilesView(_host));
				it.TileH = 124;
				it.AddUi(Glyph.Apps, Installer.IsInstalled ? "Обновить установленную копию" : "Установить в Program Files", "Ярлык в Пуске, автозапуск и удаление через «Установленные приложения»", "Сейчас программа запущена без установки", InstallFromSettings, Installer.IsInstalled ? "Обновить" : "Установить");
			}
			page.Add(new SectionLabel("Интерфейс"));
			page.Add(new SettingCard(_host, "Анимации интерфейса", "Плавные переходы, появление карточек и переключателей", (IEngine e) => !Prefs.NoAnim, delegate (IEngine e, object v)
			{
				bool on = (bool)v;
				Prefs.NoAnim = !on;
				Anim.Enabled = on && Environment.GetEnvironmentVariable("WW_NOANIM") != "1";
			}));
			page.Add(new SettingCard(_host, "Скрывать адреса на экране «Сеть»", "IP-адреса и имя Wi-Fi замаскированы — удобно для скриншотов и стримов", (IEngine e) => Prefs.HideAddr, delegate (IEngine e, object v)
			{
				Prefs.HideAddr = (bool)v;
			}));
			page.Add(new SettingCard(_host, "Экспертный режим", "Показывает настройки, которые ослабляют защиту или могут сломать программы", (IEngine e) => Prefs.Expert, delegate (IEngine e, object v)
			{
				bool on = (bool)v;
				if (on && !(bool)Invoke(new Func<bool>(ConfirmExpert)))
				{
					return;
				}
				Prefs.Expert = on;
				BeginInvoke((Action)ExpertChanged);
			}, null, Glyph.Warn));
			page.Add(new SectionLabel("Обновления"));
			page.Add(new SettingCard(_host, "Сообщать о новых версиях", "Раз в день спрашивает GitHub о свежем релизе. Ничего не скачивает и не ставит само", (IEngine e) => Prefs.UpdateCheck, delegate (IEngine e, object v)
			{
				Prefs.UpdateCheck = (bool)v;
			}));
			TilesView ut = page.Add(new TilesView(_host));
			ut.TileH = 124;
			ut.AddUi(Glyph.Sync, "Проверить обновления", L.T("Сейчас установлена версия {0}").Replace("{0}", Brand.Version), "Откроется страница релиза на GitHub", delegate
			{
				CheckUpdates(manual: true);
			}, "Проверить");
			ut.AddUi(Glyph.Restore, "Что вернула Windows", "После крупных обновлений Windows включает часть настроек обратно", "Покажет список и применит заново выбранное", delegate
			{
				CheckDrift(afterUpdate: false);
			}, "Проверить");
			page.Add(new SectionLabel("Профиль настроек"));
			TilesView pt = page.Add(new TilesView(_host));
			pt.TileH = 124;
			pt.AddUi(Glyph.Disk, "Сохранить профиль", "Текущие переключатели всех разделов в файл .json", "Удобно перенести настройки на другой ПК", ExportProfile, "Сохранить");
			pt.AddUi(Glyph.Folder, "Загрузить профиль", "Покажет отличия и применит выбранные", "Каждое изменение попадёт в «Историю»", ImportProfile, "Загрузить");
			page.Add(new SectionLabel("Обратная связь"));
			TilesView ft = page.Add(new TilesView(_host));
			ft.TileH = 124;
			ft.AddUi(Glyph.Warn, "Сообщить об ошибке", "Форма на GitHub и сведения для разработчика", "Имя пользователя в журнале скрыто", ShowBugReport, "Сообщить");
			ft.AddUi(Glyph.Info, "Предложить идею", "Новая функция или улучшение", "Откроется форма на GitHub", delegate
			{
				Shell.Open(Brand.NewIdea);
			}, "Предложить");
			ft.AddUi(Glyph.Heart, "Поддержать разработчика", "Boosty, TON или Bitcoin", "Программа бесплатная и останется такой", ShowSupport, "Поддержать");
			page.Add(new SectionLabel("Файлы программы"));
			TilesView t = page.Add(new TilesView(_host));
			t.TileH = 124;
			t.Add(Glyph.Apps, "Ярлык на Рабочий стол", "Запуск " + Brand.Name + " с рабочего стола", "Без запроса UAC", delegate (IEngine e)
			{
				e.Eval("New-FDesktopShortcut");
				return (object)null;
			}, "Создать");
			t.AddUi(Glyph.Clock, "Журнал действий", "Подробный лог всех команд", "Пригодится, если что-то пошло не так", delegate
			{
				Shell.Open("notepad.exe", Brand.LogPath);
			});
			t.AddUi(Glyph.Disk, "Папка данных", Brand.DataDir, "История изменений и резервные копии", delegate
			{
				Shell.Open("explorer.exe", Brand.DataDir);
			});
			t.AddUi(Glyph.Delete, L.T("Удалить {0}").Replace("{0}", Brand.Name), "Задачи, ярлыки, схема питания и файлы программы", "Можно сначала откатить все изменения", StartUninstall, "Удалить");
		}
	}
}
