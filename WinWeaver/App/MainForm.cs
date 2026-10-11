using AI = Avalonia.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WinWeaver;

internal class MainForm : Form
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
			foreach (Page value in _f._pages.Values)
			{
				value.ForgetData();
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

	private readonly TopNav _side = new TopNav();

	private readonly CommandPalette _palette = new CommandPalette();

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


	private Page _navTo;

	private readonly List<SettingCard> _tracked = new List<SettingCard>();

	private bool _scoreDirty = true;

	private int _scoreSeq;

	private static string[] Days = new string[4] { "Нет", "7 дней", "14 дней", "30 дней" };

	private Action _blKick;

	private bool _sysReboot;

	private DateTime _rebootAt = DateTime.MinValue;

	private Timer _rbTimer;

	private static readonly object[][] FpsMap = new object[24][]
	{
		new object[4] { "Схема питания", 10, 0, 1 },
		new object[3] { "Целостность памяти (HVCI)", 9, false },
		new object[3] { "Отключить виртуализацию (VBS)", 8, true },
		new object[3] { "Фоновая запись игр (Game DVR)", 8, false },
		new object[3] { "Запретить запись игр политикой", 3, true },
		new object[3] { "Аппаратное планирование GPU (HAGS)", 6, true },
		new object[3] { "Игровой режим", 6, true },
		new object[3] { "Оптимизация оконных игр", 5, true },
		new object[3] { "Высокий приоритет игр (MMCSS)", 5, true },
		new object[3] { "Распределение процессорного времени", 5, 2 },
		new object[3] { "Отключить Power Throttling", 4, true },
		new object[3] { "Приоритет активным программам (MMCSS)", 4, true },
		new object[3] { "Отключить оптимизацию во весь экран", 3, true },
		new object[3] { "Точный системный таймер", 3, true },
		new object[3] { "Отключить MPO", 3, true },
		new object[3] { "Запретить фоновые приложения", 3, true },
		new object[3] { "Отключить динамический тик", 2, true },
		new object[3] { "Ускорение мыши", 2, false },
		new object[3] { "Отключить SysMain (Superfetch)", 2, true },
		new object[3] { "Отключить лишние службы", 2, true },
		new object[3] { "Отключить сжатие памяти", 1, true },
		new object[3] { "Отключить индексирование поиска", 1, true },
		new object[3] { "Не выгружать ядро из памяти", 1, true },
		new object[3] { "Не обновлять метку доступа к файлам", 1, true }
	};

	private LoadScreen _load;

	private Worker _reader;

	private int _actStamp;

	private const string WarmTitle = "Читаю разделы";

	private readonly List<Page> _warm = new List<Page>();

	private Page _warming;

	private int _warmTotal;

	private bool _warmBg;

	private bool _extrasWired;

	private UpdateChecker.Result _update;

	private bool _skipCloseAsk;

	public Page Current => _cur;

	public IEnumerable<string> PageKeys => _pages.Keys;

	private bool RebootScheduled => _rebootAt > DateTime.Now;

	private Worker ReadWorker
	{
		get
		{
			if (_reader == null || !_reader.Engine.Ready || _host.IsBusy)
			{
				return _host.Worker;
			}
			return _reader;
		}
	}

	public MainForm(IEngine engine, string mode, bool fake)
	{
		Mode = mode ?? "";
		_fake = fake;
		Text = "WinWeaver";
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Theme.Bg;
		ForeColor = Theme.Text;
		Font = Theme.Body;
		base.StartPosition = FormStartPosition.CenterScreen;
		DoubleBuffered = true;
		Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
		base.Size = new Size(Math.Min(Theme.S(1220f), workingArea.Width - 40), Math.Min(Theme.S(820f), workingArea.Height - 40));
		MinimumSize = new Size(Math.Min(Theme.S(980f), workingArea.Width - 40), Math.Min(Theme.S(660f), workingArea.Height - 40));
		string environmentVariable = Environment.GetEnvironmentVariable("WW_WIN");
		if (!string.IsNullOrEmpty(environmentVariable) && environmentVariable.Contains("x"))
		{
			try
			{
				string[] array = environmentVariable.Split('x');
				base.Size = new Size(int.Parse(array[0]), int.Parse(array[1]));
			}
			catch
			{
			}
		}
		base.Icon = WDialog.AppIcon;
		try
		{
			using Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("logo.png");
			if (stream != null)
			{
				_side.Logo = Image.FromStream(stream);
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
		_side.HeightChanged += delegate
		{
			PerformLayout();
		};
		// Экран загрузки — сразу, чтобы первый же кадр окна был им, а не «замёрзшим» интерфейсом.
		_load = new LoadScreen
		{
			Logo = _side.Logo
		};
		base.Controls.Add(_load);
		base.Controls.Add(_palette);
		_palette.Visible = false;
		_palette.Source = PaletteItems;
		_side.SearchClicked += _palette.Open;
		Window.AddHandler(AI.InputElement.KeyDownEvent, delegate(object s, AI.KeyEventArgs ke)
		{
			if (ke.Key == AI.Key.K && (ke.KeyModifiers & AI.KeyModifiers.Control) != 0)
			{
				ke.Handled = true;
				_palette.Open();
			}
			else if (!_palette.Visible && _side.Enabled && ke.Key == AI.Key.Tab && (ke.KeyModifiers & AI.KeyModifiers.Control) != 0)
			{
				ke.Handled = true;
				_side.Step(((ke.KeyModifiers & AI.KeyModifiers.Shift) != 0) ? (-1) : 1);
			}
			else if (!_palette.Visible && _side.Enabled && ke.Key >= AI.Key.D1 && ke.Key <= AI.Key.D9 && (ke.KeyModifiers & AI.KeyModifiers.Control) != 0)
			{
				ke.Handled = true;
				_side.Jump(ke.Key - AI.Key.D1);
			}
		}, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
		if (engine is FakeEngine fakeEngine)
		{
			fakeEngine.Ui = ui;
		}
		_host.Worker = new Worker(this, engine)
		{
			Name = "основной"
		};
		if (!(engine is FakeEngine))
		{
			try
			{
				_reader = new Worker(this, Program.CreatePsEngine(new EngineUI(null, null), mode))
				{
					Name = "чтение"
				};
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
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		_host.Worker?.Dispose();
		_reader?.Dispose();
		base.OnFormClosed(e);
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		base.OnLayout(e);
		Size clientSize = base.ClientSize;
		int num = _side.DesiredHeight;
		_side.SetBounds(0, 0, clientSize.Width, num);
		_content.SetBounds(0, num, clientSize.Width, Math.Max(0, clientSize.Height - num));
		_status.Place(new Rectangle(0, num, Math.Max(0, clientSize.Width - Theme.S(14f)), Math.Max(0, clientSize.Height - num)));
		_status.BringToFront();
		_palette.SetBounds(0, 0, clientSize.Width, clientSize.Height);
		if (_load != null && !_load.IsDisposed)
		{
			_load.SetBounds(0, 0, clientSize.Width, clientSize.Height);
			_load.BringToFront();
		}
		if (_palette.Visible)
		{
			_palette.BringToFront();
		}
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
		Lag.RemoveOldDesktopLog();
		if (Environment.GetEnvironmentVariable("WW_LAG") == "1")
		{
			Lag.Start();
		}
		StartWithLoadScreen();
	}

	private void AfterLoaded()
	{
		Timer warm = new Timer
		{
			Interval = 400
		};
		warm.Tick += delegate
		{
			warm.Stop();
			warm.Dispose();
			_palette.Warm();
		};
		warm.Start();
		if (Mode == "/autostart" && Prefs.AutoChoice == null)
		{
			AskAutostart();
		}
		AfterLoadedExtras();
	}

	private void AskAutostart()
	{
		bool flag = WDialog.Ask(this, "Автозапуск WinWeaver", "WinWeaver открылся автоматически при входе в систему.\n\nЗдесь настраиваются приватность, оформление, производительность, программы и обслуживание, а в «Истории» можно откатить любое изменение.\n\nОставить автозапуск? Изменить это можно в любой момент в разделе «Настройки».", "Оставить автозапуск", "Не запускать");
		Prefs.AutoChoice = (flag ? "yes" : "no");
		_status.Set("ok", flag ? "Автозапуск оставлен" : "Автозапуск выключен. Программа есть на Рабочем столе и в Пуске");
		if (_pages.TryGetValue("settings", out var value))
		{
			value.Refresh(_host.Worker);
		}
	}

	public void Show(string key)
	{
		long t = Lag.T0();
		try
		{
			ShowCore(key);
		}
		finally
		{
			Lag.Done("Show " + key, t);
			Lag.Context = "раздел " + (_navTo ?? _cur)?.Key;
		}
	}

	private void ShowCore(string key)
	{
		if (!_pages.TryGetValue(key, out var value) || (_navTo ?? _cur) == value)
		{
			return;
		}
		_navTo = value;
		if (!_fake && Prefs.RememberPage) Prefs.LastPage = key;
		_palette.Close();
		_side.Select(key);
		Action action = delegate
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
			Transition.Play(_content, action, 0, 210);
		}
		else
		{
			action();
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
		if (Prefs.RebootReminder && NeedReboot && !RebootScheduled && e.CloseReason == CloseReason.UserClosing && WDialog.Ask(this, "Нужна перезагрузка", "Часть изменений вступит в силу только после перезагрузки компьютера.\n\nПерезагрузить сейчас?", "Перезагрузить", "Позже", DlgIcon.Warn))
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

	private SettingCard Tg(Page p, string title, string desc, string get, string set, string cat)
	{
		SettingCard settingCard = p.Add(SettingCard.Tracked(_host, title, desc, get, set, null, cat));
		_tracked.Add(settingCard);
		return settingCard;
	}

	private SettingCard ChT(Page p, string title, string desc, string[] opts, string get, string set, string cat)
	{
		SettingCard settingCard = p.Add(SettingCard.Tracked(_host, title, desc, get, set, opts, cat));
		_tracked.Add(settingCard);
		return settingCard;
	}

	private List<SettingCard> RecCards()
	{
		return _tracked.FindAll((SettingCard c) => c.Rec != null && !c.Unsupported);
	}

	private void UpdateScore()
	{
		if (_hero == null || !_host.Worker.Engine.Ready)
		{
			return;
		}
		if (_host.IsBusy)
		{
			_scoreDirty = true;
			return;
		}
		_scoreDirty = false;
		int seq = ++_scoreSeq;
		List<SettingCard> cards = RecCards();
		foreach (SettingCard item in FpsCards())
		{
			if (!cards.Contains(item))
			{
				cards.Add(item);
			}
		}
		_host.Worker.Run(delegate(IEngine e)
		{
			object[] array2 = new object[cards.Count];
			for (int j = 0; j < cards.Count; j++)
			{
				try
				{
					array2[j] = cards[j].Fetch(e);
				}
				catch
				{
				}
			}
			return array2;
		}, delegate(object o)
		{
			if (o is object[] array && seq == _scoreSeq)
			{
				for (int i = 0; i < cards.Count && i < array.Length; i++)
				{
					if (array[i] != null)
					{
						cards[i].Apply(array[i]);
					}
				}
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				foreach (SettingCard item2 in FpsCards())
				{
					bool? fpsOk = item2.FpsOk;
					if (fpsOk.HasValue && !IsFpsRisky(item2))
					{
						num += item2.FpsW;
						if (fpsOk.GetValueOrDefault())
						{
							num2 += item2.FpsW;
						}
						else
						{
							num3++;
						}
					}
				}
				int ok = ((num > 0) ? ((int)Math.Round(100.0 * (double)num2 / (double)num)) : 0);
				UpdateRecBadges();
				_hero.SetScore(ok, 100, num3);
			}
		});
	}

	private void UpdateRecBadges()
	{
		Dictionary<string, int> per = new Dictionary<string, int>();
		int total = 0;
		foreach (SettingCard c in RecCards())
		{
			if (c.Matches == false)
			{
				total++;
				string k = PageOf(c);
				if (k != null)
				{
					per[k] = (per.TryGetValue(k, out var n) ? n : 0) + 1;
				}
			}
		}
		_side.SetBadge("review", total);
		Dictionary<string, int> all = new Dictionary<string, int>();
		int known = 0;
		foreach (SettingCard c2 in RecCards())
		{
			if (c2.Matches.HasValue)
			{
				known++;
				string k2 = PageOf(c2);
				if (k2 != null)
				{
					all[k2] = (all.TryGetValue(k2, out var n3) ? n3 : 0) + 1;
				}
			}
		}
		if (_hero != null && known > 0)
		{
			int Pct(string k)
			{
				int t = all.TryGetValue(k, out var a) ? a : 0;
				int b = per.TryGetValue(k, out var bb) ? bb : 0;
				return (t > 0) ? ((int)Math.Round(100.0 * (t - b) / t)) : 100;
			}
			string top = null;
			int topN = 0;
			foreach (KeyValuePair<string, int> kv in per)
			{
				if (kv.Value > topN && _pages.TryGetValue(kv.Key, out var tp))
				{
					topN = kv.Value;
					top = (tp.Title == "Приватность и защита") ? "Приватность" : tp.Title;
				}
			}
			_hero.SetIndex((int)Math.Round(100.0 * (known - total) / known), Pct("privacy"), Pct("ui"), total, top);
		}
		foreach (string k in new string[] { "games", "privacy", "ui", "net", "apps", "system" })
		{
			_side.SetBadge(k, per.TryGetValue(k, out var n2) ? n2 : 0);
		}
	}

	private string PageOf(SettingCard c)
	{
		foreach (Page value in _pages.Values)
		{
			if (value.Key == "review")
			{
				continue;
			}
			foreach (IBlock item in Flatten(value.Blocks))
			{
				if (item == c)
				{
					return value.Key;
				}
			}
		}
		return null;
	}

	private static string Plural(int n, string one, string few, string many)
	{
		if (!L.IsRu)
		{
			if (n != 1)
			{
				return many;
			}
			return one;
		}
		int num = n % 100;
		int num2 = n % 10;
		if (num >= 11 && num <= 14)
		{
			return many;
		}
		switch (num2)
		{
		default:
			return many;
		case 2:
		case 3:
		case 4:
			return few;
		case 1:
			return one;
		}
	}

	private void FixRecommended()
	{
		List<SettingCard> bad = RecCards().FindAll((SettingCard c) => c.Matches == false);
		if (bad.Count == 0)
		{
			return;
		}
		List<CheckDialog.Row> list = new List<CheckDialog.Row>();
		List<SettingCard> list2 = new List<SettingCard>();
		List<string> list3 = new List<string>();
		foreach (SettingCard item in bad)
		{
			if (!list3.Contains(item.Cat ?? ""))
			{
				list3.Add(item.Cat ?? "");
			}
		}
		foreach (string item2 in list3)
		{
			list.Add(new CheckDialog.Row
			{
				Header = true,
				Text = HistoryView.CatName(item2)
			});
			list2.Add(null);
			foreach (SettingCard item3 in bad)
			{
				if (!((item3.Cat ?? "") != item2))
				{
					string tag = ((item3.Seg != null) ? ("→ " + item3.Seg.Items[(int)item3.Rec]) : (((bool)item3.Rec) ? null : "выключить"));
					list.Add(new CheckDialog.Row
					{
						Text = item3.Title,
						Hint = "\n" + (string.IsNullOrEmpty(item3.Desc) ? item3.What : item3.Desc),
						Tag = tag,
						Checked = true
					});
					list2.Add(item3);
				}
			}
		}
		bool[] array = CheckDialog.Ask(this, "Применить рекомендации", "Можно улучшить " + bad.Count + " " + Plural(bad.Count, "настройку", "настройки", "настроек") + ". Сними галочку с того, что хочешь оставить как есть. Всё попадёт в «Историю» одной группой — откатить можно целиком или по пункту.", list.ToArray(), "Применить", "Отмена", DlgIcon.Info);
		if (array == null)
		{
			return;
		}
		bad = new List<SettingCard>();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] && list2[i] != null)
			{
				bad.Add(list2[i]);
			}
		}
		if (bad.Count == 0 || !_host.TryBegin(quick: false, "Исправляю проблемы (" + bad.Count + ")"))
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
				foreach (SettingCard item4 in bad)
				{
					e.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
					{
						{
							"fa1",
							item4.HistTitle(item4.Rec)
						},
						{ "fa2", item4.GetScript },
						{ "fa3", item4.SetScript },
						{ "fa4", item4.Rec },
						{ "fa5", item4.Cat }
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
					{
						"fa3",
						undo ?? ""
					},
					{ "fa4", cat },
					{ "fa5", noUndo },
					{ "fa6", keepReg },
					{
						"fa7",
						note ?? ""
					}
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

	internal void OpenPalette(string q)
	{
		_palette.Open();
		if (!string.IsNullOrEmpty(q))
		{
			_palette.Query = q;
		}
	}

	private List<CommandPalette.Item> PaletteItems()
	{
		List<CommandPalette.Item> list = new List<CommandPalette.Item>();
		foreach (Page p in _pages.Values)
		{
			string k = p.Key;
			list.Add(new CommandPalette.Item
			{
				Key = "→",
				Text = p.Title,
				Sub = "Раздел",
				Run = delegate
				{
					Show(k);
				}
			});
		}
		foreach (Page p in _pages.Values)
		{
			foreach (IBlock b in p.Blocks)
			{
				if (b is SettingCard c && !string.IsNullOrEmpty(c.Title) && (!c.IsExpert || Prefs.Expert))
				{
					Page pg = p;
					int sec = p.SectionOf(b);
					list.Add(new CommandPalette.Item
					{
						Key = "",
						Text = c.Title,
						Sub = pg.Title,
						Run = delegate
						{
							if (sec >= 0)
							{
								ShowTab(pg.Key, sec);
							}
							else
							{
								Show(pg.Key);
							}
							pg.SelectCard(c);
						}
					});
				}
			}
		}
		return list;
	}

	public void ShowTab(string key, int tab)
	{
		Show(key);
		if (_pages.TryGetValue(key, out var value))
		{
			value.SelectTab(tab, animate: false);
		}
	}

	private void BuildPages()
	{
		BuildHome();
		Page page = NewPage("privacy", "\ue72e", "Приватность", "Приватность и защита", "Слежка, реклама, Defender, BitLocker и обновления Windows");
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
		Tw(page, "Отключить облачный буфер обмена", "Скопированное не синхронизируется с другими устройствами через Microsoft", "cloudclip", "privacy").Info("Запрещает отправлять историю буфера обмена в облако Microsoft.", "Пароли и личные данные, которые ты копируешь, не уходят на серверы.", "Нельзя вставить на телефоне или другом ПК то, что скопировал здесь.", true, "Выключить, если не пользуешься синхронизацией буфера.");
		Tw(page, "Скрыть список языков от сайтов", "Сайты не получают список установленных языков Windows", "langlist", "privacy").Info("Запрещает Windows сообщать сайтам, какие языки установлены.", "Тебя сложнее узнать среди других посетителей сайта.", "Сайты могут реже сами выбирать нужный язык.", true, "Скрыть.");
		Tw(page, "Не запоминать запуск программ", "Пуск и поиск не ведут историю того, что ты открываешь", "trackprogs", "privacy").Info("Выключает учёт часто запускаемых программ.", "Меньше следов того, чем ты пользуешься.", "Пуск перестанет показывать «Часто используемые».", null, "По желанию.");
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
		page = NewPage("ui", "\ue771", "Оформление", "Оформление", "Внешний вид Windows, панель задач, Пуск и Проводник");
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
		Tw(page, "Обои без сжатия", "Windows не пережимает картинку рабочего стола в JPEG", "jpegq", "ui").Info("Ставит качество импорта обоев 100% вместо 85%.", "Обои выглядят чётче, без артефактов сжатия.", "Минусов почти нет (файл обоев занимает чуть больше места).", true, "Включить. Применится, когда снова выберешь обои.");
		page = NewPage("games", "\ue945", "Производительность", "Производительность", "Скорость, отклик системы и игровые настройки");
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
		page = NewPage("apps", "\ue71d", "Программы", "Программы", "Установленные программы, встроенные приложения Windows и автозагрузка");
		page.AddTabs("Установленные", "Встроенные", "Автозагрузка");
		BuildInstalled(page);
		page.Section(1);
		BuildApps(page);
		page.Section(2);
		BuildStartup(page);
		page = NewPage("net", "\ue774", "Сеть", "Сеть", "Подключение, пинг, DNS и сетевые настройки");
		page.AddTabs("Подключение", "Настройки", "Инструменты");
		BuildNet(page);
		BuildFixes();
		page = NewPage("system", "\ueda2", "Обслуживание", "Обслуживание", "Очистка диска, свободное место и проверка системы");
		BuildMaintenance(page);
		page = NewPage("restore", "\ue777", "Восстановление", "Восстановление", "Точки восстановления и защита системы");
		BuildRestore(page);
		BuildReview();
		page = NewPage("history", "\ue823", "История", "История изменений", "Записанные изменения и доступный откат. Не все операции обратимы", bottom: true);
		_hist = page.Add(new HistoryView(compact: false));
		WireHistory(_hist);
		page = NewPage("settings", "\ue713", "Настройки", "Настройки программы", "О программе, запуск и интерфейс", bottom: true);
		BuildSettings(page);
	}

	private static readonly string[] TabOrder = new string[10] { "games", "privacy", "ui", "net", "apps", "system", "fixes", "restore", "history", "settings" };

	private void BuildReview()
	{
		Page page = NewPage("review", "\ue734", null, "Рекомендованные", "Всё, что WinWeaver советует для этого ПК — из всех разделов");
		_side.Add("review", "Рекомендованные", "\ue734");
		List<SettingCard> clones = new List<SettingCard>();
		Dictionary<SettingCard, string> origin = new Dictionary<SettingCard, string>();
		foreach (SettingCard item in RecCards())
		{
			SettingCard c = item.CloneFor(_host);
			string k = PageOf(item) ?? "";
			origin[c] = k;
			if (_pages.TryGetValue(k, out var src))
			{
				c.Origin = src.Title == "Приватность и защита" ? "Приватность" : src.Title;
				c.OriginColor = TopNav.Tone(k);
			}
			clones.Add(page.Add(c));
		}
		List<string> tabs = new List<string>();
		foreach (string k in TabOrder)
		{
			if (origin.ContainsValue(k))
			{
				tabs.Add(k);
			}
		}
		clones.Sort((SettingCard x, SettingCard y) => Array.IndexOf(TabOrder, origin[x]).CompareTo(Array.IndexOf(TabOrder, origin[y])));
		string[] labels = new string[tabs.Count];
		for (int i = 0; i < tabs.Count; i++)
		{
			string t = _pages[tabs[i]].Title;
			labels[i] = ((t == "Приватность и защита") ? "Приватность" : t).ToUpperInvariant();
		}
		StatusHero head = page.Add(new StatusHero(_host, "\ue734", null, "Проверяю систему…", labels, true, U("На главную", delegate
		{
			Show("home");
		})));
		SectionLabel badL = page.Add(new SectionLabel("Можно улучшить"));
		SectionLabel goodL = page.Add(new SectionLabel("Уже как рекомендуем"));
		// До проверки системы карточки не показываем: они обновляются «в резерве».
		page.Blocks.Clear();
		page.Blocks.Add(head);
		foreach (SettingCard c2 in clones)
		{
			page.Reserve.Add(c2);
			c2.Visible = false;
		}
		badL.Visible = false;
		goodL.Visible = false;
		page.AfterRefresh = delegate
		{
			List<IBlock> list = new List<IBlock>();
			List<IBlock> list2 = new List<IBlock>();
			foreach (SettingCard item3 in clones)
			{
				if (item3.Matches == false)
				{
					list.Add(item3);
				}
				else if (item3.Matches == true)
				{
					list2.Add(item3);
				}
				else
				{
					item3.Visible = false;
				}
			}
			UpdateRecBadges();
			int num3 = list2.Count + list.Count;
			page.Blocks.Clear();
			page.Blocks.Add(head);
			if (num3 == 0)
			{
				badL.Visible = false;
				goodL.Visible = false;
				head.Apply(new Hashtable
				{
					{ "Title", "Проверяю систему…" },
					{ "Sub", "Рекомендации появятся, как только WinWeaver прочитает текущие настройки" },
					{ "Tone", "warn" }
				});
				page.RelayoutSoon();
				return;
			}
			badL.Text = ((list.Count > 0) ? ("Можно улучшить — " + list.Count) : "Улучшать нечего — всё настроено");
			goodL.Text = "Уже как рекомендуем — " + list2.Count;
			object[] array = new object[tabs.Count];
			for (int j = 0; j < tabs.Count; j++)
			{
				int num = 0;
				int num2 = 0;
				foreach (SettingCard item4 in clones)
				{
					if (origin[item4] == tabs[j] && item4.Matches.HasValue)
					{
						num++;
						if (item4.Matches.GetValueOrDefault())
						{
							num2++;
						}
					}
				}
				array[j] = new Hashtable
				{
					{ "L", labels[j] },
					{ "V", num2 + " из " + num },
					{ "H", (num2 == num) ? "всё настроено" : ("можно улучшить: " + (num - num2)) },
					{ "T", (num2 == num) ? "ok" : "warn" },
					{ "P", (num > 0) ? ((float)num2 / (float)num) : 1f }
				};
			}
			head.Apply(new Hashtable
			{
				{ "Title", (list.Count == 0) ? "Всё настроено" : ("Можно улучшить " + list.Count + " " + Plural(list.Count, "настройку", "настройки", "настроек")) },
				{ "Sub", "Настроено " + list2.Count + " из " + num3 + ". На каждой карточке видно, из какого она раздела" },
				{ "Tone", (list.Count == 0) ? "ok" : "warn" },
				{ "Score", (double)list2.Count / (double)num3 },
				{ "Tiles", array }
			});
			page.Blocks.Add(badL);
			page.Blocks.AddRange(list);
			page.Blocks.Add(goodL);
			page.Blocks.AddRange(list2);
			badL.Visible = true;
			goodL.Visible = true;
			badL.Invalidate();
			goodL.Invalidate();
		};
	}

	private void BuildHome()
	{
		Page page = NewPage("home", "\ue80f", "Главная", "Главная", "Состояние системы и автонастройка в один клик");
		page.NoHeader = true;
		_hero = page.Add(new Hero());
		WireReboot();
		_hero.ChipClicked += delegate(string k)
		{
			switch (k)
			{
			case "privacy":
				Show("privacy");
				break;
			case "history":
				Show("history");
				break;
			case "fix":
				FixFps();
				break;
			case "reboot":
				RebootMenu(Cursor.Position);
				break;
			case "view":
				Show("games");
				break;
			case "rec":
				Show("review");
				break;
			case "ui":
				Show("ui");
				break;
			default:
				_hero.PlayShine();
				break;
			}
		};
		LevelCard levelCard = new LevelCard("light", "\ue72e", "Базовая", "Только приватность, программы не трогает", new string[3] { "Выключает телеметрию, Copilot, рекламу и Bing в поиске", "Запрещает Windows самой шифровать диск (BitLocker)", "Ставит фирменные обои и имя системы WeavOS" }, recommended: false);
		LevelCard levelCard2 = new LevelCard("optimal", "\ue735", "Оптимальная", "Чистая и быстрая Windows на каждый день", new string[3] { "Всё, что делает «Базовая»", "Удаляет Edge, OneDrive и предустановленный мусор — браузер выберешь сам", "Ускоряет отклик и поднимает FPS без риска для работы" }, recommended: true);
		LevelCard levelCard3 = new LevelCard("gamer", "\ue7fc", "Максимальная", "Максимум FPS, античиты продолжают работать", new string[3] { "Всё, что делает «Оптимальная»", "Максимальная схема питания, HAGS, приоритет игр и точный таймер", "Цена: не будет индексации поиска, анимаций и гибернации" }, recommended: false)
		{
			Caution = "Не рекомендуется"
		};
		LevelCard[] array = new LevelCard[3] { levelCard, levelCard2, levelCard3 };
		foreach (LevelCard levelCard4 in array)
		{
			_levels[levelCard4.Key] = levelCard4;
			levelCard4.Activated += RunLevel;
		}
		page.Add(new AutoTuneCard(array));
		_homeHist = page.Add(new HistoryView(compact: true));
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
		if (_host.TryBegin(quick: false, busyKey.StartsWith("g:") ? "Откат группы изменений" : "Откат изменения"))
		{
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
	}

	private void RunLevel(LevelCard card)
	{
		if (_running != null || _host.IsBusy)
		{
			_status.Set("warn", "Подожди — ещё выполняется предыдущее действие");
			return;
		}
		string br = TuneDialog.Ask(this, card.Key, card.Title, card.Tagline, card.GlyphText);
		if (br != null)
		{
			string opts = TuneDialog.LastOptions;
			string level = card.Key;
			if (PersonalizeDialog.Ask(this, card.Title, ref opts, out var dev, out var pw, delegate(string o, Action<string[]> done)
			{
				_host.Worker.Run((IEngine e) => e.Eval("Get-FPlanNames $lvl $br $opts", new Hashtable
				{
					{ "lvl", level },
					{ "br", br },
					{ "opts", o }
				}), delegate(object r)
				{
					done(ToStrings(r));
				});
			}))
			{
				StartLevel(card, br, opts, dev, pw);
			}
		}
	}

	public void OpenTune(string level)
	{
		if (_levels.TryGetValue(level, out var value))
		{
			RunLevel(value);
		}
	}

	public void StartLevel(string level, string browser)
	{
		if (_levels.TryGetValue(level, out var value))
		{
			StartLevel(value, browser);
		}
	}

	private static string[] ToStrings(object r)
	{
		if (r is string text)
		{
			return new string[1] { text };
		}
		List<string> list = new List<string>();
		if (r is IEnumerable enumerable)
		{
			foreach (object item in enumerable)
			{
				if (item != null)
				{
					list.Add(item.ToString());
				}
			}
		}
		return list.ToArray();
	}

	private void StartLevel(LevelCard card, string browser, string opts = "", string dev = "", string pw = "")
	{
		if (!_host.TryBegin(quick: false, "Автонастройка — уровень «" + card.Title + "»"))
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
				{
					"opts",
					opts ?? ""
				},
				{
					"dev",
					dev ?? ""
				},
				{
					"pw",
					pw ?? ""
				}
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

	private void OnEngineMsg(string kind, string text)
	{
		_status.Set(kind, text);
		if (_running != null && text != null)
		{
			Match match = Regex.Match(text, "\\[(\\d+)/(\\d+)\\]:?\\s*(.*)$");
			if (match.Success)
			{
				_running.SetProgress(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), match.Groups[3].Value.TrimEnd('…', '.', ' '));
			}
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
			string title;
			string body;
			string hist;
			string ps;
			string note;
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
			if (WDialog.Ask(this, title, body, "Удалить", "Отмена", DlgIcon.Warn) && _host.TryBegin(quick: false, "Удаление: " + it.Name))
			{
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
			}
		};
		av.AddButton("Удалить рекомендованные", BtnKind.Silver, delegate
		{
			List<AppsView.Item> list = new List<AppsView.Item>();
			foreach (AppsView.Item item in av.Items)
			{
				if (item.Installed && item.Rec == 1 && !item.System)
				{
					list.Add(item);
				}
			}
			if (list.Count == 0)
			{
				_status.Set("ok", "Рекомендованные приложения уже удалены");
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder();
				foreach (AppsView.Item item2 in list)
				{
					stringBuilder.Append("•  ").Append(item2.Name).Append('\n');
				}
				if (WDialog.Ask(this, "Удалить приложения (" + list.Count + ")?", stringBuilder.ToString() + "\nПогода, Outlook, To Do и «Связь с телефоном» не трогаются — их можно удалить отдельно. Вернуть любое приложение можно из Microsoft Store.", "Удалить", "Отмена", DlgIcon.Warn) && _host.TryBegin(quick: false, "Удаление приложений (" + list.Count + ")"))
				{
					_host.Worker.Run(delegate(IEngine e)
					{
						e.Eval("Start-FGroup 'Удаление встроенных приложений'");
						try
						{
							foreach (AppsView.Item item3 in list)
							{
								e.Eval("Invoke-FTracked $fa1 $fa2 -Cat 'apps' -NoUndo -Note 'Вернуть можно из Microsoft Store'", new Hashtable
								{
									{
										"fa1",
										"Удалено приложение: " + item3.Name
									},
									{
										"fa2",
										"Remove-FApp '" + item3.Id.Replace("'", "''") + "'"
									}
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
				}
			}
		});
		av.AddButton("Вернуть настройки после обновления", BtnKind.Ghost, delegate
		{
			if (WDialog.Ask(this, "Вернуть настройки после обновления?", "Крупные обновления Windows возвращают встроенные приложения, Edge, OneDrive, телеметрию и рекламу. Они снова будут удалены и выключены. Проводник перезапустится.", "Вернуть", "Отмена") && _host.TryBegin(quick: false, "Возврат настроек после обновления"))
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
			if (!_host.TryBegin(quick: true, it.Name + (on ? ": включить в автозагрузке" : ": выключить из автозагрузки")))
			{
				sv.Revert(it);
			}
			else
			{
				string id = it.Id;
				string name = it.Name;
				it.Toggle.Busy = true;
				_host.Worker.Run(delegate(IEngine e)
				{
					string value = "Set-FStartup '" + id.Replace("'", "''") + "' $" + (on ? "false" : "true");
					e.Eval("Invoke-FTracked $fa1 'Set-FStartup $id $on' -Undo $fa2 -Cat 'apps'", new Hashtable
					{
						{
							"fa1",
							"Автозагрузка: " + name + (on ? " — включена" : " — выключена")
						},
						{ "fa2", value },
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
			}
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
		RestoreView rv = new RestoreView
		{
			RefreshWorker = _host.Worker
		};
		rv.CreateClicked += delegate
		{
			if (_host.TryBegin(quick: false, "Создание точки восстановления"))
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
			if (_host.TryBegin(quick: false, "Включение защиты системы"))
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
			if (WDialog.Ask(this, "Откатить систему?", "Точка №" + it.Seq + ": " + it.Date + " — " + it.Desc + "\n\nКомпьютер сразу перезагрузится. Личные файлы не удаляются, но программы и драйверы, установленные после этой даты, пропадут.", "Откатить и перезагрузить", "Отмена", DlgIcon.Warn) && _host.TryBegin(quick: false, "Откат системы к точке №" + it.Seq))
			{
				int seq2 = it.Seq;
				_host.Worker.Run((IEngine en) => en.As(en.Eval("Write-FLog \"RESTORE to $seq\"; Restore-FFromPoint $seq", new Hashtable { { "seq", seq2 } }), fallback: false), delegate(object r)
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
			if (WDialog.Ask(this, "Удалить точку?", "Точка №" + it.Seq + ": " + it.Date + " — " + it.Desc + "\n\nК ней больше нельзя будет откатиться.", "Удалить", "Отмена", DlgIcon.Warn) && _host.TryBegin(quick: false, "Удаление точки №" + it.Seq))
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
			if (WDialog.Ask(this, "Удалить старые точки?", "Останется только последняя точка (" + (rv.Count - 1) + " старых будет удалено). Это освободит место на диске.", "Удалить старые", "Отмена", DlgIcon.Warn) && _host.TryBegin(quick: false, "Удаление старых точек"))
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
		ChT(p, "Место под точки", "Сколько диска Windows может занять точками восстановления", new string[4] { "3%", "5%", "10%", "15%" }, "Get-FTweakIdx 'rpspace'", "Set-FTweakIdx 'rpspace' $v", "restore").Info("Лимит теневого хранилища (vssadmin) на системном диске.", "Больше места — больше точек хранится одновременно.", "Это место недоступно для файлов.", null, "Для SSD на 256–512 ГБ хватит 5%, на больших дисках — 3%.").Opts("Минимум места: хватит на несколько последних точек. Подходит для небольших SSD.", "Обычный запас — 5–10 точек на большинстве дисков.", "Хранится заметно больше точек, можно откатиться дальше в прошлое.", "Максимальная история точек, но это место недоступно для файлов.");
	}

	private void BuildNet(Page page)
	{
		page.Add(new NetView(_host)).DnsPicked += delegate(int idx)
		{
			string[] names = new string[5] { "Авто (от провайдера)", "Cloudflare", "Google", "Quad9", "AdGuard" };
			if (_host.TryBegin(quick: false, "DNS: " + names[idx]))
			{
				_status.Set("info", "Переключаю DNS…");
				_host.Worker.Run((IEngine en) => en.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
				{
					{
						"fa1",
						"DNS-сервер: " + names[idx]
					},
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
		page.Add(new Grid(380, ActT("\ue774", "Очистить кэш DNS", "Если сайт переехал, а браузер открывает старый адрес", A("Очистить", "Clear-DnsClientCache; Show-Ok 'Кэш DNS очищен'", BtnKind.Silver)), ActT("\ue895", "Получить IP заново", "ipconfig /renew — связь пропадёт на пару секунд", C(A("Обновить IP", "[void](Invoke-FNative 'ipconfig.exe' '/release'); [void](Invoke-FNative 'ipconfig.exe' '/renew'); Show-Ok 'IP-адрес получен заново'", BtnKind.Silver), "Получить IP-адрес заново?", "Подключение пропадёт на несколько секунд, загрузки и звонки прервутся.")), ActT("\ue72c", "Сбросить кэш ARP", "Помогает после смены роутера или IP-адресов", A("Сбросить", "[void](Invoke-FNative 'netsh.exe' 'interface ip delete arpcache'); Show-Ok 'Кэш ARP очищен'", BtnKind.Silver)), ActT("\ue90f", "Сбросить сеть", "Winsock и TCP/IP по умолчанию. Нужна перезагрузка", C(T("Сбросить", "Сброшены Winsock и TCP/IP", "& netsh.exe winsock reset 2>&1 | Out-Null; & netsh.exe int ip reset 2>&1 | Out-Null; $global:NeedReboot = $true; Show-Ok 'Сетевые настройки сброшены. Перезагрузи компьютер'", "net", null, BtnKind.Silver, noUndo: true), "Сбросить сетевые настройки?", "Будут сброшены Winsock и TCP/IP. Это помогает, если интернет пропал или работает странно. Потребуется перезагрузка.")), ActT("\ue946", "Файл hosts", "Ручная блокировка или перенаправление сайтов", U("Открыть", delegate
		{
			Shell.Open("notepad.exe", Path.Combine(Shell.WinDir, "System32\\drivers\\etc\\hosts"));
		}, BtnKind.Silver)), ActT("\ue7f4", "Сетевые адаптеры", "Классическое окно сетевых подключений Windows", U("Открыть", delegate
		{
			Shell.Open("ncpa.cpl");
		}, BtnKind.Silver), U("Параметры сети", delegate
		{
			Shell.Open("ms-settings:network");
		}))));
	}

	private static string Q(string s)
	{
		return "'" + (s ?? "").Replace("'", "''") + "'";
	}

	private void BuildPrivacyHero(Page page)
	{
		page.Add(new StatusHero(_host, "\ue72e", "Get-FPrivInfo", "Проверяю приватность…", new string[4] { "СЛЕЖКА", "РЕКЛАМА", "ЗАЩИТА", "ОБНОВЛЕНИЯ" }, true)).TileClicked += delegate(int i)
		{
			page.SelectTab(i);
		};
	}

	private void BuildProtect(Page page)
	{
		page.Add(new StatusHero(_host, "\uea18", "Get-FSecInfo", "Проверяю защиту…", new string[6] { "АНТИВИРУС", "БРАНДМАУЭР", "SMARTSCREEN", "UAC", "ЗАГРУЗКА", "BITLOCKER" }, false, U("Безопасность Windows", delegate
		{
			Shell.Open("windowsdefender:");
		}, BtnKind.Silver, "\uea18"), U("Брандмауэр", delegate
		{
			Shell.Open("firewall.cpl");
		})));
	}

	private void BuildDefender(Page page)
	{
		DefenderView defenderView = page.Add(new DefenderView());
		Action<string, string, string, string> runT = null;
		Action<string, string, string> run = delegate(string title, string ps, string undo)
		{
			runT(title, ps, undo, title);
		};
		runT = delegate(string title, string ps, string undo, string job)
		{
			if (_host.TryBegin(quick: false, job))
			{
				_host.Worker.Run(delegate(IEngine e)
				{
					if (title == null)
					{
						e.Eval(ps);
					}
					else
					{
						e.Eval("Invoke-FTracked $fa1 $fa2 -Undo $fa3 -Cat 'protect'", new Hashtable
						{
							{ "fa1", title },
							{ "fa2", ps },
							{
								"fa3",
								undo ?? ""
							}
						});
					}
					return (object)null;
				}, delegate
				{
					_host.End();
					page.Refresh(_host.Worker);
					UpdateBadge();
				});
			}
		};
		defenderView.ModeClicked += delegate(int m)
		{
			switch (m)
			{
			case 0:
				run("Microsoft Defender включён", "Set-DefenderOn", "Set-DefenderOff $false");
				break;
			case 1:
				if (WDialog.Ask(this, "Выключить защиту в реальном времени?", "Defender перестанет проверять файлы при открытии и запуске — меньше нагрузки в играх. Проверять вручную можно по-прежнему.\n\nЕсли включена «Защита от подделки», сначала откроются настройки, чтобы её выключить.", "Выключить", "Отмена", DlgIcon.Warn))
				{
					run("Защита в реальном времени Defender выключена", "Set-DefenderOff $false", "Set-DefenderOn");
				}
				break;
			default:
				if (WDialog.Ask(this, "Полностью выключить Defender?", "Защита в реальном времени выключится сразу, а службы Defender — через перезагрузку в безопасный режим (WinWeaver сделает всё сам и вернёт обычный режим).\n\nПеред этим выключи «Защиту от подделки» в настройках Defender.", "Выключить", "Отмена", DlgIcon.Warn))
				{
					run("Microsoft Defender выключен полностью", "Set-DefenderOff $true", "Set-DefenderOn");
				}
				break;
			}
		};
		defenderView.ToolClicked += delegate(string k)
		{
			switch (k)
			{
			case "settings":
				Shell.Open("windowsdefender://threatsettings");
				break;
			case "sig":
				runT(null, "Update-FDefSig", null, "Defender — обновление баз");
				break;
			case "scan":
				runT(null, "Start-FDefScan", null, "Defender — быстрая проверка");
				break;
			}
		};
	}

	private void BuildBitLocker(Page page)
	{
		BitLockerView bl = page.Add(new BitLockerView(_host));
		bl.Progress += delegate(string title, string sub, float p, bool active)
		{
			if (active)
			{
				_status.SetBackground(title, sub, p);
			}
			else
			{
				_status.ClearBackground("ok", "BitLocker: расшифровка завершена");
			}
		};
		_status.BgClicked += delegate
		{
			if (!(_status.BgTitle == "Читаю разделы"))
			{
				Show("privacy");
				page.SelectTab(2, animate: false);
			}
		};
		_blKick = delegate
		{
			_host.Worker.Run((IEngine e) => bl.Fetch(e), bl.Apply);
		};
		bl.DecryptClicked += delegate
		{
			if (!Prefs.Expert)
			{
				WDialog.Info(this, "Нужен экспертный режим", "Расшифровка снимает защиту данных: если ноутбук украдут, файлы можно будет прочитать.\n\nЕсли ты понимаешь, что делаешь, включи «Экспертный режим» в «Настройках».", DlgIcon.Warn);
			}
			else if (WDialog.Ask(this, "Расшифровать диски?", "Расшифровка идёт в фоне и может занять от нескольких минут до часа. Компьютером можно пользоваться — прогресс будет виден здесь же.", "Расшифровать", "Отмена", DlgIcon.Warn) && _host.TryBegin(quick: false, "Запуск расшифровки BitLocker"))
			{
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked 'Запущена расшифровка дисков BitLocker' 'Set-BLDecrypt' -Cat 'protect' -NoUndo -Note 'Зашифровать снова можно в «Параметры → Конфиденциальность → Шифрование»'");
					return (object)null;
				}, delegate
				{
					_host.End();
					page.Refresh(_host.Worker);
					_blKick?.Invoke();
				});
			}
		};
		bl.BlockClicked += delegate
		{
			if (_host.TryBegin(quick: false, "Запрет автошифрования BitLocker"))
			{
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked 'Запрещено автошифрование BitLocker' \"Set-FReg 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\BitLocker' 'PreventDeviceEncryption' 1; Show-Ok 'Автошифрование запрещено'\" -Cat 'protect'");
					return (object)null;
				}, delegate
				{
					_host.End();
					page.Refresh(_host.Worker);
				});
			}
		};
	}

	private void BuildProtectHardening(Page page)
	{
		page.Add(new SectionLabel("Усиление защиты"));
		Tw(page, "SmartScreen для приложений и файлов", "Предупреждение перед запуском незнакомых скачанных программ", "smartscreen", "protect").Info("Включает проверку SmartScreen: перед запуском редкой или неподписанной программы из интернета Windows покажет предупреждение.", "Защищает от поддельных установщиков и вирусов, которые маскируются под нужные программы.", "Иногда предупреждает о безобидных малоизвестных утилитах — их можно запустить через «Подробнее → Выполнить в любом случае».", true, "Оставь включённым — это один из самых полезных барьеров.");
		Tw(page, "Блокировать нежелательные программы (PUA)", "Defender не даст поставить тулбары, майнеры и «оптимизаторы»", "pua", "protect").Info("Включает в Microsoft Defender блокировку потенциально нежелательных приложений: рекламных установщиков, майнеров, «ускорителей ПК» с подвохом.", "Меньше мусора, который тихо ставится вместе с бесплатными программами.", "Изредка блокирует кряки и сомнительные утилиты, которые ты ставишь осознанно.", true, "Включить. Работает, только если Defender — основной антивирус.");
		Tw(page, "Отключить автозапуск со съёмных дисков", "Флешки и диски не запускают программы сами", "autorun", "protect").Info("Запрещает автозапуск и автовоспроизведение для всех типов дисков.", "Защищает от вирусов, которые распространяются через флешки.", "При подключении флешки не будет всплывать окно выбора действия — открой её в Проводнике.", true, "Включить.");
		Tw(page, "Отключить удалённого помощника", "Никто не сможет подключиться к ПК через «Удалённый помощник»", "remoteassist", "protect").Info("Выключает приглашения «Удалённого помощника» Windows.", "Закрывает способ, которым пользуются телефонные мошенники «из службы поддержки».", "Если тебе помогает близкий через «Удалённого помощника» — включи обратно. AnyDesk и TeamViewer работают независимо.", true, "Выключить.");
		Tw(page, "Отключить устаревший протокол SMBv1", "Старый протокол общих папок, через который распространялся WannaCry", "smb1", "protect").Info("Выключает сервер SMB версии 1 — протокол общих папок из 1990-х.", "Закрывает уязвимости, через которые распространялись WannaCry и NotPetya.", "Не откроются общие папки на очень старых NAS и роутерах (до ~2012 года).", true, "Выключить.");
		Tw(page, "Не отправлять образцы файлов в Microsoft", "Defender спросит, прежде чем отправить подозрительный файл", "samples", "protect").Info("Defender перестаёт автоматически отправлять подозрительные файлы в облако Microsoft для анализа.", "Личные документы не уйдут в облако, даже если покажутся Defender подозрительными.", "Новые угрозы распознаются чуть медленнее.", null, "По желанию: для приватности — включить, для максимальной защиты — оставить как есть.");
		Ch(page, "Контроль учётных записей (UAC)", "Как спрашивать разрешение на изменения в системе", new string[4] { "Всегда", "Как обычно", "Без затемнения", "Не спрашивать" }, "Get-FTweakIdx 'uac'", "Set-FTweakIdx 'uac' $v", "protect").Expert().Info("Настраивает окно «Разрешить этому приложению вносить изменения?». «Не спрашивать» — программы получают права администратора молча.", "Затемнение экрана не даёт вирусу нажать «Да» за тебя.", "«Не спрашивать» и «Без затемнения» заметно снижают защиту.", 1, "«Как обычно» — стандарт Windows и лучший баланс.").Opts("Спрашивает даже при изменении настроек Windows самим пользователем. Максимальная защита, но окно появляется часто.", "Стандарт Windows: спрашивает, когда программа хочет права администратора, экран затемняется.", "Спрашивает так же, но без затемнения — вредоносная программа теоретически может нажать «Да» за тебя.", "Программы получают права администратора молча. Защита от тихих изменений системы выключена.");
	}

	private void BuildUpdates(Page page)
	{
		page.Add(new StatusHero(_host, "\ue895", "Get-FUpdInfo", "Проверяю Центр обновления…", new string[4] { "ВЕРСИЯ", "УСТАНОВКА", "ПАУЗА", "ПОСЛЕДНЕЕ" }, false, U("Проверить обновления", delegate
		{
			Shell.Open("ms-settings:windowsupdate-action");
		}, BtnKind.Silver, "\ue895"), U("Журнал обновлений", delegate
		{
			Shell.Open("ms-settings:windowsupdate-history");
		})));
		UpdPauseView pv = page.Add(new UpdPauseView());
		pv.CustomClicked += delegate
		{
			int num = PauseDialog.Ask(this);
			if (num > 0)
			{
				pv.Choose(num);
			}
		};
		pv.PauseChosen += delegate(int days)
		{
			if (_host.TryBegin(quick: false, "Пауза обновлений"))
			{
				pv.Busy = true;
				pv.SetPending(days);
				DateTime dateTime = DateTime.Now.AddDays(days);
				string until = ((dateTime.Year >= 2077) ? "2077 года («навсегда»)" : dateTime.ToString("dd.MM.yyyy"));
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked $fa1 $fa2 -Undo 'Set-FUpdPause 0' -Cat 'update' -KeepReg", new Hashtable
					{
						{
							"fa1",
							"Обновления приостановлены до " + until
						},
						{
							"fa2",
							"Set-FUpdPause " + days
						}
					});
					return (object)null;
				}, delegate
				{
					pv.Busy = false;
					_host.End();
					page.Refresh(_host.Worker);
				});
			}
		};
		pv.ResumeClicked += delegate
		{
			if (_host.TryBegin(quick: false, "Возобновление обновлений"))
			{
				pv.Busy = true;
				_host.Worker.Run(delegate(IEngine e)
				{
					e.Eval("Invoke-FTracked 'Обновления возобновлены' 'Set-FUpdPause 0' -Cat 'update' -NoUndo");
					return (object)null;
				}, delegate
				{
					pv.Busy = false;
					_host.End();
					page.Refresh(_host.Worker);
				});
			}
		};
		page.Add(new SectionLabel("Как приходят обновления"));
	}

	private void BuildUpdatesExtra(Page page)
	{
		Ch(page, "Как ставить обновления", "Кто решает, когда скачивать и устанавливать", new string[3] { "Автоматически", "Скачивать, ставить по кнопке", "Только уведомлять" }, "Get-FTweakIdx 'wumode'", "Set-FTweakIdx 'wumode' $v", "update").Info("Меняет политику автообновления. «Скачивать, ставить по кнопке» — Windows загрузит обновления в фоне, а установит, когда ты нажмёшь «Установить». «Только уведомлять» — даже скачивание по твоей команде.", "Обновление не начнёт устанавливаться перед важной игрой или созвоном.", "Легко забыть про обновления безопасности — заглядывай в Центр обновления раз в пару недель.", null, "«Автоматически» подходит большинству. «Скачивать, ставить по кнопке» — если не любишь сюрпризы.").Opts("Windows сама скачивает и ставит обновления, перезагрузку планирует вне активных часов.", "Обновления скачиваются в фоне, а устанавливаются, только когда ты нажмёшь «Установить».", "Windows только сообщает о новых обновлениях — и скачивание, и установка по твоей команде.");
		Ch(page, "Отложить ежемесячные обновления", "Обновления качества приходят позже, когда их уже проверили другие", Days, "Get-FTweakIdx 'wudefer'", "Set-FTweakIdx 'wudefer' $v", "update").Info("Откладывает ежемесячные накопительные обновления на выбранный срок.", "Если Microsoft выпустит кривое обновление, его успеют отозвать до тебя.", "Исправления безопасности тоже придут позже.", null, "7 дней — разумный компромисс.").Opts("Накопительные обновления приходят в день выхода — исправления безопасности сразу.", "Неделя задержки: если обновление окажется кривым, его успеют отозвать.", "Две недели — ещё надёжнее, но и исправления безопасности приходят позже.", "Максимальная задержка. Подходит рабочим машинам, где стабильность важнее новизны.");
		Tw(page, "Не ставить драйверы через Центр обновления", "Windows не будет заменять драйверы видеокарты и устройств", "wudrivers", "update").Info("Исключает драйверы из обновлений Windows.", "Свежий драйвер NVIDIA или AMD не заменится старой версией от Microsoft.", "Драйверы новых устройств придётся ставить самому с сайта производителя.", null, "Включай, если сам следишь за драйверами видеокарты.");
		Tw(page, "Не получать новые функции первым", "Выключает «Получать последние обновления, как только они будут доступны»", "wuearly", "update").Info("Выключает ранний доступ к новым функциям Windows, которые раскатываются постепенно.", "Меньше сырых нововведений и связанных с ними багов.", "Новые функции появятся на несколько недель позже.", null, "Выключить, если важна стабильность.").Builds(22621);
		Tw(page, "Предупреждать о перезагрузке", "Уведомление, когда для обновления нужно перезагрузить ПК", "wunotify", "update").Info("Показывает уведомление, когда Windows готова перезагрузиться для установки обновлений.", "Перезагрузка не станет неожиданностью.", "Минусов почти нет (лишнее уведомление может отвлечь в игре или на презентации).", null, "Включить.");
		Tw(page, "Обновлять другие продукты Microsoft", "Office, .NET и драйверы Microsoft через Центр обновления", "wumsupd", "update").Info("Разрешает Центру обновления ставить обновления Office, .NET, Visual C++ и других продуктов Microsoft.", "Всё обновляется в одном месте.", "Чуть больше загрузок.", null, "Включи, если у тебя установлен Microsoft Office.");
	}

	private void PerfResp(Page page)
	{
		page.Add(new SectionLabel("Тонкая настройка отклика"));
		Ch(page, "Распределение процессорного времени", "Сколько времени процессора получает активное окно", new string[3] { "Стандарт", "Активные окна", "Игры" }, "Get-FTweakIdx 'prisep'", "Set-FTweakIdx 'prisep' $v", "game").Info("Меняет Win32PrioritySeparation — как планировщик Windows делит процессор между активным окном и фоном. «Игры» — короткие кванты с максимальным приоритетом переднего плана.", "Активная программа или игра отзывчивее, меньше микрофризов от фоновых задач.", "Фоновые задачи (рендер, архивация) выполняются чуть медленнее.", null, "«Активные окна» — для повседневной работы, «Игры» — для игрового ПК.").Opts("Стандарт Windows: баланс между активным окном и фоновыми задачами.", "Активному окну — больше процессорного времени. Отзывчивее в повседневной работе.", "Короткие кванты и максимальный приоритет переднего плана — меньше микрофризов в играх, фон чуть медленнее.");
		Tw(page, "Приоритет активным программам (MMCSS)", "Фон получает меньше процессора, когда ты чем-то занят", "sysresp", "game").Info("Снижает долю процессора, которую служба мультимедиа резервирует под фоновые задачи (SystemResponsiveness 20 → 10%).", "Чуть плавнее игры и аудио.", "Минусов практически нет (в редких случаях фоновые задачи — загрузки, стриминг, запись — работают чуть медленнее, а на некоторых системах бывает треск звука).", null, "Включить на игровом ПК.");
		Tw(page, "Быстрое закрытие зависших программ", "Windows быстрее предлагает снять зависшее окно и не ждёт при выходе", "hungapp", "game").Info("Сокращает время, через которое окно считается зависшим, и ожидание закрытия программ при выходе.", "Выход из системы и выключение быстрее, зависшие окна не держат минутами.", "Программа с несохранёнными данными может закрыться при выключении быстрее, чем успеет спросить.", null, "Включай, если не забываешь сохранять работу.");
		Tw(page, "Быстрое выключение ПК", "Службам даётся 2 секунды на завершение вместо 5", "fastoff", "game").Info("Уменьшает время ожидания служб при выключении.", "Компьютер выключается быстрее.", "Редкие службы (СУБД, виртуальные машины) могут не успеть корректно завершиться.", null, "Обычному домашнему ПК — включить.");
		Tw(page, "Отключить анимацию сворачивания окон", "Окна сворачиваются и разворачиваются мгновенно", "winanim", "game").Info("Выключает анимацию сворачивания и разворачивания окон.", "Интерфейс ощущается быстрее, особенно на слабом ПК.", "Выглядит менее плавно.", null, "По вкусу.");
		Tw(page, "Быстрый повтор клавиш", "Минимальная задержка и максимальная скорость автоповтора", "kbdrepeat", "game").Info("Ставит минимальную задержку перед автоповтором и максимальную скорость повтора клавиш.", "Удобнее удалять текст и перемещаться стрелками.", "Поначалу клавиша может «проскакивать» лишний символ.", null, "По желанию.");
		Tw(page, "Быстрые подсказки при наведении", "Всплывающие подсказки появляются через 0,1 секунды", "hovertime", "game").Info("Уменьшает задержку перед появлением подсказок и превью при наведении курсора.", "Превью окон на панели задач появляются быстрее.", "Подсказки могут всплывать чаще.", null, "По желанию.");
		Tw(page, "Edge не висит в фоне", "Edge не запускается вместе с Windows и закрывается полностью", "edgebg", "game").Info("Выключает «ускорение запуска» и работу Microsoft Edge в фоне после закрытия окна.", "Меньше процессов и занятой памяти сразу после входа.", "Edge открывается на полсекунды дольше.", true, "Выключить, если Edge не основной браузер.");
		Tw(page, "Закрывать зависшие программы при выключении", "Выключение не останавливается на окне «Программа мешает завершению работы»", "autoend", "game").Info("Windows сама закрывает программы, которые не отвечают, когда ты выключаешь или перезагружаешь ПК.", "ПК выключается без лишних вопросов.", "Несохранённые данные в зависшей программе пропадут.", null, "По желанию: удобно, если часто выключаешь ПК и уходишь.");
	}

	private void PerfGames(Page page)
	{
		page.Add(new SectionLabel("Для игр — продвинутое"));
		Tw(page, "Высокий приоритет игр (MMCSS)", "Игры получают больше процессора и видеокарты в планировщике мультимедиа", "gameprio", "game").Info("Повышает приоритет задачи «Games» в службе мультимедиа: процессор, ввод-вывод и видеокарта.", "Стабильнее время кадра в играх, которые пользуются MMCSS.", "Эффект зависит от игры — где-то незаметен.", null, "Включить на игровом ПК.");
		Tw(page, "Точный системный таймер", "Игры получают таймер 0,5 мс независимо от других программ (Windows 11)", "timerres", "game").Info("Возвращает глобальное разрешение таймера: если игра просит точный таймер, он действует для всей системы, как в Windows 10.", "Меньше задержка ввода и ровнее кадры в старых играх.", "Чуть выше энергопотребление в простое.", null, "Включить на настольном ПК. Применяется после перезагрузки.").Builds(22000);
		Tw(page, "Отключить динамический тик", "Таймер ядра работает с постоянной частотой", "dyntick", "game").Info("Выключает динамический тик таймера ядра (bcdedit disabledynamictick).", "У некоторых систем уходят микрофризы и плавает время кадра.", "Ноутбук от батареи работает немного меньше.", null, "Пробуй, только если есть микрофризы. Применяется после перезагрузки.");
		Tw(page, "Отключить MPO", "Многоплоскостное наложение — частая причина мерцания и чёрных экранов", "mpo", "game").Info("Выключает Multi-Plane Overlay в DWM.", "Пропадают мерцание, полосы и чёрные экраны в браузере и играх на некоторых видеокартах NVIDIA/AMD.", "Чуть выше нагрузка на видеокарту при видео в окне.", null, "Включай, только если видишь мерцание или чёрные вспышки.");
		Tw(page, "Запретить запись игр политикой", "Game DVR выключен полностью — даже после обновлений", "gamedvrpol", "game").Info("Запрещает фоновую запись игр групповой политикой.", "Запись не включится обратно после обновления Windows.", "Не будет записи клипов через Xbox Game Bar.", null, "Включи, если пользуешься ShadowPlay, ReLive или OBS.");
		Tw(page, "Отключить службы Xbox", "Вход Xbox Live, сохранения в облаке и геймпады Xbox по беспроводу", "xboxsvc", "game").Info("Отключает службы Xbox: авторизацию, облачные сохранения, сеть Xbox Live и аксессуары.", "Меньше фоновых служб.", "Не будут работать игры из Xbox app/Game Pass, облачные сохранения и беспроводной адаптер Xbox.", null, "Только если не пользуешься Game Pass и Xbox-приложением.");
	}

	private void PerfSys(Page page)
	{
		page.Add(new SectionLabel("Память и диск"));
		Tw(page, "Отключить лишние службы", "Карты, демо-режим, факс, удалённый реестр, родительский контроль…", "junksvc", "game").Expert().Info("Отключает службы, которые почти никому не нужны: загрузку карт, демо-режим магазина, WAP Push, сетевой доступ к медиатеке, удалённый реестр, факс, родительский контроль и телефонную связь.", "Меньше фоновых процессов и открытых портов.", "Перестанут работать офлайн-карты Windows, факс и «Семейная безопасность».", null, "Включить на домашнем ПК.");
		Tw(page, "Не выгружать ядро из памяти", "Драйверы и ядро всегда остаются в ОЗУ", "pagingexec", "game").Info("Запрещает выгружать код ядра и драйверов в файл подкачки.", "Чуть быстрее отклик после простоя.", "Требует свободной памяти — на ПК с 8 ГБ ОЗУ и меньше не нужно.", null, "Включай при 16 ГБ памяти и больше. Применяется после перезагрузки.");
		Tw(page, "Отключить сжатие памяти", "Windows не сжимает данные в ОЗУ", "memcomp", "game").Expert().Info("Выключает сжатие страниц памяти.", "Меньше нагрузка на процессор при большом объёме ОЗУ.", "При нехватке памяти система начнёт активнее использовать файл подкачки.", null, "Только при 32 ГБ ОЗУ и больше.");
		Tw(page, "Не обновлять метку доступа к файлам", "NTFS не записывает время каждого открытия файла", "lastaccess", "game").Info("Отключает обновление «Даты доступа» при каждом чтении файла.", "Меньше мелких записей на диск.", "Некоторые программы резервного копирования используют эту дату.", null, "Включить.");
		Tw(page, "Отключить короткие имена 8.3", "NTFS не создаёт имена вида PROGRA~1 для новых файлов", "shortnames", "game").Info("Запрещает создавать короткие DOS-имена для новых файлов и папок.", "Чуть быстрее работа с папками, где тысячи файлов.", "Очень старые 16-битные программы могут не найти файлы.", null, "Включить.");
		Tw(page, "Отключить виртуализацию (VBS)", "Безопасность на основе виртуализации — минус 5–15% FPS", "vbs", "game").Expert().Info("Выключает Virtualization-Based Security — основу «Целостности памяти» и Credential Guard.", "Заметный прирост FPS и отклика, особенно на процессорах до 2020 года.", "Ниже защита от вредоносных драйверов. Античиты Vanguard и FACEIT могут потребовать включить обратно.", null, "Только для игрового ПК без античитов, требующих VBS. Применяется после перезагрузки.");
		Tw(page, "Отключить защиту от Spectre и Meltdown", "Патчи процессорных уязвимостей замедляют старые CPU", "spectre", "game").Expert().Info("Выключает программные заплатки от уязвимостей Spectre и Meltdown.", "До 10–30% прироста в задачах с диском и системными вызовами на процессорах Intel до 9-го поколения.", "Компьютер становится уязвим к атакам через вредоносный код в браузере и программах.", false, "Не рекомендуем. Только для изолированного игрового ПК со старым процессором.");
		page.Add(new SectionLabel("Фоновые задачи Windows"));
		Tw(page, "Отключить автообслуживание по расписанию", "Windows не запускает дефрагментацию и проверки сама в простое", "maint", "game").Expert().Info("Выключает «Автоматическое обслуживание» — ночные проверки, оптимизацию дисков и обновление индексов.", "Ничего не начинает грузить диск и процессор посреди игры или работы.", "Оптимизацию дисков и проверки придётся запускать вручную (раздел «Обслуживание»).", null, "Только если обслуживание мешает тебе в работе.");
		Tw(page, "Отключить помощника совместимости", "Windows не следит за старыми программами и не предлагает «исправить» их", "pca", "game").Info("Выключает службу помощника по совместимости программ (PCA).", "Меньше фоновых проверок при запуске программ и игр.", "Не будет подсказок, если старая программа запустилась с ошибкой.", null, "Можно выключить, если не пользуешься старыми программами.");
		Tw(page, "Отключить Fault Tolerant Heap", "Windows не «смягчает» ошибки программ ценой их скорости", "fth", "game").Expert().Info("Выключает механизм FTH: после нескольких сбоев программы Windows тихо замедляет её работу с памятью.", "Программа, которая однажды упала, не начинает работать медленнее навсегда.", "Нестабильные программы могут падать чаще.", null, "Для опытных: если игра стала тормозить после пары вылетов.");
		Tw(page, "Отключить проверку свободного места", "Нет всплывающих предупреждений «Мало места на диске»", "lowdisk", "game").Info("Выключает фоновую проверку свободного места и уведомления о нём.", "Нет навязчивых уведомлений, если диск занят намеренно.", "Можно не заметить, что место действительно кончается.", null, "По желанию.");
		Tw(page, "Отключить службу мониторинга сети (NDU)", "Служба подсчёта трафика по программам — на некоторых ПК «съедает» память", "ndu", "game").Expert().Info("Отключает службу Network Data Usage, которая считает трафик каждой программы.", "Убирает известную утечку памяти в невыгружаемом пуле на части систем.", "Не будет статистики «Использование данных» в Параметрах.", null, "Если память со временем «утекает» без видимой причины. Нужна перезагрузка.");
	}

	private void BuildInstalled(Page p)
	{
		InstalledView iv = p.Add(new InstalledView());
		iv.RefreshClicked += delegate
		{
			p.Refresh(_host.Worker);
		};
		iv.UninstallClicked += delegate(InstalledView.Item it)
		{
			string body = (it.Store ? "Приложение Microsoft Store будет удалено для текущего пользователя. Вернуть его можно из Store." : "Запустится деинсталлятор программы — если появится его окно, пройди шаги до конца.\n\nПосле этого WinWeaver поищет остатки и покажет их списком, прежде чем удалять.");
			bool[] array = CheckDialog.Ask(this, "Удалить «" + it.Name + "»?", body, new CheckDialog.Row[1]
			{
				new CheckDialog.Row
				{
					Text = "Найти и удалить остатки",
					Hint = "\nПапки в Program Files и AppData, ярлыки, ключи реестра",
					Checked = !it.Store
				}
			}, "Удалить", "Отмена", DlgIcon.Warn);
			if (array != null && _host.TryBegin(quick: false, "Удаление: " + it.Name))
			{
				bool left = array[0];
				iv.SetBusy(it, busy: true);
				_host.Worker.Run((IEngine e) => (!(e.Eval("$r = Uninstall-FProgram $fa1; if ($r.Ok) { Add-FHist $fa2 'apps' '' $null $fa3 $false }; $r", new Hashtable
				{
					{ "fa1", it.Id },
					{
						"fa2",
						"Удалена программа: " + it.Name
					},
					{ "fa3", "Вернуть можно, установив программу заново" }
				}) is Hashtable hashtable) || ((!(hashtable["Ok"] is bool) || !(bool)hashtable["Ok"]) && (!(hashtable["Missing"] is bool) || !(bool)hashtable["Missing"])) || !left) ? null : (e.Eval("Find-FLeftovers $fa1 $fa2 $fa3", new Hashtable
				{
					{ "fa1", it.Name },
					{ "fa2", it.Pub },
					{ "fa3", it.Loc }
				}) ?? new object[0]), delegate(object v)
				{
					iv.SetBusy(it, busy: false);
					_host.End();
					p.Refresh(_host.Worker);
					if (v != null)
					{
						AskLeftovers(it.Name, v, p);
					}
				});
			}
		};
	}

	private void AskLeftovers(string name, object v, Page p)
	{
		object[] array = v as object[];
		if (array == null && v is Hashtable)
		{
			array = new object[1] { v };
		}
		List<string> list2 = new List<string>();
		List<CheckDialog.Row> list3 = new List<CheckDialog.Row>();
		if (array != null)
		{
			object[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] is Hashtable hashtable && hashtable["Path"] != null)
				{
					string text = hashtable["Path"].ToString();
					string text2 = (hashtable["Kind"] ?? "").ToString();
					double num = 0.0;
					try
					{
						num = Convert.ToDouble(hashtable["Size"] ?? ((object)0), CultureInfo.InvariantCulture);
					}
					catch
					{
					}
					string text3 = ((text2 == "reg") ? "Ключ реестра" : ((text2 == "dir") ? "Папка" : "Файл"));
					if (text2 != "reg" && num > 0.0)
					{
						text3 = text3 + "  •  " + CleanView.Fmt(num);
					}
					list2.Add(text);
					list3.Add(new CheckDialog.Row
					{
						Text = text,
						Hint = "\n" + text3,
						Checked = true
					});
				}
			}
		}
		if (list3.Count == 0)
		{
			_status.Set("ok", "«" + name + "» удалена, остатков не найдено");
			return;
		}
		bool[] array3 = CheckDialog.Ask(this, "Остатки «" + name + "»", "Найдено " + list3.Count + " " + InstalledView.Plural(list3.Count, "объект", "объекта", "объектов") + ". Сними галочки с того, что нужно оставить. Удаление необратимо.", list3.ToArray(), "Удалить отмеченное", "Оставить всё", DlgIcon.Warn);
		if (array3 == null || !_host.TryBegin(quick: false, "Удаление остатков «" + name + "»"))
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int j = 0; j < array3.Length; j++)
		{
			if (array3[j])
			{
				stringBuilder.Append(list2[j]).Append('\n');
			}
		}
		string list = stringBuilder.ToString();
		_host.Worker.Run((IEngine e) => (list == "") ? null : e.Eval("$n = Remove-FLeftovers $fa1; Add-FHist $fa2 'apps' '' $null ('Удалено объектов: ' + $n) $false; $n", new Hashtable
		{
			{ "fa1", list },
			{
				"fa2",
				"Удалены остатки: " + name
			}
		}), delegate(object v2)
		{
			_host.End();
			if (v2 != null)
			{
				_status.Set("ok", "Остатки «" + name + "» удалены: " + v2);
			}
		});
	}

	private void WireStartup(StartupView sv, Page p)
	{
		sv.MenuClicked += delegate(StartupView.Item it, Point at)
		{
			DarkMenu darkMenu = new DarkMenu();
			darkMenu.Header(it.Name, it.On ? "Запускается вместе с Windows" : "Сейчас не запускается");
			darkMenu.Item(it.On ? "Выключить" : "Включить", "\ue7e8", it.On ? "Не запускать при входе в систему" : "Запускать при входе в систему", delegate
			{
				it.Toggle.SimClick();
			});
			ToolStripMenuItem toolStripMenuItem = darkMenu.Sub("Отложить запуск", "\ue823", "Запуск через N секунд после входа");
			int[] array2 = new int[5] { 0, 30, 60, 120, 300 };
			string[] array3 = new string[5] { "Без задержки", "На 30 секунд", "На 1 минуту", "На 2 минуты", "На 5 минут" };
			for (int l = 0; l < array2.Length; l++)
			{
				int s = array2[l];
				string nm = array3[l];
				DarkMenu.Item(toolStripMenuItem.DropDownItems, nm, delegate
				{
					StartupDo(p, (s == 0) ? ("Автозагрузка: " + it.Name + " — без задержки") : ("Автозагрузка: " + it.Name + " — запуск через " + nm.Substring(3)), "Set-FStartupDelay " + Q(it.Id) + " " + s, "Set-FStartupDelay " + Q(it.Id) + " " + it.Delay, keepReg: false);
				}, danger: false, it.Delay == s);
			}
			darkMenu.Sep();
			darkMenu.Item("Открыть расположение файла", "\ue8b7", null, delegate
			{
				if (!string.IsNullOrEmpty(it.Path) && File.Exists(it.Path))
				{
					Shell.Open("explorer.exe", "/select,\"" + it.Path + "\"");
				}
				else
				{
					_status.Set("warn", "Файл программы не найден");
				}
			}).Enabled = it.Exists && !string.IsNullOrEmpty(it.Path);
			darkMenu.Item("Найти в интернете", "\ue721", "Что это за программа и нужна ли она", delegate
			{
				string text = it.Name + " " + (string.IsNullOrEmpty(it.Path) ? "" : Path.GetFileName(it.Path)) + " автозагрузка";
				Shell.Open("https://www.google.com/search?q=" + Uri.EscapeDataString(text.Trim()));
			});
			darkMenu.Sep();
			darkMenu.Item("Удалить из автозагрузки", "\ue74d", "Программа останется, вернуть можно в «Истории»", delegate
			{
				if (WDialog.Ask(this, "Удалить «" + it.Name + "» из автозагрузки?", "Запись автозагрузки будет удалена, сама программа останется. Вернуть запись можно в «Истории».", "Удалить", "Отмена", DlgIcon.Warn))
				{
					StartupDo(p, "Автозагрузка: удалена запись «" + it.Name + "»", "Remove-FStartup " + Q(it.Id), "Restore-FStartup " + Q(it.Id), keepReg: true);
				}
			}, danger: true);
			darkMenu.Show(at);
		};
		sv.AddButton("Выключить лишние", BtnKind.Silver, delegate
		{
			List<StartupView.Item> list = sv.Items.FindAll((StartupView.Item i) => i.On && i.Advice == 1);
			if (list.Count == 0)
			{
				_status.Set("ok", "Лишних программ в автозагрузке нет");
			}
			else
			{
				CheckDialog.Row[] array = new CheckDialog.Row[list.Count];
				for (int j = 0; j < list.Count; j++)
				{
					array[j] = new CheckDialog.Row
					{
						Text = list[j].Name,
						Hint = "\n" + ((list[j].Company != "") ? list[j].Company : list[j].Source),
						Checked = true
					};
				}
				bool[] sel = CheckDialog.Ask(this, "Выключить лишнее в автозагрузке?", "Эти программы не нужны сразу после входа. Они останутся установленными — просто не будут стартовать сами.", array, "Выключить");
				if (sel != null && _host.TryBegin(quick: false, "Очистка автозагрузки"))
				{
					_host.Worker.Run(delegate(IEngine e)
					{
						e.Eval("Start-FGroup 'Автозагрузка: выключены лишние программы'");
						try
						{
							for (int k = 0; k < list.Count; k++)
							{
								if (sel[k])
								{
									e.Eval("Invoke-FTracked $fa1 'Set-FStartup $id $false' -Undo $fa2 -Cat 'apps'", new Hashtable
									{
										{
											"fa1",
											"Автозагрузка: " + list[k].Name + " — выключена"
										},
										{
											"fa2",
											"Set-FStartup " + Q(list[k].Id) + " $true"
										},
										{
											"id",
											list[k].Id
										}
									});
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
						p.Refresh(_host.Worker);
					});
				}
			}
		});
	}

	private void StartupDo(Page p, string title, string ps, string undo, bool keepReg)
	{
		if (_host.TryBegin(quick: false, title))
		{
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("Invoke-FTracked $fa1 $fa2 -Undo $fa3 -Cat 'apps' -KeepReg:$fa4", new Hashtable
				{
					{ "fa1", title },
					{ "fa2", ps },
					{ "fa3", undo },
					{ "fa4", keepReg }
				});
				return (object)null;
			}, delegate
			{
				_host.End();
				p.Refresh(_host.Worker);
			});
		}
	}

	private Func<IEngine, object> Fix(string id, string hist)
	{
		return delegate(IEngine e)
		{
			e.Eval("Invoke-FTracked $fa1 $fa2 -Cat 'fix' -NoUndo", new Hashtable
			{
				{ "fa1", hist },
				{
					"fa2",
					"Invoke-FFix " + Q(id)
				}
			});
			return (object)null;
		};
	}

	private void BuildFixes()
	{
		Page page = NewPage("fixes", "\ue90f", "Исправления", "Исправления", "Типичные проблемы Windows — решаются одной кнопкой");
		page.Add(new SectionLabel("Сеть и интернет"));
		TilesView tilesView = page.Add(new TilesView(_host));
		tilesView.Add("\ue774", "Нет интернета", "Сброс Winsock, TCP/IP, кэша DNS и перезапуск адаптеров", "Сеть пропадёт на несколько секунд", Fix("net", "Исправление: сброс сети")).Confirm = "Сетевые адаптеры перезапустятся, соединение пропадёт на 5–10 секунд. Для полного эффекта потом лучше перезагрузить ПК.";
		tilesView.Add("\ue968", "Сайты не открываются", "Очистка кэша DNS и перезапуск DNS-клиента", "Помогает, когда «сайт не найден»", Fix("dns", "Исправление: очищен кэш DNS"));
		tilesView.AddUi("\ue713", "Ничего не помогло", "Полный сброс сети Windows: адаптеры удаляются и ставятся заново", "Откроются Параметры → «Сброс сети»", delegate
		{
			Shell.Open("ms-settings:network-status");
		});
		tilesView.Add("\ue823", "Неверное время", "Синхронизация часов с сервером времени Windows", "Из-за неверного времени ломаются сайты и игры", Fix("time", "Исправление: синхронизировано время"));
		page.Add(new SectionLabel("Система и Проводник"));
		TilesView tilesView2 = page.Add(new TilesView(_host));
		tilesView2.Add("\ue80f", "Не открывается Пуск или поиск", "Перерегистрация Пуска, поиска и оболочки", "Проводник перезапустится", Fix("start", "Исправление: Пуск и поиск"));
		tilesView2.Add("\ue71d", "Пропали или сломались значки", "Пересоздание кэша значков", "Проводник перезапустится", Fix("icons", "Исправление: кэш значков"));
		tilesView2.Add("\ue91b", "Не видно превью картинок", "Очистка и пересоздание кэша эскизов", "Проводник перезапустится", Fix("thumbs", "Исправление: кэш эскизов"));
		tilesView2.Add("\ue8b7", "Папки открываются не так", "Сброс запомненных видов и размеров папок", "Вид всех папок вернётся к стандартному", Fix("folders", "Исправление: сброшены виды папок"));
		tilesView2.Add("\ue72c", "Завис Проводник", "Перезапуск Проводника, панели задач и рабочего стола", "Открытые окна папок закроются", Fix("explorer", "Исправление: перезапущен Проводник"), "Перезапустить");
		tilesView2.Add("\ue771", "Шрифты отображаются криво", "Перестроение кэша шрифтов", "Полный эффект после перезагрузки", Fix("fonts", "Исправление: кэш шрифтов"));
		tilesView2.Add("\ue71d", "Значки в трее пропали", "Сброс области уведомлений", "Проводник перезапустится", Fix("tray", "Исправление: область уведомлений"));
		tilesView2.Add("\ue73e", "Не работает буфер обмена", "Очистка буфера и перезапуск его службы", "Скопированное сейчас пропадёт", Fix("clipboard", "Исправление: буфер обмена"));
		tilesView2.Add("\ue721", "Поиск не находит файлы", "Перестроение индекса поиска Windows", "Индекс соберётся заново за 10–60 минут", Fix("searchidx", "Исправление: индекс поиска"));
		tilesView2.Add("\ue950", "Ошибки WMI", "Проверка и восстановление репозитория WMI", "Помогает, если не открываются сведения о системе", Fix("wmi", "Исправление: WMI"));
		page.Add(new SectionLabel("Устройства"));
		TilesView tilesView3 = page.Add(new TilesView(_host));
		tilesView3.Add("\ue767", "Нет звука", "Перезапуск звуковых служб Windows", "Звук пропадёт на пару секунд", Fix("audio", "Исправление: звук"));
		tilesView3.Add("\ue749", "Принтер не печатает", "Очистка очереди печати и перезапуск диспетчера", "Задания в очереди будут удалены", Fix("print", "Исправление: очередь печати"));
		tilesView3.Add("\ue702", "Bluetooth не работает", "Перезапуск служб Bluetooth", "Наушники переподключатся", Fix("bt", "Исправление: Bluetooth"));
		tilesView3.Add("\ue88e", "Устройство не определяется", "Повторный поиск оборудования", "Как «Обновить конфигурацию» в диспетчере устройств", Fix("usb", "Исправление: поиск устройств"));
		page.Add(new SectionLabel("Windows и Store"));
		TilesView tilesView4 = page.Add(new TilesView(_host));
		tilesView4.Add("\ue895", "Обновления не ставятся", "Сброс компонентов Центра обновления и его кэша", "Займёт 1–3 минуты", Fix("wu", "Исправление: Центр обновления")).Confirm = "Службы Центра обновления остановятся, кэш загрузок будет очищен, компоненты перерегистрируются. Обновления скачаются заново.";
		tilesView4.Add("\ue719", "Не работает Microsoft Store", "Сброс кэша Store и перерегистрация приложения", "Store откроется сам", Fix("store", "Исправление: Microsoft Store"));
		tilesView4.Add("\uea18", "Не открывается «Безопасность Windows»", "Перерегистрация приложения и перезапуск службы", "Defender не выключается", Fix("sechealth", "Исправление: Безопасность Windows"));
		tilesView4.AddUi("\ue90f", "Повреждены системные файлы", "SFC + DISM: проверка и восстановление Windows", "10–30 минут, отдельное окно", delegate
		{
			Shell.Console("sfc.exe /scannow; dism.exe /Online /Cleanup-Image /RestoreHealth");
		}, "Запустить");
	}

	private void BuildMaintenance(Page page)
	{
		CleanView cv = page.Add(new CleanView());
		cv.RescanClicked += delegate
		{
			cv.SetScanning();
			page.Refresh(_host.Worker);
		};
		cv.CleanClicked += delegate
		{
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			foreach (CleanView.Cat cat in cv.Cats)
			{
				if (cat.On && cat.Size != 0.0)
				{
					list.Add(cat.Id);
					stringBuilder.Append("•  ").Append(cat.Name).Append("  —  ")
						.Append(CleanView.Fmt(cat.Size))
						.Append('\n');
				}
			}
			if (list.Count == 0)
			{
				_status.Set("ok", "Отмеченные категории уже пусты");
			}
			else
			{
				bool flag = list.Contains("recycle");
				if (WDialog.Ask(this, "Очистить " + CleanView.Fmt(cv.Selected) + "?", stringBuilder.ToString() + (flag ? "\nКорзина будет очищена безвозвратно." : "\nЛичные файлы не затрагиваются."), "Очистить", "Отмена") && _host.TryBegin(quick: false, "Очистка диска — " + CleanView.Fmt(cv.Selected)))
				{
					string arg = string.Join(",", list.ToArray());
					cv.SetScanning();
					_host.Worker.Run((IEngine e) => e.Eval("$b = Clear-FItems $fa1; Add-FHist 'Очистка диска' 'clean' '' $null ('Освобождено: ' + (Format-FSize $b)) $false; $b", new Hashtable { { "fa1", arg } }), delegate(object r)
					{
						_host.End();
						double b = 0.0;
						try
						{
							b = Convert.ToDouble(r ?? ((object)0), CultureInfo.InvariantCulture);
						}
						catch
						{
						}
						cv.LastResult = "Освобождено " + CleanView.Fmt(b);
						_status.Set("ok", cv.LastResult);
						page.Refresh(_host.Worker);
					});
				}
			}
		};
		page.Add(new SectionLabel("Место на диске"));
		Tw(page, "Контроль памяти", "Windows сама удаляет временные файлы и старое содержимое корзины", "storagesense", "clean").Info("Включает «Контроль памяти»: Windows периодически чистит временные файлы и корзину, когда место заканчивается.", "Диск не забивается мусором без твоего участия.", "Файлы в корзине старше 30 дней удаляются автоматически.", null, "Включить, если не хранишь нужное в корзине.");
		Tw(page, "Отключить зарезервированное хранилище", "Вернуть ~7 ГБ, которые Windows держит под обновления", "reserved", "clean").Info("Выключает «зарезервированное хранилище» — место, которое Windows заранее занимает под обновления.", "Освобождается около 7 ГБ на системном диске.", "При почти полном диске крупное обновление может не установиться.", null, "Включи, если на диске C: мало места.");
		page.Add(new SectionLabel("Проверка и обслуживание"));
		TilesView tilesView = page.Add(new TilesView(_host));
		tilesView.AddUi("\ue90f", "Проверка системных файлов", "SFC + DISM RestoreHealth находят и чинят повреждённые файлы Windows", "10–30 минут, отдельное окно", delegate
		{
			Shell.Console("sfc.exe /scannow; dism.exe /Online /Cleanup-Image /RestoreHealth");
		}, "Запустить");
		tilesView.AddUi("\ueda2", "Очистка компонентов", "DISM удаляет старые версии обновлений из WinSxS", "5–20 минут, отдельное окно", delegate
		{
			Shell.Console("dism.exe /Online /Cleanup-Image /StartComponentCleanup");
		}, "Запустить");
		tilesView.AddUi("\ue73e", "Проверка диска", "chkdsk ищет ошибки файловой системы без перезагрузки", "1–5 минут, отдельное окно", delegate
		{
			Shell.Console("chkdsk.exe C: /scan");
		}, "Проверить");
		tilesView.AddUi("\ueda2", "Оптимизация дисков", "TRIM для SSD и дефрагментация HDD", "Откроется утилита Windows", delegate
		{
			Shell.Open("dfrgui.exe");
		});
		tilesView.AddUi("\ueda2", "Очистка диска Windows", "Классическая утилита cleanmgr", "Откроется утилита Windows", delegate
		{
			Shell.Open("cleanmgr.exe");
		});
		tilesView.AddUi("\ue713", "Параметры хранилища", "Что занимает место и настройки «Контроля памяти»", "Откроются Параметры", delegate
		{
			Shell.Open("ms-settings:storagesense");
		});
		BuildRestartTools(page);
	}

	private void WireReboot()
	{
		_side.RebootClicked += RebootMenu;
		_rbTimer = new Timer
		{
			Interval = 15000
		};
		_rbTimer.Tick += delegate
		{
			UpdateRebootUi();
		};
		_rbTimer.Start();
	}

	private void ApplyRebootInfo(object r)
	{
		Hashtable hashtable = r as Hashtable;
		bool needReboot = hashtable != null && hashtable["App"] is bool && (bool)hashtable["App"];
		bool sysReboot = hashtable != null && hashtable["Sys"] is bool && (bool)hashtable["Sys"];
		if (hashtable == null && r is bool)
		{
			needReboot = (bool)r;
		}
		NeedReboot = needReboot;
		_sysReboot = sysReboot;
		UpdateRebootUi();
	}

	private void UpdateRebootUi()
	{
		bool flag = NeedReboot || _sysReboot;
		if (RebootScheduled)
		{
			int num = Math.Max(1, (int)Math.Ceiling((_rebootAt - DateTime.Now).TotalMinutes));
			_side.SetReboot("Перезагрузка в " + _rebootAt.ToString("HH:mm"), "через " + num + " мин • нажми, чтобы изменить", scheduled: true);
		}
		else if (flag)
		{
			_side.SetReboot("Нужна перезагрузка", NeedReboot ? "чтобы применить изменения" : "обновление Windows ждёт", scheduled: false);
		}
		else
		{
			_side.SetReboot(null, null, scheduled: false);
		}
		if (_hero != null && _hero.NeedReboot != (flag && !RebootScheduled))
		{
			_hero.NeedReboot = flag && !RebootScheduled;
			_hero.Invalidate();
		}
	}

	private static void RunWait(string file, string args)
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo(file, args)
			{
				UseShellExecute = false,
				CreateNoWindow = true
			});
			process?.WaitForExit(5000);
		}
		catch
		{
		}
	}

	internal void DemoMenu()
	{
		RebootMenu(PointToScreen(new Point(Theme.S(40f), Theme.S(300f))));
	}

	internal void DemoQueue(bool expand = true)
	{
		_status.SetBackground("Расшифровка BitLocker — диск C:", "расшифровано 72%  •  осталось ≈ 6 мин", 0.72f);
		_host.TryBegin(quick: false, "Defender — обновление баз");
		_host.TryBegin(quick: true, "Быстрые подсказки при наведении: ВКЛ");
		_host.TryBegin(quick: true, "Отключить рекламу в Проводнике: ВКЛ");
		_host.TryBegin(quick: false, "Очистка DNS — Очистить");
		if (expand)
		{
			_status.DemoExpand();
		}
	}

	private void RebootMenu(Point at)
	{
		DarkMenu darkMenu = new DarkMenu();
		darkMenu.Header("Перезагрузка", RebootScheduled ? ("Запланирована на " + _rebootAt.ToString("HH:mm")) : (NeedReboot ? "Чтобы применить изменения" : "Выбери, когда перезагрузить"));
		darkMenu.Item("Перезагрузить сейчас", "\ue777", "Через 5 секунд после подтверждения", delegate
		{
			RebootNow();
		});
		if (RebootScheduled)
		{
			darkMenu.Item("Отменить перезагрузку", "\ue711", "Компьютер не перезагрузится", delegate
			{
				RunWait("shutdown.exe", "/a");
				_rebootAt = DateTime.MinValue;
				_status.Set("ok", "Запланированная перезагрузка отменена");
				UpdateRebootUi();
			});
		}
		else
		{
			darkMenu.Item("Через 10 минут", "\ue823", "Успеешь сохранить работу, можно отменить", delegate
			{
				ScheduleReboot(10);
			});
			darkMenu.Item("Через час", "\ue823", "Можно отменить в любой момент", delegate
			{
				ScheduleReboot(60);
			});
		}
		darkMenu.Sep();
		darkMenu.Item("Только перезапустить Проводник", "\ue8b7", "Без перезагрузки — применит изменения оформления", delegate
		{
			RestartExplorer();
		});
		darkMenu.Show(at);
	}

	private void RebootNow()
	{
		if (WDialog.Ask(this, "Перезагрузить компьютер?", "Сохрани открытые документы — программы закроются. Перезагрузка начнётся через 5 секунд." + (NeedReboot ? "\n\nПосле неё вступят в силу все изменения, которые этого ждут." : ""), "Перезагрузить", "Отмена", DlgIcon.Warn))
		{
			if (RebootScheduled)
			{
				RunWait("shutdown.exe", "/a");
			}
			Shell.Open("shutdown.exe", "/r /t 5");
		}
	}

	private void OfferRebootAfterLevel(string level)
	{
		if (NeedReboot && !RebootScheduled)
		{
			string body = "Политики, службы и удалённые приложения окончательно применятся только после перезагрузки. Сохрани открытые документы — программы закроются.\n\nЕсли сейчас неудобно, напоминание останется в меню слева.";
			switch (WDialog.Choose(this, "Перезагрузить, чтобы завершить «" + level + "»?", body, "Перезагрузить сейчас", "Через 10 минут", "Позже", DlgIcon.Warn))
			{
			case 0:
				Shell.Open("shutdown.exe", "/r /t 5");
				break;
			case 1:
				ScheduleReboot(10);
				break;
			}
		}
	}

	private void ScheduleReboot(int minutes)
	{
		if (RebootScheduled)
		{
			RunWait("shutdown.exe", "/a");
		}
		RunWait("shutdown.exe", "/r /t " + minutes * 60 + " /c \"WinWeaver: перезагрузка, чтобы применить изменения\"");
		_rebootAt = DateTime.Now.AddMinutes(minutes);
		_status.Set("ok", "Компьютер перезагрузится в " + _rebootAt.ToString("HH:mm") + " — отменить можно в меню слева");
		UpdateRebootUi();
	}

	private void RestartExplorer()
	{
		if (_host.TryBegin(quick: false, "Перезапуск Проводника"))
		{
			_status.Set("info", "Перезапускаю Проводник…");
			_host.Worker.Run(delegate(IEngine e)
			{
				e.Eval("$global:FDeferExplorer = $false; Restart-FExplorer");
				return (object)null;
			}, delegate
			{
				_host.End();
				_status.Set("ok", "Проводник перезапущен");
			});
		}
	}

	private void BuildRestartTools(Page page)
	{
		page.Add(new SectionLabel("Перезагрузка и перезапуск"));
		TilesView tilesView = page.Add(new TilesView(_host));
		tilesView.AddUi("\ue72c", "Перезагрузить компьютер", "Сейчас, через 10 минут или через час — по выбору", "Можно отменить, пока не началась", delegate
		{
			RebootMenu(Cursor.Position);
		}, "Выбрать…");
		tilesView.AddUi("\ue8b7", "Перезапустить Проводник", "Обновляет панель задач, Пуск и рабочий стол без перезагрузки", "Окна Проводника закроются", delegate
		{
			RestartExplorer();
		}, "Перезапустить");
		tilesView.AddUi("\ue8bb", "Выйти из системы", "Применяет настройки, которым хватает нового входа", "Программы закроются", delegate
		{
			if (WDialog.Ask(this, "Выйти из системы?", "Все программы будут закрыты — сохрани открытые документы.", "Выйти", "Отмена", DlgIcon.Warn))
			{
				Shell.Open("shutdown.exe", "/l");
			}
		}, "Выйти");
		tilesView.AddUi("\ue713", "Загрузиться в BIOS / UEFI", "Перезагрузка сразу в настройки прошивки материнской платы", "Только для UEFI", delegate
		{
			if (WDialog.Ask(this, "Перезагрузиться в BIOS / UEFI?", "Компьютер перезагрузится и откроет настройки прошивки. Сохрани открытые документы.", "Перезагрузить", "Отмена", DlgIcon.Warn))
			{
				Shell.Open("shutdown.exe", "/r /fw /t 5");
			}
		}, "Перезагрузить");
		tilesView.AddUi("\ue90f", "Особые варианты загрузки", "Безопасный режим, восстановление, командная строка", "Меню восстановления Windows", delegate
		{
			if (WDialog.Ask(this, "Открыть особые варианты загрузки?", "Компьютер перезагрузится в меню восстановления Windows: оттуда можно включить безопасный режим или вернуть систему.", "Перезагрузить", "Отмена", DlgIcon.Warn))
			{
				Shell.Open("shutdown.exe", "/r /o /t 5");
			}
		}, "Перезагрузить");
	}

	internal static bool IsFpsRisky(SettingCard c)
	{
		if (c.Title != null)
		{
			if (!c.Title.Contains("HVCI"))
			{
				return c.Title.Contains("VBS");
			}
			return true;
		}
		return false;
	}

	internal void DemoFps()
	{
		FixFps();
	}

	private void ApplyFpsMap()
	{
		foreach (SettingCard item in _tracked)
		{
			object[][] fpsMap = FpsMap;
			foreach (object[] array in fpsMap)
			{
				if ((string)array[0] == item.Title)
				{
					object[] array2 = new object[array.Length - 2];
					Array.Copy(array, 2, array2, 0, array2.Length);
					item.Fps((int)array[1], array2);
				}
			}
		}
	}

	private List<SettingCard> FpsCards()
	{
		return _tracked.FindAll((SettingCard c) => c.FpsW > 0 && !c.Unsupported);
	}

	private static object FpsTarget(SettingCard c)
	{
		return c.FpsBest[(c.FpsBest.Length > 1 && c.Seg != null) ? 1 : 0];
	}

	private void FixFps()
	{
		List<SettingCard> list = FpsCards().FindAll((SettingCard c) => c.FpsOk == false);
		if (list.Count == 0)
		{
			return;
		}
		list.Sort((SettingCard a, SettingCard b) => b.FpsW.CompareTo(a.FpsW));
		List<CheckDialog.Row> list2 = new List<CheckDialog.Row>();
		foreach (SettingCard item in list)
		{
			object obj = FpsTarget(item);
			string tag = ((item.Seg != null) ? ("→ " + item.Seg.Items[(int)obj]) : (((bool)obj) ? null : "выключить"));
			string text = ((item.FpsW >= 7) ? "сильно влияет на FPS" : ((item.FpsW >= 4) ? "заметно" : "немного"));
			bool flag = IsFpsRisky(item);
			list2.Add(new CheckDialog.Row
			{
				Text = item.Title,
				Hint = "\n" + text + "  •  " + (string.IsNullOrEmpty(item.Desc) ? item.What : item.Desc),
				Tag = tag,
				Checked = !flag
			});
		}
		bool[] array = CheckDialog.Ask(this, "Выжать максимум FPS", "Эти настройки Windows мешают играм. Снизу — то, что влияет меньше. Защита ядра (HVCI, VBS) по умолчанию не отмечена: её выключение даёт прирост, но ослабляет защиту от вирусов. Всё попадёт в «Историю» одной группой.", list2.ToArray(), "Применить", "Отмена", DlgIcon.Info);
		if (array == null)
		{
			return;
		}
		List<SettingCard> sel = new List<SettingCard>();
		for (int i = 0; i < array.Length && i < list.Count; i++)
		{
			if (array[i])
			{
				sel.Add(list[i]);
			}
		}
		if (sel.Count == 0 || !_host.TryBegin(quick: false, "Исправляю проблемы (" + sel.Count + ")"))
		{
			return;
		}
		_hero.Fixing = true;
		_hero.Invalidate();
		_host.Worker.Run(delegate(IEngine e)
		{
			e.Eval("Start-FGroup 'Максимум FPS'");
			try
			{
				foreach (SettingCard item2 in sel)
				{
					object obj2 = FpsTarget(item2);
					e.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
					{
						{
							"fa1",
							item2.HistTitle(obj2)
						},
						{ "fa2", item2.GetScript },
						{ "fa3", item2.SetScript },
						{ "fa4", obj2 },
						{ "fa5", item2.Cat }
					});
				}
			}
			finally
			{
				e.Eval("Stop-FGroup; Show-Ok ('Применены игровые настройки: ' + $fa1)", new Hashtable { { "fa1", sel.Count } });
			}
			return (object)null;
		}, delegate
		{
			_hero.Fixing = false;
			_host.End();
			CheckReboot();
			_scoreDirty = true;
			UpdateScore();
			_cur?.Refresh(_host.Worker);
		});
	}

	private bool IsFresh(Page p)
	{
		if (p.FreshStamp == _actStamp)
		{
			return (DateTime.Now - p.FreshAt).TotalSeconds < 600.0;
		}
		return false;
	}

	private static bool NeedsGate(Page p)
	{
		return false;
	}

	private void RefreshPage(Page p, Action done = null)
	{
		if (done == null && NeedsGate(p))
		{
			if (p.Gated)
			{
				return;
			}
			p.ShowGate(delegate
			{
				p.Gate.Busy = true;
				ReadPage(p, delegate
				{
					p.HideGate();
				});
			});
		}
		else
		{
			ReadPage(p, done);
		}
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
		p.Refresh(ReadWorker, all: true, delegate
		{
			p.FreshStamp = stamp;
			p.FreshAt = DateTime.Now;
			done?.Invoke();
		});
	}

	private void StartWithLoadScreen()
	{
		if (_load == null || _load.IsDisposed)
		{
			_load = new LoadScreen
			{
				Logo = _side.Logo
			};
			base.Controls.Add(_load);
		}
		_load.SetBounds(0, 0, base.ClientSize.Width, base.ClientSize.Height);
		_load.BringToFront();
		_side.Enabled = false;
		_status.Held = true;
		_content.Enabled = false;
		string startPage = !_fake && Prefs.RememberPage ? Prefs.LastPage : "home";
		Show(_pages.ContainsKey(startPage) ? startPage : "home");
		_warm.Clear();
		foreach (Page value in _pages.Values)
		{
			if (value.Key != "home" && value.Key != "review" && value.Key != "settings")
			{
				_warm.Add(value);
			}
		}
		_warmTotal = _warm.Count;
		int total = 4 + _warmTotal;
		int done = 0;
		int lanes = 2;
		Action<string, string> step = delegate(string text, string hint)
		{
			done++;
			_load?.SetStep(text, (float)done / (float)total, hint);
			WarmStatus();
		};
		Action unlock = delegate
		{
			if (_load != null)
			{
				_side.Enabled = true;
				_content.Enabled = true;
				LoadScreen load = _load;
				_load = null;
				load.Finish(delegate
				{
					_status.Held = false;
					AfterLoaded();
				});
			}
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
			if (lanes <= 0)
			{
				step("Готово", "");
				PrewarmHandles();
				StartDeferredScan();
				if (_warmBg)
				{
					_warmBg = false;
					if (_status.BgTitle == "Читаю разделы")
					{
						_status.ClearBackground();
					}
				}
				if (!(Environment.GetEnvironmentVariable("WW_HOLDLOAD") == "1"))
				{
					unlock();
				}
			}
		};
		_load.SetStep("Запускаю движок…", 0.03f, "первый запуск может занять несколько секунд");
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
				home.Refresh(_host.Worker, all: true, delegate
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
		bool hasScan = false;
		foreach (IBlock b in p.Blocks)
		{
			if (b is CleanView cv)
			{
				cv.Deferred = true;
				hasScan = true;
			}
		}
		if (hasScan)
		{
			_scanPage = p;
		}
		int st = _actStamp;
		string wd = Environment.GetEnvironmentVariable("WW_WARMDELAY");
		if (wd != null)
		{
			Console.Error.WriteLine("WARM " + p.Title);
			w.Run(delegate
			{
				Thread.Sleep(int.Parse(wd));
				return (object)null;
			}, delegate
			{
			});
		}
		p.Refresh(w, all: true, delegate
		{
			foreach (IBlock b in p.Blocks)
			{
				if (b is CleanView cv)
				{
					cv.Deferred = false;
				}
			}
			if (p.FreshStamp != _actStamp)
			{
				p.FreshStamp = st;
				p.FreshAt = DateTime.Now;
			}
			step("Читаю разделы…", p.Title);
			WarmNext(w, step, finished);
		});
	}

	private Page _scanPage;

	// Скан диска после чтения всех разделов: прогресс виден только в «Обслуживании»
	// (скелетон и «считаю…»), общий статус «Читаю разделы» его не ждёт.
	private void StartDeferredScan()
	{
		Page p = _scanPage;
		_scanPage = null;
		if (p == null)
		{
			return;
		}
		p.Refresh(ReadWorker, all: true, null);
	}

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
		string bgTitle = _status.BgTitle;
		if (bgTitle != null && bgTitle != "Читаю разделы")
		{
			return;
		}
		int num = _warm.Count + ((_warming != null) ? 1 : 0);
		if (num > 0)
		{
			int num2 = Math.Max(0, _warmTotal - num);
			string text = "прочитано " + num2 + " из " + _warmTotal + ((_warming != null) ? ("  •  " + _warming.Title) : "");
			_status.SetBackground("Читаю разделы", text, (float)num2 / (float)Math.Max(1, _warmTotal));
			if (Environment.GetEnvironmentVariable("WW_WARMDELAY") != null)
			{
				Console.Error.WriteLine("BG " + text);
			}
		}
	}

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

	private void PrewarmHandles()
	{
		Queue<Page> q = new Queue<Page>();
		foreach (Page value in _pages.Values)
		{
			if (value != _cur)
			{
				q.Enqueue(value);
			}
		}
		Timer t = new Timer
		{
			Interval = 60
		};
		t.Tick += delegate
		{
			if (!(DateTime.Now < Transition.BusyUntil))
			{
				if (q.Count != 0)
				{
					Page page = q.Dequeue();
					try
					{
						if (!page.Visible)
						{
							page.Bounds = _content.ClientRectangle;
							page.DoLayoutBlocks();
						}
						MakeHandles(page);
						return;
					}
					catch
					{
						return;
					}
				}
				t.Stop();
				t.Dispose();
			}
		};
		t.Start();
	}

	private static void MakeHandles(Control c)
	{
		if (!c.IsHandleCreated)
		{
			_ = c.Handle;
		}
		foreach (Control control in c.Controls)
		{
			MakeHandles(control);
		}
	}

	private void Mark(Worker w, Action a)
	{
		w.Run((IEngine e) => (object)null, delegate
		{
			a();
		});
	}

	private static string CancelRisk(string title)
	{
		string text = title ?? "";
		if (text.StartsWith("Автонастройка"))
		{
			return "Часть настроек уже применена, остальные — нет: система останется в смешанном состоянии. Применённое можно откатить в «Истории».";
		}
		if (text.StartsWith("Исправляю") || text.StartsWith("Откат"))
		{
			return "Изменения успеют примениться только частично. Проверь результат и при необходимости повтори действие.";
		}
		if (text.StartsWith("Удаление остатков"))
		{
			return "Часть остатков уже удалена, остальные останутся на диске и в реестре.";
		}
		if (text.StartsWith("Удаление точ") || text.StartsWith("Удаление старых точек"))
		{
			return "Часть точек может быть уже удалена — их не вернуть.";
		}
		if (text.StartsWith("Удаление"))
		{
			return "Программа может остаться удалённой наполовину: без ярлыков или с повреждённой установкой. Тогда её придётся переустановить или удалить заново.";
		}
		if (text.StartsWith("Очистка диска"))
		{
			return "Уже удалённые файлы не вернутся, остальное останется на диске.";
		}
		if (text.StartsWith("Создание точки"))
		{
			return "Точка восстановления не будет создана — откатиться к текущему состоянию будет нельзя.";
		}
		if (text.StartsWith("DNS"))
		{
			return "Сетевой адаптер может остаться с неполными настройками DNS — сайты могут перестать открываться, пока DNS не выбран заново.";
		}
		if (text.StartsWith("Перезапуск Проводника"))
		{
			return "Проводник может не запуститься обратно, и пропадёт панель задач. Тогда запусти explorer.exe через Диспетчер задач (Ctrl+Shift+Esc).";
		}
		if (text.Contains("BitLocker"))
		{
			return "Команда могла уже уйти в Windows — отмена её не остановит, а состояние на экране может обновиться не сразу.";
		}
		if (text.StartsWith("Microsoft Defender") || text.StartsWith("Защита в реальном"))
		{
			return "Defender может остаться в промежуточном состоянии (часть служб выключена, часть — нет). Проверь его состояние после отмены.";
		}
		if (text.StartsWith("Пауза обновлений") || text.StartsWith("Возобновление обновлений"))
		{
			return "Настройки Центра обновления могут записаться не полностью.";
		}
		if (text.StartsWith("Возврат настроек"))
		{
			return "Часть приложений и настроек уже вернётся в нужное состояние, часть — нет. Проводник может остаться выключенным.";
		}
		return null;
	}

	private void OnCancelJob(int id, string title, bool running)
	{
		if (running)
		{
			string text = CancelRisk(title);
			if (text != null && !WDialog.Ask(this, "Отменить «" + (title ?? "действие") + "»?", "Действие уже выполняется и прервётся на середине.\n\n" + text, "Отменить действие", "Продолжить", DlgIcon.Warn))
			{
				return;
			}
		}
		_host.Cancel(id);
		_status.MarkCancelled(id);
	}

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
			Shell.Open((_update != null && _update.Url != null) ? _update.Url : "https://github.com/fel7xxx/WinWeaver/releases");
		};
		L.Changed += delegate
		{
			if (!base.IsDisposed && base.IsHandleCreated)
			{
				BeginInvoke((Action)delegate
				{
					foreach (Page value in _pages.Values)
					{
						value.DoLayoutBlocks();
					}
					Invalidate(invalidateChildren: true);
				});
			}
		};
	}

	private void AfterLoadedExtras()
	{
		if (Mode == "/uninstall")
		{
			BeginInvoke(new Action(StartUninstall));
			return;
		}
		if (UpdateChecker.Due())
		{
			CheckUpdates(manual: false);
		}
		_host.Worker.Run((IEngine e) => e.As(e.Eval("Test-FBuildChanged"), fallback: false), delegate(object r)
		{
			if (r is bool && (bool)r)
			{
				CheckDrift(afterUpdate: true);
			}
		});
	}

	public void Demo(string what)
	{
		if (what == null)
		{
			return;
		}
		switch (what.Length)
		{
		case 7:
			switch (what[0])
			{
			case 'w':
			{
				if (!(what == "welcome"))
				{
					break;
				}
				using WelcomeDialog welcomeDialog = new WelcomeDialog();
				welcomeDialog.ShowFor(this);
				break;
			}
			case 's':
				if (what == "support")
				{
					ShowSupport();
				}
				break;
			}
			break;
		case 6:
			switch (what[0])
			{
			case 'e':
				if (what == "expert")
				{
					ConfirmExpert();
				}
				break;
			case 'u':
				if (what == "update")
				{
					_update = new UpdateChecker.Result
					{
						Tag = "v1.1.0",
						Url = "https://github.com/fel7xxx/WinWeaver/releases",
						Newer = true
					};
					_side.SetUpdate(L.T("Доступна версия {0}").Replace("{0}", "1.1.0"), "Открыть страницу загрузки");
				}
				break;
			}
			break;
		case 11:
			if (what == "personalize")
			{
				string opts = "7zip,ext,temp;def=0";
				PersonalizeDialog.Ask(this, "Оптимальная", ref opts, out var _, out var _, null);
			}
			break;
		case 3:
			if (what == "bug")
			{
				ShowBugReport();
			}
			break;
		case 5:
			if (what == "drift")
			{
				CheckDrift(afterUpdate: true);
			}
			break;
		case 9:
			if (what == "uninstall")
			{
				WDialog.Choose(this, L.T("Удалить {0}?").Replace("{0}", "WinWeaver"), "Будут удалены задачи планировщика, автозапуск, ярлыки, схема питания WinWeaver, папка программы и папка данных (история, журнал).\n\nМожно сначала откатить все изменения из «Истории» — система вернётся к состоянию до WinWeaver (удалённые приложения Windows придётся поставить из Microsoft Store).", "Откатить всё и удалить", "Только удалить", "Отмена", DlgIcon.Warn);
			}
			break;
		case 4:
		case 8:
		case 10:
			break;
		}
	}

	public void ShowSupport()
	{
		using SupportDialog supportDialog = new SupportDialog();
		supportDialog.ShowFor(this);
	}

	public void ShowBugReport()
	{
		using BugReportDialog bugReportDialog = new BugReportDialog();
		bugReportDialog.ShowFor(this);
	}

	private void CheckUpdates(bool manual)
	{
		if (manual)
		{
			_status.Set("ok", "Проверяю обновления на GitHub…");
		}
		UpdateChecker.CheckAsync(delegate(UpdateChecker.Result r)
		{
			if (!base.IsDisposed && base.IsHandleCreated)
			{
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
					}
					else if (!r.Newer)
					{
						if (manual)
						{
							_status.Set("ok", L.T("Установлена последняя версия {0}").Replace("{0}", "1.0.1"));
						}
					}
					else
					{
						_update = r;
						_side.SetUpdate(L.T("Доступна версия {0}").Replace("{0}", r.Tag.TrimStart('v', 'V')), "Открыть страницу загрузки");
						if (manual || Prefs.SkipVersion != r.Tag)
						{
							switch (WDialog.Choose(this, L.T("Вышла версия {0}").Replace("{0}", r.Tag.TrimStart('v', 'V')), L.T("Сейчас установлена {0}. Новую версию можно скачать со страницы релиза на GitHub — там же список изменений.\n\nПрограмма ничего не скачивает и не обновляет сама.").Replace("{0}", "1.0.1"), "Открыть страницу", "Пропустить эту версию", "Позже", DlgIcon.Info))
							{
							case 0:
								Shell.Open(r.Url ?? "https://github.com/fel7xxx/WinWeaver/releases");
								break;
							case 1:
								Prefs.SkipVersion = r.Tag;
								break;
							}
						}
					}
				});
			}
		});
	}

	private void CheckDrift(bool afterUpdate)
	{
		if (!afterUpdate && !_host.TryBegin(quick: true, "Проверка настроек"))
		{
			return;
		}
		_host.Worker.Run((IEngine e) => e.Eval("Get-FDrift"), delegate(object r)
		{
			if (!afterUpdate)
			{
				_host.End(quick: true);
			}
			List<Hashtable> list = new List<Hashtable>();
			if (r is IEnumerable enumerable && !(r is string))
			{
				foreach (object item2 in enumerable)
				{
					if (item2 is Hashtable item)
					{
						list.Add(item);
					}
				}
			}
			if (list.Count == 0)
			{
				if (!afterUpdate)
				{
					_status.Set("ok", "Все настройки на месте — Windows ничего не вернула");
				}
			}
			else
			{
				CheckDialog.Row[] array = new CheckDialog.Row[list.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = new CheckDialog.Row
					{
						Text = Convert.ToString(list[i]["Title"]),
						Checked = true
					};
				}
				string title = (afterUpdate ? "Windows обновилась и вернула часть настроек" : "Windows вернула часть настроек");
				string body = L.T("Эти изменения были сделаны в {0}, но сейчас снова выключены или вернулись к значению Windows ({1} шт.). Отметь, что применить заново — каждое снова попадёт в «Историю».").Replace("{0}", "WinWeaver").Replace("{1}", list.Count.ToString());
				bool[] array2 = CheckDialog.Ask(this, title, body, array, "Применить заново", "Не сейчас", DlgIcon.Warn);
				if (array2 != null)
				{
					List<string> ids = new List<string>();
					for (int j = 0; j < array2.Length; j++)
					{
						if (array2[j])
						{
							ids.Add(Convert.ToString(list[j]["Id"]));
						}
					}
					if (ids.Count != 0 && _host.TryBegin(quick: false, "Возврат настроек после обновления Windows"))
					{
						_host.Worker.Run(delegate(IEngine e)
						{
							e.Eval("[void](Repair-FDrift $fa1)", new Hashtable { 
							{
								"fa1",
								ids.ToArray()
							} });
							return (object)null;
						}, delegate
						{
							_host.End();
							_cur?.Refresh(_host.Worker);
						});
					}
				}
			}
		});
	}

	private List<SettingCard> TrackedCards()
	{
		List<SettingCard> list = new List<SettingCard>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (Page value in _pages.Values)
		{
			if (value.Key == "review")
			{
				continue;
			}
			foreach (IBlock item in Flatten(value.Blocks))
			{
				if (item is SettingCard { GetScript: not null, Unsupported: false } settingCard && hashSet.Add(settingCard.Title))
				{
					list.Add(settingCard);
				}
			}
		}
		return list;
	}

	private static IEnumerable<IBlock> Flatten(IEnumerable<IBlock> blocks)
	{
		foreach (IBlock block in blocks)
		{
			if (block is Grid grid)
			{
				foreach (IBlock item in Flatten(grid.Items))
				{
					yield return item;
				}
			}
			else
			{
				yield return block;
			}
		}
	}

	private static string J(string s)
	{
		StringBuilder stringBuilder = new StringBuilder("\"");
		string text = s ?? "";
		foreach (char c in text)
		{
			if (c == '"' || c == '\\')
			{
				stringBuilder.Append('\\').Append(c);
			}
			else if (c < ' ')
			{
				StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
				int num = c;
				stringBuilder2.Append(num.ToString("x4"));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.Append('"').ToString();
	}

	private static string UnJ(string s)
	{
		return Regex.Replace(s, "\\\\(u([0-9a-fA-F]{4})|.)", (Match m) => (!m.Groups[2].Success) ? m.Groups[1].Value : ((char)Convert.ToInt32(m.Groups[2].Value, 16)).ToString());
	}

	private void ExportProfile()
	{
		string path;
		using (SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = L.T("Профиль WinWeaver") + " (*.json)|*.json",
			FileName = "WinWeaver-profile-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json"
		})
		{
			if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			path = saveFileDialog.FileName;
		}
		if (!_host.TryBegin(quick: false, "Экспорт профиля"))
		{
			return;
		}
		List<SettingCard> cards = TrackedCards();
		_host.Worker.Run(delegate(IEngine e)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{\n  \"app\": \"WinWeaver\",\n  \"format\": 1,\n  \"version\": ").Append(J("1.0.1")).Append(",\n  \"created\": ")
				.Append(J(DateTime.Now.ToString("s")))
				.Append(",\n  \"settings\": {");
			int num = 0;
			foreach (SettingCard item in cards)
			{
				object obj = null;
				try
				{
					obj = item.Fetch(e);
				}
				catch
				{
				}
				string text = ((!(obj is bool)) ? ((obj is int && (int)obj >= 0) ? ((int)obj).ToString(CultureInfo.InvariantCulture) : null) : (((bool)obj) ? "true" : "false"));
				if (text != null)
				{
					stringBuilder.Append((num++ == 0) ? "\n" : ",\n").Append("    ").Append(J(item.Title))
						.Append(": ")
						.Append(text);
				}
			}
			stringBuilder.Append("\n  }\n}\n");
			File.WriteAllText(path, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			return num;
		}, delegate(object r)
		{
			_host.End();
			_status.Set("ok", L.T("Профиль сохранён: {0} настроек").Replace("{0}", Convert.ToString(r)));
		});
	}

	private void ImportProfile()
	{
		string text;
		using (OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = L.T("Профиль WinWeaver") + " (*.json)|*.json"
		})
		{
			if (openFileDialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			try
			{
				if (new FileInfo(openFileDialog.FileName).Length > 1048576)
				{
					throw new Exception("file too large");
				}
				text = File.ReadAllText(openFileDialog.FileName, Encoding.UTF8);
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
		Dictionary<string, SettingCard> dictionary = new Dictionary<string, SettingCard>(StringComparer.Ordinal);
		foreach (SettingCard item in TrackedCards())
		{
			dictionary[item.Title] = item;
		}
		string input = text.Substring(text.IndexOf("\"settings\"", StringComparison.Ordinal));
		List<KeyValuePair<SettingCard, object>> wanted = new List<KeyValuePair<SettingCard, object>>();
		int skipped = 0;
		foreach (Match item2 in Regex.Matches(input, "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*(true|false|\\d{1,2})"))
		{
			string key = UnJ(item2.Groups[1].Value);
			if (!dictionary.TryGetValue(key, out var value) || (value.IsExpert && !Prefs.Expert))
			{
				skipped++;
				continue;
			}
			string value2 = item2.Groups[2].Value;
			object value3;
			if (value.Seg != null)
			{
				if (!int.TryParse(value2, out var result) || result < 0 || result >= value.Seg.Items.Length)
				{
					skipped++;
					continue;
				}
				value3 = result;
			}
			else
			{
				if (value2 != "true" && value2 != "false")
				{
					skipped++;
					continue;
				}
				value3 = value2 == "true";
			}
			wanted.Add(new KeyValuePair<SettingCard, object>(value, value3));
		}
		if (!_host.TryBegin(quick: true, "Сравнение профиля"))
		{
			return;
		}
		_host.Worker.Run(delegate(IEngine e)
		{
			List<KeyValuePair<SettingCard, object>> list = new List<KeyValuePair<SettingCard, object>>();
			foreach (KeyValuePair<SettingCard, object> item3 in wanted)
			{
				object objA = null;
				try
				{
					objA = item3.Key.Fetch(e);
				}
				catch
				{
				}
				if (!object.Equals(objA, item3.Value))
				{
					list.Add(item3);
				}
			}
			return list;
		}, delegate(object r)
		{
			_host.End(quick: true);
			List<KeyValuePair<SettingCard, object>> diff = (List<KeyValuePair<SettingCard, object>>)r;
			if (diff.Count == 0)
			{
				_status.Set("ok", "Профиль совпадает с текущими настройками — менять нечего");
			}
			else
			{
				CheckDialog.Row[] array = new CheckDialog.Row[diff.Count];
				for (int i = 0; i < array.Length; i++)
				{
					SettingCard key2 = diff[i].Key;
					object value4 = diff[i].Value;
					array[i] = new CheckDialog.Row
					{
						Text = key2.Title,
						Tag = ((key2.Seg != null) ? key2.Seg.Items[(int)value4] : (((bool)value4) ? "включить" : "выключить")),
						Checked = true
					};
				}
				string text2 = L.T("Из профиля будет изменено настроек: {0}. Каждое изменение попадёт в «Историю», и его можно будет отменить.").Replace("{0}", diff.Count.ToString());
				if (skipped > 0)
				{
					text2 = text2 + " " + L.T("Пропущено: {0} (нет в этой версии или нужен экспертный режим).").Replace("{0}", skipped.ToString());
				}
				bool[] pick = CheckDialog.Ask(this, "Применить профиль?", text2, array, "Применить");
				if (pick != null && _host.TryBegin(quick: false, "Импорт профиля"))
				{
					_host.Worker.Run(delegate(IEngine e)
					{
						e.Eval("Start-FGroup 'Импорт профиля настроек'");
						try
						{
							for (int j = 0; j < pick.Length; j++)
							{
								if (pick[j])
								{
									diff[j].Key.SetValue(e, diff[j].Value);
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
						foreach (Page value5 in _pages.Values)
						{
							value5.ForgetData();
						}
						_cur?.Refresh(_host.Worker);
						_status.Set("ok", "Профиль применён");
					});
				}
			}
		});
	}

	private bool ConfirmExpert()
	{
		return WDialog.Ask(this, "Включить экспертный режим?", "Появятся настройки, которые ослабляют защиту или могут сломать программы: полное отключение Defender, выключение VBS/HVCI и защиты от Spectre, «Не спрашивать» в UAC, отключение служб и сжатия памяти, расшифровка BitLocker.\n\nВключай, только если понимаешь, что делает каждая из них. Любое изменение по-прежнему можно отменить в «Истории».", "Включить", "Отмена", DlgIcon.Warn);
	}

	private void ExpertChanged()
	{
		foreach (Page value in _pages.Values)
		{
			value.DoLayoutBlocks();
		}
		_status.Set(Prefs.Expert ? "warn" : "ok", Prefs.Expert ? "Экспертный режим включён — рискованные настройки показаны" : "Экспертный режим выключен — рискованные настройки скрыты");
	}

	public void StartUninstall()
	{
		int num = WDialog.Choose(this, L.T("Удалить {0}?").Replace("{0}", "WinWeaver"), "Будут удалены задачи планировщика, автозапуск, ярлыки, схема питания WinWeaver, папка программы и папка данных (история, журнал).\n\nМожно сначала откатить все изменения из «Истории» — система вернётся к состоянию до WinWeaver (удалённые приложения Windows придётся поставить из Microsoft Store).", "Откатить всё и удалить", "Только удалить", "Отмена", DlgIcon.Warn);
		if (num == 2)
		{
			if (Mode == "/uninstall")
			{
				Close();
			}
		}
		else
		{
			if (!_host.TryBegin(quick: false, L.T("Удаление {0}").Replace("{0}", "WinWeaver")))
			{
				return;
			}
			bool undo = num == 0;
			_host.Worker.Run((IEngine e) => e.As(e.Eval("Uninstall-FWinWeaver $fa1", new Hashtable { { "fa1", undo } }), fallback: false), delegate
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
				WDialog.Info(this, L.T("{0} удалён").Replace("{0}", "WinWeaver"), "Программа и её данные удалены. Чтобы всё вступило в силу, перезагрузи компьютер.\n\nСпасибо, что пользовался!");
				_skipCloseAsk = true;
				Close();
			});
		}
	}

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
			if (WDialog.Ask(this, L.T("{0} установлен").Replace("{0}", "WinWeaver"), "Программа скопирована в Program Files, ярлык добавлен в Пуск. Перезапустить из установленной копии?", "Перезапустить", "Позже"))
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

	private void BuildSettings(Page page)
	{
		page.Add(new AboutCard(_side.Logo, this));
		page.Add(new SectionLabel("Язык"));
		List<string> codes = L.Available();
		List<string> list = new List<string> { "Как в Windows" };
		foreach (string item in codes)
		{
			list.Add(L.NameOf(item));
		}
		page.Add(new SettingCard(_host, "Язык интерфейса", "«Как в Windows» — русский для русской Windows, английский для остальных", delegate
		{
			string lang = Prefs.Lang;
			return (!string.IsNullOrEmpty(lang)) ? (codes.IndexOf(lang) + 1) : 0;
		}, delegate(IEngine e, object v)
		{
			int num2 = (int)v;
			L.Set(L.Resolve(Prefs.Lang = ((num2 <= 0 || num2 > codes.Count) ? "" : codes[num2 - 1])));
		}, list.ToArray()));
		page.Add(new SectionLabel("Запуск"));
		if (Installer.RunningInstalled || _fake)
		{
			page.Add(new SettingCard(_host, "Открывать программу при входе в систему", "Без запроса UAC — через планировщик задач", (IEngine e) => Prefs.AutoChoice == "yes", delegate(IEngine e, object v)
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
			TilesView tilesView = page.Add(new TilesView(_host));
			tilesView.TileH = 124;
			tilesView.AddUi("\ue71d", Installer.IsInstalled ? "Обновить установленную копию" : "Установить в Program Files", "Ярлык в Пуске, автозапуск и удаление через «Установленные приложения»", "Сейчас программа запущена без установки", InstallFromSettings, Installer.IsInstalled ? "Обновить" : "Установить");
		}
		page.Add(new SectionLabel("Интерфейс"));
		page.Add(new SettingCard(_host, "Запоминать последний раздел", "При следующем запуске открыть раздел, на котором вы остановились", e => Prefs.RememberPage, (e, v) => Prefs.RememberPage = (bool)v));
		page.Add(new SettingCard(_host, "Увеличенный текст", "Увеличивает основной текст и подписи. Вступит в силу после перезапуска программы", e => Prefs.LargerText, (e, v) => Prefs.LargerText = (bool)v));
		page.Add(new SettingCard(_host, "Напоминать о перезагрузке при выходе", "Предлагает перезагрузку только после вашего подтверждения", e => Prefs.RebootReminder, (e, v) => Prefs.RebootReminder = (bool)v));
		page.Add(new SettingCard(_host, "Анимации интерфейса", "Плавные переходы, появление карточек и переключателей", (IEngine e) => !Prefs.NoAnim, delegate(IEngine e, object v)
		{
			bool num = (bool)v;
			Prefs.NoAnim = !num;
			Anim.Enabled = num && Environment.GetEnvironmentVariable("WW_NOANIM") != "1";
		}));
		page.Add(new SettingCard(_host, "Скрывать адреса на экране «Сеть»", "IP-адреса и имя Wi-Fi замаскированы — удобно для скриншотов и стримов", (IEngine e) => Prefs.HideAddr, delegate(IEngine e, object v)
		{
			Prefs.HideAddr = (bool)v;
		}));
		page.Add(new SettingCard(_host, "Экспертный режим", "Показывает настройки, которые ослабляют защиту или могут сломать программы", (IEngine e) => Prefs.Expert, delegate(IEngine e, object v)
		{
			bool flag = (bool)v;
			if (!flag || (bool)Invoke(new Func<bool>(ConfirmExpert)))
			{
				Prefs.Expert = flag;
				BeginInvoke(new Action(ExpertChanged));
			}
		}, null, "\ue7ba"));
		page.Add(new SectionLabel("Обновления"));
		page.Add(new SettingCard(_host, "Сообщать о новых версиях", "Раз в день спрашивает GitHub о свежем релизе. Ничего не скачивает и не ставит само", (IEngine e) => Prefs.UpdateCheck, delegate(IEngine e, object v)
		{
			Prefs.UpdateCheck = (bool)v;
		}));
		TilesView tilesView2 = page.Add(new TilesView(_host));
		tilesView2.TileH = 124;
		tilesView2.AddUi("\ue895", "Проверить обновления", L.T("Сейчас установлена версия {0}").Replace("{0}", "1.0.1"), "Откроется страница релиза на GitHub", delegate
		{
			CheckUpdates(manual: true);
		}, "Проверить");
		tilesView2.AddUi("\ue777", "Что вернула Windows", "После крупных обновлений Windows включает часть настроек обратно", "Покажет список и применит заново выбранное", delegate
		{
			CheckDrift(afterUpdate: false);
		}, "Проверить");
		page.Add(new SectionLabel("Профиль настроек"));
		TilesView tilesView3 = page.Add(new TilesView(_host));
		tilesView3.TileH = 124;
		tilesView3.AddUi("\ueda2", "Сохранить профиль", "Текущие переключатели всех разделов в файл .json", "Удобно перенести настройки на другой ПК", ExportProfile, "Сохранить");
		tilesView3.AddUi("\ue8b7", "Загрузить профиль", "Покажет отличия и применит выбранные", "Каждое изменение попадёт в «Историю»", ImportProfile, "Загрузить");
		page.Add(new SectionLabel("Обратная связь"));
		TilesView tilesView4 = page.Add(new TilesView(_host));
		tilesView4.TileH = 124;
		tilesView4.AddUi("\ue7ba", "Сообщить об ошибке", "Форма на GitHub и сведения для разработчика", "Имя пользователя в журнале скрыто", ShowBugReport, "Сообщить");
		tilesView4.AddUi("\ue946", "Предложить идею", "Новая функция или улучшение", "Откроется форма на GitHub", delegate
		{
			Shell.Open("https://github.com/fel7xxx/WinWeaver/issues/new?template=feature_request.yml");
		}, "Предложить");
		tilesView4.AddUi("\ueb51", "Поддержать разработчика", "Boosty, TON или Bitcoin", "Программа бесплатная и останется такой", ShowSupport, "Поддержать");
		page.Add(new SectionLabel("Файлы программы"));
		TilesView tilesView5 = page.Add(new TilesView(_host));
		tilesView5.TileH = 124;
		tilesView5.Add("\ue71d", "Ярлык на Рабочий стол", "Запуск WinWeaver с рабочего стола", "Без запроса UAC", delegate(IEngine e)
		{
			e.Eval("New-FDesktopShortcut");
			return (object)null;
		}, "Создать");
		tilesView5.AddUi("\ue823", "Журнал действий", "Подробный лог всех команд", "Пригодится, если что-то пошло не так", delegate
		{
			Shell.Open("notepad.exe", Brand.LogPath);
		});
		tilesView5.AddUi("\ueda2", "Папка данных", Brand.DataDir, "История изменений и резервные копии", delegate
		{
			Shell.Open("explorer.exe", Brand.DataDir);
		});
		tilesView5.AddUi("\ue74d", L.T("Удалить {0}").Replace("{0}", "WinWeaver"), "Задачи, ярлыки, схема питания и файлы программы", "Можно сначала откатить все изменения", StartUninstall, "Удалить");
	}
}
