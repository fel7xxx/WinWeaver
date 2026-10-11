using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace WinWeaver;

internal class HistoryView : Card
{
	private class Entry
	{
		public string Id;

		public DateTime Time;

		public string Title;

		public string Cat;

		public string Group;

		public string GroupTitle;

		public bool Can;

		public bool Done;

		public string Note;
	}

	private enum RowKind
	{
		Day,
		Group,
		Item
	}

	private class Row
	{
		public RowKind Kind;

		public string Text;

		public Entry E;

		public List<Entry> Items;

		public Rectangle R;

		public Rectangle Btn;

		public bool Indent;

		public bool Last;
	}

	private class HitR
	{
		public Rectangle R;

		public string Kind;

		public string Arg;

		public string Title;
	}

	public readonly bool Compact;

	public int CompactMax = 5;

	private List<Entry> _items = new List<Entry>();

	private readonly List<Row> _rows = new List<Row>();

	private readonly List<HitR> _hits = new List<HitR>();

	private readonly HashSet<string> _open = new HashSet<string>();

	private readonly HashSet<string> _seenGroups = new HashSet<string>();

	private string _hover;

	private string _busy;

	private float _appear = 1f;

	private bool _loaded;

	private string _strikeId;

	private float _strike = 1f;

	private Rectangle _headR;

	private Rectangle _allR;

	public Action<string, string> UndoItem;

	public Action<string, string> UndoGroup;

	public Action ShowAll;

	private static CultureInfo Ru => L.Culture;

	public int ActiveCount
	{
		get
		{
			int num = 0;
			foreach (Entry item in _items)
			{
				if (!item.Done)
				{
					num++;
				}
			}
			return num;
		}
	}

	private int ItemH => Theme.S(54f);

	private int GroupH => Theme.S(60f);

	private int DayH => Theme.S(32f);

	private static CultureInfo MakeRu()
	{
		try
		{
			return new CultureInfo("ru-RU");
		}
		catch
		{
			return CultureInfo.InvariantCulture;
		}
	}

	public HistoryView(bool compact)
	{
		Compact = compact;
	}

	public void SetBusy(string key)
	{
		_busy = key;
		Invalidate();
	}

	public override object Fetch(IEngine e)
	{
		return e.Eval("Get-FHistory");
	}

	private static string Str(object o)
	{
		if (o != null)
		{
			return o.ToString();
		}
		return "";
	}

	public override void Apply(object v)
	{
		List<Entry> list = new List<Entry>();
		object[] array = v as object[];
		if (array == null && v is Hashtable)
		{
			array = new object[1] { v };
		}
		if (array != null)
		{
			object[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] is Hashtable hashtable)
				{
					if (!DateTime.TryParse(Str(hashtable["Time"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
					{
						result = DateTime.MinValue;
					}
					list.Add(new Entry
					{
						Id = Str(hashtable["Id"]),
						Time = result,
						Title = Str(hashtable["Title"]),
						Cat = Str(hashtable["Cat"]),
						Group = Str(hashtable["Group"]),
						GroupTitle = Str(hashtable["GroupTitle"]),
						Can = (hashtable["Can"] is bool && (bool)hashtable["Can"]),
						Done = (hashtable["Done"] is bool && (bool)hashtable["Done"]),
						Note = Str(hashtable["Note"])
					});
				}
			}
		}
		string text = null;
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
		foreach (Entry item in _items)
		{
			dictionary[item.Id] = item.Done;
		}
		foreach (Entry item2 in list)
		{
			if (dictionary.TryGetValue(item2.Id, out var value) && !value && item2.Done)
			{
				text = item2.Id;
			}
		}
		bool flag = list.Count != _items.Count;
		_items = list;
		bool flag2 = true;
		foreach (Entry item3 in _items)
		{
			if (!(item3.Group == ""))
			{
				if (_seenGroups.Add(item3.Group) && flag2)
				{
					_open.Add(item3.Group);
				}
				flag2 = false;
			}
		}
		_busy = null;
		if (!_loaded || flag)
		{
			_loaded = true;
			Appear();
		}
		if (text != null && Anim.Enabled)
		{
			_strikeId = text;
			_strike = 0f;
			Anim.Start(this, 420, delegate(float t)
			{
				_strike = t;
				Invalidate();
			}, delegate
			{
				_strike = 1f;
				Invalidate();
			}, "hist-strike-" + GetHashCode());
		}
		RelayoutPage();
	}

	private void Appear()
	{
		if (!Anim.Enabled)
		{
			_appear = 1f;
			return;
		}
		_appear = 0f;
		Anim.Start(this, 650, delegate(float t)
		{
			_appear = t;
			Invalidate();
		}, delegate
		{
			_appear = 1f;
			Invalidate();
		}, "hist-appear-" + GetHashCode());
	}

	public void PlayAppear()
	{
		if (_loaded)
		{
			Appear();
		}
	}

	private void RelayoutPage()
	{
		for (Control parent = base.Parent; parent != null; parent = parent.Parent)
		{
			if (parent is Page page)
			{
				page.RelayoutSoon();
				return;
			}
		}
		Invalidate();
	}

	private static string DayName(DateTime t)
	{
		if (t == DateTime.MinValue)
		{
			return "Раньше";
		}
		DateTime today = DateTime.Today;
		if (t.Date == today)
		{
			return "Сегодня";
		}
		if (t.Date == today.AddDays(-1.0))
		{
			return "Вчера";
		}
		return t.ToString((t.Year == today.Year) ? "d MMMM" : "d MMMM yyyy", Ru);
	}

	private static string TimeText(DateTime t)
	{
		if (t == DateTime.MinValue)
		{
			return "";
		}
		return t.ToString("HH:mm", Ru);
	}

	public static string CatName(string c)
	{
		return c switch
		{
			"privacy" => "Приватность", 
			"protect" => "Защита", 
			"update" => "Обновления", 
			"ui" => "Оформление", 
			"game" => "Игры", 
			"apps" => "Программы", 
			"clean" => "Очистка", 
			"net" => "Сеть", 
			"restore" => "Восстановление", 
			"fix" => "Исправления", 
			"app" => "WinWeaver", 
			_ => "Прочее", 
		};
	}

	public static string CatGlyph(string c)
	{
		return c switch
		{
			"privacy" => "\ue72e", 
			"protect" => "\uea18", 
			"update" => "\ue895", 
			"ui" => "\ue771", 
			"game" => "\ue7fc", 
			"apps" => "\ue71d", 
			"clean" => "\ueda2", 
			"net" => "\ue774", 
			"restore" => "\ue777", 
			"fix" => "\ue90f", 
			"app" => "\ue713", 
			_ => "\ue946", 
		};
	}

	public static Color CatColor(string c)
	{
		return c switch
		{
			"privacy" => Color.FromArgb(130, 170, 255), 
			"protect" => Color.FromArgb(111, 207, 151), 
			"update" => Color.FromArgb(110, 200, 225), 
			"ui" => Color.FromArgb(185, 150, 255), 
			"game" => Color.FromArgb(240, 165, 100), 
			"apps" => Color.FromArgb(235, 135, 185), 
			"clean" => Color.FromArgb(226, 194, 107), 
			"net" => Color.FromArgb(95, 205, 180), 
			"fix" => Color.FromArgb(240, 130, 115), 
			_ => Theme.Silver2, 
		};
	}

	private List<Row> Units()
	{
		List<Row> list = new List<Row>();
		Dictionary<string, Row> dictionary = new Dictionary<string, Row>();
		foreach (Entry item in _items)
		{
			if (item.Group != "")
			{
				if (!dictionary.TryGetValue(item.Group, out var value))
				{
					value = new Row
					{
						Kind = RowKind.Group,
						Text = ((item.GroupTitle != "") ? item.GroupTitle : "Группа изменений"),
						E = item,
						Items = new List<Entry>()
					};
					dictionary[item.Group] = value;
					list.Add(value);
				}
				value.Items.Add(item);
			}
			else
			{
				list.Add(new Row
				{
					Kind = RowKind.Item,
					E = item
				});
			}
		}
		return list;
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		_rows.Clear();
		int num = pad;
		int num2 = Theme.S(44f);
		_headR = new Rectangle(pad, num, w - pad * 2, num2);
		num += num2 + Theme.S(Compact ? 14f : 8f);
		List<Row> list = Units();
		if (Compact)
		{
			int num3 = 0;
			foreach (Row item2 in list)
			{
				if (num3++ >= CompactMax)
				{
					break;
				}
				int num4 = Theme.S((item2.Kind == RowKind.Group) ? 58f : 52f);
				item2.R = new Rectangle(pad, num, w - pad * 2, num4);
				_rows.Add(item2);
				num += num4;
			}
		}
		else
		{
			string text = null;
			foreach (Row item3 in list)
			{
				string text2 = DayName(item3.E.Time);
				if (text2 != text)
				{
					text = text2;
					_rows.Add(new Row
					{
						Kind = RowKind.Day,
						Text = text2,
						R = new Rectangle(pad, num, w - pad * 2, DayH)
					});
					num += DayH;
				}
				if (item3.Kind == RowKind.Group)
				{
					item3.R = new Rectangle(pad, num, w - pad * 2, GroupH);
					_rows.Add(item3);
					num += GroupH;
					if (_open.Contains(item3.E.Group))
					{
						for (int i = 0; i < item3.Items.Count; i++)
						{
							Row item = new Row
							{
								Kind = RowKind.Item,
								E = item3.Items[i],
								Indent = true,
								Last = (i == item3.Items.Count - 1),
								R = new Rectangle(pad, num, w - pad * 2, ItemH)
							};
							_rows.Add(item);
							num += ItemH;
						}
						num += Theme.S(6f);
					}
				}
				else
				{
					item3.R = new Rectangle(pad, num, w - pad * 2, ItemH);
					_rows.Add(item3);
					num += ItemH;
				}
			}
		}
		if (_items.Count == 0)
		{
			num += Theme.S(Compact ? 52f : 120f);
		}
		int num5 = num + pad - Theme.S(4f);
		SetBounds(x, y, w, num5);
		Invalidate();
		return num5;
	}

	private float RowA(int k)
	{
		return Anim.EaseOut(Anim.Clamp(_appear * 1.8f - (float)Math.Min(k, 10) * 0.07f));
	}

	private static Color Fade(Color c, float a)
	{
		return Theme.Mix(Theme.Card1, c, a);
	}

	private void Button(Graphics g, Rectangle r, string text, string key, bool primary, float a)
	{
		bool flag = _hover == key;
		bool flag2 = _busy != null && _busy == key;
		using (GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), (float)r.Height / 2f))
		{
			if (primary)
			{
				Theme.FillGradient(g, path, r, Fade(flag ? Color.White : Theme.Silver1, a), Fade(Theme.Silver2, a));
			}
			else
			{
				Theme.FillGradient(g, path, r, Fade(flag ? Theme.Mix(Theme.Control1, Color.White, 0.12f) : Theme.Control1, a), Fade(Theme.Control2, a));
				using Pen pen = new Pen(Fade(flag ? Theme.BorderHi : Theme.Border, a));
				g.DrawPath(pen, path);
			}
		}
		TextRenderer.DrawText(g, flag2 ? "Откатываю…" : text, Theme.Small, r, Fade(primary ? Theme.OnSilver : Theme.Text, a), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		_hits.Clear();
		_ = base.Pad;
		PaintHead(g);
		if (_items.Count == 0)
		{
			PaintEmpty(g);
			return;
		}
		int num = 0;
		for (int i = 0; i < _rows.Count; i++)
		{
			Row row = _rows[i];
			float num2 = RowA(num++);
			int y = (int)((1f - num2) * (float)Theme.S(10f));
			Rectangle r = row.R;
			r.Offset(0, y);
			if (Compact)
			{
				PaintCompactRow(g, row, r, num2, i == 0, i == _rows.Count - 1);
				continue;
			}
			switch (row.Kind)
			{
			case RowKind.Day:
				TextRenderer.DrawText(g, row.Text.ToUpperInvariant(), Theme.Tiny, new Rectangle(r.X + Theme.S(2f), r.Y + Theme.S(10f), r.Width, Theme.S(18f)), Fade(Theme.Dim, num2), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				break;
			case RowKind.Group:
				PaintGroup(g, row, r, num2);
				break;
			default:
				PaintItem(g, row, r, num2);
				break;
			}
		}
		_ = Compact;
	}

	private void PaintHead(Graphics g)
	{
		Rectangle headR = _headR;
		if (Compact)
		{
			PaintCompactHead(g, headR);
			return;
		}
		int count = _items.Count;
		int num = 0;
		int num2 = 0;
		foreach (Entry item in _items)
		{
			if (!item.Done)
			{
				num++;
				if (item.Can)
				{
					num2++;
				}
			}
		}
		TextRenderer.DrawText(g, "Журнал изменений", Theme.Title, new Point(headR.X, headR.Y + Theme.S(2f)), Theme.Text, TextFormatFlags.NoPrefix);
		string text = ((count == 0) ? "Пока пусто" : ("Всего: " + count + "  •  действуют: " + num + "  •  можно откатить: " + num2));
		TextRenderer.DrawText(g, text, Theme.Small, new Point(headR.X, headR.Y + Theme.S(24f)), Theme.Sub, TextFormatFlags.NoPrefix);
		using Pen pen = new Pen(Theme.Border);
		g.DrawLine(pen, headR.X, headR.Bottom + Theme.S(2f), headR.Right, headR.Bottom + Theme.S(2f));
	}

	private void PaintEmpty(Graphics g)
	{
		Rectangle r = new Rectangle(_headR.X, _headR.Bottom + Theme.S(Compact ? 18f : 24f), _headR.Width, Theme.S(Compact ? 40f : 70f));
		string text = (Compact ? "Здесь появятся все изменения, которые делает программа. Любое можно откатить." : "Изменений пока нет.\nВсё, что меняет программа — переключатели, кнопки и автонастройка, — появится здесь, и любое изменение можно будет откатить отдельно.");
		TextRenderer.DrawText(g, text, Compact ? Theme.Small : Theme.Body, r, Theme.Sub, (TextFormatFlags)(0x810 | ((!Compact) ? 1 : 0)));
	}

	private void Counts(out int act, out int can)
	{
		act = 0;
		can = 0;
		foreach (Entry item in _items)
		{
			if (!item.Done)
			{
				act++;
				if (item.Can)
				{
					can++;
				}
			}
		}
	}

	private void PaintCompactHead(Graphics g, Rectangle h)
	{
		Rectangle r = new Rectangle(h.X, h.Y + (h.Height - Theme.S(38f)) / 2, Theme.S(38f), Theme.S(38f));
		Theme.IconBox(g, r, "\ue823", bright: false, 0f, Theme.IconSmall);
		Counts(out var act, out var can);
		string text = "Вся история  →";
		int num = Theme.Measure(text, Theme.Small).Width + Theme.S(30f);
		Rectangle r2 = new Rectangle(h.Right - num, h.Y + (h.Height - Theme.S(32f)) / 2, num, Theme.S(32f));
		int num2 = r.Right + Theme.S(14f);
		int width = r2.Left - num2 - Theme.S(12f);
		TextRenderer.DrawText(g, "Недавние изменения", Theme.Title, new Rectangle(num2, h.Y + Theme.S(2f), width, Theme.S(22f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		string text2 = ((_items.Count == 0) ? "Пока ничего не менялось" : ("Действуют: " + act + "  •  можно откатить: " + can + ((_items.Count > CompactMax) ? ("  •  всего: " + _items.Count) : "")));
		TextRenderer.DrawText(g, text2, Theme.Small, new Rectangle(num2, h.Y + Theme.S(24f), width, Theme.S(18f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		if (_items.Count > 0)
		{
			Button(g, r2, text, "all", primary: false, 1f);
			_hits.Add(new HitR
			{
				R = r2,
				Kind = "all"
			});
		}
		using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(h.X, h.Bottom + Theme.S(6f), Math.Max(2, h.Width), 2), Theme.Border, Color.FromArgb(0, Theme.Border), 0f);
		g.FillRectangle(brush, h.X, h.Bottom + Theme.S(7f), h.Width, 1);
	}

	private void PaintCompactRow(Graphics g, Row r, Rectangle rr, float a, bool first, bool last)
	{
		Entry e = r.E;
		bool flag = r.Kind == RowKind.Group;
		string text = (flag ? ("g:" + e.Group) : ("i:" + e.Id));
		bool num = _hover == text || _hover == "row:" + text;
		Rectangle rectangle = new Rectangle(rr.X - Theme.S(8f), rr.Y + Theme.S(2f), rr.Width + Theme.S(16f), rr.Height - Theme.S(4f));
		if (num)
		{
			using GraphicsPath path = Theme.Round(rectangle, Theme.S(10f));
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)(14f * a), 255, 255, 255));
			g.FillPath(brush, path);
		}
		int num2 = rr.X + Theme.S(15f);
		int num3 = rr.Y + rr.Height / 2;
		using (Pen pen = new Pen(Fade(Theme.Border, a), Math.Max(1f, Theme.S(1.5f))))
		{
			if (!first)
			{
				g.DrawLine(pen, num2, rr.Y - Theme.S(1f), num2, num3);
			}
			if (!last)
			{
				g.DrawLine(pen, num2, num3, num2, rr.Bottom + Theme.S(1f));
			}
		}
		int num4 = 0;
		int num5 = 0;
		_ = e.Cat;
		if (flag)
		{
			foreach (Entry item in r.Items)
			{
				if (item.Done)
				{
					num5++;
				}
				else if (item.Can)
				{
					num4++;
				}
			}
		}
		bool flag2 = (flag ? (num5 == r.Items.Count) : e.Done);
		Color color = (flag ? Theme.Silver1 : CatColor(e.Cat));
		if (flag2)
		{
			color = Theme.Dim;
		}
		int num6 = Theme.S(flag ? 30f : 26f);
		Rectangle rectangle2 = new Rectangle(num2 - num6 / 2, num3 - num6 / 2, num6, num6);
		using (SolidBrush brush2 = new SolidBrush(Theme.Card2))
		{
			g.FillEllipse(brush2, rectangle2);
		}
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb((int)(40f * a), color)))
		{
			g.FillEllipse(brush3, rectangle2);
		}
		using (Pen pen2 = new Pen(Color.FromArgb((int)(150f * a), color), Math.Max(1f, Theme.S(1.2f))))
		{
			g.DrawEllipse(pen2, rectangle2);
		}
		Theme.DrawIcon(g, flag ? "\ue945" : CatGlyph(e.Cat), Theme.IconSmall, rectangle2, Fade(color, a));
		string text2 = (flag ? "Откатить всё" : "Откатить");
		int num7 = Theme.Measure(text2, Theme.Small).Width + Theme.S(28f);
		Rectangle r2 = new Rectangle(rr.Right - num7, rr.Y + (rr.Height - Theme.S(30f)) / 2, num7, Theme.S(30f));
		int num8 = r2.Left;
		if (!flag2 && (flag ? (num4 > 0) : e.Can))
		{
			Button(g, r2, text2, text, primary: false, a);
			_hits.Add(new HitR
			{
				R = r2,
				Kind = (flag ? "group" : "item"),
				Arg = (flag ? e.Group : e.Id),
				Title = (flag ? r.Text : e.Title)
			});
		}
		else
		{
			string text3 = (flag2 ? "Откачено" : "Без отката");
			Color c = (flag2 ? Theme.Green : Theme.Dim);
			Size size = Theme.Measure(text3, Theme.Tiny);
			int num9 = rr.Right - size.Width - Theme.S(6f);
			int num10 = Theme.S(6f);
			using (SolidBrush brush4 = new SolidBrush(Fade(c, a)))
			{
				g.FillEllipse(brush4, num9 - num10 - Theme.S(6f), num3 - num10 / 2, num10, num10);
			}
			TextRenderer.DrawText(g, text3, Theme.Tiny, new Rectangle(num9, num3 - size.Height / 2 - 1, size.Width + Theme.S(4f), size.Height + 2), Fade(flag2 ? Theme.Green : Theme.Sub, a), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			num8 = num9 - num10 - Theme.S(12f);
		}
		int num11 = num2 + Theme.S(flag ? 26f : 24f);
		int num12 = Math.Max(10, num8 - num11 - Theme.S(12f));
		string text4 = (flag ? r.Text : e.Title);
		Rectangle r3 = new Rectangle(num11, rr.Y + Theme.S(7f), num12, Theme.S(20f));
		TextRenderer.DrawText(g, text4, flag ? Theme.Title : Theme.Body, r3, Fade(flag2 ? Theme.Dim : Theme.Text, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		if (!flag && e.Done)
		{
			float num13 = ((_strikeId == e.Id) ? Anim.EaseOut(_strike) : 1f);
			int num14 = Math.Min(num12, Theme.Measure(e.Title, Theme.Body).Width);
			int num15 = r3.Y + r3.Height / 2 + Theme.S(1f);
			using Pen pen3 = new Pen(Fade(Theme.Dim, a), Math.Max(1f, Theme.S(1.2f)));
			g.DrawLine(pen3, num11, num15, num11 + (int)((float)num14 * num13), num15);
		}
		int y = rr.Y + Theme.S(29f);
		int num16 = num11;
		string text5 = (flag ? "Автонастройка" : CatName(e.Cat));
		Size size2 = Theme.Measure(text5, Theme.Tiny);
		TextRenderer.DrawText(g, text5, Theme.Tiny, new Rectangle(num16, y, Math.Min(size2.Width + Theme.S(4f), num12), Theme.S(18f)), Fade(flag2 ? Theme.Dim : Theme.Mix(color, Theme.Text, 0.15f), a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		num16 += size2.Width + Theme.S(4f);
		string text6 = "  •  " + DayName(e.Time) + ", " + TimeText(e.Time);
		if (flag)
		{
			text6 = text6 + "  •  изменений: " + r.Items.Count + ((num5 > 0 && num5 < r.Items.Count) ? (", откачено: " + num5) : "");
		}
		else if (e.Note != "")
		{
			text6 = text6 + "  •  " + e.Note;
		}
		if (num16 < num11 + num12)
		{
			TextRenderer.DrawText(g, text6, Theme.Tiny, new Rectangle(num16, y, num11 + num12 - num16, Theme.S(18f)), Fade(Theme.Sub, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		_hits.Insert(0, new HitR
		{
			R = rectangle,
			Kind = "row",
			Arg = text
		});
	}

	private void PaintGroup(Graphics g, Row r, Rectangle rr, float a)
	{
		string text = "g:" + r.E.Group;
		bool flag = _hover == text || _hover == "t:" + r.E.Group;
		using (GraphicsPath path = Theme.Round(new Rectangle(rr.X - Theme.S(8f), rr.Y + Theme.S(3f), rr.Width + Theme.S(16f), rr.Height - Theme.S(6f)), Theme.S(10f)))
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)((float)(flag ? 30 : 16) * a), 255, 255, 255));
			g.FillPath(brush, path);
		}
		int num = Theme.S(34f);
		Rectangle r2 = new Rectangle(rr.X + Theme.S(4f), rr.Y + (rr.Height - num) / 2, num, num);
		Theme.IconBox(g, r2, "\ue945", a > 0.5f, 0f, Theme.IconSmall);
		int num2 = 0;
		int num3 = 0;
		foreach (Entry item in r.Items)
		{
			if (item.Done)
			{
				num3++;
			}
			else if (item.Can)
			{
				num2++;
			}
		}
		string text2 = "Откатить всё";
		int num4 = Theme.Measure(text2, Theme.Small).Width + Theme.S(30f);
		int num5 = rr.Right - num4 - Theme.S(4f);
		int num6 = r2.Right + Theme.S(12f);
		int width = num5 - num6 - Theme.S(36f);
		TextRenderer.DrawText(g, r.Text, Theme.Title, new Rectangle(num6, rr.Y + Theme.S(11f), width, Theme.S(20f)), Fade((num3 == r.Items.Count) ? Theme.Sub : Theme.Text, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		string text3 = TimeText(r.E.Time) + "  •  изменений: " + r.Items.Count + ((num3 > 0) ? ("  •  откачено: " + num3) : "");
		if (Compact)
		{
			text3 = DayName(r.E.Time) + ", " + text3;
		}
		TextRenderer.DrawText(g, text3, Theme.Tiny, new Rectangle(num6, rr.Y + Theme.S(32f), width, Theme.S(18f)), Fade(Theme.Sub, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		if (!Compact)
		{
			bool flag2 = _open.Contains(r.E.Group);
			Rectangle rectangle = new Rectangle(num5 - Theme.S(34f), rr.Y + (rr.Height - Theme.S(28f)) / 2, Theme.S(28f), Theme.S(28f));
			using (Pen pen = new Pen(Fade(flag ? Theme.Text : Theme.Sub, a), Math.Max(1.4f, Theme.S(1.6f)))
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round,
				LineJoin = LineJoin.Round
			})
			{
				float num7 = (float)rectangle.X + (float)rectangle.Width / 2f;
				float num8 = (float)rectangle.Y + (float)rectangle.Height / 2f;
				float num9 = Theme.S(4f);
				if (flag2)
				{
					g.DrawLines(pen, new PointF[3]
					{
						new PointF(num7 - num9, num8 + num9 / 2f),
						new PointF(num7, num8 - num9 / 2f),
						new PointF(num7 + num9, num8 + num9 / 2f)
					});
				}
				else
				{
					g.DrawLines(pen, new PointF[3]
					{
						new PointF(num7 - num9, num8 - num9 / 2f),
						new PointF(num7, num8 + num9 / 2f),
						new PointF(num7 + num9, num8 - num9 / 2f)
					});
				}
			}
			_hits.Add(new HitR
			{
				R = new Rectangle(rr.X, rr.Y, num5 - rr.X - Theme.S(4f), rr.Height),
				Kind = "toggle",
				Arg = r.E.Group
			});
		}
		else
		{
			_hits.Add(new HitR
			{
				R = new Rectangle(rr.X, rr.Y, num5 - rr.X - Theme.S(4f), rr.Height),
				Kind = "all"
			});
		}
		Rectangle r3 = new Rectangle(num5, rr.Y + (rr.Height - Theme.S(32f)) / 2, num4, Theme.S(32f));
		if (num2 > 0)
		{
			Button(g, r3, text2, text, primary: false, a);
			_hits.Add(new HitR
			{
				R = r3,
				Kind = "group",
				Arg = r.E.Group,
				Title = r.Text
			});
		}
		else
		{
			string text4 = ((num3 > 0) ? "Откачено" : "Только точкой восстановления");
			int num10 = Theme.PillW(text4, Theme.Tiny);
			Theme.Pill(g, new Rectangle(rr.Right - num10 - Theme.S(4f), rr.Y + (rr.Height - Theme.S(22f)) / 2, num10, Theme.S(22f)), text4, Theme.Tiny, Fade(Theme.Dim, a), 20);
		}
	}

	private void PaintItem(Graphics g, Row r, Rectangle rr, float a)
	{
		Entry e = r.E;
		string text = "i:" + e.Id;
		bool flag = _hover == text;
		int num = rr.X + (r.Indent ? Theme.S(20f) : Theme.S(4f));
		if (r.Indent)
		{
			using (Pen pen = new Pen(Fade(Theme.Border, a), Math.Max(1f, Theme.S(1.5f))))
			{
				int num2 = rr.X + Theme.S(21f);
				g.DrawLine(pen, num2, rr.Y, num2, r.Last ? (rr.Y + rr.Height / 2) : rr.Bottom);
				g.DrawLine(pen, num2, rr.Y + rr.Height / 2, num2 + Theme.S(10f), rr.Y + rr.Height / 2);
			}
			num += Theme.S(14f);
		}
		int num3 = Theme.S(30f);
		Rectangle r2 = new Rectangle(num, rr.Y + (rr.Height - num3) / 2, num3, num3);
		if (r.Indent)
		{
			int num4 = Theme.S(8f);
			Rectangle rect = new Rectangle(num + Theme.S(2f), rr.Y + (rr.Height - num4) / 2, num4, num4);
			using (SolidBrush brush = new SolidBrush(Fade(e.Done ? Theme.Dim : (e.Can ? Theme.Silver1 : Theme.Silver2), a)))
			{
				g.FillEllipse(brush, rect);
			}
			r2 = new Rectangle(num, r2.Y, Theme.S(12f), num3);
		}
		else
		{
			Theme.IconBox(g, r2, CatGlyph(e.Cat), bright: false, flag ? 1f : 0f, Theme.IconSmall);
		}
		string text2 = "Откатить";
		int num5 = Theme.Measure(text2, Theme.Small).Width + Theme.S(30f);
		int num6 = rr.Right - num5 - Theme.S(4f);
		int num7 = r2.Right + Theme.S(12f);
		int num8 = num6 - num7 - Theme.S(12f);
		Color c = (e.Done ? Theme.Dim : Theme.Text);
		Rectangle r3 = new Rectangle(num7, rr.Y + Theme.S(8f), num8, Theme.S(20f));
		TextRenderer.DrawText(g, e.Title, Theme.Body, r3, Fade(c, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		if (e.Done)
		{
			float num9 = ((_strikeId == e.Id) ? Anim.EaseOut(_strike) : 1f);
			int num10 = Math.Min(num8, Theme.Measure(e.Title, Theme.Body).Width);
			int num11 = r3.Y + r3.Height / 2 + Theme.S(1f);
			using Pen pen2 = new Pen(Fade(Theme.Dim, a), Math.Max(1f, Theme.S(1.2f)));
			g.DrawLine(pen2, num7, num11, num7 + (int)((float)num10 * num9), num11);
		}
		string text3 = TimeText(e.Time);
		if (!r.Indent)
		{
			text3 = (Compact ? (DayName(e.Time) + ", ") : "") + text3 + "  •  " + CatName(e.Cat);
		}
		if (e.Note != "")
		{
			text3 = text3 + "  •  " + e.Note;
		}
		TextRenderer.DrawText(g, text3, Theme.Tiny, new Rectangle(num7, rr.Y + Theme.S(30f), num8, Theme.S(18f)), Fade(Theme.Sub, a), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		Rectangle r4 = new Rectangle(num6, rr.Y + (rr.Height - Theme.S(30f)) / 2, num5, Theme.S(30f));
		if (e.Done)
		{
			string text4 = "Откачено";
			int num12 = Theme.PillW(text4, Theme.Tiny);
			Theme.Pill(g, new Rectangle(rr.Right - num12 - Theme.S(4f), rr.Y + (rr.Height - Theme.S(22f)) / 2, num12, Theme.S(22f)), text4, Theme.Tiny, Fade(Theme.Green, a * 0.8f), 20);
		}
		else if (e.Can)
		{
			Button(g, r4, text2, text, primary: false, a);
			_hits.Add(new HitR
			{
				R = r4,
				Kind = "item",
				Arg = e.Id,
				Title = e.Title
			});
		}
		else
		{
			string text5 = "Без отката";
			int num13 = Theme.PillW(text5, Theme.Tiny);
			Theme.Pill(g, new Rectangle(rr.Right - num13 - Theme.S(4f), rr.Y + (rr.Height - Theme.S(22f)) / 2, num13, Theme.S(22f)), text5, Theme.Tiny, Fade(Theme.Dim, a), 14);
		}
		if (!r.Indent && !Compact)
		{
			using (Pen pen3 = new Pen(Color.FromArgb((int)(28f * a), 255, 255, 255)))
			{
				g.DrawLine(pen3, num7, rr.Bottom - 1, rr.Right, rr.Bottom - 1);
			}
		}
	}

	private HitR Hit(Point p)
	{
		for (int num = _hits.Count - 1; num >= 0; num--)
		{
			if (_hits[num].R.Contains(p))
			{
				return _hits[num];
			}
		}
		return null;
	}

	private static string KeyOf(HitR h)
	{
		if (h == null)
		{
			return null;
		}
		return h.Kind switch
		{
			"item" => "i:" + h.Arg, 
			"group" => "g:" + h.Arg, 
			"toggle" => "t:" + h.Arg, 
			"row" => "row:" + h.Arg, 
			_ => h.Kind, 
		};
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		HitR hitR = Hit(e.Location);
		string text = KeyOf(hitR);
		if (text != _hover)
		{
			_hover = text;
			Cursor = ((hitR != null) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = null;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		HitR hitR = Hit(e.Location);
		if (hitR != null && e.Button == MouseButtons.Left && _busy == null)
		{
			switch (hitR.Kind)
			{
			case "item":
				UndoItem?.Invoke(hitR.Arg, hitR.Title);
				break;
			case "group":
				UndoGroup?.Invoke(hitR.Arg, hitR.Title);
				break;
			case "toggle":
				if (!_open.Remove(hitR.Arg))
				{
					_open.Add(hitR.Arg);
				}
				RelayoutPage();
				break;
			case "all":
			case "row":
				ShowAll?.Invoke();
				break;
			}
		}
		base.OnMouseClick(e);
	}
}
