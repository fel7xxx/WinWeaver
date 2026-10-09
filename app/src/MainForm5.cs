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
using System.Windows.Forms;

namespace WinWeaver
{
	internal partial class MainForm
	{
		private LoadScreen _load;

		private Worker _reader;

		private int _actStamp;

		// reads go to the second engine when nothing is running (otherwise after the queue, to see the result)
		private Worker ReadWorker => (_reader != null && _reader.Engine.Ready && !_host.IsBusy) ? _reader : _host.Worker;

		private bool IsFresh(Page p)
		{
			return p.FreshStamp == _actStamp && (DateTime.Now - p.FreshAt).TotalSeconds < 600.0;
		}

		// whole page (all tabs) in one go; a page read recently and untouched since is not read again
		// every page reads itself: in the background after start and right away when it is opened
		private static bool NeedsGate(Page p)
		{
			return false;
		}

		private void RefreshPage(Page p, Action done = null)
		{
			if (done == null && NeedsGate(p))
			{
				if (!p.Gated)
				{
					p.ShowGate(delegate
					{
						p.Gate.Busy = true;
						ReadPage(p, delegate
						{
							p.HideGate();
						});
					});
				}
				return;
			}
			ReadPage(p, done);
		}

		private void ReadPage(Page p, Action done)
		{
			if (WarmClaim(p) && done == null)
			{
				return;
			}
			if (IsFresh(p))
			{
				done?.Invoke();
				return;
			}
			int stamp = _actStamp;
			p.Refresh(ReadWorker, all: true, done: delegate
			{
				p.FreshStamp = stamp;
				p.FreshAt = DateTime.Now;
				done?.Invoke();
			});
		}

		private const string WarmTitle = "Читаю разделы";

		// pages still waiting for their first read (one at a time, so an opened page can jump the line)
		private readonly List<Page> _warm = new List<Page>();

		private Page _warming;

		private int _warmTotal;

		private bool _warmBg;

		// Start: a full-window loading screen while the engines start and every page reads its state.
		// The main engine counts the home page and the scores, the reader fills all other pages at the same time.
		// Once the home page is ready the screen can be skipped: the other pages keep reading in the background.
		private void StartWithLoadScreen()
		{
			_load = new LoadScreen { Logo = _side.Logo };
			Controls.Add(_load);
			_load.BringToFront();
			_side.Enabled = false;
			_status.Held = true;
			_content.Enabled = false;
			Show("home");
			// every other page is read in the background; an opened page jumps the line (WarmClaim)
			_warm.Clear();
			foreach (Page wp in _pages.Values)
			{
				if (wp.Key != "home" && wp.Key != "review" && wp.Key != "settings")
				{
					_warm.Add(wp);
				}
			}
			_warmTotal = _warm.Count;
			int total = 4 + _warmTotal;
			int done = 0;
			int lanes = 2;
			Action<string, string> step = delegate(string text, string hint)
			{
				done++;
				_load?.SetStep(text, (float)done / total, hint);
				WarmStatus();
			};
			Action unlock = delegate
			{
				if (_load == null)
				{
					return;
				}
				_side.Enabled = true;
				_content.Enabled = true;
				_status.Held = false;
				LoadScreen ls = _load;
				_load = null;
				ls.Finish(AfterLoaded);
			};
			_load.Skip += delegate
			{
				_warmBg = true;
				unlock();
				WarmStatus();
			};
			Action laneDone = delegate
			{
				lanes--;
				if (lanes > 0)
				{
					return;
				}
				step("Готово", "");
				PrewarmHandles();
				if (_warmBg)
				{
					_warmBg = false;
					if (_status.BgTitle == WarmTitle)
					{
						_status.ClearBackground();
					}
				}
				if (Environment.GetEnvironmentVariable("WW_HOLDLOAD") == "1")
				{
					return;
				}
				unlock();
			};
			_load.SetStep("Запускаю движок…", 0.03f, "первый запуск может занять несколько секунд");
			// lane 1: main engine — home, reboot state, badges, scores
			_host.Worker.Run(delegate(IEngine en)
			{
				en.Start();
				return (object)null;
			}, delegate
			{
				step("Проверяю систему…", "главная страница");
				_blKick?.Invoke();
				if (_cur != null)
				{
					int st = _actStamp;
					Page home = _cur;
					home.Refresh(_host.Worker, all: true, done: delegate
					{
						home.FreshStamp = st;
						home.FreshAt = DateTime.Now;
					});
				}
				CheckReboot();
				UpdateBadge();
				UpdateScore();
				Mark(_host.Worker, delegate
				{
					step("Считаю оценки и рекомендации…", "FPS, приватность, проблемы");
					// the home page is ready
					// the window opens as soon as the home page is ready, the rest keeps reading in the background
					if (Environment.GetEnvironmentVariable("WW_HOLDLOAD") != "1" && _load != null)
					{
						_warmBg = true;
						unlock();
						WarmStatus();
					}
					if (_reader == null)
					{
						WarmNext(_host.Worker, step, laneDone);
					}
					laneDone();
				});
			});
			// lane 2: the reader engine starts at the same time and fills every other page
			if (_reader != null)
			{
				_reader.Run(delegate(IEngine en)
				{
					en.Start();
					return (object)null;
				}, delegate
				{
					step("Готовлю движок…", "");
					WarmNext(_reader, step, laneDone);
				});
			}
			else
			{
				step("Готовлю движок…", "");
			}
		}

		// next page of the warm-up; when the user is busy with an action the reads still go to the reader engine
		private void WarmNext(Worker w, Action<string, string> step, Action finished)
		{
			while (_warm.Count > 0 && IsFresh(_warm[0]))
			{
				_warm.RemoveAt(0);
				step("Читаю разделы…", "");
			}
			if (_warm.Count == 0)
			{
				_warming = null;
				finished();
				return;
			}
			Page p = _warm[0];
			_warm.RemoveAt(0);
			_warming = p;
			int st = _actStamp;
			string wd = Environment.GetEnvironmentVariable("WW_WARMDELAY");
			if (wd != null)
			{
				Console.Error.WriteLine("WARM " + p.Title);
				w.Run(delegate
				{
					System.Threading.Thread.Sleep(int.Parse(wd));
					return null;
				}, delegate
				{
				});
			}
			p.Refresh(w, all: true, done: delegate
			{
				if (p.FreshStamp != _actStamp)
				{
					p.FreshStamp = st;
					p.FreshAt = DateTime.Now;
				}
				step("Читаю разделы…", p.Title);
				WarmNext(w, step, finished);
			});
		}

		// background progress on the task toast while the rest is read after "skip"
		private void WarmStatus()
		{
			if (Environment.GetEnvironmentVariable("WW_WARMDELAY") != null)
			{
				Console.Error.WriteLine("WS bg=" + _warmBg + " load=" + (_load != null) + " bt=" + (_status.BgTitle ?? "-") + " left=" + _warm.Count);
			}
			if (!_warmBg || _load != null)
			{
				return;
			}
			string bt = _status.BgTitle;
			if (bt != null && bt != WarmTitle)
			{
				return;
			}
			int left = _warm.Count + ((_warming != null) ? 1 : 0);
			if (left <= 0)
			{
				return;
			}
			int read = Math.Max(0, _warmTotal - left);
			string sub = "прочитано " + read + " из " + _warmTotal + ((_warming != null) ? ("  •  " + _warming.Title) : "");
			_status.SetBackground(WarmTitle, sub, (float)read / Math.Max(1, _warmTotal));
			if (Environment.GetEnvironmentVariable("WW_WARMDELAY") != null)
			{
				Console.Error.WriteLine("BG " + sub);
			}
		}

		// an opened page jumps the warm-up line (or is simply waited for when it is being read right now)
		private bool WarmClaim(Page p)
		{
			if (Environment.GetEnvironmentVariable("WW_WARMDELAY") != null)
			{
				Console.Error.WriteLine("CLAIM " + p.Key + " warming=" + (p == _warming) + " queued=" + _warm.Contains(p));
			}
			if (p == _warming)
			{
				return true;
			}
			if (_warm.Remove(p))
			{
				_warmTotal--;
			}
			return false;
		}

		// window handles of hidden pages are created in idle moments, one page per tick:
		// otherwise the first visit of a page pays for creating all its controls right during the transition
		private void PrewarmHandles()
		{
			Queue<Page> q = new Queue<Page>();
			foreach (Page p in _pages.Values)
			{
				if (p != _cur)
				{
					q.Enqueue(p);
				}
			}
			Timer t = new Timer { Interval = 60 };
			t.Tick += delegate
			{
				if (DateTime.Now < Transition.BusyUntil)
				{
					return;
				}
				if (q.Count == 0)
				{
					t.Stop();
					t.Dispose();
					return;
				}
				Page p = q.Dequeue();
				try
				{
					if (!p.Visible)
					{
						p.Bounds = _content.ClientRectangle;
						p.DoLayoutBlocks();
					}
					MakeHandles(p);
				}
				catch
				{
				}
			};
			t.Start();
		}

		private static void MakeHandles(Control c)
		{
			if (!c.IsHandleCreated)
			{
				IntPtr h = c.Handle;
			}
			foreach (Control ch in c.Controls)
			{
				MakeHandles(ch);
			}
		}

		// runs on the UI thread after everything queued on that engine before it
		private void Mark(Worker w, Action a)
		{
			w.Run((IEngine e) => null, delegate
			{
				a();
			});
		}

		// risks shown before stopping an important action halfway
		private static string CancelRisk(string title)
		{
			string t = title ?? "";
			if (t.StartsWith("Автонастройка"))
			{
				return "Часть настроек уже применена, остальные — нет: система останется в смешанном состоянии. Применённое можно откатить в «Истории».";
			}
			if (t.StartsWith("Исправляю") || t.StartsWith("Откат"))
			{
				return "Изменения успеют примениться только частично. Проверь результат и при необходимости повтори действие.";
			}
			if (t.StartsWith("Удаление остатков"))
			{
				return "Часть остатков уже удалена, остальные останутся на диске и в реестре.";
			}
			if (t.StartsWith("Удаление точ") || t.StartsWith("Удаление старых точек"))
			{
				return "Часть точек может быть уже удалена — их не вернуть.";
			}
			if (t.StartsWith("Удаление"))
			{
				return "Программа может остаться удалённой наполовину: без ярлыков или с повреждённой установкой. Тогда её придётся переустановить или удалить заново.";
			}
			if (t.StartsWith("Очистка диска"))
			{
				return "Уже удалённые файлы не вернутся, остальное останется на диске.";
			}
			if (t.StartsWith("Создание точки"))
			{
				return "Точка восстановления не будет создана — откатиться к текущему состоянию будет нельзя.";
			}
			if (t.StartsWith("DNS"))
			{
				return "Сетевой адаптер может остаться с неполными настройками DNS — сайты могут перестать открываться, пока DNS не выбран заново.";
			}
			if (t.StartsWith("Перезапуск Проводника"))
			{
				return "Проводник может не запуститься обратно, и пропадёт панель задач. Тогда запусти explorer.exe через Диспетчер задач (Ctrl+Shift+Esc).";
			}
			if (t.Contains("BitLocker"))
			{
				return "Команда могла уже уйти в Windows — отмена её не остановит, а состояние на экране может обновиться не сразу.";
			}
			if (t.StartsWith("Microsoft Defender") || t.StartsWith("Защита в реальном"))
			{
				return "Defender может остаться в промежуточном состоянии (часть служб выключена, часть — нет). Проверь его состояние после отмены.";
			}
			if (t.StartsWith("Пауза обновлений") || t.StartsWith("Возобновление обновлений"))
			{
				return "Настройки Центра обновления могут записаться не полностью.";
			}
			if (t.StartsWith("Возврат настроек"))
			{
				return "Часть приложений и настроек уже вернётся в нужное состояние, часть — нет. Проводник может остаться выключенным.";
			}
			return null;
		}

		private void OnCancelJob(int id, string title, bool running)
		{
			if (running)
			{
				string risk = CancelRisk(title);
				if (risk != null && !WDialog.Ask(this, "Отменить «" + (title ?? "действие") + "»?", "Действие уже выполняется и прервётся на середине.\n\n" + risk, "Отменить действие", "Продолжить", DlgIcon.Warn))
				{
					return;
				}
			}
			_host.Cancel(id);
			_status.MarkCancelled(id);
		}
	}
}
