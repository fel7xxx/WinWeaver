using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace WinWeaver;

internal class InstalledView : Card
{
	public class Item
	{
		public string Id;

		public string Name;

		public string Pub;

		public string Ver;

		public double Size;

		public string Date;

		public string Loc;

		public string IconPath;

		public bool Store;

		public Image Icon;

		public Rectangle R;

		public Rectangle DelR;

		public Rectangle DirR;

		public bool Busy;
	}

	private static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

	public readonly List<Item> Items = new List<Item>();

	private readonly List<Item> _shown = new List<Item>();

	private readonly TextBox _search = new TextBox();

	private readonly Segment _filter = new Segment
	{
		Items = new string[3] { "Все", "Программы", "Microsoft Store" }
	};

	private readonly string[] _sorts = new string[3] { "по имени", "по размеру", "по дате" };

	private int _sort;

	private int _limit = 80;

	private bool _loaded;

	private int _hot = -1;

	private int _hotBtn;

	private Rectangle _headR;

	private Rectangle _statsR;

	private Rectangle _searchR;

	private Rectangle _sortR;

	private Rectangle _moreR;

	private int _listTop;

	private readonly FButton _refresh;

	private int RowH => Theme.S(64f);

	public event Action<Item> UninstallClicked;

	public event Action RefreshClicked;


	public InstalledView()
	{
		_search.BorderStyle = BorderStyle.None;
		_search.BackColor = Color.FromArgb(30, 30, 35);
		_search.ForeColor = Theme.Text;
		_search.Font = Theme.Body;
		_search.TextChanged += delegate
		{
			_limit = 80;
			_hot = -1;
			LayoutAgain();
		};
		_search.GotFocus += delegate
		{
			Invalidate();
		};
		_search.LostFocus += delegate
		{
			Invalidate();
		};
		_search.HandleCreated += delegate
		{
			try
			{
				_search.PlaceholderText = "Найти программу или издателя";
			}
			catch
			{
			}
		};
		_filter.Selected = 0;
		_filter.Changed += delegate
		{
			_limit = 80;
			_hot = -1;
			LayoutAgain();
		};
		_refresh = new FButton
		{
			Text = "Обновить",
			Kind = BtnKind.Ghost,
			Font = Theme.Body,
			Glyph = "\ue72c"
		};
		_refresh.AutoWidth(Theme.S(30f));
		_refresh.Click += delegate
		{
			this.RefreshClicked?.Invoke();
		};
		base.Controls.Add(_search);
		base.Controls.Add(_filter);
		base.Controls.Add(_refresh);
	}

	private static string Str(object o)
	{
		if (o != null)
		{
			return o.ToString();
		}
		return "";
	}

	private static double D(object o)
	{
		try
		{
			return (o == null) ? 0.0 : Convert.ToDouble(o, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0.0;
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
					switch (Path.GetExtension(path).ToLowerInvariant())
					{
					case ".ico":
					{
						using (Icon icon2 = new Icon(path, 32, 32))
						{
							value = icon2.ToBitmap();
						}
						break;
					}
					case ".png":
					case ".jpg":
					case ".bmp":
					{
						using (Image original = Image.FromFile(path))
						{
							value = new Bitmap(original, 32, 32);
						}
						break;
					}
					default:
					{
						using (Icon icon = Icon.ExtractAssociatedIcon(path))
						{
							if (icon != null)
							{
								value = icon.ToBitmap();
							}
						}
						break;
					}
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
		object obj = e.Eval("Get-FInstalled");
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
						Pub = Str(hashtable["Pub"]),
						Ver = Str(hashtable["Ver"]),
						Size = D(hashtable["Size"]),
						Date = Str(hashtable["Date"]),
						Loc = Str(hashtable["Loc"]),
						IconPath = Str(hashtable["Icon"]),
						Store = (Str(hashtable["Kind"]) == "store")
					};
					if (!(item.Name == ""))
					{
						item.Icon = LoadIcon(item.IconPath);
						list.Add(item);
					}
				}
			}
		}
		return list;
	}

	public override void Apply(object v)
	{
		if (v is List<Item> collection)
		{
			Items.Clear();
			Items.AddRange(collection);
			_loaded = true;
			Skeleton.Track(this, loading: false);
			_hot = -1;
			LayoutAgain();
		}
	}

	public void SetBusy(Item it, bool busy)
	{
		it.Busy = busy;
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

	private void Rebuild()
	{
		_shown.Clear();
		string text = _search.Text.Trim();
		foreach (Item item in Items)
		{
			if ((_filter.Selected != 1 || !item.Store) && (_filter.Selected != 2 || item.Store) && (!(text != "") || item.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || item.Pub.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0))
			{
				_shown.Add(item);
			}
		}
		_shown.Sort(delegate(Item a, Item b)
		{
			int num = 0;
			if (_sort == 1)
			{
				num = b.Size.CompareTo(a.Size);
			}
			else if (_sort == 2)
			{
				num = string.CompareOrdinal(b.Date, a.Date);
			}
			return (num == 0) ? string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase) : num;
		});
	}

	public override int DoLayout(int x, int y, int w)
	{
		Rebuild();
		int pad = base.Pad;
		int val = Theme.Measure("Ag", Theme.Title).Height + Theme.S(4f) + Theme.Measure("Ag", Theme.Small).Height;
		_headR = new Rectangle(pad, pad, w - pad * 2, Math.Max(Theme.S(44f), val));
		_refresh.Location = new Point(w - pad - _refresh.Width, _headR.Y + (_headR.Height - _refresh.Height) / 2);
		int y2 = _headR.Bottom + Theme.S(16f);
		_statsR = new Rectangle(pad, y2, w - pad * 2, Theme.S(74f));
		y2 = _statsR.Bottom + Theme.S(16f);
		int height = _filter.Height;
		int num = Math.Min(Theme.S(420f), (w - pad * 2) / 2);
		int num2 = w - pad * 2 - num - Theme.S(12f);
		bool num3 = num2 < Theme.S(220f);
		if (num3)
		{
			num2 = w - pad * 2;
		}
		_searchR = new Rectangle(pad, y2, num2, height);
		int preferredHeight = _search.PreferredHeight;
		_search.SetBounds(_searchR.X + Theme.S(38f), _searchR.Y + (height - preferredHeight) / 2, _searchR.Width - Theme.S(52f), preferredHeight);
		if (num3)
		{
			y2 += height + Theme.S(10f);
			_filter.SetBounds(pad, y2, w - pad * 2, height);
		}
		else
		{
			_filter.SetBounds(w - pad - num, y2, num, height);
		}
		y2 += height + Theme.S(14f);
		int height2 = Theme.Measure("Ag", Theme.Small).Height;
		_sortR = new Rectangle(pad, y2, w - pad * 2, height2 + Theme.S(8f));
		int num4 = (_listTop = _sortR.Bottom + Theme.S(6f));
		foreach (Item item2 in Items)
		{
			item2.R = Rectangle.Empty;
		}
		int num5 = Math.Min(_limit, _shown.Count);
		int num6 = Theme.S(30f);
		int num7 = Theme.Measure("Удалить", Theme.Small).Width + Theme.S(34f);
		int num8 = num6 + Theme.S(4f);
		for (int i = 0; i < num5; i++)
		{
			Item item = _shown[i];
			item.R = new Rectangle(pad - Theme.S(8f), num4, w - pad * 2 + Theme.S(16f), RowH);
			int y3 = num4 + (RowH - num6) / 2;
			item.DelR = new Rectangle(w - pad - num7, y3, num7, num6);
			item.DirR = ((item.Loc != "" && !item.Store) ? new Rectangle(item.DelR.X - Theme.S(8f) - num8, y3, num8, num6) : Rectangle.Empty);
			num4 += RowH;
		}
		_moreR = Rectangle.Empty;
		if (_shown.Count > num5)
		{
			_moreR = new Rectangle(pad, num4 + Theme.S(8f), w - pad * 2, Theme.S(40f));
			num4 = _moreR.Bottom;
		}
		if (!_loaded)
		{
			num4 += RowH * 6;
		}
		else if (_shown.Count == 0)
		{
			num4 += Theme.S(70f);
		}
		int num9 = num4 + pad - Theme.S(4f);
		SetBounds(x, y, w, num9);
		return num9;
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
			using Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		if (it.Icon != null)
		{
			int num = r.Width - Theme.S(10f);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.DrawImage(it.Icon, new Rectangle(r.X + (r.Width - num) / 2, r.Y + (r.Height - num) / 2, num, num));
			return;
		}
		string text = it.Name.Substring(0, 1).ToUpperInvariant();
		using Font font = Theme.F(13f, semi: true);
		TextRenderer.DrawText(g, text, font, r, Color.FromArgb(236, 236, 240), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	internal static void Stat(Graphics g, Rectangle r, string value, string label, Color c, string hint)
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

	internal static void Btn(Graphics g, Rectangle r, string text, string glyph, bool hot, bool danger)
	{
		Theme.Smooth(g);
		Color color = (danger ? Theme.Red : Theme.Silver2);
		using (GraphicsPath path = Theme.Round(r, (float)r.Height / 2f))
		{
			using (SolidBrush brush = new SolidBrush(hot ? Color.FromArgb(danger ? 46 : 30, danger ? color : Color.White) : Color.FromArgb(12, 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(hot ? Color.FromArgb(140, color) : Color.FromArgb(40, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		Color color2 = ((!hot) ? Theme.Sub : (danger ? Theme.Mix(color, Color.White, 0.25f) : Theme.Text));
		if (text == null)
		{
			Theme.DrawIcon(g, glyph, Theme.Icon, r, color2);
		}
		else
		{
			TextRenderer.DrawText(g, text, Theme.Small, r, color2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	internal static string Plural(int n, string one, string few, string many)
	{
		if (!L.IsRu)
		{
			if (n != 1)
			{
				return many;
			}
			return one;
		}
		int num = n % 10;
		int num2 = n % 100;
		if (num == 1 && num2 != 11)
		{
			return one;
		}
		if (num >= 2 && num <= 4 && (num2 < 12 || num2 > 14))
		{
			return few;
		}
		return many;
	}

	private static string FmtDate(string d)
	{
		if (DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return result.ToString("d MMM yyyy", L.Culture);
		}
		return d;
	}

	protected override void PaintContent(Graphics g)
	{
		int pad = base.Pad;
		Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - Theme.S(44f)) / 2, Theme.S(44f), Theme.S(44f)), "\ue71d");
		int num = _headR.X + Theme.S(58f);
		int num2 = _refresh.Left - Theme.S(12f);
		int height = Theme.Measure("Ag", Theme.Title).Height;
		int num3 = _headR.Y + (_headR.Height - height - Theme.S(4f) - Theme.Measure("Ag", Theme.Small).Height) / 2;
		TextRenderer.DrawText(g, "Установленные программы", Theme.Title, new Rectangle(num, num3, num2 - num, height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, "Удаление вместе с остатками — папками, файлами и записями реестра", Theme.Small, new Rectangle(num, num3 + height + Theme.S(4f), num2 - num, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		int num4 = 0;
		int num5 = 0;
		double num6 = 0.0;
		Item item = null;
		foreach (Item item3 in Items)
		{
			if (item3.Store)
			{
				num5++;
			}
			else
			{
				num4++;
			}
			num6 += item3.Size;
			if (item == null || item3.Size > item.Size)
			{
				item = item3;
			}
		}
		int num7 = Theme.S(10f);
		int num8 = (_statsR.Width - num7 * 2) / 3;
		Stat(g, new Rectangle(_statsR.X, _statsR.Y, num8, _statsR.Height), _loaded ? num4.ToString() : "…", Plural(num4, "программа", "программы", "программ"), Theme.Silver2, "установлены обычным установщиком");
		Stat(g, new Rectangle(_statsR.X + num8 + num7, _statsR.Y, num8, _statsR.Height), _loaded ? num5.ToString() : "…", "из Store", Theme.Silver1, "приложения Microsoft Store");
		Stat(g, new Rectangle(_statsR.X + (num8 + num7) * 2, _statsR.Y, _statsR.Width - (num8 + num7) * 2, _statsR.Height), _loaded ? CleanView.Fmt(num6) : "…", "занято", (num6 > 107374182400.0) ? Theme.Yellow : Theme.Green, (item != null && item.Size > 0.0) ? ("больше всех: " + item.Name) : "по данным установщиков");
		Theme.Smooth(g);
		using (GraphicsPath path = Theme.Round(_searchR, (float)_searchR.Height / 2f))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 30, 35)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(_search.Focused ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(44, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		Theme.DrawIcon(g, "\ue721", Theme.Icon, new Rectangle(_searchR.X + Theme.S(10f), _searchR.Y, Theme.S(24f), _searchR.Height), Theme.Sub);
		string text = (_loaded ? ("Показано: " + _shown.Count) : "Загружаю список программ…");
		TextRenderer.DrawText(g, text, Theme.Small, _sortR, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		int num9 = _sortR.Right;
		for (int num10 = _sorts.Length - 1; num10 >= 0; num10--)
		{
			int num11 = Theme.Measure(_sorts[num10], Theme.Small).Width + Theme.S(16f);
			num9 -= num11;
			Rectangle rectangle = new Rectangle(num9, _sortR.Y, num11, _sortR.Height);
			if (num10 == _sort)
			{
				using GraphicsPath path2 = Theme.Round(rectangle, (float)rectangle.Height / 2f);
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb(26, 255, 255, 255));
				g.FillPath(brush2, path2);
			}
			TextRenderer.DrawText(g, _sorts[num10], Theme.Small, rectangle, (num10 == _sort) ? Theme.Text : Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			num9 -= Theme.S(2f);
		}
		TextRenderer.DrawText(g, "Сортировка:", Theme.Small, new Rectangle(num9 - Theme.S(110f), _sortR.Y, Theme.S(104f), _sortR.Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		int num12 = Math.Min(_limit, _shown.Count);
		for (int i = 0; i < num12; i++)
		{
			Item item2 = _shown[i];
			Rectangle r = item2.R;
			bool flag = i == _hot;
			if (flag)
			{
				using GraphicsPath path3 = Theme.Round(r, Theme.S(10f));
				using SolidBrush brush3 = new SolidBrush(Color.FromArgb(14, 255, 255, 255));
				g.FillPath(brush3, path3);
			}
			else if (i > 0 && _hot != i - 1)
			{
				using SolidBrush brush4 = new SolidBrush(Color.FromArgb(36, 255, 255, 255));
				g.FillRectangle(brush4, r.X + Theme.S(60f), r.Y, r.Width - Theme.S(68f), 1);
			}
			int num13 = Theme.S(38f);
			Rectangle r2 = new Rectangle(r.X + Theme.S(8f), r.Y + (r.Height - num13) / 2, num13, num13);
			DrawAppIcon(g, item2, r2);
			int num14 = r2.Right + Theme.S(14f);
			int num15 = Theme.S(80f);
			int num16 = (item2.DirR.IsEmpty ? item2.DelR.X : item2.DirR.X) - Theme.S(14f) - num15;
			int num17 = r.Y + Theme.S(12f);
			int num18 = Math.Min(Theme.Measure(item2.Name, Theme.Title).Width + 2, Math.Max(Theme.S(60f), num16 - num14 - (item2.Store ? Theme.S(70f) : 0)));
			TextRenderer.DrawText(g, item2.Name, Theme.Title, new Rectangle(num14, num17, num18, Theme.S(22f)), item2.Busy ? Theme.Sub : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			if (item2.Store)
			{
				int num19 = Theme.PillW("Store", Theme.Tiny);
				if (num14 + num18 + Theme.S(10f) + num19 <= num16)
				{
					Theme.Pill(g, new Rectangle(num14 + num18 + Theme.S(10f), num17, num19, Theme.S(20f)), "Store", Theme.Tiny, Theme.Silver2, 20);
				}
			}
			List<string> list = new List<string>();
			list.Add((item2.Pub != "") ? item2.Pub : "Издатель неизвестен");
			if (item2.Ver != "")
			{
				list.Add("v" + item2.Ver);
			}
			if (item2.Date != "")
			{
				list.Add(FmtDate(item2.Date));
			}
			string text2 = (item2.Busy ? "Удаляю…" : string.Join("   •   ", list.ToArray()));
			TextRenderer.DrawText(g, text2, Theme.Small, new Rectangle(num14, num17 + Theme.S(25f), num16 - num14, Theme.S(20f)), item2.Busy ? Theme.Yellow : Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			string text3 = ((item2.Size > 0.0) ? CleanView.Fmt(item2.Size) : "—");
			TextRenderer.DrawText(g, text3, Theme.Body, new Rectangle(num16, r.Y, num15, r.Height), (item2.Size >= 1073741824.0) ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			if (!item2.DirR.IsEmpty)
			{
				Btn(g, item2.DirR, null, "\ue8b7", flag && _hotBtn == 2, danger: false);
			}
			Btn(g, item2.DelR, item2.Busy ? "…" : "Удалить", null, flag && _hotBtn == 1 && !item2.Busy, danger: true);
		}
		if (!_moreR.IsEmpty)
		{
			string text4 = "Показать ещё " + Math.Min(80, _shown.Count - num12) + "  (осталось " + (_shown.Count - num12) + ")";
			Btn(g, _moreR, text4, null, _hot == -2, danger: false);
		}
		if (!_loaded)
		{
			for (int j = 0; j < 6; j++)
			{
				Skeleton.Row(g, new Rectangle(pad - Theme.S(8f), _listTop + j * RowH, base.Width - pad * 2, RowH), this);
			}
		}
		if (_shown.Count == 0 && _loaded)
		{
			Rectangle r3 = new Rectangle(pad, _listTop + Theme.S(10f), base.Width - pad * 2, Theme.S(50f));
			string text5 = ((_search.Text.Trim() != "") ? "Ничего не нашлось — попробуй другое название" : "Здесь пусто");
			TextRenderer.DrawText(g, text5, Theme.Body, r3, Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}

	private int SortHit(Point p)
	{
		if (!_sortR.Contains(p))
		{
			return -1;
		}
		int num = _sortR.Right;
		for (int num2 = _sorts.Length - 1; num2 >= 0; num2--)
		{
			int num3 = Theme.Measure(_sorts[num2], Theme.Small).Width + Theme.S(16f);
			num -= num3;
			if (p.X >= num && p.X < num + num3)
			{
				return num2;
			}
			num -= Theme.S(2f);
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		int num2 = 0;
		int num3 = Math.Min(_limit, _shown.Count);
		for (int i = 0; i < num3; i++)
		{
			Item item = _shown[i];
			if (item.R.Contains(e.Location))
			{
				num = i;
				num2 = (item.DelR.Contains(e.Location) ? 1 : (item.DirR.Contains(e.Location) ? 2 : 0));
				break;
			}
		}
		if (num == -1 && _moreR.Contains(e.Location))
		{
			num = -2;
		}
		Cursor = ((num2 != 0 || num == -2 || SortHit(e.Location) >= 0) ? Cursors.Hand : Cursors.Default);
		if (num != _hot || num2 != _hotBtn)
		{
			_hot = num;
			_hotBtn = num2;
			Invalidate();
		}
		base.OnMouseMove(e);
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

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (e.Button != MouseButtons.Left)
		{
			return;
		}
		int num = SortHit(e.Location);
		if (num >= 0)
		{
			if (num != _sort)
			{
				_sort = num;
				LayoutAgain();
			}
			return;
		}
		if (_moreR.Contains(e.Location))
		{
			_limit += 80;
			LayoutAgain();
			return;
		}
		int num2 = Math.Min(_limit, _shown.Count);
		for (int i = 0; i < num2; i++)
		{
			Item item = _shown[i];
			if (!item.Busy)
			{
				if (item.DelR.Contains(e.Location))
				{
					this.UninstallClicked?.Invoke(item);
					break;
				}
				if (item.DirR.Contains(e.Location))
				{
					Shell.Open("explorer.exe", "\"" + item.Loc + "\"");
					break;
				}
			}
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Skeleton.Track(this, !_loaded);
	}
}
