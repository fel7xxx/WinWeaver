using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace WinWeaver;

internal class StartupView : Card
{
	public class Item
	{
		public string Id;

		public string Name;

		public string Company;

		public string Desc;

		public string Source;

		public string Command;

		public string Path;

		public bool Exists;

		public bool On;

		public int Advice;

		public string Why;

		public Image Icon;

		public ToggleSwitch Toggle;

		public Rectangle R;

		public Rectangle MoreR;

		public int Delay;
	}

	private static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

	private readonly List<Item> _items = new List<Item>();

	private readonly Segment _filter = new Segment
	{
		Items = new string[4] { "Все", "Включённые", "Выключенные", "Советуем выкл." }
	};

	public readonly List<FButton> Buttons = new List<FButton>();

	private bool _hotMore;

	private bool _loaded;

	private int _hot = -1;

	private Rectangle _headR;

	private Rectangle _statsR;

	private int _listTop;

	private readonly Tween _appear;

	private const string Keep = "securityhealth|defender|msmpeng|realtek|rtkaud|rthdvcpl|nahimic|waves|maxxaudio|dolby|nvidia|nvcontainer|nvbackend|\\bamd\\b|radeon|igfx|intel|synaptics|\\belan|alps|ctfmon|bluetooth|kaspersky|\\beset\\b|egui|avast|\\bavg\\b|bitdefender|drweb|malwarebytes|norton|mcafee|wacom|touchpad|ibmpmsvc|asus.*armoury|lenovo.*vantage|hp.*hotkey|fn.?key|hotkey";

	private const string Off = "discord|steam|epic|spotify|telegram|teams|skype|zoom|origin|eadesktop|\\bea\\b app|battle\\.net|ubisoft|uplay|goggalaxy|gog galaxy|adobe|creative cloud|ccleaner|onedrive|\\bedge\\b|msedge|torrent|viber|whatsapp|yandex|opera|chrome|brave|vivaldi|googleupdate|google update|dropbox|itunes|icloud|jusched|java update|cortana|riotclient|riot client|overwolf|slack|notion|figma|\\bobs\\b|vkplay|vk play|lghub_updater|update\\.exe|updater|autoupdate";

	public List<Item> Items => _items;

	private int RowH => Theme.S(66f);

	public event Action<Item, bool> ItemToggled;

	public event Action<Item, Point> MenuClicked;

	public StartupView()
	{
		_appear = new Tween(this, 0f, 0.12f);
		_filter.Selected = 0;
		_filter.Changed += delegate
		{
			_hot = -1;
			LayoutAgain();
		};
		base.Controls.Add(_filter);
	}

	public FButton AddButton(string text, BtnKind kind, EventHandler click, string glyph = null)
	{
		FButton fButton = new FButton
		{
			Text = text,
			Kind = kind,
			Font = Theme.Body,
			Glyph = glyph
		};
		fButton.AutoWidth(Theme.S(30f));
		fButton.Click += click;
		Buttons.Add(fButton);
		base.Controls.Add(fButton);
		return fButton;
	}

	private static string Str(object o)
	{
		if (o != null)
		{
			return o.ToString();
		}
		return "";
	}

	private static void Classify(Item it)
	{
		string input = (it.Name + " " + it.Path + " " + it.Company + " " + it.Desc).ToLowerInvariant();
		if (Regex.IsMatch(input, "securityhealth|defender|msmpeng|realtek|rtkaud|rthdvcpl|nahimic|waves|maxxaudio|dolby|nvidia|nvcontainer|nvbackend|\\bamd\\b|radeon|igfx|intel|synaptics|\\belan|alps|ctfmon|bluetooth|kaspersky|\\beset\\b|egui|avast|\\bavg\\b|bitdefender|drweb|malwarebytes|norton|mcafee|wacom|touchpad|ibmpmsvc|asus.*armoury|lenovo.*vantage|hp.*hotkey|fn.?key|hotkey"))
		{
			it.Advice = 2;
			it.Why = "Драйвер, звук, тачпад или защита — без неё могут пропасть значки, горячие клавиши или звук";
		}
		else if (Regex.IsMatch(input, "discord|steam|epic|spotify|telegram|teams|skype|zoom|origin|eadesktop|\\bea\\b app|battle\\.net|ubisoft|uplay|goggalaxy|gog galaxy|adobe|creative cloud|ccleaner|onedrive|\\bedge\\b|msedge|torrent|viber|whatsapp|yandex|opera|chrome|brave|vivaldi|googleupdate|google update|dropbox|itunes|icloud|jusched|java update|cortana|riotclient|riot client|overwolf|slack|notion|figma|\\bobs\\b|vkplay|vk play|lghub_updater|update\\.exe|updater|autoupdate"))
		{
			it.Advice = 1;
			it.Why = "Не нужна сразу после входа — откроешь, когда понадобится. Windows загрузится быстрее";
		}
		if (!it.Exists && !string.IsNullOrEmpty(it.Path))
		{
			it.Advice = 1;
			it.Why = "Файл программы не найден — запись, скорее всего, осталась от удалённой программы";
		}
	}

	private static Image LoadIcon(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}
		lock (IconCache)
		{
			if (IconCache.TryGetValue(path, out var value))
			{
				return value;
			}
			value = null;
			try
			{
				if (File.Exists(path))
				{
					using Icon icon = Icon.ExtractAssociatedIcon(path);
					if (icon != null)
					{
						value = icon.ToBitmap();
					}
				}
			}
			catch
			{
				value = null;
			}
			IconCache[path] = value;
			return value;
		}
	}

	public override object Fetch(IEngine e)
	{
		object obj = e.Eval("Get-FStartup");
		List<Item> list = new List<Item>();
		object[] array = obj as object[];
		if (array == null && obj is Hashtable)
		{
			array = new object[1] { obj };
		}
		if (array != null)
		{
			object[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] is Hashtable hashtable)
				{
					Item item = new Item
					{
						Id = Str(hashtable["Id"]),
						Name = Str(hashtable["Name"]),
						Company = Str(hashtable["Company"]),
						Desc = Str(hashtable["Desc"]),
						Source = Str(hashtable["Source"]),
						Command = Str(hashtable["Command"]),
						Path = Str(hashtable["Path"]),
						Exists = (!(hashtable["Exists"] is bool) || (bool)hashtable["Exists"]),
						On = (hashtable["Enabled"] is bool && (bool)hashtable["Enabled"])
					};
					try
					{
						item.Delay = ((hashtable["Delay"] != null) ? Convert.ToInt32(hashtable["Delay"]) : 0);
					}
					catch
					{
						item.Delay = 0;
					}
					if (item.Name == "")
					{
						item.Name = item.Id;
					}
					Classify(item);
					item.Icon = (item.Exists ? LoadIcon(item.Path) : null);
					list.Add(item);
				}
			}
		}
		list.Sort((Item a, Item b) => (a.On != b.On) ? ((!a.On) ? 1 : (-1)) : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
		return list;
	}

	public override void Apply(object v)
	{
		if (!(v is List<Item> list))
		{
			return;
		}
		Dictionary<string, ToggleSwitch> dictionary = new Dictionary<string, ToggleSwitch>();
		foreach (Item item2 in _items)
		{
			if (item2.Toggle != null && !dictionary.ContainsKey(item2.Id))
			{
				dictionary[item2.Id] = item2.Toggle;
			}
		}
		_items.Clear();
		foreach (Item item3 in list)
		{
			if (dictionary.TryGetValue(item3.Id, out var value))
			{
				dictionary.Remove(item3.Id);
				value.Tag = item3;
				if (value.On != item3.On)
				{
					value.On = item3.On;
				}
			}
			else
			{
				value = new ToggleSwitch();
				value.Tag = item3;
				value.SetOnNoAnim(item3.On);
				ToggleSwitch t0 = value;
				value.Toggled += delegate
				{
					if (t0.Tag is Item item)
					{
						item.On = !item.On;
						t0.On = item.On;
						Invalidate();
						this.ItemToggled?.Invoke(item, item.On);
					}
				};
				base.Controls.Add(value);
			}
			item3.Toggle = value;
			_items.Add(item3);
		}
		foreach (ToggleSwitch value2 in dictionary.Values)
		{
			base.Controls.Remove(value2);
			value2.Dispose();
		}
		if (!_loaded)
		{
			_loaded = true;
			Skeleton.Track(this, loading: false);
			_appear.Target = 1f;
		}
		LayoutAgain();
	}

	public void Revert(Item it)
	{
		it.On = !it.On;
		it.Toggle.On = it.On;
		Invalidate();
	}

	private void LayoutAgain()
	{
		if (base.Parent is Canvas)
		{
			((Canvas)base.Parent).Page.RelayoutSoon();
		}
		Invalidate();
	}

	private bool Shown(Item it)
	{
		switch (_filter.Selected)
		{
		case 1:
			return it.On;
		case 2:
			return !it.On;
		case 3:
			if (it.Advice == 1)
			{
				return it.On;
			}
			return false;
		default:
			return true;
		}
	}

	private int Count(Func<Item, bool> f)
	{
		int num = 0;
		foreach (Item item in _items)
		{
			if (f(item))
			{
				num++;
			}
		}
		return num;
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		int num = pad;
		int val = Theme.Measure("Ag", Theme.Title).Height + Theme.S(4f) + Theme.Measure("Ag", Theme.Small).Height;
		int num2 = w - pad;
		int num3 = 0;
		foreach (FButton button in Buttons)
		{
			num3 += button.Width + Theme.S(8f);
		}
		bool num4 = w - pad * 2 - Theme.S(56f) - Theme.S(330f) < num3;
		_headR = new Rectangle(pad, num, w - pad * 2, Math.Max(Theme.S(44f), val));
		int num5 = _headR.Bottom + Theme.S(16f);
		if (!num4)
		{
			for (int num6 = Buttons.Count - 1; num6 >= 0; num6--)
			{
				FButton fButton = Buttons[num6];
				num2 -= fButton.Width;
				fButton.Location = new Point(num2, num + (_headR.Height - fButton.Height) / 2);
				num2 -= Theme.S(8f);
			}
		}
		else
		{
			int num7 = pad;
			foreach (FButton button2 in Buttons)
			{
				button2.Location = new Point(num7, num5);
				num7 += button2.Width + Theme.S(8f);
			}
			num5 += Buttons[0].Height + Theme.S(16f);
		}
		_statsR = new Rectangle(pad, num5, w - pad * 2, Theme.S(74f));
		num5 = _statsR.Bottom + Theme.S(16f);
		int width = Math.Min(w - pad * 2, Theme.S(560f));
		_filter.SetBounds(pad, num5, width, _filter.Height);
		int num8 = (_listTop = num5 + (_filter.Height + Theme.S(12f)));
		int num9 = 0;
		foreach (Item item in _items)
		{
			bool flag = Shown(item);
			if (item.Toggle.Visible != flag)
			{
				item.Toggle.Visible = flag;
			}
			if (!flag)
			{
				item.R = Rectangle.Empty;
				continue;
			}
			item.R = new Rectangle(pad - Theme.S(8f), num8, w - pad * 2 + Theme.S(16f), RowH);
			item.Toggle.Location = new Point(w - pad - item.Toggle.Width, num8 + (RowH - item.Toggle.Height) / 2);
			int num10 = Theme.S(32f);
			item.MoreR = new Rectangle(item.Toggle.Left - Theme.S(14f) - num10, num8 + (RowH - num10) / 2, num10, num10);
			num8 += RowH;
			num9++;
		}
		if (!_loaded)
		{
			num8 += RowH * 5;
		}
		else if (num9 == 0)
		{
			num8 += Theme.S(70f);
		}
		int num11 = num8 + pad - Theme.S(4f);
		SetBounds(x, y, w, num11);
		return num11;
	}

	private static Color Tint(string name)
	{
		Color[] array = new Color[6]
		{
			Color.FromArgb(96, 112, 150),
			Color.FromArgb(120, 98, 150),
			Color.FromArgb(80, 130, 128),
			Color.FromArgb(150, 110, 90),
			Color.FromArgb(110, 130, 90),
			Color.FromArgb(140, 92, 110)
		};
		int num = 0;
		string text = name ?? "";
		foreach (char c in text)
		{
			num = num * 31 + c;
		}
		return array[Math.Abs(num) % array.Length];
	}

	private void DrawAppIcon(Graphics g, Item it, Rectangle r)
	{
		Theme.Smooth(g);
		using (GraphicsPath path = Theme.Round(r, (float)r.Width * 0.28f))
		{
			Color a = Tint(it.Name);
			Theme.FillGradient(g, path, r, Theme.Mix(a, Color.FromArgb(70, 70, 80), 0.35f), Theme.Mix(a, Color.FromArgb(30, 30, 36), 0.6f));
			using (Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255)))
			{
				g.DrawPath(pen, path);
			}
			if (it.Icon != null)
			{
				int num = r.Width - Theme.S(10f);
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(it.Icon, new Rectangle(r.X + (r.Width - num) / 2, r.Y + (r.Height - num) / 2, num, num));
				return;
			}
		}
		string text = (string.IsNullOrEmpty(it.Name) ? "?" : it.Name.Substring(0, 1).ToUpperInvariant());
		using Font font = Theme.F(13f, semi: true);
		TextRenderer.DrawText(g, text, font, r, Color.FromArgb(236, 236, 240), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	private void Stat(Graphics g, Rectangle r, string value, string label, Color c, string hint)
	{
		Theme.Smooth(g);
		using (GraphicsPath path = Theme.Round(r, Theme.S(12f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(34, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		int num = Theme.S(3f);
		using (GraphicsPath path2 = Theme.Round(new RectangleF(r.X + Theme.S(14f), r.Y + Theme.S(16f), num, r.Height - Theme.S(32f)), (float)num / 2f))
		{
			using SolidBrush brush2 = new SolidBrush(c);
			g.FillPath(brush2, path2);
		}
		int num2 = r.X + Theme.S(28f);
		using (Font font = Theme.F(19f, semi: true))
		{
			Size size = TextRenderer.MeasureText(g, value, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, value, font, new Point(num2, r.Y + Theme.S(12f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, label, Theme.Small, new Rectangle(num2 + size.Width + Theme.S(8f), r.Y + Theme.S(12f), r.Right - num2 - size.Width - Theme.S(12f), size.Height), Theme.Text, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(num2, r.Bottom - Theme.S(26f), r.Right - num2 - Theme.S(10f), Theme.S(18f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
	}

	protected override void PaintContent(Graphics g)
	{
		int pad = base.Pad;
		Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - Theme.S(44f)) / 2, Theme.S(44f), Theme.S(44f)), "\ue945");
		int num = _headR.X + Theme.S(58f);
		int num2 = ((Buttons.Count > 0 && Buttons[0].Top < _headR.Bottom) ? (Buttons[0].Left - Theme.S(12f)) : _headR.Right);
		int height = Theme.Measure("Ag", Theme.Title).Height;
		int num3 = _headR.Y + (_headR.Height - height - Theme.S(4f) - Theme.Measure("Ag", Theme.Small).Height) / 2;
		TextRenderer.DrawText(g, "Программы при входе в Windows", Theme.Title, new Rectangle(num, num3, num2 - num, height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, "Выключение не удаляет программу. В меню «•••» — отложенный запуск и удаление записи", Theme.Small, new Rectangle(num, num3 + height + Theme.S(4f), num2 - num, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		int count = _items.Count;
		int num4 = Count((Item i) => i.On);
		int num5 = Count((Item i) => i.On && i.Advice == 1);
		int num6 = Theme.S(10f);
		int num7 = (_statsR.Width - num6 * 2) / 3;
		string text = ((num4 <= 3) ? "быстрый вход" : ((num4 <= 7) ? "средняя нагрузка" : "медленный вход"));
		Stat(g, new Rectangle(_statsR.X, _statsR.Y, num7, _statsR.Height), _loaded ? count.ToString() : "…", "в списке", Theme.Silver2, "программ, которые могут стартовать сами");
		Stat(g, new Rectangle(_statsR.X + num7 + num6, _statsR.Y, num7, _statsR.Height), _loaded ? num4.ToString() : "…", "включено", (num4 <= 3) ? Theme.Green : ((num4 <= 7) ? Theme.Silver1 : Theme.Yellow), _loaded ? ("при входе: " + text) : "");
		Stat(g, new Rectangle(_statsR.X + (num7 + num6) * 2, _statsR.Y, _statsR.Width - (num7 + num6) * 2, _statsR.Height), _loaded ? num5.ToString() : "…", "можно выключить", (num5 == 0) ? Theme.Green : Theme.Yellow, (num5 == 0) ? "лишнего в автозагрузке нет" : "фильтр «Советуем выкл.» покажет их");
		int num8 = 0;
		for (int j = 0; j < _items.Count; j++)
		{
			Item item = _items[j];
			if (item.R.IsEmpty)
			{
				continue;
			}
			Rectangle r = item.R;
			if (j == _hot)
			{
				using GraphicsPath path = Theme.Round(r, Theme.S(10f));
				using SolidBrush brush = new SolidBrush(Color.FromArgb(14, 255, 255, 255));
				g.FillPath(brush, path);
			}
			else if (num8 > 0)
			{
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
				g.FillRectangle(brush2, r.X + Theme.S(60f), r.Y, r.Width - Theme.S(68f), 1);
			}
			num8++;
			int num9 = Theme.S(38f);
			Rectangle r2 = new Rectangle(r.X + Theme.S(8f), r.Y + (r.Height - num9) / 2, num9, num9);
			DrawAppIcon(g, item, r2);
			int num10 = r2.Right + Theme.S(14f);
			int num11 = item.MoreR.Left - Theme.S(12f);
			InstalledView.Btn(g, item.MoreR, null, "\ue712", j == _hot && _hotMore, danger: false);
			int num12 = Math.Min(Theme.Measure(item.Name, Theme.Title).Width + 2, Math.Max(Theme.S(60f), num11 - num10));
			int num13 = r.Y + Theme.S(12f);
			Color fore = (item.On ? Theme.Text : Theme.Sub);
			TextRenderer.DrawText(g, item.Name, Theme.Title, new Rectangle(num10, num13, num12, Theme.S(22f)), fore, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			int num14 = num10 + num12 + Theme.S(10f);
			string text2 = null;
			Color c = Theme.Sub;
			if (!item.Exists && !string.IsNullOrEmpty(item.Path))
			{
				text2 = "Файл не найден";
				c = Theme.Red;
			}
			else if (item.Advice == 1)
			{
				text2 = (item.On ? "Можно выключить" : "Выключено — так и надо");
				c = (item.On ? Theme.Yellow : Theme.Green);
			}
			else if (item.Advice == 2)
			{
				text2 = (item.On ? "Лучше оставить" : "Лучше включить");
				c = (item.On ? Theme.Green : Theme.Yellow);
			}
			if (text2 != null)
			{
				int num15 = Theme.PillW(text2, Theme.Tiny);
				if (num14 + num15 <= num11)
				{
					Theme.Pill(g, new Rectangle(num14, num13, num15, Theme.S(20f)), text2, Theme.Tiny, c, 24);
					num14 += num15 + Theme.S(6f);
				}
			}
			if (item.Delay > 0 && item.On)
			{
				string text3 = "через " + ((item.Delay % 60 == 0) ? (item.Delay / 60 + " мин") : (item.Delay + " с"));
				int num16 = Theme.PillW(text3, Theme.Tiny);
				if (num14 + num16 <= num11)
				{
					Theme.Pill(g, new Rectangle(num14, num13, num16, Theme.S(20f)), text3, Theme.Tiny, Theme.Silver2, 18);
				}
			}
			string text4 = ((item.Company != "") ? item.Company : ((item.Desc != "") ? item.Desc : "Издатель неизвестен")) + "   •   " + item.Source;
			string text5 = ((item.Path != "") ? Path.GetFileName(item.Path) : item.Command);
			if (!string.IsNullOrEmpty(text5))
			{
				text4 = text4 + "   •   " + text5;
			}
			if (j == _hot && !string.IsNullOrEmpty(item.Why))
			{
				text4 = item.Why;
			}
			TextRenderer.DrawText(g, text4, Theme.Small, new Rectangle(num10, num13 + Theme.S(25f), num11 - num10, Theme.S(20f)), (j == _hot && item.Why != null) ? Theme.Mix(Theme.Sub, Theme.Text, 0.4f) : Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		if (!_loaded)
		{
			for (int k = 0; k < 5; k++)
			{
				Skeleton.Row(g, new Rectangle(pad - Theme.S(8f), _listTop + k * RowH, base.Width - pad * 2, RowH), this);
			}
		}
		if (num8 == 0 && _loaded)
		{
			Rectangle r3 = new Rectangle(pad, _listTop + Theme.S(10f), base.Width - pad * 2, Theme.S(50f));
			string text6 = ((_items.Count == 0) ? "Автозагрузка пуста — Windows стартует максимально быстро" : ((_filter.Selected == 3) ? "Отлично: лишних программ в автозагрузке нет" : "Здесь пусто — выбери другой фильтр"));
			TextRenderer.DrawText(g, text6, Theme.Body, r3, Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		for (int i = 0; i < _items.Count; i++)
		{
			if (!_items[i].R.IsEmpty && _items[i].R.Contains(e.Location))
			{
				num = i;
			}
		}
		bool flag = num >= 0 && _items[num].MoreR.Contains(e.Location);
		Cursor = (flag ? Cursors.Hand : Cursors.Default);
		if (num != _hot || flag != _hotMore)
		{
			_hot = num;
			_hotMore = flag;
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		foreach (Item item in _items)
		{
			if (!item.R.IsEmpty && item.R.Contains(e.Location))
			{
				if (item.MoreR.Contains(e.Location) || e.Button == MouseButtons.Right)
				{
					Point p = (item.MoreR.Contains(e.Location) ? new Point(item.MoreR.Left, item.MoreR.Bottom + Theme.S(4f)) : e.Location);
					this.MenuClicked?.Invoke(item, PointToScreen(p));
				}
				break;
			}
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (_hot != -1)
		{
			_hot = -1;
			Invalidate();
		}
		base.OnMouseLeave(e);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Skeleton.Track(this, !_loaded);
	}
}
