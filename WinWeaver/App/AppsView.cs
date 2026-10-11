using System;
using System.Collections;
using System.Collections.Generic;

namespace WinWeaver;

internal class AppsView : Card
{
	public class Item
	{
		public string Id;

		public string Name;

		public string Desc;

		public bool Installed;

		public int Rec;

		public bool System;

		public FButton Btn;

		public Rectangle R;
	}

	private readonly List<Item> _items = new List<Item>();

	public readonly List<FButton> Buttons = new List<FButton>();

	private bool _loaded;

	private int _hot = -1;

	private Rectangle _headR;

	private Rectangle _statsR;

	private int _sysTop;

	private int _appsTop;

	private int RowH => Theme.S(62f);

	public IEnumerable<Item> Items => _items;

	public int RecLeft => _items.FindAll((Item i) => i.Installed && i.Rec == 1 && !i.System).Count;

	public event Action<Item> RemoveClicked;

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

	public override object Fetch(IEngine e)
	{
		List<Item> list = new List<Item>();
		list.Add(new Item
		{
			Id = "edge",
			Name = "Microsoft Edge",
			Desc = "Браузер Microsoft. WebView2 остаётся — он нужен приложениям",
			Installed = (e.Str(e.Eval("Get-Edge")) != "удалён"),
			Rec = 0,
			System = true
		});
		list.Add(new Item
		{
			Id = "onedrive",
			Name = "OneDrive",
			Desc = "Облако Microsoft. Файлы в папке OneDrive на диске не удаляются",
			Installed = (e.Str(e.Eval("Get-OneDrive")) != "удалён"),
			Rec = 1,
			System = true
		});
		if (e.Eval("Get-FApps") is object[] array)
		{
			object[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] is Hashtable hashtable)
				{
					list.Add(new Item
					{
						Id = Str(hashtable["Id"]),
						Name = Str(hashtable["Name"]),
						Desc = Str(hashtable["Desc"]),
						Installed = (hashtable["Installed"] is bool && (bool)hashtable["Installed"]),
						Rec = ((hashtable["Rec"] is int) ? ((int)hashtable["Rec"]) : 0)
					});
				}
			}
		}
		return list;
	}

	public override void Apply(object v)
	{
		if (!(v is List<Item> list))
		{
			return;
		}
		foreach (Item item2 in _items)
		{
			if (item2.Btn != null)
			{
				base.Controls.Remove(item2.Btn);
				item2.Btn.Dispose();
			}
		}
		_items.Clear();
		List<Item> list2 = list.FindAll((Item i) => i.System);
		List<Item> list3 = list.FindAll((Item i) => !i.System);
		list3.Sort(delegate(Item a, Item b)
		{
			if (a.Installed != b.Installed)
			{
				if (!a.Installed)
				{
					return 1;
				}
				return -1;
			}
			return (a.Rec != b.Rec) ? (b.Rec - a.Rec) : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
		});
		foreach (Item item3 in list2)
		{
			_items.Add(item3);
		}
		foreach (Item item4 in list3)
		{
			_items.Add(item4);
		}
		foreach (Item item5 in _items)
		{
			Item item = item5;
			if (item.Installed)
			{
				item.Btn = new FButton
				{
					Text = "Удалить",
					Kind = BtnKind.Ghost,
					Font = Theme.Small
				};
				item.Btn.AutoWidth(Theme.S(22f));
				item.Btn.Height = Theme.S(32f);
				item.Btn.Click += delegate
				{
					this.RemoveClicked?.Invoke(item);
				};
				base.Controls.Add(item.Btn);
			}
		}
		_loaded = true;
		if (base.Parent is Canvas)
		{
			((Canvas)base.Parent).Page.RelayoutSoon();
		}
		Invalidate();
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		int val = Theme.Measure("Ag", Theme.Title).Height + Theme.S(4f) + Theme.Measure("Ag", Theme.Small).Height;
		_headR = new Rectangle(pad, pad, w - pad * 2, Math.Max(Theme.S(44f), val));
		int y2 = _headR.Bottom + Theme.S(16f);
		_statsR = new Rectangle(pad, y2, w - pad * 2, Theme.S(74f));
		y2 = _statsR.Bottom + Theme.S(14f);
		int num = pad;
		foreach (FButton button in Buttons)
		{
			if (num + button.Width > w - pad && num > pad)
			{
				num = pad;
				y2 += button.Height + Theme.S(8f);
			}
			button.Location = new Point(num, y2);
			num += button.Width + Theme.S(8f);
		}
		if (Buttons.Count > 0)
		{
			y2 += Buttons[0].Height + Theme.S(18f);
		}
		int num2 = Theme.S(28f);
		_sysTop = y2;
		y2 += num2;
		bool flag = true;
		foreach (Item item in _items)
		{
			if (!item.System && flag)
			{
				flag = false;
				y2 = (_appsTop = y2 + Theme.S(10f));
				y2 += num2;
			}
			item.R = new Rectangle(pad - Theme.S(8f), y2, w - pad * 2 + Theme.S(16f), RowH);
			if (item.Btn != null)
			{
				item.Btn.Location = new Point(w - pad - item.Btn.Width, y2 + (RowH - item.Btn.Height) / 2);
			}
			y2 += RowH;
		}
		if (!_loaded)
		{
			y2 += Theme.S(60f);
		}
		int num3 = y2 + pad - Theme.S(6f);
		SetBounds(x, y, w, num3);
		return num3;
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

	private void SectionHead(Graphics g, int y, string text, string right)
	{
		TextRenderer.DrawText(g, text, Theme.Tiny, new Point(base.Pad, y + Theme.S(4f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		int width = TextRenderer.MeasureText(g, text, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
		int num = base.Pad + width + Theme.S(12f);
		int num2 = base.Width - base.Pad;
		if (!string.IsNullOrEmpty(right))
		{
			int width2 = TextRenderer.MeasureText(g, right, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
			TextRenderer.DrawText(g, right, Theme.Tiny, new Point(num2 - width2, y + Theme.S(4f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			num2 -= width2 + Theme.S(12f);
		}
		if (num2 > num)
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(num, 0, num2 - num, 1), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(8, 255, 255, 255), 0f))
			{
				g.FillRectangle(brush, num, y + Theme.S(11f), num2 - num, 1);
			}
		}
	}

	protected override void PaintContent(Graphics g)
	{
		_ = base.Pad;
		Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - Theme.S(44f)) / 2, Theme.S(44f), Theme.S(44f)), "\ue71d");
		int num = _headR.X + Theme.S(58f);
		int height = Theme.Measure("Ag", Theme.Title).Height;
		int num2 = _headR.Y + (_headR.Height - height - Theme.S(4f) - Theme.Measure("Ag", Theme.Small).Height) / 2;
		TextRenderer.DrawText(g, "Встроенные приложения Windows", Theme.Title, new Rectangle(num, num2, _headR.Right - num, height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, "Удаляй по одному или все рекомендованные сразу. Вернуть приложение можно из Microsoft Store", Theme.Small, new Rectangle(num, num2 + height + Theme.S(4f), _headR.Right - num, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		int count = _items.FindAll((Item i) => i.Installed && !i.System).Count;
		int count2 = _items.FindAll((Item i) => !i.Installed && !i.System).Count;
		int recLeft = RecLeft;
		int count3 = _items.FindAll((Item i) => i.Installed && i.System).Count;
		int num3 = Theme.S(10f);
		int num4 = (_statsR.Width - num3 * 2) / 3;
		Stat(g, new Rectangle(_statsR.X, _statsR.Y, num4, _statsR.Height), _loaded ? recLeft.ToString() : "…", "можно удалить", (recLeft == 0) ? Theme.Green : Theme.Yellow, (recLeft == 0) ? "мусора не осталось" : "реклама, подписки и лишние сервисы");
		Stat(g, new Rectangle(_statsR.X + num4 + num3, _statsR.Y, num4, _statsR.Height), _loaded ? count.ToString() : "…", "установлено", Theme.Silver1, _loaded ? ("из списка, удалено: " + count2) : "");
		Stat(g, new Rectangle(_statsR.X + (num4 + num3) * 2, _statsR.Y, _statsR.Width - (num4 + num3) * 2, _statsR.Height), _loaded ? (2 - count3 + " / 2") : "…", "удалено", (count3 == 0) ? Theme.Green : Theme.Silver2, "Edge и OneDrive");
		if (!_loaded)
		{
			return;
		}
		SectionHead(g, _sysTop, "СИСТЕМНЫЕ КОМПОНЕНТЫ", null);
		SectionHead(g, _appsTop, "ПРЕДУСТАНОВЛЕННЫЕ ПРИЛОЖЕНИЯ", count + " установлено");
		Item item = null;
		for (int j = 0; j < _items.Count; j++)
		{
			Item item2 = _items[j];
			Rectangle r = item2.R;
			if (j == _hot)
			{
				using GraphicsPath path = Theme.Round(r, Theme.S(10f));
				using SolidBrush brush = new SolidBrush(Color.FromArgb(14, 255, 255, 255));
				g.FillPath(brush, path);
			}
			else if (item != null && item.System == item2.System)
			{
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb(34, 255, 255, 255));
				g.FillRectangle(brush2, r.X + Theme.S(60f), r.Y, r.Width - Theme.S(68f), 1);
			}
			item = item2;
			int num5 = Theme.S(36f);
			Rectangle rectangle = new Rectangle(r.X + Theme.S(8f), r.Y + (r.Height - num5) / 2, num5, num5);
			using (GraphicsPath path2 = Theme.Round(rectangle, (float)rectangle.Width * 0.28f))
			{
				Color a = (item2.Installed ? Tint(item2.Name) : Color.FromArgb(70, 70, 78));
				Theme.FillGradient(g, path2, rectangle, Theme.Mix(a, Color.FromArgb(70, 70, 80), 0.35f), Theme.Mix(a, Color.FromArgb(30, 30, 36), 0.6f));
				using Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255));
				g.DrawPath(pen, path2);
			}
			using (Font font = Theme.F(12f, semi: true))
			{
				TextRenderer.DrawText(g, item2.Name.Substring(0, 1).ToUpperInvariant(), font, rectangle, item2.Installed ? Theme.Text : Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
			int num6 = rectangle.Right + Theme.S(14f);
			int num7 = ((item2.Btn != null) ? (item2.Btn.Left - Theme.S(14f)) : (r.Right - Theme.S(120f)));
			if (num7 < num6 + Theme.S(120f))
			{
				num7 = r.Right - Theme.S(120f);
			}
			int num8 = r.Y + Theme.S(11f);
			int num9 = Math.Min(Theme.Measure(item2.Name, Theme.Title).Width + 2, Math.Max(Theme.S(60f), num7 - num6));
			TextRenderer.DrawText(g, item2.Name, Theme.Title, new Rectangle(num6, num8, num9, Theme.S(22f)), item2.Installed ? Theme.Text : Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			string text = ((!item2.Installed) ? null : ((item2.Rec == 1) ? "Можно удалить" : (item2.System ? "По желанию" : "Пригодится не всем")));
			if (text != null)
			{
				int num10 = Theme.PillW(text, Theme.Tiny);
				int num11 = num6 + num9 + Theme.S(10f);
				if (num11 + num10 <= num7)
				{
					Theme.Pill(g, new Rectangle(num11, num8, num10, Theme.S(20f)), text, Theme.Tiny, (item2.Rec == 1) ? Theme.Yellow : Theme.Silver2, 24);
				}
			}
			TextRenderer.DrawText(g, item2.Desc, Theme.Small, new Rectangle(num6, num8 + Theme.S(24f), num7 - num6, Theme.S(20f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			if (!item2.Installed)
			{
				string text2 = "Удалено";
				int width = TextRenderer.MeasureText(g, text2, Theme.Small, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
				int num12 = r.Right - Theme.S(8f) - width;
				int num13 = r.Y + r.Height / 2;
				TextRenderer.DrawText(g, text2, Theme.Small, new Point(num12, num13 - Theme.S(9f)), Theme.Green, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				float num14 = Theme.S(1f);
				float num15 = num12 - Theme.S(18f);
				using Pen pen2 = new Pen(Theme.Green, Theme.S(1.7f));
				pen2.StartCap = LineCap.Round;
				pen2.EndCap = LineCap.Round;
				pen2.LineJoin = LineJoin.Round;
				g.DrawLines(pen2, new PointF[3]
				{
					new PointF(num15, num13),
					new PointF(num15 + 4f * num14, (float)num13 + 4f * num14),
					new PointF(num15 + 12f * num14, (float)num13 - 4f * num14)
				});
			}
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		for (int i = 0; i < _items.Count; i++)
		{
			if (_items[i].R.Contains(e.Location))
			{
				num = i;
			}
		}
		if (num != _hot)
		{
			_hot = num;
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
}
