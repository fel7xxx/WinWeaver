using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WinWeaver;

internal class RestoreView : Card
{
	public class Item
	{
		public int Seq;

		public string Desc;

		public string Date;

		public DateTime? When;

		public int Type;

		public Rectangle R;

		public Rectangle RollR;

		public Rectangle DelR;
	}

	private readonly List<Item> _items = new List<Item>();

	private readonly List<FButton> _buttons = new List<FButton>();

	private Hashtable _info;

	private bool _loaded;

	private int _hot = -1;

	private string _hotBtn;

	private static int _lastCount = 3;

	private Rectangle _headR;

	private Rectangle _statsR;

	private int _listTop;

	private readonly FButton _create;

	private readonly FButton _wizard;

	private readonly FButton _props;

	private readonly FButton _old;

	private readonly FButton _refresh;

	public Worker RefreshWorker;

	private int RowH => Theme.S(66f);

	public int Count => _items.Count;

	private bool On
	{
		get
		{
			if (_info != null && _info["On"] is bool)
			{
				return (bool)_info["On"];
			}
			return false;
		}
	}

	public event Action CreateClicked;

	public event Action EnableClicked;

	public event Action DeleteOldClicked;

	public event Action<Item> RollbackClicked;

	public event Action<Item> DeleteClicked;

	public RestoreView()
	{
		_create = Btn("Включить защиту", BtnKind.Silver, null, delegate
		{
			if (On)
			{
				this.CreateClicked?.Invoke();
			}
			else
			{
				this.EnableClicked?.Invoke();
			}
		});
		_create.Text = "Создать точку";
		_create.Enabled = false;
		_wizard = Btn("Мастер восстановления", BtnKind.Ghost, null, delegate
		{
			Shell.Open(Path.Combine(Shell.WinDir, "System32\\rstrui.exe"));
		});
		_props = Btn("Настройки защиты", BtnKind.Ghost, null, delegate
		{
			Shell.Open(Path.Combine(Shell.WinDir, "System32\\SystemPropertiesProtection.exe"));
		});
		_old = Btn("Удалить старые", BtnKind.Ghost, null, delegate
		{
			this.DeleteOldClicked?.Invoke();
		});
		_refresh = Btn("Обновить", BtnKind.Ghost, "\ue72c", delegate
		{
			if (RefreshWorker != null && base.Parent is Canvas)
			{
				((Canvas)base.Parent).Page.Refresh(RefreshWorker);
			}
		});
		_old.Enabled = false;
	}

	private FButton Btn(string text, BtnKind kind, string glyph, Action click)
	{
		FButton fButton = new FButton
		{
			Text = text,
			Kind = kind,
			Font = Theme.Body,
			Glyph = glyph
		};
		fButton.AutoWidth(Theme.S(30f));
		fButton.Click += delegate
		{
			click();
		};
		_buttons.Add(fButton);
		base.Controls.Add(fButton);
		return fButton;
	}

	private static double Dbl(object o)
	{
		if (o == null)
		{
			return 0.0;
		}
		try
		{
			return Convert.ToDouble(o, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0.0;
		}
	}

	public override object Fetch(IEngine e)
	{
		return new object[2]
		{
			e.Eval("Get-FRestoreInfo"),
			e.Eval("Get-FRestoreList")
		};
	}

	public override void Apply(object v)
	{
		if (!(v is object[] array))
		{
			return;
		}
		_info = array[0] as Hashtable;
		_items.Clear();
		if (array[1] is object[] array2)
		{
			object[] array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				if (array3[i] is Hashtable hashtable)
				{
					Item item = new Item
					{
						Seq = ((hashtable["Seq"] is int) ? ((int)hashtable["Seq"]) : 0),
						Desc = (hashtable["Desc"] ?? "").ToString(),
						Date = (hashtable["Date"] ?? "").ToString(),
						Type = ((hashtable["Type"] is int) ? ((int)hashtable["Type"]) : 12)
					};
					if (DateTime.TryParse((hashtable["Iso"] ?? "").ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
					{
						item.When = result;
						item.Date = result.ToString("dd.MM.yyyy  HH:mm");
					}
					_items.Add(item);
				}
			}
		}
		_items.Sort((Item x, Item y) => y.Seq.CompareTo(x.Seq));
		_loaded = true;
		Skeleton.Track(this, loading: false);
		_lastCount = _items.Count;
		_create.Text = (On ? "Создать точку" : "Включить защиту");
		_create.Enabled = true;
		_old.Enabled = _items.Count > 1;
		_create.Invalidate();
		Invalidate();
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

	private static string Ago(DateTime? w)
	{
		if (!w.HasValue)
		{
			return "";
		}
		TimeSpan timeSpan = DateTime.Now - w.Value;
		if (timeSpan.TotalMinutes < 2.0)
		{
			return "только что";
		}
		if (timeSpan.TotalHours < 1.0)
		{
			int n = (int)timeSpan.TotalMinutes;
			return n + " " + Plural(n, "минуту", "минуты", "минут") + " назад";
		}
		if (timeSpan.TotalHours < 24.0)
		{
			int n2 = (int)timeSpan.TotalHours;
			return n2 + " " + Plural(n2, "час", "часа", "часов") + " назад";
		}
		int num = (int)(DateTime.Now.Date - w.Value.Date).TotalDays;
		if (num <= 1)
		{
			return "вчера";
		}
		if (num < 31)
		{
			return num + " " + Plural(num, "день", "дня", "дней") + " назад";
		}
		int n3 = num / 30;
		return n3 + " " + Plural(n3, "месяц", "месяца", "месяцев") + " назад";
	}

	private static bool Ours(Item it)
	{
		return (it.Desc ?? "").StartsWith("WinWeaver", StringComparison.OrdinalIgnoreCase);
	}

	private static void Kind(Item it, out string chip, out Color c, out string title)
	{
		string text = (title = it.Desc ?? "");
		if (Ours(it))
		{
			chip = "WinWeaver";
			c = Theme.Silver1;
			title = (text.Contains("auto") ? "Автоматическая точка" : "Точка от WinWeaver");
			return;
		}
		string text2 = text.ToLowerInvariant();
		if (text2.Contains("windows update") || text2.Contains("установщик модулей") || text2.Contains("modules installer") || text2.Contains("обновлени"))
		{
			chip = "Обновление Windows";
			c = Color.FromArgb(110, 190, 200);
			return;
		}
		switch (it.Type)
		{
		case 0:
			chip = "Установка программы";
			c = Color.FromArgb(125, 165, 235);
			break;
		case 1:
			chip = "Удаление программы";
			c = Color.FromArgb(125, 165, 235);
			break;
		case 10:
			chip = "Драйвер";
			c = Color.FromArgb(175, 145, 235);
			break;
		case 13:
			chip = "Отменённая операция";
			c = Theme.Dim;
			break;
		default:
			chip = ((text2.Contains("driver") || text2.Contains("драйвер")) ? "Драйвер" : "Изменение настроек");
			c = ((chip == "Драйвер") ? Color.FromArgb(175, 145, 235) : Theme.Silver2);
			break;
		}
		if (string.IsNullOrEmpty(title))
		{
			title = "Без описания";
		}
	}

	private new static string Size(double b)
	{
		if (b <= 0.0)
		{
			return "0 ГБ";
		}
		double num = b / 1073741824.0;
		if (num < 1.0)
		{
			return Math.Round(b / 1048576.0) + " МБ";
		}
		return num.ToString((num < 10.0) ? "0.0" : "0", L.Culture) + " ГБ";
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(64f));
		int y2 = _headR.Bottom + Theme.S(16f);
		_statsR = new Rectangle(pad, y2, w - pad * 2, Theme.S(78f));
		y2 = _statsR.Bottom + Theme.S(14f);
		int num = pad;
		int num2 = 0;
		foreach (FButton button in _buttons)
		{
			if (num + button.Width > w - pad && num > pad)
			{
				num = pad;
				y2 += button.Height + Theme.S(8f);
			}
			button.Location = new Point(num, y2);
			num += button.Width + Theme.S(8f);
			num2 = button.Height;
		}
		y2 = (_listTop = y2 + (num2 + Theme.S(20f))) + Theme.S(30f);
		int num3 = Theme.S(32f);
		int num4 = Theme.Measure("Удалить", Theme.Small).Width + Theme.S(28f);
		int num5 = Theme.Measure("Откатить", Theme.Small).Width + Theme.S(28f);
		foreach (Item item in _items)
		{
			item.R = new Rectangle(pad - Theme.S(8f), y2, w - pad * 2 + Theme.S(16f), RowH);
			int num6 = w - pad;
			item.DelR = new Rectangle(num6 - num4, y2 + (RowH - num3) / 2, num4, num3);
			item.RollR = new Rectangle(item.DelR.Left - Theme.S(8f) - num5, item.DelR.Top, num5, num3);
			y2 += RowH;
		}
		if (!_loaded)
		{
			y2 += RowH * Math.Max(1, _lastCount);
		}
		else if (_items.Count == 0)
		{
			y2 += Theme.S(96f);
		}
		int num7 = y2 + pad - Theme.S(4f);
		SetBounds(x, y, w, num7);
		return num7;
	}

	private void Tile(Graphics g, Rectangle r)
	{
		using GraphicsPath path = Theme.Round(r, Theme.S(12f));
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
		{
			g.FillPath(brush, path);
		}
		using Pen pen = new Pen(Color.FromArgb(34, 255, 255, 255));
		g.DrawPath(pen, path);
	}

	private void Stat(Graphics g, Rectangle r, string label, string value, string unit, string hint, Color c, float bar = -1f)
	{
		Tile(g, r);
		int num = Theme.S(3f);
		using (GraphicsPath path = Theme.Round(new RectangleF(r.X + Theme.S(14f), r.Y + Theme.S(16f), num, r.Height - Theme.S(32f)), (float)num / 2f))
		{
			using SolidBrush brush = new SolidBrush(c);
			g.FillPath(brush, path);
		}
		int num2 = r.X + Theme.S(28f);
		TextRenderer.DrawText(g, label, Theme.Tiny, new Rectangle(num2, r.Y + Theme.S(10f), r.Right - num2 - Theme.S(10f), Theme.S(16f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		using (Font font = Theme.F(17f, semi: true))
		{
			Size size = TextRenderer.MeasureText(g, value, font, System.Drawing.Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, value, font, new Point(num2, r.Y + Theme.S(26f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			unit = Theme.FitOrShort(unit, Theme.Small, r.Right - num2 - size.Width - Theme.S(14f));
			if (!string.IsNullOrEmpty(unit))
			{
				TextRenderer.DrawText(g, unit, Theme.Small, new Rectangle(num2 + size.Width + Theme.S(6f), r.Y + Theme.S(26f), r.Right - num2 - size.Width - Theme.S(14f), size.Height - Theme.S(3f)), Theme.Sub, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			}
		}
		if (bar >= 0f)
		{
			RectangleF r2 = new RectangleF(num2, r.Bottom - Theme.S(16f), r.Right - num2 - Theme.S(14f), Theme.S(5f));
			using (GraphicsPath path2 = Theme.Round(r2, r2.Height / 2f))
			{
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb(26, 255, 255, 255));
				g.FillPath(brush2, path2);
			}
			float num3 = Math.Max(r2.Height, r2.Width * Math.Min(1f, bar));
			using GraphicsPath path3 = Theme.Round(new RectangleF(r2.X, r2.Y, num3, r2.Height), r2.Height / 2f);
			using LinearGradientBrush brush3 = new LinearGradientBrush(new RectangleF(r2.X, r2.Y, Math.Max(1f, num3), r2.Height), Theme.Silver1, Theme.Silver2, 0f);
			g.FillPath(brush3, path3);
			return;
		}
		TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(num2, r.Bottom - Theme.S(24f), r.Right - num2 - Theme.S(10f), Theme.S(16f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
	}

	private void ShieldIcon(Graphics g, Rectangle r)
	{
		Color color = ((!_loaded) ? Theme.Silver2 : (On ? Theme.Green : Theme.Yellow));
		using (GraphicsPath path = Theme.Round(r, Theme.S(16f)))
		{
			Theme.FillGradient(g, path, r, Theme.Mix(Color.FromArgb(62, 64, 74), color, 0.18f), Color.FromArgb(30, 30, 36));
			using Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		float num = Theme.S(1f);
		float num2 = (float)r.X + (float)r.Width / 2f;
		float num3 = (float)r.Y + 15f * num;
		float num4 = 26f * num;
		float num5 = 32f * num;
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddLine(num2, num3, num2 + num4 / 2f, num3 + 5f * num);
			graphicsPath.AddBezier(num2 + num4 / 2f, num3 + 5f * num, num2 + num4 / 2f, num3 + num5 * 0.62f, num2 + num4 * 0.3f, num3 + num5 * 0.85f, num2, num3 + num5);
			graphicsPath.AddBezier(num2, num3 + num5, num2 - num4 * 0.3f, num3 + num5 * 0.85f, num2 - num4 / 2f, num3 + num5 * 0.62f, num2 - num4 / 2f, num3 + 5f * num);
			graphicsPath.CloseFigure();
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(36, color)))
			{
				g.FillPath(brush, graphicsPath);
			}
			using Pen pen2 = new Pen(color, 2f * num);
			pen2.LineJoin = LineJoin.Round;
			g.DrawPath(pen2, graphicsPath);
		}
		using Pen pen3 = new Pen(color, 2.2f * num);
		pen3.StartCap = LineCap.Round;
		pen3.EndCap = LineCap.Round;
		float num6 = num3 + num5 * 0.48f;
		if (!_loaded || On)
		{
			g.DrawLines(pen3, new PointF[3]
			{
				new PointF(num2 - 6f * num, num6),
				new PointF(num2 - 1.5f * num, num6 + 4.5f * num),
				new PointF(num2 + 6.5f * num, num6 - 4f * num)
			});
			return;
		}
		g.DrawLine(pen3, num2, num6 - 6f * num, num2, num6 + 2f * num);
		using SolidBrush brush2 = new SolidBrush(color);
		g.FillEllipse(brush2, num2 - 1.4f * num, num6 + 5f * num, 2.8f * num, 2.8f * num);
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		int pad = base.Pad;
		Rectangle r = new Rectangle(_headR.X, _headR.Y, Theme.S(64f), Theme.S(64f));
		ShieldIcon(g, r);
		int num = r.Right + Theme.S(16f);
		Item item = ((_items.Count > 0) ? _items[0] : null);
		string text = ((_info != null && _info["Drive"] != null) ? _info["Drive"].ToString() : "C:");
		string text2 = ((!_loaded) ? "Проверяю защиту системы…" : (On ? "Защита системы включена" : "Защита системы выключена"));
		string text3 = ((!_loaded) ? "" : (On ? ("Диск " + text + " • откат вернёт систему, программы и драйверы, но не тронет личные файлы") : "Без неё нельзя создать точку и откатить систему. Включи — это займёт секунду"));
		using (Font font = Theme.F(15f, semi: true))
		{
			int height = Theme.Measure("Ag", font).Height;
			int num2 = _headR.Y + Theme.S(8f);
			TextRenderer.DrawText(g, text2, font, new Rectangle(num, num2, _headR.Right - num, height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, text3, Theme.Small, new Rectangle(num, num2 + height + Theme.S(6f), _headR.Right - num, Theme.S(20f)), On ? Theme.Sub : Theme.Yellow, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		int num3 = Theme.S(10f);
		int num4 = (_statsR.Width - num3 * 2) / 3;
		string text4 = "нет";
		string unit = "";
		string hint = "создай первую точку";
		Color color = Theme.Red;
		if (item != null)
		{
			string text5 = Ago(item.When);
			if (text5.Length == 0)
			{
				text4 = item.Date;
			}
			else
			{
				int num5 = text5.IndexOf(' ');
				if (char.IsDigit(text5[0]) && num5 > 0)
				{
					text4 = text5.Substring(0, num5);
					unit = text5.Substring(num5 + 1);
				}
				else
				{
					text4 = text5;
				}
			}
			hint = item.Date;
			color = ((item.When.HasValue && (DateTime.Now - item.When.Value).TotalDays > 7.0) ? Theme.Yellow : Theme.Green);
			if (color == Theme.Yellow)
			{
				hint = "давно — лучше создать новую";
			}
		}
		Stat(g, new Rectangle(_statsR.X, _statsR.Y, num4, _statsR.Height), "ПОСЛЕДНЯЯ ТОЧКА", _loaded ? text4 : "…", unit, hint, _loaded ? color : Theme.Dim);
		string hint2 = ((_items.Count > 0) ? ("самая старая — " + _items[_items.Count - 1].Date.Split(' ')[0]) : "пока пусто");
		Stat(g, new Rectangle(_statsR.X + num4 + num3, _statsR.Y, num4, _statsR.Height), "ТОЧЕК НА ДИСКЕ", _loaded ? _items.Count.ToString() : "…", _loaded ? Plural(_items.Count, "точка", "точки", "точек") : "", hint2, Theme.Silver1);
		double num6 = ((_info != null) ? Dbl(_info["Used"]) : 0.0);
		double num7 = ((_info != null) ? Dbl(_info["Max"]) : 0.0);
		double num8 = ((_info != null) ? Dbl(_info["Cap"]) : 0.0);
		bool flag = num7 > 0.0 && (num8 <= 0.0 || num7 <= num8);
		string text6 = (flag ? ("из " + Size(num7) + ((num8 > 0.0) ? (" • " + Math.Round(num7 * 100.0 / num8) + "% диска") : "")) : "без лимита");
		Stat(g, new Rectangle(_statsR.X + (num4 + num3) * 2, _statsR.Y, _statsR.Width - (num4 + num3) * 2, _statsR.Height), "МЕСТО ПОД ТОЧКИ", _loaded ? Size(num6) : "…", _loaded ? text6 : "", "", Theme.Silver2, (_loaded && flag) ? ((float)(num6 / num7)) : (-1f));
		if (!_loaded)
		{
			int num9 = _listTop + Theme.S(30f);
			for (int i = 0; i < Math.Max(1, _lastCount); i++)
			{
				Skeleton.Row(g, new Rectangle(pad - Theme.S(8f), num9, base.Width - pad * 2, RowH), this, 12);
				num9 += RowH;
			}
			return;
		}
		TextRenderer.DrawText(g, "ХРОНОЛОГИЯ", Theme.Tiny, new Point(pad, _listTop + Theme.S(6f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		int width = TextRenderer.MeasureText(g, "ХРОНОЛОГИЯ", Theme.Tiny, System.Drawing.Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
		string text7 = ((_items.Count > 0) ? "новые сверху" : "");
		int width2 = TextRenderer.MeasureText(g, text7, Theme.Tiny, System.Drawing.Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
		TextRenderer.DrawText(g, text7, Theme.Tiny, new Point(base.Width - pad - width2, _listTop + Theme.S(6f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		int num10 = pad + width + Theme.S(12f);
		int num11 = base.Width - pad - width2 - Theme.S(12f);
		if (num11 > num10)
		{
			using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(num10, 0, num11 - num10, 1), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(8, 255, 255, 255), 0f);
			g.FillRectangle(brush, num10, _listTop + Theme.S(13f), num11 - num10, 1);
		}
		if (_items.Count == 0)
		{
			Rectangle r2 = new Rectangle(pad, _listTop + Theme.S(36f), base.Width - pad * 2, Theme.S(80f));
			Tile(g, r2);
			TextRenderer.DrawText(g, On ? "Точек восстановления пока нет" : "Точек нет — защита системы выключена", Theme.Title, new Rectangle(r2.X, r2.Y + Theme.S(16f), r2.Width, Theme.S(24f)), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
			TextRenderer.DrawText(g, On ? "Нажми «Создать точку» — это займёт до минуты" : "Нажми «Включить защиту», затем создай первую точку", Theme.Small, new Rectangle(r2.X, r2.Y + Theme.S(44f), r2.Width, Theme.S(20f)), Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
			return;
		}
		int num12 = pad + Theme.S(10f);
		Item item2 = _items[0];
		Item item3 = _items[_items.Count - 1];
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(num12 - 1, item2.R.Y, 2, Math.Max(2, item3.R.Bottom - item2.R.Y)), Color.FromArgb(90, 255, 255, 255), Color.FromArgb(14, 255, 255, 255), 90f))
		{
			g.FillRectangle(brush2, num12 - 1, item2.R.Y + RowH / 2, 2, Math.Max(0, item3.R.Y - item2.R.Y));
		}
		for (int j = 0; j < _items.Count; j++)
		{
			Item item4 = _items[j];
			Rectangle r3 = item4.R;
			if (j == _hot)
			{
				using GraphicsPath path = Theme.Round(new Rectangle(r3.X + Theme.S(28f), r3.Y + Theme.S(3f), r3.Width - Theme.S(28f), r3.Height - Theme.S(6f)), Theme.S(10f));
				using SolidBrush brush3 = new SolidBrush(Color.FromArgb(14, 255, 255, 255));
				g.FillPath(brush3, path);
			}
			Kind(item4, out var chip, out var c, out var title);
			float num13 = (float)r3.Y + (float)r3.Height / 2f;
			float num14 = ((j == 0) ? Theme.S(6f) : Theme.S(4.5f));
			if (j == 0)
			{
				using SolidBrush brush4 = new SolidBrush(Color.FromArgb(40, c));
				g.FillEllipse(brush4, (float)num12 - num14 * 2f, num13 - num14 * 2f, num14 * 4f, num14 * 4f);
			}
			using (SolidBrush brush5 = new SolidBrush(Theme.Card2))
			{
				g.FillEllipse(brush5, (float)num12 - num14 - 2f, num13 - num14 - 2f, num14 * 2f + 4f, num14 * 2f + 4f);
			}
			using (SolidBrush brush6 = new SolidBrush(c))
			{
				g.FillEllipse(brush6, (float)num12 - num14, num13 - num14, num14 * 2f, num14 * 2f);
			}
			int num15 = pad + Theme.S(36f);
			int num16 = item4.RollR.Left - Theme.S(14f);
			RowBtn(g, item4.RollR, "Откатить", j + ":roll");
			RowBtn(g, item4.DelR, "Удалить", j + ":del");
			int num17 = r3.Y + Theme.S(12f);
			int num18 = Math.Min(Theme.Measure(title, Theme.Title).Width + 2, Math.Max(Theme.S(80f), num16 - num15));
			TextRenderer.DrawText(g, title, Theme.Title, new Rectangle(num15, num17, num18, Theme.S(22f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			int num19 = num15 + num18 + Theme.S(10f);
			int num20 = Theme.PillW(chip, Theme.Tiny);
			if (num19 + num20 <= num16)
			{
				Theme.Pill(g, new Rectangle(num19, num17, num20, Theme.S(20f)), chip, Theme.Tiny, c, 24);
				num19 += num20 + Theme.S(6f);
			}
			if (j == 0)
			{
				int num21 = Theme.PillW("последняя", Theme.Tiny);
				if (num19 + num21 <= num16)
				{
					Theme.Pill(g, new Rectangle(num19, num17, num21, Theme.S(20f)), "последняя", Theme.Tiny, Theme.Green, 22);
				}
			}
			string text8 = Ago(item4.When);
			string text9 = item4.Date + ((text8.Length > 0) ? ("  •  " + text8) : "") + "  •  №" + item4.Seq + ((Ours(item4) && title != item4.Desc) ? ("  •  " + item4.Desc) : "");
			TextRenderer.DrawText(g, text9, Theme.Small, new Rectangle(num15, num17 + Theme.S(25f), num16 - num15, Theme.S(20f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
	}

	private void RowBtn(Graphics g, Rectangle r, string text, string key)
	{
		bool flag = _hotBtn == key;
		bool flag2 = key.EndsWith(":del");
		using (GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), (float)r.Height / 2f))
		{
			Theme.FillGradient(g, path, r, flag ? Theme.Mix(Theme.Control1, flag2 ? Theme.Red : Color.White, flag2 ? 0.18f : 0.12f) : Theme.Control1, Theme.Control2);
			using Pen pen = new Pen((!flag) ? Theme.Border : (flag2 ? Color.FromArgb(140, Theme.Red) : Theme.BorderHi));
			g.DrawPath(pen, path);
		}
		TextRenderer.DrawText(g, text, Theme.Small, r, (flag && flag2) ? Theme.Red : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	private string HitBtn(Point pt, out Item it)
	{
		it = null;
		for (int i = 0; i < _items.Count; i++)
		{
			if (_items[i].RollR.Contains(pt))
			{
				it = _items[i];
				return i + ":roll";
			}
			if (_items[i].DelR.Contains(pt))
			{
				it = _items[i];
				return i + ":del";
			}
		}
		return null;
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
		Item it;
		string text = HitBtn(e.Location, out it);
		if (num != _hot || text != _hotBtn)
		{
			_hot = num;
			_hotBtn = text;
			Cursor = ((text != null) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		Item it;
		string text = HitBtn(e.Location, out it);
		if (text != null && e.Button == MouseButtons.Left)
		{
			if (text.EndsWith(":roll"))
			{
				this.RollbackClicked?.Invoke(it);
			}
			else
			{
				this.DeleteClicked?.Invoke(it);
			}
		}
		base.OnMouseClick(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (_hot != -1 || _hotBtn != null)
		{
			_hot = -1;
			_hotBtn = null;
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
