// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WinWeaver
{
	internal partial class MainForm : Form
	{
		internal class AppHost : Host
		{
			private readonly MainForm _f;

			public AppHost(MainForm f)
			{
				_f = f;
			}

			public override void Status(string kind, string text)
			{
				_f._status.Set(kind, text);
			}

			protected override void JobBegin(int id, string title, bool queued)
			{
				_f._status.JobBegin(id, title, queued);
			}

			protected override void JobEnd()
			{
				_f._status.JobEnd();
			}

			protected override void BusyChanged(bool busy)
			{
				_f._status.Busy = base.IsBusy;
				_f.Cursor = (base.IsBusy ? Cursors.AppStarting : Cursors.Default);
			}

			protected override void AfterAction()
			{
				_f._actStamp++;
				// after an action every block applies its next read, even when the values look the same
				foreach (Page pg in _f._pages.Values)
				{
					pg.ForgetData();
				}
				_f.CheckReboot();
				_f.UpdateBadge();
				_f._scoreDirty = true;
				if (_f._cur != null && _f._cur.Key == "home")
				{
					_f.UpdateScore();
				}
			}
		}

		private readonly Sidebar _side = new Sidebar();

		private readonly StatusBar _status = new StatusBar();

		private readonly Panel _content = new Panel();

		private readonly Dictionary<string, Page> _pages = new Dictionary<string, Page>();

		private readonly AppHost _host;

		private Page _cur;

		private Hero _hero;

		private HistoryView _hist;

		private HistoryView _homeHist;

		private LevelCard _running;

		private readonly Dictionary<string, LevelCard> _levels = new Dictionary<string, LevelCard>();

		public readonly string Mode;

		private readonly bool _fake;

		public bool NeedReboot;

		private WheelFilter _wheel;

		public Page Current => _cur;

		public IEnumerable<string> PageKeys => _pages.Keys;

		public MainForm(IEngine engine, string mode, bool fake)
		{
			Mode = mode ?? "";
			_fake = fake;
			Text = Brand.Name;
			base.AutoScaleMode = AutoScaleMode.None;
			BackColor = Theme.Bg;
			ForeColor = Theme.Text;
			Font = Theme.Body;
			base.StartPosition = FormStartPosition.CenterScreen;
			DoubleBuffered = true;
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			base.Size = new Size(Math.Min(Theme.S(1220f), workingArea.Width - 40), Math.Min(Theme.S(820f), workingArea.Height - 40));
			MinimumSize = new Size(Math.Min(Theme.S(980f), workingArea.Width - 40), Math.Min(Theme.S(660f), workingArea.Height - 40));
			base.Icon = WDialog.AppIcon;
			try
			{
				using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("logo.png"))
				{
					if (stream != null)
					{
						_side.Logo = Image.FromStream(stream);
					}
				}
			}
			catch
			{
			}
			_host = new AppHost(this);
			_content.BackColor = Theme.Bg;
			base.Controls.Add(_content);
			base.Controls.Add(_status);
			base.Controls.Add(_side);
			_side.Navigate += Show;
			WireExtras();
			EngineUI ui = new EngineUI(delegate(string k, string t)
			{
				if (base.IsHandleCreated)
				{
					BeginInvoke((Action)delegate
					{
						OnEngineMsg(k, t);
					});
				}
			}, (string title, string body, string yes, string no) => _host.Worker.Sync(() => WDialog.Ask(this, title, body, yes, no)));
			if (engine == null)
			{
				engine = Program.CreatePsEngine(ui, mode);
			}
			if (engine is FakeEngine fe)
			{
				fe.Ui = ui;
			}
			_host.Worker = new Worker(this, engine) { Name = "основной" };
			// second engine only for reading state: pages fill in parallel and a long action never blocks them
			if (!(engine is FakeEngine))
			{
				try
				{
					_reader = new Worker(this, Program.CreatePsEngine(new EngineUI(null, null), mode)) { Name = "чтение" };
				}
				catch
				{
					_reader = null;
				}
			}
			_status.CancelRequested += OnCancelJob;
			_host.Worker.Failed += delegate(Exception ex)
			{
				_status.Set("err", "Ошибка: " + ex.Message);
			};
			BuildPages();
			ApplyFpsMap();
			foreach (Page value in _pages.Values)
			{
				value.Visible = false;
				_content.Controls.Add(value);
			}
		}

		internal void DemoRec()
		{
			FixRecommended();
		}

		// screenshot helper
		internal void DemoToast(bool busy)
		{
			if (busy)
			{
				_status.Busy = true;
				_status.Set("info", "Автонастройка «Оптимальная» [7/18]: отключаю телеметрию");
			}
			else
			{
				_status.Busy = false;
				_status.Set("ok", "Остатки «Microsoft Teams» удалены: 214 МБ");
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Win.DarkTitle(base.Handle, Theme.Side1);
			if (_wheel == null)
			{
				_wheel = new WheelFilter(this);
				Application.AddMessageFilter(_wheel);
			}
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			if (_wheel != null)
			{
				Application.RemoveMessageFilter(_wheel);
				_wheel = null;
			}
			base.OnFormClosed(e);
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			base.OnLayout(e);
			int num = Theme.S(250f);
			int num2 = Theme.S(34f);
			Size clientSize = base.ClientSize;
			_side.SetBounds(0, 0, num, clientSize.Height);
			_content.SetBounds(num, 0, clientSize.Width - num, clientSize.Height);
			_status.Place(new Rectangle(num, 0, Math.Max(0, clientSize.Width - num - Theme.S(14f)), clientSize.Height));
			_status.BringToFront();
			foreach (Page value in _pages.Values)
			{
				if (value.Bounds != _content.ClientRectangle)
				{
					value.Bounds = _content.ClientRectangle;
				}
			}
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			if (!(_host.Worker.Engine is FakeEngine) || Environment.GetEnvironmentVariable("WW_LAG") == "1")
			{
				Lag.Start();
			}
			StartWithLoadScreen();
		}

		// everything that used to run after the engine came up; called once the start screen is gone
		private void AfterLoaded()
		{
			if (Mode == "/autostart" && Prefs.AutoChoice == null)
			{
				AskAutostart();
			}
			AfterLoadedExtras();
		}

		private void AskAutostart()
		{
			bool flag = WDialog.Ask(this, "Автозапуск " + Brand.Name, Brand.Name + " открылся автоматически при входе в систему.\n\nЗдесь настраиваются приватность, оформление, производительность, программы и обслуживание, а в «Истории» можно откатить любое изменение.\n\nОставить автозапуск? Изменить это можно в любой момент в разделе «Настройки».", "Оставить автозапуск", "Не запускать");
			Prefs.AutoChoice = (flag ? "yes" : "no");
			_status.Set("ok", flag ? "Автозапуск оставлен" : "Автозапуск выключен. Программа есть на Рабочем столе и в Пуске");
			if (_pages.TryGetValue("settings", out var value))
			{
				value.Refresh(_host.Worker);
			}
		}

		private Page _navTo;

		public void Show(string key)
		{
			long st0 = Lag.T0();
			try
			{
				ShowCore(key);
			}
			finally
			{
				Lag.Done("Show " + key, st0);
				Lag.Context = "раздел " + (_navTo ?? _cur)?.Key;
			}
		}

		private void ShowCore(string key)
		{
			// _navTo: the page being switched to (the switch itself may still be a few frames away)
			if (!_pages.TryGetValue(key, out var value) || (_navTo ?? _cur) == value)
			{
				return;
			}
			_navTo = value;
			_side.Select((key == "review") ? "home" : key);
			Action change = delegate
			{
				value.Bounds = _content.ClientRectangle;
				value.ScrollTop();
				value.Visible = true;
				value.BringToFront();
				value.DoLayoutBlocks();
				if (_cur != null)
				{
					_cur.Visible = false;
				}
				_cur = value;
			};
			if (_cur != null)
			{
				Transition.Play(_content, change, 0, 210);
			}
			else
			{
				change();
			}
			if (_host.Worker.Engine.Ready)
			{
				RefreshPage(value);
				if (key == "home" && _scoreDirty)
				{
					UpdateScore();
				}
			}
			if (value.OnShown != null)
			{
				value.OnShown();
			}
		}

		public void Status(string kind, string text)
		{
			_status.Set(kind, text);
		}

		private void CheckReboot()
		{
			_host.Worker.Run((IEngine e) => e.Eval("Get-FRebootInfo"), ApplyRebootInfo);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (_skipCloseAsk)
			{
				base.OnFormClosing(e);
				Lag.Stop();
				return;
			}
			if (_host.IsBusy && e.CloseReason == CloseReason.UserClosing && !WDialog.Ask(this, "Идёт операция", "Сейчас выполняется действие (например, автонастройка или установка браузера). Если закрыть программу сейчас, оно прервётся.\n\nВсё равно закрыть?", "Закрыть", "Подождать", DlgIcon.Warn))
			{
				e.Cancel = true;
				return;
			}
			if (NeedReboot && !RebootScheduled && e.CloseReason == CloseReason.UserClosing && WDialog.Ask(this, "Нужна перезагрузка", "Часть изменений вступит в силу только после перезагрузки компьютера.\n\nПерезагрузить сейчас?", "Перезагрузить", "Позже", DlgIcon.Warn))
			{
				Shell.Open("shutdown.exe", "/r /t 5");
			}
			base.OnFormClosing(e);
			Lag.Stop();
		}

		private Page NewPage(string key, string glyph, string nav, string title, string sub, bool bottom = false)
		{
			Page page = new Page(key, glyph, title, sub);
			_pages[key] = page;
			if (nav != null)
			{
				_side.Add(key, nav, glyph, bottom);
			}
			page.AfterTab = delegate
			{
				if (_host.Worker.Engine.Ready)
				{
					RefreshPage(page);
				}
			};
			return page;
		}

		// toggle/choice recorded in the history (Invoke-FSetting remembers the previous value)
		private SettingCard Tg(Page p, string title, string desc, string get, string set, string cat)
		{
			SettingCard c = p.Add(SettingCard.Tracked(_host, title, desc, get, set, null, cat));
			_tracked.Add(c);
			return c;
		}

		private SettingCard ChT(Page p, string title, string desc, string[] opts, string get, string set, string cat)
		{
			SettingCard c = p.Add(SettingCard.Tracked(_host, title, desc, get, set, opts, cat));
			_tracked.Add(c);
			return c;
		}

		// every tracked setting; the ones with a recommendation feed the home score
		private readonly List<SettingCard> _tracked = new List<SettingCard>();

		private bool _scoreDirty = true;

		private List<SettingCard> RecCards()
		{
			return _tracked.FindAll((SettingCard c) => c.Rec != null && !c.Unsupported);
		}

		private int _scoreSeq;

		private void UpdateScore()
		{
			if (_hero == null || !_host.Worker.Engine.Ready)
			{
				return;
			}
			// while the queue still runs, values change under us: count once when everything is done
			if (_host.IsBusy)
			{
				_scoreDirty = true;
				return;
			}
			_scoreDirty = false;
			int seq = ++_scoreSeq;
			List<SettingCard> cards = RecCards();
			foreach (SettingCard f in FpsCards())
			{
				if (!cards.Contains(f))
				{
					cards.Add(f);
				}
			}
			_host.Worker.Run(delegate(IEngine e)
			{
				object[] r = new object[cards.Count];
				for (int i = 0; i < cards.Count; i++)
				{
					try
					{
						r[i] = cards[i].Fetch(e);
					}
					catch
					{
					}
				}
				return r;
			}, delegate(object o)
			{
				object[] r = o as object[];
				if (r == null || seq != _scoreSeq)
				{
					return;
				}
				for (int i = 0; i < cards.Count && i < r.Length; i++)
				{
					// a failed read (null) must not turn the setting "off" and drag the score down
					if (r[i] != null)
					{
						cards[i].Apply(r[i]);
					}
				}
				// 0..100 by FPS weight
				int all = 0;
				int got = 0;
				int bad = 0;
				foreach (SettingCard c in FpsCards())
				{
					bool? ok = c.FpsOk;
					// security trade-offs (HVCI, VBS) are a personal choice: they never lower the score
					if (ok == null || IsFpsRisky(c))
					{
						continue;
					}
					all += c.FpsW;
					if (ok == true)
					{
						got += c.FpsW;
					}
					else
					{
						bad++;
					}
				}
				int score = (all > 0) ? (int)Math.Round(100.0 * got / all) : 0;
				_hero.SetScore(score, 100, bad);
			});
		}

		private static string Plural(int n, string one, string few, string many)
		{
			if (!L.IsRu)
			{
				return (n == 1) ? one : many;
			}
			int a = n % 100;
			int b = n % 10;
			if (a >= 11 && a <= 14)
			{
				return many;
			}
			return (b == 1) ? one : ((b >= 2 && b <= 4) ? few : many);
		}

		private void FixRecommended()
		{
			List<SettingCard> bad = RecCards().FindAll((SettingCard c) => c.Matches == false);
			if (bad.Count == 0)
			{
				return;
			}
			// grouped list: what each setting does + what will change; anything can be unticked
			List<CheckDialog.Row> rows = new List<CheckDialog.Row>();
			List<SettingCard> map = new List<SettingCard>();
			List<string> cats = new List<string>();
			foreach (SettingCard c in bad)
			{
				if (!cats.Contains(c.Cat ?? ""))
				{
					cats.Add(c.Cat ?? "");
				}
			}
			foreach (string cat in cats)
			{
				rows.Add(new CheckDialog.Row { Header = true, Text = HistoryView.CatName(cat) });
				map.Add(null);
				foreach (SettingCard c in bad)
				{
					if ((c.Cat ?? "") != cat)
					{
						continue;
					}
					string tag = (c.Seg != null) ? ("→ " + c.Seg.Items[(int)c.Rec]) : (((bool)c.Rec) ? null : "выключить");
					rows.Add(new CheckDialog.Row { Text = c.Title, Hint = "\n" + (string.IsNullOrEmpty(c.Desc) ? c.What : c.Desc), Tag = tag, Checked = true });
					map.Add(c);
				}
			}
			bool[] picked = CheckDialog.Ask(this, "Применить рекомендации", "Можно улучшить " + bad.Count + " " + Plural(bad.Count, "настройку", "настройки", "настроек") + ". Сними галочку с того, что хочешь оставить как есть. Всё попадёт в «Историю» одной группой — откатить можно целиком или по пункту.", rows.ToArray(), "Применить", "Отмена", DlgIcon.Info);
			if (picked == null)
			{
				return;
			}
			bad = new List<SettingCard>();
			for (int i = 0; i < picked.Length; i++)
			{
				if (picked[i] && map[i] != null)
				{
					bad.Add(map[i]);
				}
			}
			if (bad.Count == 0 || !_host.TryBegin(title: "Исправляю проблемы (" + bad.Count + ")"))
			{
				return;
			}
			_hero.Fixing = true;
			_hero.Invalidate();
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("Start-FGroup 'Рекомендованные настройки'");
				try
				{
					foreach (SettingCard c in bad)
					{
						e.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
						{
							{ "fa1", c.HistTitle(c.Rec) },
							{ "fa2", c.GetScript },
							{ "fa3", c.SetScript },
							{ "fa4", c.Rec },
							{ "fa5", c.Cat }
						});
					}
				}
				finally
				{
					e.Eval("Stop-FGroup; Show-Ok ('Применены рекомендованные настройки: ' + $fa1)", new Hashtable { { "fa1", bad.Count } });
				}
				return (object)null;
			}, delegate
			{
				_hero.Fixing = false;
				_host.End();
				_cur?.Refresh(_host.Worker);
			});
		}

		// data-driven tweak from engine.ps1 (Add-FTweak id …)
		private SettingCard Tw(Page p, string title, string desc, string id, string cat)
		{
			return Tg(p, title, desc, "Get-FTweak '" + id + "'", "Set-FTweak '" + id + "' $v", cat);
		}

		private SettingCard Ch(Page p, string title, string desc, string[] opts, string get, string set, string cat)
		{
			return ChT(p, title, desc, opts, get, set, cat);
		}

		private static ActionDef A(string text, string ps, BtnKind k = BtnKind.Ghost, string glyph = null)
		{
			return new ActionDef(text, k)
			{
				Work = delegate(IEngine e)
				{
					e.Eval(ps);
					return (object)null;
				},
				Glyph = glyph
			};
		}

		// action recorded in the history. undo == null: registry values touched by the script are restored on rollback
		private static ActionDef T(string text, string histTitle, string ps, string cat, string undo = null, BtnKind k = BtnKind.Ghost, bool noUndo = false, bool keepReg = false, string note = "")
		{
			return new ActionDef(text, k)
			{
				Work = delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked $fa1 $fa2 -Undo $fa3 -Cat $fa4 -NoUndo:$fa5 -KeepReg:$fa6 -Note $fa7", new Hashtable
					{
						{ "fa1", histTitle },
						{ "fa2", ps },
						{ "fa3", undo ?? "" },
						{ "fa4", cat },
						{ "fa5", noUndo },
						{ "fa6", keepReg },
						{ "fa7", note ?? "" }
					});
					return (object)null;
				}
			};
		}

		private static ActionDef C(ActionDef a, string title, string confirm, string yes = null)
		{
			a.ConfirmTitle = title;
			a.Confirm = confirm;
			a.ConfirmYes = yes;
			return a;
		}

		private static ActionDef U(string text, Action ui, BtnKind k = BtnKind.Ghost, string glyph = null)
		{
			return new ActionDef(text, k)
			{
				Ui = ui,
				Glyph = glyph
			};
		}

		private ActionCard Act(string glyph, string title, string status, params ActionDef[] a)
		{
			return new ActionCard(_host, glyph, title, (status == null) ? null : ActionCard.PsStatus(status), a);
		}

		private ActionCard ActT(string glyph, string title, string fixedText, params ActionDef[] a)
		{
			return new ActionCard(_host, glyph, title, null, a)
			{
				Status = fixedText
			};
		}

		public void ShowTab(string key, int tab)
		{
			Show(key);
			if (_pages.TryGetValue(key, out var p))
			{
				p.SelectTab(tab, animate: false);
			}
		}

		private void BuildPages()
		{
			BuildHome();
			// ---------- privacy & protection ----------
			Page page = NewPage("privacy", Glyph.Lock, "Приватность", "Приватность и защита", "Слежка, реклама, Defender, BitLocker и обновления Windows");
			BuildPrivacyHero(page);
			page.AddTabs("Слежка", "Реклама", "Защита", "Обновления");
			Tg(page, "Отключить телеметрию", "Службы DiagTrack, отчёты об ошибках, CEIP, рекламный идентификатор", "(Get-Telemetry) -eq 'ВЫКЛ'", "Set-Telemetry $v", "privacy").Info("Останавливает службу сбора диагностики DiagTrack, отправку отчётов об ошибках и программу улучшения качества, сбрасывает рекламный идентификатор и ставит минимальный уровень диагностических данных.", "Меньше данных уходит в Microsoft, меньше фоновой активности диска и сети. Работе Windows и программ это не мешает.", "Участники программы Windows Insider не получат тестовые сборки, а Microsoft не увидит отчёты о твоих сбоях.", true, "Для домашнего ПК телеметрия ничего полезного не даёт — смело выключай.");
			Tg(page, "Отключить Copilot и Recall", "Политики Windows AI, кнопка Copilot, функция Recall", "(Get-AI) -eq 'ВЫКЛ'", "Set-AI $v", "privacy").Info("Выключает Copilot политиками, прячет его кнопку и запрещает Recall — функцию, которая делает снимки экрана каждые несколько секунд, чтобы потом «вспомнить», что ты делал.", "Recall хранит историю всего, что было на экране, — это риск для паролей и переписок. Copilot висит в фоне и занимает память.", "Если ты пользуешься Copilot каждый день, его придётся открывать в браузере.", true, "Большинству ИИ-помощник в системе не нужен, а Recall лучше держать выключенным всегда.");
			Tg(page, "Отключить Bing в поиске", "Поиск Windows ищет только на этом компьютере", "(Get-Bing) -eq 'ВЫКЛ'", "Set-Bing $v; Restart-FExplorer", "privacy").Info("Запрещает поиску в Пуске отправлять запросы в Bing и показывать результаты из интернета.", "Поиск работает быстрее и не отправляет в сеть всё, что ты набираешь. Нет лишних веб-ссылок среди файлов и программ.", "Пропадут быстрые ответы из интернета (погода, курсы) прямо в Пуске.", true, "Искать в интернете удобнее в браузере, а поиск Windows пусть ищет файлы и программы.");
			Tw(page, "Отключить определение местоположения", "Приложения и Windows не узнают, где ты находишься", "location", "privacy").Info("Запрещает доступ к геолокации для всех приложений и самой Windows.", "Приложения не могут отслеживать твоё местоположение.", "Перестанут работать автоопределение часового пояса, погода по месту, «Поиск устройства» и карты.", null, "На стационарном ПК можно выключить, на ноутбуке — по желанию: «Поиск устройства» без геолокации не работает.");
			Tw(page, "Отключить персональные советы", "Советы и реклама на основе диагностических данных", "tailored", "privacy").Info("Запрещает Microsoft использовать твои диагностические данные, чтобы подбирать советы, рекламу и рекомендации.", "Меньше навязчивых подсказок и предложений, основанных на том, как ты пользуешься ПК.", "Минусов практически нет (разве что советы в Windows станут общими, а не под тебя).", true, "Выключить — ничего полезного эта функция не даёт.");
			Tw(page, "Отключить онлайн-распознавание речи", "Голосовой ввод не отправляет запись в облако", "speech", "privacy").Info("Запрещает отправлять записи голоса на серверы Microsoft для распознавания.", "Голос не уходит в облако.", "Перестанет работать облачный голосовой ввод (Win + H) и диктовка в некоторых приложениях.", true, "Если не диктуешь текст голосом — выключай.");
			Tw(page, "Отключить сбор рукописного и набранного текста", "Windows не изучает, что и как ты печатаешь", "inking", "privacy").Info("Запрещает собирать образцы набранного и рукописного текста и контакты для «персонализации ввода».", "Тексты, которые ты набираешь, не используются для обучения словарей Microsoft.", "Подсказки при рукописном вводе на планшете могут стать чуть хуже.", true, "Выключить — на обычном ПК без пера разницы не будет.");
			Tw(page, "Отключить журнал действий", "История открытых файлов и сайтов не публикуется и не отправляется", "activity", "privacy").Info("Запрещает Windows собирать и отправлять историю действий: какие файлы, программы и сайты ты открывал.", "Меньше следов твоей работы хранится в системе и в облаке.", "Не будет синхронизации истории между устройствами (её и так почти нигде нет).", true, "Выключить.");
			Tw(page, "Отключить запросы отзывов", "Окна «Оцените Windows» больше не появляются", "feedback", "privacy").Info("Запрещает центру отзывов показывать уведомления с просьбой оценить Windows.", "Меньше отвлекающих уведомлений.", "Минусов почти нет (но и сам не сможешь быстро отправить отзыв Microsoft из этих окон — только через «Центр отзывов»).", true, "Выключить.");
			page.Section(1);
			Tg(page, "Отключить рекламу и рекомендации", "Реклама в Пуске, советы, тихая установка спонсорских приложений", "(Get-Ads) -eq 'ВЫКЛ'", "Set-Ads $v", "privacy").Info("Выключает рекламу в Пуске и Параметрах, «советы и подсказки», а также тихую установку спонсорских приложений вроде игр и TikTok после обновлений.", "Чистый Пуск без рекламы и приложений, которые ты не ставил.", "Минусов почти нет (иногда пропадают и полезные подсказки о новых функциях Windows, а предложенные приложения придётся искать в Store самому).", true, "Выключить обязательно.");
			Tw(page, "Отключить советы на экране блокировки", "Без «интересных фактов» и рекламы поверх обоев", "lockads", "privacy").Info("Убирает подсказки, факты и рекламу, которые Windows рисует поверх картинки экрана блокировки.", "Чистый экран блокировки, обои видно целиком.", "Минусов почти нет (пропадут и «интересные факты» о фото, если они тебе нравились).", true, "Выключить.");
			Tw(page, "Отключить «Главное в поиске»", "Без картинок дня и новостей в окне поиска", "searchhl", "privacy").Info("Убирает «основные моменты поиска»: праздничные картинки, тренды и новости в панели поиска.", "Поиск открывается быстрее и выглядит чище.", "Минусов почти нет (не будет праздничных картинок и быстрых трендов в поиске — кому-то это нравится).", true, "Выключить.");
			Tw(page, "Отключить «Давайте завершим настройку»", "Экран с предложениями Microsoft 365 и OneDrive после обновлений", "scoobe", "privacy").Info("Запрещает полноэкранное окно «Давайте завершим настройку устройства», которое после обновлений предлагает купить Microsoft 365 и включить OneDrive.", "Не нужно каждый раз пролистывать навязчивые предложения.", "Минусов почти нет (о новых функциях и бонусах Microsoft 365/OneDrive после обновлений ты не узнаешь из этого окна).", true, "Выключить.");
			Tw(page, "Отключить рекламу в Проводнике", "Уведомления поставщиков синхронизации (OneDrive, Microsoft 365)", "syncads", "privacy").Info("Убирает баннеры OneDrive и Microsoft 365 в верхней части Проводника.", "Проводник без рекламы.", "Минусов почти нет (иногда в баннерах бывают полезные сообщения — например, что OneDrive заполнен или синхронизация остановилась).", true, "Выключить.");
			page.Section(2);
			BuildProtect(page);
			BuildDefender(page);
			BuildBitLocker(page);
			BuildProtectHardening(page);
			page.Section(3);
			BuildUpdates(page);
			Tg(page, "Остаться на текущей версии Windows", "Обновления безопасности приходят, переход на новую версию (26H2 и т. д.) — нет", "(Get-WU) -like 'остаётся*'", "Set-WUStay $v", "update").Info("Фиксирует текущую версию Windows (например, 24H2) политикой TargetReleaseVersion. Ежемесячные обновления безопасности продолжают приходить.", "Крупное обновление не поменяет интерфейс, не вернёт удалённые приложения и не сломает драйверы в неудобный момент.", "Новые функции следующей версии не придут, пока не снимешь фиксацию. Поддержка версии заканчивается примерно через 2 года.", null, "Удобно, если всё работает и сюрпризы не нужны. Раз в год лучше снять фиксацию и обновиться.");
			Tw(page, "Не раздавать обновления другим ПК", "Оптимизация доставки не использует твой интернет для чужих компьютеров", "dop2p", "update").Info("Выключает раздачу скачанных обновлений другим компьютерам в интернете и локальной сети (Delivery Optimization).", "Не тратится исходящий трафик, меньше нагрузка на сеть во время игр и звонков.", "Если в доме несколько ПК с Windows, каждый будет качать обновления сам.", true, "Выключить — особенно при лимитном или медленном интернете.");
			Tw(page, "Не перезагружать ПК, пока я в системе", "Обновления не перезагрузят компьютер сами, если ты вошёл в Windows", "noreboot", "update").Info("Запрещает автоматическую перезагрузку после установки обновлений, пока в системе есть вошедший пользователь.", "Компьютер не перезагрузится ночью посреди загрузки, рендера или игры.", "Обновления устанавливаются только после того, как ты сам перезагрузишь ПК.", true, "Включить — перезагружайся, когда удобно тебе.");
			Tw(page, "Обновлять приложения Store только вручную", "Microsoft Store не скачивает обновления в фоне", "storeupd", "update").Info("Выключает автоматические обновления приложений из Microsoft Store.", "Store не качает обновления в фоне и не возвращает удалённые встроенные приложения.", "Приложения из Store придётся обновлять вручную, иначе можно пропустить исправления.", null, "Включай, если почти не пользуешься приложениями из Store.");
			BuildUpdatesExtra(page);
			// ---------- look ----------
			page = NewPage("ui", Glyph.Brush, "Оформление", "Оформление", "Внешний вид Windows, панель задач, Пуск и Проводник");
			page.AddTabs("Вид", "Панель задач", "Пуск", "Проводник");
			Tg(page, "Тёмная тема", "Для системы и приложений", "(Get-Theme) -eq 'Тёмная'", "Set-Theme $v", "ui").Info("Переключает Windows и приложения, которые поддерживают темы, на тёмное оформление.", "Меньше устают глаза вечером, на OLED-экранах экономит заряд.", "При ярком дневном свете тёмный текст на светлом фоне читается легче.", true, "Большинству удобнее тёмная тема — особенно вечером. Но это дело вкуса.");
			Tg(page, "Прозрачность", "Эффекты прозрачности окон и панели задач", "(Get-Transp) -eq 'ВКЛ'", "Set-Transp $v", "ui").Info("Включает эффект «матового стекла» (Mica и Acrylic) у панели задач, Пуска и окон.", "Система выглядит современнее.", "На слабых видеокартах и ноутбуках от батареи тратит немного ресурсов.", null, "Оставь, если ПК не слабый. Игровая автонастройка выключает её сама.");
			Tg(page, "Классическое контекстное меню", "Полное меню правой кнопки мыши, как в Windows 10", "(Get-Menu) -like 'Класс*'", "Set-Menu $v", "ui").Info("Возвращает полное контекстное меню вместо сокращённого с пунктом «Показать дополнительные параметры».", "Все пункты (7-Zip, WinRAR, «Отправить») сразу, без лишнего клика.", "Меню выглядит в стиле Windows 10, без закруглений.", true, "Включить — экономит клик каждый раз.");
			Tw(page, "Макеты при наведении на «Развернуть»", "Схемы прикрепления окон появляются при наведении на кнопку", "snapflyout", "ui").Info("Показывает схемы прикрепления окон (половина, четверть экрана), когда ты держишь курсор над кнопкой «Развернуть».", "Быстро раскладывать окна мышью.", "Иногда всплывает, когда хочешь просто развернуть окно.", null, "Дело привычки. Сочетание Win + стрелки работает в любом случае.");
			page.Section(1);
			Ch(page, "Поиск на панели задач", "Как отображать поиск", new string[3] { "Значок", "Поле", "Скрыть" }, "Get-FSearchIdx", "Set-FSearchIdx $v", "ui").Info("Меняет вид поиска на панели задач: значок, широкое поле или ничего.", "Значок экономит место для приложений.", "Без значка поиск открывается только клавишей Win или Win + S.", 0, "«Значок» — поиск под рукой и не занимает полпанели.").Opts("Компактная лупа — поиск под рукой и почти не занимает место.", "Широкое поле ввода, как в стандартной Windows. Удобно, если часто ищешь, но занимает треть панели.", "Ни значка, ни поля. Поиск открывается клавишей Win или Win + S.");
			Tg(page, "Панель задач слева", "Выравнивание значков: слева или по центру", "(Get-Align) -eq 'Слева'", "Set-Align $v", "ui").Info("Выравнивает кнопку Пуск и значки по левому краю, как в Windows 10.", "Привычное расположение, Пуск всегда в углу.", "На широких мониторах до значков дальше тянуться.", null, "Дело вкуса.");
			Tw(page, "Убрать виджеты", "Кнопку погоды и новостей с панели задач", "widgets", "ui").Info("Выключает виджеты политикой: пропадает кнопка погоды и новостей и сама доска виджетов. Работает и на 24H2+, где обычный переключатель заблокирован.", "Меньше фоновых процессов (WebView, новости) и случайных открытий при наведении.", "Не будет погоды на панели задач.", true, "Убрать — виджеты грузят новости и рекламу в фоне.");
			Tw(page, "Скрыть кнопку Chat (Teams)", "Кнопка чата Teams на панели задач", "teamschat", "ui").Info("Убирает кнопку Chat (бесплатный Teams), которая была в Windows 11 22H2–23H2.", "Чище панель задач.", "Минусов почти нет (если пользуешься Teams, открывать его придётся из Пуска).", true, "Скрыть. В 24H2 кнопки уже нет.").Builds(0, 22631);
			Tg(page, "Кнопка «Представление задач»", "Значок виртуальных рабочих столов рядом с поиском", "(Get-TaskView) -eq 'ВКЛ'", "Set-TaskView $v", "ui").Info("Показывает кнопку «Представление задач» — все окна и виртуальные рабочие столы.", "Удобно, если пользуешься несколькими рабочими столами.", "Занимает место на панели. То же самое открывает Win + Tab.", null, "Большинству не нужна — автонастройка её скрывает.");
			Tg(page, "«Завершить задачу» в меню панели задач", "Пункт в меню правой кнопки по значку приложения", "(Get-EndTask) -eq 'ВКЛ'", "Set-EndTask $v", "ui").Info("Добавляет пункт «Завершить задачу» в меню значка приложения на панели задач.", "Зависшую программу можно закрыть в два клика, без Диспетчера задач.", "Минусов почти нет (можно случайно закрыть программу без сохранения — пункт стоит рядом с обычными).", true, "Включить.").Builds(22631);
			Ch(page, "Группировать кнопки", "Объединять окна одного приложения в одну кнопку", new string[3] { "Всегда", "При переполнении", "Никогда" }, "Get-FTweakIdx 'combine'", "Set-FTweakIdx 'combine' $v", "ui").Info("Решает, когда окна одной программы склеиваются в одну кнопку и показываются ли подписи.", "«Никогда» и «При переполнении» показывают названия окон — проще переключаться.", "Без группировки панель быстро заполняется.", null, "«При переполнении» — золотая середина.").Builds(22631).Opts("Окна одной программы всегда собраны в одну кнопку без подписей — самая компактная панель.", "Подписи и отдельные кнопки, пока хватает места; склеиваются, только когда панель заполнится.", "Каждое окно — своя кнопка с названием. Проще переключаться, но панель быстро забивается.");
			Tw(page, "Секунды в часах", "Часы на панели задач показывают секунды", "seconds", "ui").Info("Показывает секунды в часах в правом нижнем углу.", "Удобно для точного времени.", "Чуть больше перерисовок панели задач (на ноутбуке — копейки заряда).", null, "По желанию.").Builds(22621);
			Tw(page, "«Свернуть все окна» в правом углу", "Клик по краю панели задач показывает рабочий стол", "deskcorner", "ui").Info("Включает узкую кнопку в самом правом углу панели задач, которая сворачивает все окна.", "Быстрый доступ к рабочему столу мышью.", "Можно случайно нажать. Win + D делает то же самое.", null, "По желанию.").Builds(22621);
			page.Section(2);
			Tw(page, "Убрать рекомендации в Пуске", "Советы приложений и сайтов в разделе «Рекомендуем»", "startiris", "ui").Info("Выключает рекомендации приложений, советы и сайты в Пуске.", "Пуск чище и не предлагает установить то, что тебе не нужно.", "Минусов почти нет (вместе с рекламой пропадут и полезные подсказки — например, только что установленные программы не будут подсвечиваться в Пуске).", true, "Убрать.").Builds(22631);
			Tw(page, "Скрыть недавние файлы в Пуске", "Пуск и списки переходов не показывают недавно открытое", "startdocs", "ui").Info("Не показывает недавно открытые файлы в Пуске, списках переходов и Проводнике.", "Никто не увидит, какие файлы ты открывал. Больше места для закреплённых приложений.", "Нельзя быстро вернуться к файлу, с которым работал вчера.", null, "Включай, если за ПК сидит не только твоя семья или ты стримишь экран.");
			Tw(page, "Больше закреплённых в Пуске", "Раздел «Закреплено» становится больше, «Рекомендуем» — меньше", "startlayout", "ui").Info("Переключает макет Пуска на «Больше закреплённых»: добавляется ряд значков.", "Больше любимых программ в одном клике.", "В новом Пуске 25H2 макет устроен иначе — там настройка может не влиять.", null, "Удобно, если закрепляешь много программ.").Builds(22621);
			page.Section(3);
			Tg(page, "Показывать расширения файлов", ".exe, .txt, .jpg и т. д.", "(Get-Ext) -eq 'Показывать'", "Set-Ext $v", "ui").Info("Показывает расширение в конце имени файла: документ.pdf, setup.exe.", "Видно, что файл на самом деле программа, а не картинка — это защищает от вирусов вида «фото.jpg.exe».", "Легко случайно стереть расширение при переименовании (Windows предупредит).", true, "Включить — это базовая безопасность.");
			Tg(page, "Показывать скрытые файлы", "Скрытые папки и файлы в Проводнике", "(Get-Hidden) -eq 'Показывать'", "Set-Hidden $v", "ui").Info("Показывает скрытые файлы и папки, например AppData.", "Проще найти сохранения игр, конфиги и кэш программ.", "Больше мусора в папках, можно случайно удалить служебные файлы.", null, "Включай, если часто копаешься в настройках программ.");
			Tw(page, "Открывать «Этот компьютер»", "Проводник по Win + E открывается на дисках, а не на «Главной»", "launchpc", "ui").Info("Проводник при запуске показывает диски и папки вместо «Главной» с недавними файлами и OneDrive.", "Сразу видно диски и свободное место.", "Недавние файлы будут на шаг дальше.", null, "Большинству привычнее «Этот компьютер».");
			Tw(page, "Компактный вид Проводника", "Меньше отступов между файлами", "compact", "ui").Info("Уменьшает расстояние между строками в Проводнике, как в Windows 10.", "На экран помещается больше файлов.", "Сложнее попадать пальцем на сенсорном экране.", null, "Удобно на мониторах с небольшим разрешением.");
			Tw(page, "Флажки для выбора файлов", "Галочки рядом с файлами", "checkbox", "ui").Info("Показывает флажок рядом с каждым файлом для выделения нескольких файлов мышью.", "Удобно на планшете и тачпаде.", "Лишние элементы, если выделяешь через Ctrl.", null, "По желанию.");
			Tw(page, "Не показывать недавнее в быстром доступе", "Без списка недавних файлов и частых папок", "qarecent", "ui").Info("Убирает недавние файлы и часто используемые папки из «Главной» и быстрого доступа.", "Приватнее и чище: никто не увидит, что ты открывал.", "Придётся закреплять нужные папки вручную.", null, "Включай, если важна приватность.");
			Tw(page, "Значок «Этот компьютер» на рабочем столе", "Классический значок компьютера", "thispc", "ui").Info("Добавляет на рабочий стол значок «Этот компьютер».", "Быстрый доступ к дискам и свойствам системы.", "Ещё один значок на рабочем столе.", null, "По желанию.");
			// ---------- performance ----------
			page = NewPage("games", Glyph.Bolt, "Производительность", "Производительность", "Скорость, отклик системы и игровые настройки");
			Page games = page;
			page.AddTabs("Отклик", "Игры", "Система");
			Ch(page, "Схема питания", "WinWeaver — своя схема программы: быстрый отклик без лишнего нагрева", new string[4] { "WinWeaver", "Максимальная", "Высокая", "Сбалансированная" }, "Get-FPowerIdx", "Set-FPowerIdx $v", "game").Info("Выбирает план электропитания. «WinWeaver» — собственная схема программы, создаётся при выборе: на сети процессор сразу выходит на полную частоту (агрессивный буст, без парковки ядер), PCIe и USB не засыпают, ПК не уходит в сон, экран гаснет через 15 минут. На батарее схема экономит: мягкий буст, сон через 15 минут.", "Отклик как у «Максимальной», но в простое процессор сбрасывает частоту и не греется. Нет фризов от засыпающих USB-устройств и видеокарты.", "Компьютер на сети не засыпает сам — выключай его или отправляй в сон вручную. На ноутбуке от батареи «Сбалансированная» экономнее.", 0, "«WinWeaver» подходит и ПК, и ноутбукам на зарядке. «Максимальная» нужна только для бенчмарков, «Сбалансированная» — для долгой работы от батареи.").Opts("Своя схема программы: на сети процессор сразу выходит на полную частоту, USB и PCIe не засыпают, а в простое частота сбрасывается и ПК не греется.", "Стандартная «Максимальная производительность»: всё всегда на пределе. Больше нагрев и расход, имеет смысл для бенчмарков.", "Стандартная «Высокая производительность»: быстрее сбалансированной, но ядра паркуются и устройства могут засыпать.", "Стандарт Windows: частота растёт по нагрузке. Самая экономная, лучший выбор для ноутбука от батареи.");
			Tw(page, "Без задержки автозапуска", "Программы из автозагрузки стартуют сразу после входа", "startdelay", "game").Info("Убирает искусственную задержку, с которой Windows запускает программы из автозагрузки после входа.", "Рабочий стол готов к работе на несколько секунд раньше.", "На медленном HDD сразу после входа диск будет загружен сильнее.", true, "Включить — на SSD это только плюс.");
			Tw(page, "Быстрые меню", "Подменю открываются без задержки", "menudelay", "game").Info("Уменьшает задержку перед открытием подменю с 400 до 100 мс.", "Система ощущается отзывчивее.", "Минусов почти нет (при случайном наведении мышью подменю могут раскрываться слишком резко). Применяется после перезагрузки.", true, "Включить.");
			Tw(page, "Запретить фоновые приложения", "Приложения из Store не работают в фоне", "bgapps", "game").Info("Запрещает приложениям из Microsoft Store работать в фоне, когда они закрыты.", "Меньше процессов, памяти и сетевой активности.", "Могут перестать приходить уведомления от приложений Store (Почта, Телефон, WhatsApp из Store).", null, "Включай, если не ждёшь уведомлений от приложений из Store.");
			Tw(page, "Отключить Power Throttling", "Windows не урезает частоту фоновым программам", "pthrottle", "game").Info("Выключает механизм, который снижает производительность фоновых процессов ради экономии энергии.", "Фоновые задачи (стрим, запись, рендер) не тормозят.", "На ноутбуке снижается время работы от батареи.", null, "Для ПК и стримеров — включить, для ноутбуков — оставить как есть.").Builds(16299);
			PerfResp(page);
			page.Section(1);
			Tg(page, "Игровой режим", "Windows отдаёт приоритет игре", "(Get-GameMode) -eq 'ВКЛ'", "Set-GameMode $v", "game").Info("Во время игры Windows откладывает обновления и уведомления и отдаёт игре больше ресурсов.", "Меньше фризов из-за фоновых задач.", "На очень старых ПК в редких играх бывают просадки — тогда выключи.", true, "Включить.");
			Tg(page, "Фоновая запись игр (Game DVR)", "Постоянная запись последних минут игры — снижает FPS", "(Get-Dvr) -eq 'ВКЛ'", "Set-Dvr $v", "game").Info("Xbox Game Bar постоянно пишет видео последних минут игры, чтобы можно было сохранить момент.", "Можно сохранить хайлайт задним числом.", "Постоянная запись отнимает FPS и нагружает диск.", false, "Выключить. Для записи лучше ShadowPlay, ReLive или OBS.");
			Tg(page, "Ускорение мыши", "Для шутеров лучше выключить", "(Get-MouseAcc) -eq 'ВКЛ'", "Set-MouseAcc $v", "game").Info("«Повышенная точность указателя»: курсор проходит больше при быстром движении мыши.", "В офисе на маленьком столе удобнее.", "В играх рушит мышечную память: одно и то же движение даёт разный поворот.", false, "Выключить — прицеливание станет предсказуемым.");
			Tg(page, "Аппаратное планирование GPU (HAGS)", "Применяется после перезагрузки, если видеокарта поддерживает", "(Get-Hags) -eq 'ВКЛ'", "Set-Hags $v", "game").Info("Видеокарта сама управляет своей памятью и очередью задач, без участия процессора.", "Чуть меньше задержка, нужно для DLSS Frame Generation на NVIDIA.", "На старых драйверах и видеокартах бывают глюки — тогда выключи.", true, "Включить на GeForce 10xx/Radeon 5000 и новее.");
			Tw(page, "Оптимизация оконных игр", "Меньше задержка в играх в окне и без рамки", "windowed", "game").Info("Включает современную модель вывода для игр на DirectX 10/11 в оконном режиме и без рамки.", "Меньше задержка ввода, работают VRR и Auto HDR в окне.", "В отдельных старых играх могут быть артефакты.", true, "Включить.").Builds(22000);
			Tw(page, "Отключить оптимизацию во весь экран", "Настоящий полноэкранный режим для старых игр", "fso", "game").Info("Возвращает классический эксклюзивный полноэкранный режим вместо «оптимизированного» Windows.", "В некоторых старых играх меньше задержка и нет тиринга.", "Медленнее Alt + Tab, не работает оверлей Game Bar. Современным играм не нужно.", null, "Включай только если в конкретной старой игре есть проблемы.");
			Tw(page, "Отключить запрос залипания клавиш", "Окно после пяти нажатий Shift больше не появляется", "sticky", "game").Info("Выключает сочетания, которые включают залипание, фильтрацию и озвучивание клавиш (5 раз Shift и т. д.).", "Окно «Залипание клавиш» не выбросит тебя из игры.", "Если тебе нужны эти специальные возможности — их можно включить в Параметрах.", true, "Включить.");
			Tw(page, "Game Bar с кнопки Xbox на геймпаде", "Кнопка Xbox не открывает Game Bar", "nexus", "game").Info("Запрещает открывать Xbox Game Bar кнопкой Xbox на геймпаде.", "Кнопка не будет мешать в играх и Steam Big Picture.", "Game Bar придётся открывать клавишами Win + G.", null, "Включай, если играешь с геймпадом в Steam.");
			PerfGames(page);
			page.Section(2);
			Tg(page, "Целостность памяти (HVCI)", "Выключение даёт прирост FPS, но снижает защиту. Некоторые античиты (FACEIT, Vanguard) требуют ВКЛ", "Get-HvciWanted", "Set-Hvci $v", "game").Expert().Info("Изоляция ядра: драйверы проверяются в защищённой виртуальной среде.", "Защищает от вредоносных драйверов и некоторых античит-обходов.", "Отнимает 5–10% FPS, особенно на процессорах до Intel 11 и Ryzen 3000.", null, "Если играешь с Vanguard (Valorant) или FACEIT — оставь включённой. Иначе можно выключить ради FPS.");
			Tg(page, "Быстрый запуск и гибернация", "В выключенном состоянии драйверы работают стабильнее", "(Get-FastBoot) -eq 'ВКЛ'", "Set-FastBoot $v", "game").Info("«Быстрый запуск» при выключении сохраняет ядро Windows на диск, чтобы следующий старт был быстрее.", "Включение на пару секунд быстрее.", "Драйверы и обновления не перезапускаются полностью, бывают странные глюки. Файл гибернации занимает гигабайты.", null, "На SSD быстрый запуск почти не нужен — можно выключить. Ноутбукам гибернация бывает полезна.");
			Tw(page, "Отключить SysMain (Superfetch)", "Служба предзагрузки программ в память", "sysmain", "game").Info("Выключает службу SysMain, которая заранее загружает часто используемые программы в память.", "На SSD меньше фоновой нагрузки на диск, освобождается память.", "На HDD программы будут открываться медленнее.", null, "Выключай на SSD, оставляй на HDD. Применяется после перезагрузки.");
			Tw(page, "Отключить индексирование поиска", "Служба Windows Search", "wsearch", "game").Info("Выключает службу индексирования содержимого файлов.", "Меньше фоновой нагрузки на диск и процессор.", "Поиск файлов в Пуске и Проводнике станет заметно медленнее, не будет поиска в Outlook.", null, "Выключай, только если не пользуешься поиском Windows.");
			PerfSys(page);
			// ---------- apps ----------
			page = NewPage("apps", Glyph.Apps, "Программы", "Программы", "Установленные программы, встроенные приложения Windows и автозагрузка");
			page.AddTabs("Установленные", "Встроенные", "Автозагрузка");
			BuildInstalled(page);
			page.Section(1);
			BuildApps(page);
			page.Section(2);
			BuildStartup(page);
			// ---------- network ----------
			page = NewPage("net", Glyph.Globe, "Сеть", "Сеть", "Подключение, пинг, DNS и сетевые настройки");
			page.AddTabs("Подключение", "Настройки", "Инструменты");
			BuildNet(page);
			BuildFixes();
			// ---------- maintenance ----------
			page = NewPage("system", Glyph.Disk, "Обслуживание", "Обслуживание", "Очистка диска, свободное место и проверка системы");
			BuildMaintenance(page);
			// ---------- restore ----------
			page = NewPage("restore", Glyph.Restore, "Восстановление", "Восстановление", "Точки восстановления и защита системы");
			BuildRestore(page);
			BuildReview();
			// ---------- bottom: history & settings ----------
			page = NewPage("history", Glyph.Clock, "История", "История изменений", "Всё, что сделала программа. Любое изменение можно откатить отдельно", bottom: true);
			_hist = page.Add(new HistoryView(compact: false));
			WireHistory(_hist);
			page = NewPage("settings", Glyph.Settings, "Настройки", "Настройки программы", "О программе, запуск и интерфейс", bottom: true);
			BuildSettings(page);
		}

		// page opened from the home score: every setting with a recommendation, the ones that differ first
		private void BuildReview()
		{
			Page page = NewPage("review", Glyph.Check, null, "Рекомендации", "Настройки, для которых у WinWeaver есть совет");
			// summary on top: ring with the share already set, one tile per category
			List<string> cats = new List<string>();
			foreach (SettingCard c in RecCards())
			{
				if (!cats.Contains(c.Cat ?? ""))
				{
					cats.Add(c.Cat ?? "");
				}
			}
			string[] labels = new string[cats.Count];
			for (int i = 0; i < cats.Count; i++)
			{
				labels[i] = HistoryView.CatName(cats[i]).ToUpperInvariant();
			}
			StatusHero head = page.Add(new StatusHero(_host, Glyph.Check, null, "Проверяю настройки…", labels, true, U("На главную", delegate
			{
				Show("home");
			})));
			SectionLabel badL = page.Add(new SectionLabel("Не как рекомендуем"));
			List<SettingCard> clones = new List<SettingCard>();
			foreach (SettingCard c in RecCards())
			{
				clones.Add(page.Add(c.CloneFor(_host)));
			}
			SectionLabel goodL = page.Add(new SectionLabel("Уже как рекомендуем"));
			page.AfterRefresh = delegate
			{
				List<IBlock> bad = new List<IBlock>();
				List<IBlock> good = new List<IBlock>();
				foreach (SettingCard c in clones)
				{
					if (c.Matches == false)
					{
						bad.Add(c);
					}
					else
					{
						good.Add(c);
					}
				}
				badL.Text = (bad.Count > 0) ? ("Можно улучшить — " + bad.Count) : "Улучшать нечего — всё настроено";
				goodL.Text = "Уже настроено — " + good.Count;
				object[] tiles = new object[cats.Count];
				for (int i = 0; i < cats.Count; i++)
				{
					int all = 0;
					int ok = 0;
					foreach (SettingCard c in clones)
					{
						if ((c.Cat ?? "") == cats[i] && c.Matches != null)
						{
							all++;
							if (c.Matches == true)
							{
								ok++;
							}
						}
					}
					tiles[i] = new Hashtable { { "L", labels[i] }, { "V", ok + " из " + all }, { "H", (ok == all) ? "всё настроено" : ("можно улучшить: " + (all - ok)) }, { "T", (ok == all) ? "ok" : "warn" }, { "P", (all > 0) ? ((float)ok / all) : 1f } };
				}
				int total = good.Count + bad.Count;
				head.Apply(new Hashtable
				{
					{ "Title", (bad.Count == 0) ? "Всё настроено" : ("Можно улучшить " + bad.Count + " " + Plural(bad.Count, "настройку", "настройки", "настроек")) },
					{ "Sub", "Настроено " + good.Count + " из " + total + ". Нажми на карточку ниже, чтобы узнать, что делает настройка" },
					{ "Tone", (bad.Count == 0) ? "ok" : "warn" },
					{ "Score", (total > 0) ? ((double)good.Count / total) : 1.0 },
					{ "Tiles", tiles }
				});

				page.Blocks.Clear();
				page.Blocks.Add(head);
				page.Blocks.Add(badL);
				page.Blocks.AddRange(bad);
				page.Blocks.Add(goodL);
				page.Blocks.AddRange(good);
				badL.Invalidate();
				goodL.Invalidate();
			};
		}

		private void BuildHome()
		{
			Page home = NewPage("home", Glyph.Home, "Главная", "Главная", "Состояние системы и автонастройка в один клик");
			_hero = home.Add(new Hero());
			WireReboot();
			_hero.ChipClicked += delegate(string k)
			{
				if (k == "privacy")
				{
					Show("privacy");
				}
				else if (k == "history")
				{
					Show("history");
				}
				else if (k == "fix")
				{
					FixFps();
				}
				else if (k == "reboot")
				{
					RebootMenu(Cursor.Position);
				}
				else if (k == "view")
				{
					Show("games");
				}
				else
				{
					_hero.PlayShine();
				}
			};
			home.Add(new SectionLabel("Автонастройка — выбери уровень"));
			LevelCard basic = new LevelCard("light", Glyph.Lock, "Базовая", "Приватность без удаления программ", new string[3] { "Без телеметрии, Copilot, рекламы и Bing", "Запрет автошифрования BitLocker", "Обои и название WeavOS" }, recommended: false);
			LevelCard optimal = new LevelCard("optimal", "\ue735", "Оптимальная", "Чистая и быстрая Windows на каждый день", new string[3] { "Всё из базовой + точка восстановления", "Без Edge, OneDrive и мусора — браузер на выбор", "Быстрый отклик и FPS без риска для работы" }, recommended: true);
			LevelCard gamer = new LevelCard("gamer", Glyph.Game, "Максимальная", "Максимум FPS, античиты работают", new string[3] { "Всё из оптимальной + максимум для игр", "Максимальная схема питания, HAGS, приоритет и точный таймер для игр", "Жертвует удобствами: поиском, анимациями, гибернацией" }, recommended: false) { Caution = "Не рекомендуется" };
			foreach (LevelCard c in new LevelCard[3] { basic, optimal, gamer })
			{
				_levels[c.Key] = c;
				c.Activated += RunLevel;
			}
			home.Add(new Grid(250, basic, optimal, gamer));
			_homeHist = home.Add(new HistoryView(compact: true));
			_homeHist.ShowAll = delegate
			{
				Show("history");
			};
			WireHistory(_homeHist);
		}

		private void WireHistory(HistoryView v)
		{
			v.UndoItem = delegate(string id, string title)
			{
				UndoHistory("[void](Undo-FHistory $fa1)", id, "i:" + id);
			};
			v.UndoGroup = delegate(string g, string title)
			{
				if (WDialog.Ask(this, "Откатить «" + title + "»?", "Все изменения этой группы, которые можно откатить, вернутся к прежним значениям — по очереди, от последнего к первому.\n\nУдалённые программы не возвращаются: для них есть точка восстановления.", "Откатить всё", "Отмена", DlgIcon.Warn))
				{
					UndoHistory("[void](Undo-FHistGroup $fa1)", g, "g:" + g);
				}
			};
		}

		private void UndoHistory(string script, string arg, string busyKey)
		{
			if (!_host.TryBegin(title: busyKey.StartsWith("g:") ? "Откат группы изменений" : "Откат изменения"))
			{
				return;
			}
			_hist.SetBusy(busyKey);
			_homeHist.SetBusy(busyKey);
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval(script, new Hashtable { { "fa1", arg } });
				return (object)null;
			}, delegate
			{
				_host.End();
				_cur?.Refresh(_host.Worker);
			});
		}

		private void RunLevel(LevelCard card)
		{
			if (_running != null || _host.IsBusy)
			{
				_status.Set("warn", "Подожди — ещё выполняется предыдущее действие");
				return;
			}
			string br = TuneDialog.Ask(this, card.Key, card.Title, card.Tagline, card.GlyphText);
			if (br == null)
			{
				return;
			}
			string opts = TuneDialog.LastOptions;
			string level = card.Key;
			if (!PersonalizeDialog.Ask(this, card.Title, ref opts, out string dev, out string pw, delegate (string o, Action<string[]> done)
			{
				_host.Worker.Run((IEngine e) => e.Eval("Get-FPlanNames $lvl $br $opts", new Hashtable
				{
					{ "lvl", level },
					{ "br", br },
					{ "opts", o }
				}), delegate (object r)
				{
					done(ToStrings(r));
				});
			}))
			{
				return;
			}
			StartLevel(card, br, opts, dev, pw);
		}

		public void OpenTune(string level)
		{
			if (_levels.TryGetValue(level, out var c))
			{
				RunLevel(c);
			}
		}

		public void StartLevel(string level, string browser)
		{
			if (_levels.TryGetValue(level, out var c))
			{
				StartLevel(c, browser);
			}
		}

		private static string[] ToStrings(object r)
		{
			if (r is string one)
			{
				return new string[1] { one };
			}
			List<string> l = new List<string>();
			if (r is IEnumerable en)
			{
				foreach (object o in en)
				{
					if (o != null)
					{
						l.Add(o.ToString());
					}
				}
			}
			return l.ToArray();
		}

		private void StartLevel(LevelCard card, string browser, string opts = "", string dev = "", string pw = "")
		{
			if (!_host.TryBegin(title: "Автонастройка — уровень «" + card.Title + "»"))
			{
				return;
			}
			_running = card;
			card.StartProgress();
			string level = card.Key;
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("[void](Invoke-FLevel $lvl $br $opts $dev $pw)", new Hashtable
				{
					{ "lvl", level },
					{ "br", browser },
					{ "opts", opts ?? "" },
					{ "dev", dev ?? "" },
					{ "pw", pw ?? "" }
				});
				pw = null;
				e.Eval("$pw = $null; $dev = $null");
				return (object)null;
			}, delegate
			{
				card.EndProgress();
				_running = null;
				_host.End();
				_cur?.Refresh(_host.Worker);
				_host.Worker.Run((IEngine e2) => e2.Eval("Get-FRebootInfo"), delegate(object r)
				{
					ApplyRebootInfo(r);
					OfferRebootAfterLevel(card.Title);
				});
			});
		}

		// engine messages; "[i/n]: step…" also drives the progress of the running level card
		private void OnEngineMsg(string kind, string text)
		{
			_status.Set(kind, text);
			if (_running == null || text == null)
			{
				return;
			}
			Match m = Regex.Match(text, "\\[(\\d+)/(\\d+)\\]:?\\s*(.*)$");
			if (m.Success)
			{
				_running.SetProgress(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Value.TrimEnd('…', '.', ' '));
			}
		}

		private void UpdateBadge()
		{
			_host.Worker.Run((IEngine e) => e.As(e.Eval("Get-FHistCount"), 0), delegate(object r)
			{
				_side.SetBadge("history", (r is int) ? ((int)r) : 0);
			});
		}

		private void BuildApps(Page p)
		{
			AppsView av = new AppsView();
			av.RemoveClicked += delegate(AppsView.Item it)
			{
				string title, body, hist, ps, note;
				if (it.Id == "edge")
				{
					title = "Удалить Microsoft Edge?";
					body = "Edge будет удалён, WebView2 останется. Проводник перезапустится.\n\nСначала поставь другой браузер — например, через автонастройку.";
					hist = "Удалён Microsoft Edge";
					ps = "Remove-Edge";
					note = "WebView2 оставлен. Edge можно скачать заново с microsoft.com/edge";
				}
				else if (it.Id == "onedrive")
				{
					title = "Удалить OneDrive?";
					body = "OneDrive будет удалён и заблокирован. Файлы, которые уже лежат в папке OneDrive на диске, останутся.";
					hist = "Удалён OneDrive";
					ps = "Remove-OneDrive";
					note = "Установщик OneDrive можно скачать с сайта Microsoft";
				}
				else
				{
					title = "Удалить «" + it.Name + "»?";
					body = it.Desc + ".\n\nПриложение удалится для всех пользователей. Вернуть его можно из Microsoft Store.";
					hist = "Удалено приложение: " + it.Name;
					ps = "Remove-FApp '" + it.Id.Replace("'", "''") + "'";
					note = "Вернуть можно из Microsoft Store";
				}
				if (!WDialog.Ask(this, title, body, "Удалить", "Отмена", DlgIcon.Warn) || !_host.TryBegin(title: "Удаление: " + it.Name))
				{
					return;
				}
				if (it.Btn != null)
				{
					it.Btn.Enabled = false;
					it.Btn.Text = "Удаляю…";
				}
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked $fa1 $fa2 -Cat 'apps' -NoUndo -Note $fa3", new Hashtable
					{
						{ "fa1", hist },
						{ "fa2", ps },
						{ "fa3", note }
					});
					return (object)null;
				}, delegate
				{
					_host.End();
					p.Refresh(_host.Worker);
				});
			};
			av.AddButton("Удалить рекомендованные", BtnKind.Silver, delegate
			{
				List<AppsView.Item> list = new List<AppsView.Item>();
				foreach (AppsView.Item it in av.Items)
				{
					if (it.Installed && it.Rec == 1 && !it.System)
					{
						list.Add(it);
					}
				}
				if (list.Count == 0)
				{
					_status.Set("ok", "Рекомендованные приложения уже удалены");
					return;
				}
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				foreach (AppsView.Item it in list)
				{
					sb.Append("•  ").Append(it.Name).Append('\n');
				}
				if (!WDialog.Ask(this, "Удалить приложения (" + list.Count + ")?", sb.ToString() + "\nПогода, Outlook, To Do и «Связь с телефоном» не трогаются — их можно удалить отдельно. Вернуть любое приложение можно из Microsoft Store.", "Удалить", "Отмена", DlgIcon.Warn) || !_host.TryBegin(title: "Удаление приложений (" + list.Count + ")"))
				{
					return;
				}
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Start-FGroup 'Удаление встроенных приложений'");
					try
					{
						foreach (AppsView.Item it in list)
						{
							e.Eval("Invoke-FTracked $fa1 $fa2 -Cat 'apps' -NoUndo -Note 'Вернуть можно из Microsoft Store'", new Hashtable
							{
								{ "fa1", "Удалено приложение: " + it.Name },
								{ "fa2", "Remove-FApp '" + it.Id.Replace("'", "''") + "'" }
							});
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
					p.Refresh(_host.Worker);
				});
			});
			av.AddButton("Вернуть настройки после обновления", BtnKind.Ghost, delegate
			{
				if (WDialog.Ask(this, "Вернуть настройки после обновления?", "Крупные обновления Windows возвращают встроенные приложения, Edge, OneDrive, телеметрию и рекламу. Они снова будут удалены и выключены. Проводник перезапустится.", "Вернуть", "Отмена") && _host.TryBegin(title: "Возврат настроек после обновления"))
				{
					_host.Worker.Run(delegate(IEngine e)
					{
						e.Eval("Invoke-FTracked 'Настройки возвращены после обновления Windows' 'Invoke-Repair' -Cat 'apps'");
						return (object)null;
					}, delegate
					{
						_host.End();
						p.Refresh(_host.Worker);
					});
				}
			});
			av.AddButton("Обновить", BtnKind.Ghost, delegate
			{
				p.Refresh(_host.Worker);
			}, "\ue72c");
			p.Add(av);
		}

		private void BuildStartup(Page p)
		{
			StartupView sv = new StartupView();
			WireStartup(sv, p);
			sv.ItemToggled += delegate(StartupView.Item it, bool on)
			{
				if (!_host.TryBegin(quick: true, title: it.Name + (on ? ": включить в автозагрузке" : ": выключить из автозагрузки")))
				{
					sv.Revert(it);
					return;
				}
				string id = it.Id;
				string name = it.Name;
				it.Toggle.Busy = true;
				_host.Worker.Run(delegate(IEngine e)
				{
					string undo = "Set-FStartup '" + id.Replace("'", "''") + "' $" + (on ? "false" : "true");
					e.Eval("Invoke-FTracked $fa1 'Set-FStartup $id $on' -Undo $fa2 -Cat 'apps'", new Hashtable
					{
						{ "fa1", "Автозагрузка: " + name + (on ? " — включена" : " — выключена") },
						{ "fa2", undo },
						{ "id", id },
						{ "on", on }
					});
					return (object)null;
				}, delegate
				{
					it.Toggle.Busy = false;
					_host.End(quick: true);
					sv.Invalidate();
				});
			};
			sv.AddButton("Обновить", BtnKind.Silver, delegate
			{
				p.Refresh(_host.Worker);
			}, "\ue72c");
			sv.AddButton("Папка", BtnKind.Ghost, delegate
			{
				Shell.Open("explorer.exe", "shell:startup");
			});
			sv.AddButton("Диспетчер задач", BtnKind.Ghost, delegate
			{
				Shell.Open("taskmgr.exe", "/7 /startup");
			});
			p.Add(sv);
		}

		private void BuildRestore(Page p)
		{
			RestoreView rv = new RestoreView { RefreshWorker = _host.Worker };
			rv.CreateClicked += delegate
			{
				if (_host.TryBegin(title: "Создание точки восстановления"))
				{
					_status.Set("info", "Создаю точку восстановления (до минуты)…");
					_host.Worker.Run((IEngine en) => en.As(en.Eval("New-FRestorePointNow"), fallback: false), delegate(object r)
					{
						if (r is bool && (bool)r)
						{
							_host.Worker.Run((IEngine en) => en.Eval("Add-FHist 'Создана точка восстановления' 'restore' '' $null 'К ней можно откатить всю систему в разделе «Восстановление»'"), null);
						}
						_host.End();
						p.Refresh(_host.Worker);
					});
				}
			};
			rv.EnableClicked += delegate
			{
				if (_host.TryBegin(title: "Включение защиты системы"))
				{
					_host.Worker.Run((IEngine en) => en.Eval("Enable-FRestore"), delegate
					{
						_host.End();
						p.Refresh(_host.Worker);
					});
				}
			};
			rv.RollbackClicked += delegate(RestoreView.Item it)
			{
				if (WDialog.Ask(this, "Откатить систему?", "Точка №" + it.Seq + ": " + it.Date + " — " + it.Desc + "\n\nКомпьютер сразу перезагрузится. Личные файлы не удаляются, но программы и драйверы, установленные после этой даты, пропадут.", "Откатить и перезагрузить", "Отмена", DlgIcon.Warn) && _host.TryBegin(title: "Откат системы к точке №" + it.Seq))
				{
					int seq = it.Seq;
					_host.Worker.Run((IEngine en) => en.As(en.Eval("Write-FLog \"RESTORE to $seq\"; Restore-FFromPoint $seq", new Hashtable { { "seq", seq } }), fallback: false), delegate(object r)
					{
						_host.End();
						if (!(r is bool) || !(bool)r)
						{
							_status.Set("err", "Windows не дала запустить откат. Попробуй «Мастер восстановления».");
						}
					});
				}
			};
			rv.DeleteClicked += delegate(RestoreView.Item it)
			{
				if (WDialog.Ask(this, "Удалить точку?", "Точка №" + it.Seq + ": " + it.Date + " — " + it.Desc + "\n\nК ней больше нельзя будет откатиться.", "Удалить", "Отмена", DlgIcon.Warn) && _host.TryBegin(title: "Удаление точки №" + it.Seq))
				{
					int seq = it.Seq;
					_host.Worker.Run((IEngine en) => en.Eval("[void](Remove-FRestorePoint $seq)", new Hashtable { { "seq", seq } }), delegate
					{
						_host.End();
						p.Refresh(_host.Worker);
					});
				}
			};
			rv.DeleteOldClicked += delegate
			{
				if (WDialog.Ask(this, "Удалить старые точки?", "Останется только последняя точка (" + (rv.Count - 1) + " старых будет удалено). Это освободит место на диске.", "Удалить старые", "Отмена", DlgIcon.Warn) && _host.TryBegin(title: "Удаление старых точек"))
				{
					_host.Worker.Run((IEngine en) => en.Eval("[void](Remove-FOldRestorePoints)"), delegate
					{
						_host.End();
						p.Refresh(_host.Worker);
					});
				}
			};
			p.Add(rv);
			p.Add(new SectionLabel("Настройки точек"));
			Tw(p, "Создавать точки без лимита", "Windows по умолчанию разрешает только одну точку в сутки", "rpfreq", "restore").Info("Снимает встроенное ограничение «не чаще раза в 24 часа» для новых точек восстановления.", "Точка создаётся каждый раз, когда ты её просишь — в том числе перед автонастройкой.", "Если программы часто создают точки, они быстрее занимают отведённое место (старые удаляются автоматически).", true, "Включи: без этого кнопка «Создать точку» иногда молча ничего не делает.");
			ChT(p, "Автоматические точки", "WinWeaver сам создаёт точку по расписанию", new string[3] { "Выкл.", "Раз в неделю", "Каждый день" }, "Get-FTweakIdx 'rpauto'", "Set-FTweakIdx 'rpauto' $v", "restore").Info("Задача планировщика от имени системы создаёт точку восстановления по расписанию.", "Всегда есть свежая точка, даже если забыл сделать её вручную.", "Каждая точка занимает место; при нехватке Windows удаляет самые старые.", null, "«Раз в неделю» — хороший баланс между защитой и местом на диске.").Opts("Точки создаются только вручную или самой Windows перед установкой обновлений и драйверов.", "Свежая точка раз в неделю — хороший баланс между защитой и местом на диске.", "Точка каждый день: откатиться можно почти к любому моменту, но старые точки вытесняются быстрее.");
			ChT(p, "Место под точки", "Сколько диска " + "Windows может занять точками восстановления", new string[4] { "3%", "5%", "10%", "15%" }, "Get-FTweakIdx 'rpspace'", "Set-FTweakIdx 'rpspace' $v", "restore").Info("Лимит теневого хранилища (vssadmin) на системном диске.", "Больше места — больше точек хранится одновременно.", "Это место недоступно для файлов.", null, "Для SSD на 256–512 ГБ хватит 5%, на больших дисках — 3%.").Opts("Минимум места: хватит на несколько последних точек. Подходит для небольших SSD.", "Обычный запас — 5–10 точек на большинстве дисков.", "Хранится заметно больше точек, можно откатиться дальше в прошлое.", "Максимальная история точек, но это место недоступно для файлов.");
		}

		private void BuildNet(Page page)
		{
			NetView nv = page.Add(new NetView(_host));
			nv.DnsPicked += delegate(int idx)
			{
				string[] names = new string[5] { "Авто (от провайдера)", "Cloudflare", "Google", "Quad9", "AdGuard" };
				if (_host.TryBegin(title: "DNS: " + names[idx]))
				{
					_status.Set("info", "Переключаю DNS…");
					_host.Worker.Run((IEngine en) => en.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
					{
						{ "fa1", "DNS-сервер: " + names[idx] },
						{ "fa2", "Get-FDns" },
						{ "fa3", "Set-FDns $v" },
						{ "fa4", idx },
						{ "fa5", "net" }
					}), delegate
					{
						_host.End();
						page.Refresh(_host.Worker);
					});
				}
			};
			page.Section(1);
			page.Add(new SectionLabel("Скорость и пинг"));
			Tw(page, "Отключить алгоритм Нейгла", "Пакеты уходят сразу, без склейки — меньше задержка в онлайн-играх", "nagle", "net").Info("Для каждого сетевого адаптера включает TcpNoDelay и TcpAckFrequency = 1: Windows перестаёт копить мелкие TCP-пакеты и ждать подтверждений.", "В играх и программах на TCP (MMO, некоторые шутеры, удалённый рабочий стол) отклик может стать ровнее.", "Чуть больше мелких пакетов в сети. Игры на UDP (большинство современных шутеров) эффекта почти не почувствуют.", null, "Безопасно попробовать, если играешь в онлайн-игры. Применяется к текущим адаптерам.");
			Tw(page, "Снять ограничение сети для мультимедиа", "NetworkThrottlingIndex — Windows не урезает сеть во время воспроизведения", "netthrottle", "net").Info("Отключает механизм, который ограничивает обработку сетевых пакетов, пока играет звук или видео.", "Меньше скачков пинга, когда параллельно играет музыка, стрим или голосовой чат.", "На очень слабых процессорах звук может чаще прерываться под сетевой нагрузкой.", null, "Включён в игровой профиль. Применяется после перезагрузки.");
			ChT(page, "Автонастройка окна TCP", "Как Windows подбирает размер буфера для скачивания", new string[3] { "Нормальная", "Ограниченная", "Выключена" }, "Get-FTweakIdx 'tcpauto'", "Set-FTweakIdx 'tcpauto' $v", "net").Info("Механизм TCP Window Auto-Tuning: буфер приёма растёт под скорость канала.", "«Нормальная» даёт полную скорость на быстрых каналах. «Ограниченная» иногда помогает со старыми роутерами.", "«Выключена» сильно режет скорость загрузок на каналах быстрее 10 Мбит/с — старый «твик», который давно вредит.", 0, "Держи «Нормальную». Меняй, только если со старым роутером рвутся загрузки.").Opts("Буфер приёма растёт под скорость канала — полная скорость загрузок. Стандарт Windows.", "Буфер растёт осторожнее. Иногда помогает со старыми роутерами, на которых рвутся загрузки.", "Буфер фиксирован: на каналах быстрее 10 Мбит/с скорость загрузки сильно падает. Старый вредный «твик».");
			Tw(page, "Распределять сеть по ядрам (RSS)", "Receive Side Scaling — обработка пакетов на нескольких ядрах процессора", "rss", "net").Info("Обработка входящего трафика распределяется по ядрам процессора вместо одного.", "Выше скорость и ниже задержка при большом трафике, ядро 0 не захлёбывается.", "Почти нет. Выключать имеет смысл только для диагностики старых драйверов.", true, "Должно быть включено — это настройка по умолчанию.");
			Tw(page, "ECN — уведомление о перегрузке", "Роутер предупреждает о перегрузке вместо потери пакетов", "ecn", "net").Info("Explicit Congestion Notification: устройства в сети помечают пакеты при перегрузке, и скорость снижается плавно, без потерь.", "С современным оборудованием — меньше потерь и скачков пинга под нагрузкой.", "Редкие старые роутеры и провайдеры отбрасывают такие пакеты — сайты начинают открываться через раз.", null, "Попробуй включить; если появятся проблемы с сайтами — выключи обратно.");
			Tw(page, "Резерв канала QoS — 0%", "Политика планировщика пакетов: не держать полосу «про запас»", "qosres", "net").Info("Задаёт NonBestEffortLimit = 0 в политике QoS.", "Ничего не резервируется для приоритетного трафика.", "Windows и так не отнимает 20% канала без QoS-приложений — эффект обычно нулевой.", null, "Популярный твик с небольшой пользой. Включай, если хочешь «на всякий случай». После перезагрузки.");
			Tw(page, "Не усыплять сетевую карту", "Windows не отключает адаптер ради экономии энергии", "nicpower", "net").Info("Снимает разрешение «Разрешить отключение этого устройства для экономии энергии» у активных адаптеров.", "Нет обрывов связи и долгого «просыпания» Wi-Fi после простоя.", "Ноутбук чуть дольше разряжается в простое.", null, "Включи на ПК и если Wi-Fi отваливается после сна. Адаптер может на секунду переподключиться.");
			Tw(page, "IPv4 в приоритете над IPv6", "IPv6 остаётся включённым, но соединения сначала идут по IPv4", "ipv4pref", "net").Info("Меняет таблицу предпочтений (DisabledComponents = 0x20), не отключая IPv6.", "Помогает, если у провайдера кривой IPv6 и сайты открываются с задержкой.", "При хорошем IPv6 преимуществ нет.", null, "Безопаснее, чем полностью выключать IPv6. Применяется после перезагрузки.");
			page.Add(new SectionLabel("Безопасность и приватность"));
			Tw(page, "Шифрованный DNS (DoH)", "Запросы к DNS шифруются, провайдер не видит, какие сайты ты открываешь", "doh", "net").Info("Разрешает Windows автоматически использовать DNS-over-HTTPS для известных серверов (Cloudflare, Google, Quad9, AdGuard).", "Провайдер и сеть Wi-Fi не видят и не подменяют DNS-запросы.", "Работает, только если выбран один из этих DNS-серверов. С «Авто» ничего не меняется.", true, "Включи и выбери Cloudflare, Quad9 или AdGuard на вкладке «Подключение».").Builds(22000);
			Tw(page, "Отключить LLMNR", "Устаревший способ искать компьютеры в локальной сети", "llmnr", "net").Info("Выключает Link-Local Multicast Name Resolution через политику DNS-клиента.", "Закрывает известную атаку в публичных сетях: перехват паролей через поддельные ответы (Responder).", "В старых домашних сетях компьютер может перестать находиться по имени — по IP всё работает.", true, "Включи: в обычной домашней сети ничего не сломается.");
			Tw(page, "Отключить NetBIOS через TCP/IP", "Ещё более старый протокол имён из времён Windows XP", "netbios", "net").Info("Для всех интерфейсов NetBIOS переводится в «Отключить».", "Меньше «болтовни» в сети и поверхности для атак.", "Ломает доступ к очень старым NAS и принтерам, которые видны только по NetBIOS-имени.", null, "Включай, если нет старых сетевых устройств. Применяется после перезагрузки.");
			Tw(page, "Не подключаться к открытым Wi-Fi сами", "Windows не цепляется автоматически к предложенным хот-спотам", "hotspot", "net").Info("Выключает автоподключение к открытым точкам доступа, которые предлагает Windows.", "Ноутбук не окажется незаметно в чужой открытой сети.", "Нет.", true, "Включи, особенно на ноутбуке.");
			Tw(page, "Отключить Teredo", "Туннель IPv6 поверх IPv4", "teredo", "net").Info("Выключает адаптер Teredo, через который Windows строит IPv6-туннель.", "Меньше лишних подключений и туннелей.", "Чат и пати в Xbox-приложении и некоторые P2P-игры перестанут подключаться («Teredo не может пройти проверку»).", null, "Не трогай, если играешь через Xbox / Game Pass с друзьями.");
			page.Section(2);
			page.Add(new Grid(380, ActT(Glyph.Globe, "Очистить кэш DNS", "Если сайт переехал, а браузер открывает старый адрес", A("Очистить", "Clear-DnsClientCache; Show-Ok 'Кэш DNS очищен'", BtnKind.Silver)), ActT(Glyph.Sync, "Получить IP заново", "ipconfig /renew — связь пропадёт на пару секунд", C(A("Обновить IP", "[void](Invoke-FNative 'ipconfig.exe' '/release'); [void](Invoke-FNative 'ipconfig.exe' '/renew'); Show-Ok 'IP-адрес получен заново'", BtnKind.Silver), "Получить IP-адрес заново?", "Подключение пропадёт на несколько секунд, загрузки и звонки прервутся.")), ActT(Glyph.Refresh, "Сбросить кэш ARP", "Помогает после смены роутера или IP-адресов", A("Сбросить", "[void](Invoke-FNative 'netsh.exe' 'interface ip delete arpcache'); Show-Ok 'Кэш ARP очищен'", BtnKind.Silver)), ActT(Glyph.Repair, "Сбросить сеть", "Winsock и TCP/IP по умолчанию. Нужна перезагрузка", C(T("Сбросить", "Сброшены Winsock и TCP/IP", "& netsh.exe winsock reset 2>&1 | Out-Null; & netsh.exe int ip reset 2>&1 | Out-Null; $global:NeedReboot = $true; Show-Ok 'Сетевые настройки сброшены. Перезагрузи компьютер'", "net", null, BtnKind.Silver, true), "Сбросить сетевые настройки?", "Будут сброшены Winsock и TCP/IP. Это помогает, если интернет пропал или работает странно. Потребуется перезагрузка.")), ActT(Glyph.Info, "Файл hosts", "Ручная блокировка или перенаправление сайтов", U("Открыть", delegate
			{
				Shell.Open("notepad.exe", Path.Combine(Shell.WinDir, "System32\\drivers\\etc\\hosts"));
			}, BtnKind.Silver)), ActT(Glyph.Monitor, "Сетевые адаптеры", "Классическое окно сетевых подключений Windows", U("Открыть", delegate
			{
				Shell.Open("ncpa.cpl");
			}, BtnKind.Silver), U("Параметры сети", delegate
			{
				Shell.Open("ms-settings:network");
			}))));
		}

	}
}
