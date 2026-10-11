using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class PersonalizeDialog : PanelDialog
{
	private readonly TileChoice _walls;

	private readonly FCheck _wallOn;

	private readonly FCheck _nameOn;

	private readonly FField _dev;

	private readonly FField _pw1;

	private readonly FField _pw2;

	private readonly string _baseOpts;

	private readonly Action<string, Action<string[]>> _fetchPlan;

	private int _labWall;

	private int _labPc;

	private int _labPw;

	private int _noteY;

	private string _error;

	private List<int> _skip = new List<int>();

	private int _planCount;

	public string Options
	{
		get
		{
			string[] array = _baseOpts.Split(new char[1] { ';' }, 2);
			List<string> list = new List<string>();
			string[] array2 = array[0].Split(',');
			foreach (string text in array2)
			{
				if (text.Trim().Length > 0)
				{
					list.Add(text.Trim());
				}
			}
			if (!_wallOn.Checked)
			{
				list.Add("nowall");
			}
			if (!_nameOn.Checked)
			{
				list.Add("noname");
			}
			string[] obj = new string[5]
			{
				string.Join(",", list.ToArray()),
				";",
				(array.Length > 1) ? array[1] : "def=0",
				";wp=",
				null
			};
			int j = _walls.Selected;
			obj[4] = j.ToString();
			string text2 = string.Concat(obj);
			if (_skip.Count > 0)
			{
				text2 = text2 + ";skip=" + string.Join(".", _skip.ConvertAll((int i) => i.ToString()).ToArray());
			}
			return text2;
		}
	}

	public string DeviceName
	{
		get
		{
			string text = _dev.Value.Trim();
			if (!string.Equals(text, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
			return "";
		}
	}

	public string Password => _pw1.Value;

	private PersonalizeDialog(string levelTitle, string baseOpts, Action<string, Action<string[]>> fetchPlan)
		: base("Стиль WeavOS и компьютер", "Шаг 2 из 2 • «" + levelTitle + "». Всё на этой странице — по желанию: сними галочки, если хочешь оставить как есть.", "Начать настройку", "Отмена", DlgIcon.Question, 760f)
	{
		_baseOpts = baseOpts ?? "";
		_fetchPlan = fetchPlan;
		int num = 0;
		int innerW = base.InnerW;
		_labWall = num;
		_wallOn = new FCheck("Поставить обои WeavOS на рабочий стол и экран блокировки");
		_wallOn.Location = new Point(base.M, num + Theme.S(20f));
		_wallOn.Click += delegate
		{
			_walls.Enabled = _wallOn.Checked;
			_walls.Invalidate();
			ResetSkip();
		};
		Content.Controls.Add(_wallOn);
		num = _wallOn.Bottom + Theme.S(8f);
		_walls = new TileChoice
		{
			PictureH = Theme.S(84f)
		};
		for (int i = 0; i < Walls.Count; i++)
		{
			_walls.Add(Walls.Names[i], "4K • 3840 × 2160", Walls.Load(i, 480));
		}
		_walls.Selected = Walls.Clamp(Prefs.LastWallpaper);
		_walls.SetBounds(base.M, num, innerW, _walls.Measure(innerW));
		Content.Controls.Add(_walls);
		num = _walls.Bottom + Theme.S(10f);
		_nameOn = new FCheck("Показывать «WeavOS» как название системы в «О компьютере»");
		_nameOn.Location = new Point(base.M, num);
		_nameOn.Click += delegate
		{
			ResetSkip();
		};
		Content.Controls.Add(_nameOn);
		num = (_labPc = _nameOn.Bottom + Theme.S(18f)) + Theme.S(22f);
		_dev = new FField("Имя компьютера", password: false, 15)
		{
			Value = ((Environment.GetEnvironmentVariable("WW_SHOT") != null) ? "WEAVOS-PC" : Environment.MachineName)
		};
		_dev.SetBounds(base.M, num, innerW, Theme.S(40f));
		Content.Controls.Add(_dev);
		num = (_labPw = _dev.Bottom + Theme.S(18f)) + Theme.S(22f);
		int num2 = (innerW - Theme.S(10f)) / 2;
		_pw1 = new FField("Новый пароль", password: true, 127);
		_pw1.SetBounds(base.M, num, num2, Theme.S(40f));
		_pw2 = new FField("Повтори пароль", password: true, 127);
		_pw2.SetBounds(base.M + num2 + Theme.S(10f), num, innerW - num2 - Theme.S(10f), Theme.S(40f));
		Content.Controls.Add(_pw1);
		Content.Controls.Add(_pw2);
		num = (_noteY = _pw1.Bottom + Theme.S(10f)) + Theme.S(36f);
		if (_fetchPlan != null)
		{
			AddExtra("Выбрать шаги…", delegate
			{
				PickSteps();
			});
		}
		Finish(num);
	}

	private void ResetSkip()
	{
		if (_skip.Count > 0)
		{
			_skip.Clear();
			if (Extra != null)
			{
				Extra.Text = "Выбрать шаги…";
			}
		}
	}

	private void PickSteps()
	{
		if (Extra != null)
		{
			Extra.Enabled = false;
		}
		string text = Options;
		int num = text.IndexOf(";skip=", StringComparison.Ordinal);
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		_fetchPlan(text, delegate(string[] names)
		{
			if (Extra != null)
			{
				Extra.Enabled = true;
			}
			if (names != null && names.Length != 0 && !base.IsDisposed)
			{
				CheckDialog.Row[] array = new CheckDialog.Row[names.Length];
				for (int i = 0; i < names.Length; i++)
				{
					array[i] = new CheckDialog.Row
					{
						Text = names[i],
						Checked = !_skip.Contains(i)
					};
				}
				bool[] array2 = CheckDialog.Ask(this, "Шаги автонастройки", "Выполнятся по порядку сверху вниз. Сними галочку с того, что делать не нужно — остальное пройдёт как обычно.", array, "Готово");
				if (array2 != null)
				{
					_skip = new List<int>();
					for (int j = 0; j < array2.Length; j++)
					{
						if (!array2[j])
						{
							_skip.Add(j);
						}
					}
					_planCount = names.Length;
					if (Extra != null)
					{
						Extra.Text = ((_skip.Count == 0) ? "Выбрать шаги…" : L.T("Шаги: {0} из {1}").Replace("{0}", (names.Length - _skip.Count).ToString()).Replace("{1}", names.Length.ToString()));
						Extra.AutoWidth(Theme.S(30f));
						Extra.Width = Math.Max(Theme.S(112f), Extra.Width);
					}
				}
			}
		});
	}

	protected override bool Validate()
	{
		_error = null;
		string text = _dev.Value.Trim();
		if (text.Length > 0 && !string.Equals(text, Environment.MachineName, StringComparison.OrdinalIgnoreCase) && !ValidName(text))
		{
			_error = "Имя компьютера: до 15 символов — латинские буквы, цифры и дефис, не только цифры";
		}
		else if (_pw1.Value != _pw2.Value)
		{
			_error = "Пароли не совпадают";
		}
		if (_error != null)
		{
			Content.Invalidate();
			return false;
		}
		Prefs.LastWallpaper = _walls.Selected;
		return true;
	}

	public static bool ValidName(string n)
	{
		if (n.Length < 1 || n.Length > 15 || n.StartsWith("-") || n.EndsWith("-"))
		{
			return false;
		}
		bool result = false;
		foreach (char c in n)
		{
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '-')
			{
				return false;
			}
			if (c < '0' || c > '9')
			{
				result = true;
			}
		}
		return result;
	}

	protected override void PaintContent(Graphics g)
	{
		Label(g, "Обои", _labWall);
		Label(g, "Имя компьютера", _labPc, "латиница, цифры и дефис, до 15 символов • вступит в силу после перезагрузки");
		Label(g, "Пароль для входа в Windows", _labPw, "необязательно • оставь пустым, чтобы не менять");
		string text = _error ?? "Пароль меняется только у локальной учётной записи (у учётной записи Microsoft — на сайте Microsoft). Программа его не сохраняет и не пишет в журнал.";
		TextRenderer.DrawText(g, text, Theme.Tiny, new Rectangle(base.M, _noteY, base.InnerW, Theme.S(34f)), (_error != null) ? Theme.Red : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
	}

	public static bool Ask(IWin32Window owner, string levelTitle, ref string opts, out string dev, out string pw, Action<string, Action<string[]>> fetchPlan)
	{
		dev = "";
		pw = "";
		using PersonalizeDialog personalizeDialog = new PersonalizeDialog(levelTitle, opts, fetchPlan);
		if (personalizeDialog.ShowFor(owner) != DialogResult.Yes)
		{
			return false;
		}
		opts = personalizeDialog.Options;
		dev = personalizeDialog.DeviceName;
		pw = personalizeDialog.Password;
		return true;
	}
}
