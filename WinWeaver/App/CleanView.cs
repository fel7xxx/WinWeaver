using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace WinWeaver;

internal class CleanView : Card
{
	public class Cat
	{
		public string Id;

		public string Name;

		public string Desc;

		public double Size = -1.0;

		public bool On;

		public Rectangle R;
	}

	private static readonly string[][] Known = new string[10][]
	{
		new string[4] { "temp", "Временные файлы", "Папка TEMP твоего профиля", "1" },
		new string[4] { "wintemp", "Временные файлы Windows", "Windows\\Temp и остатки установщиков", "1" },
		new string[4] { "wucache", "Кэш обновлений", "Уже установленные пакеты Центра обновления", "1" },
		new string[4] { "docache", "Оптимизация доставки", "Файлы, которые Windows раздаёт другим ПК", "1" },
		new string[4] { "dumps", "Дампы и отчёты об ошибках", "Снимки памяти после сбоев и отчёты Windows", "1" },
		new string[4] { "logs", "Журналы установки", "Старые журналы CBS, DISM и обновлений", "1" },
		new string[4] { "thumbs", "Эскизы картинок", "Пересоздадутся сами при открытии папок", "0" },
		new string[4] { "shader", "Кэш шейдеров", "DirectX, NVIDIA и AMD — игры соберут его заново", "0" },
		new string[4] { "browser", "Кэш браузеров", "Пароли, вкладки и история не трогаются", "0" },
		new string[4] { "recycle", "Корзина", "Удалённые файлы во всех корзинах", "0" }
	};

	public readonly List<Cat> Cats = new List<Cat>();

	private readonly FButton _clean;

	private readonly FButton _rescan;

	private double _free = -1.0;

	private double _size;

	private string _drive = "C:";

	private bool _scanned;

	private DateTime _scanAt = DateTime.MinValue;

	public bool Dirty = true;

	public string LastResult = "";

	private int _hot = -1;

	private Rectangle _barR;

	private Rectangle _headR;

	private int _listTop;

	private readonly Tween _barT;

	private int RowH => Theme.S(52f);

	public double Selected
	{
		get
		{
			double num = 0.0;
			foreach (Cat cat in Cats)
			{
				if (cat.On && cat.Size > 0.0)
				{
					num += cat.Size;
				}
			}
			return num;
		}
	}

	public event Action CleanClicked;

	public event Action RescanClicked;

	public CleanView()
	{
		string[][] known = Known;
		foreach (string[] array in known)
		{
			Cats.Add(new Cat
			{
				Id = array[0],
				Name = array[1],
				Desc = array[2],
				On = (array[3] == "1")
			});
		}
		_barT = new Tween(this, 0f, 0.12f).Timed(800);
		_clean = new FButton
		{
			Text = "Очистить выбранное",
			Kind = BtnKind.Silver,
			Font = Theme.Body
		};
		_clean.AutoWidth(Theme.S(36f));
		_clean.Click += delegate
		{
			this.CleanClicked?.Invoke();
		};
		_rescan = new FButton
		{
			Text = "Пересканировать",
			Kind = BtnKind.Ghost,
			Font = Theme.Body,
			Glyph = "\ue72c"
		};
		_rescan.AutoWidth(Theme.S(30f));
		_rescan.Click += delegate
		{
			this.RescanClicked?.Invoke();
		};
		base.Controls.Add(_clean);
		base.Controls.Add(_rescan);
	}

	public static string Fmt(double b)
	{
		CultureInfo culture = L.Culture;
		if (b < 0.0)
		{
			return "…";
		}
		if (b >= 1073741824.0)
		{
			return (b / 1073741824.0).ToString("0.0", culture) + " ГБ";
		}
		if (b >= 1048576.0)
		{
			return Math.Round(b / 1048576.0).ToString(culture) + " МБ";
		}
		if (b > 0.0)
		{
			return Math.Max(1.0, Math.Round(b / 1024.0)).ToString(culture) + " КБ";
		}
		return "пусто";
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

	public void SetScanning()
	{
		Dirty = true;
		_scanned = false;
		foreach (Cat cat in Cats)
		{
			cat.Size = -1.0;
		}
		_clean.Enabled = false;
		Skeleton.Track(this, loading: true);
		Invalidate();
	}

	/// <summary>Во время фонового чтения разделов долгий скан диска пропускается —
	/// он идёт отдельно и показывает прогресс только на своей странице.</summary>
	public volatile bool Deferred;

	public override object Fetch(IEngine e)
	{
		object obj = e.Eval("Get-FDiskInfo");
		object obj2 = null;
		if (!Deferred && (Dirty || (DateTime.Now - _scanAt).TotalSeconds > 90.0))
		{
			obj2 = e.Eval("Get-FCleanScan");
		}
		return new object[2] { obj, obj2 };
	}

	public override void Apply(object v)
	{
		if (!(v is object[] array))
		{
			return;
		}
		if (array[0] is Hashtable hashtable)
		{
			_free = D(hashtable["Free"]);
			_size = D(hashtable["Size"]);
			_drive = (hashtable["Drive"] ?? "C:").ToString();
			_barT.Target = ((_size > 0.0) ? ((float)(1.0 - _free / _size)) : 0f);
		}
		object[] array2 = array[1] as object[];
		if (array2 == null && array[1] is Hashtable)
		{
			array2 = new object[1] { array[1] };
		}
		if (array2 != null)
		{
			object[] array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				if (!(array3[i] is Hashtable hashtable2))
				{
					continue;
				}
				string text = (hashtable2["Id"] ?? "").ToString();
				foreach (Cat cat in Cats)
				{
					if (cat.Id == text)
					{
						cat.Size = D(hashtable2["Size"]);
					}
				}
			}
			_scanned = true;
			Skeleton.Track(this, loading: false);
			Dirty = false;
			_scanAt = DateTime.Now;
		}
		_clean.Enabled = _scanned;
		Invalidate();
	}

	public override int DoLayout(int x, int y, int w)
	{
		int num = base.Pad + Theme.S(2f);
		_headR = new Rectangle(num, num, w - num * 2, Theme.S(56f));
		_barR = new Rectangle(num, _headR.Bottom + Theme.S(12f), w - num * 2, Theme.S(10f));
		int num2 = _barR.Bottom + Theme.S(18f);
		_clean.SetBounds(num, num2, _clean.Width, Theme.S(36f));
		_rescan.SetBounds(_clean.Right + Theme.S(8f), num2, _rescan.Width, Theme.S(36f));
		_listTop = num2 + Theme.S(36f) + Theme.S(18f);
		int num3 = ((w - num * 2 < Theme.S(760f)) ? 1 : 2);
		int num4 = Theme.S(12f);
		int num5 = (w - num * 2 - num4 * (num3 - 1)) / num3;
		for (int i = 0; i < Cats.Count; i++)
		{
			int num6 = i % num3;
			int num7 = i / num3;
			Cats[i].R = new Rectangle(num + num6 * (num5 + num4), _listTop + num7 * (RowH + Theme.S(6f)), num5, RowH);
		}
		int num8 = (Cats.Count + num3 - 1) / num3;
		int num9 = _listTop + num8 * (RowH + Theme.S(6f)) - Theme.S(6f) + num;
		SetBounds(x, y, w, num9);
		Invalidate();
		return num9;
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		Rectangle r = new Rectangle(_headR.X, _headR.Y, Theme.S(52f), Theme.S(52f));
		Theme.IconBox(g, r, "\ueda2", bright: true, 0f, Theme.Icon);
		int num = r.Right + Theme.S(16f);
		string text = Fmt(Selected);
		string text2 = (_scanned ? text : "считаю…");
		using (Font font = Theme.F(20f, semi: true))
		{
			Size size = Theme.Measure(text2, font);
			int num2 = _headR.Right - size.Width;
			TextRenderer.DrawText(g, "МОЖНО ОСВОБОДИТЬ", Theme.Tiny, new Rectangle(num2 - Theme.S(60f), _headR.Y + Theme.S(2f), size.Width + Theme.S(60f), Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine);
			TextRenderer.DrawText(g, text2, font, new Point(num2, _headR.Y + Theme.S(18f)), _scanned ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int width = num2 - Theme.S(60f) - num;
			using (Font font2 = Theme.F(15f, semi: true))
			{
				TextRenderer.DrawText(g, "Диск " + _drive, font2, new Rectangle(num, _headR.Y + Theme.S(4f), width, Theme.S(26f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			}
			string text3 = ((_free < 0.0) ? "проверяю…" : (Fmt(_free) + " свободно из " + Fmt(_size) + ((LastResult != "") ? ("  •  " + LastResult) : "")));
			TextRenderer.DrawText(g, text3, Theme.Small, new Rectangle(num, _headR.Y + Theme.S(32f), width, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		}
		using (GraphicsPath path = Theme.Round(_barR, (float)_barR.Height / 2f))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			float num3 = Anim.Clamp(_barT.Value);
			float num4 = Math.Max(_barR.Height, (float)_barR.Width * num3);
			RectangleF r2 = new RectangleF(_barR.X, _barR.Y, num4, _barR.Height);
			Color c = ((num3 > 0.9f) ? Theme.Red : ((num3 > 0.8f) ? Theme.Yellow : Theme.Silver2));
			using (GraphicsPath path2 = Theme.Round(r2, (float)_barR.Height / 2f))
			{
				Theme.FillGradient(g, path2, r2, Theme.Silver1, c, 0f);
			}
			if (_scanned && _size > 0.0 && Selected > 0.0)
			{
				float val = (float)((double)_barR.Width * Math.Min(num3, Selected / _size));
				val = Math.Max(Theme.S(4f), val);
				RectangleF rect = new RectangleF((float)_barR.X + num4 - val, _barR.Y, val, _barR.Height);
				Region clip = g.Clip;
				g.SetClip(path, CombineMode.Intersect);
				using (SolidBrush brush2 = new SolidBrush(Theme.Green))
				{
					g.FillRectangle(brush2, rect);
				}
				g.Clip = clip;
				clip.Dispose();
			}
		}
		for (int i = 0; i < Cats.Count; i++)
		{
			PaintCat(g, Cats[i], i == _hot);
		}
	}

	private void PaintCat(Graphics g, Cat c, bool hot)
	{
		Rectangle r = c.R;
		using (GraphicsPath path = Theme.Round(r, Theme.S(10f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(hot ? 24 : (c.On ? 16 : 8), 255, 255, 255)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(c.On ? 46 : 26, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		Rectangle rectangle = new Rectangle(r.X + Theme.S(14f), r.Y + (r.Height - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
		using (GraphicsPath path2 = Theme.Round(rectangle, Theme.S(4f)))
		{
			using (SolidBrush brush2 = new SolidBrush(c.On ? Theme.Silver1 : Theme.Control2))
			{
				g.FillPath(brush2, path2);
			}
			using Pen pen2 = new Pen(c.On ? Theme.Silver1 : Theme.BorderHi);
			g.DrawPath(pen2, path2);
		}
		if (c.On)
		{
			using Pen pen3 = new Pen(Theme.OnSilver, Math.Max(1.6f, Theme.S(2f)))
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round,
				LineJoin = LineJoin.Round
			};
			g.DrawLines(pen3, new PointF[3]
			{
				new PointF((float)rectangle.X + (float)rectangle.Width * 0.24f, (float)rectangle.Y + (float)rectangle.Height * 0.52f),
				new PointF((float)rectangle.X + (float)rectangle.Width * 0.43f, (float)rectangle.Y + (float)rectangle.Height * 0.7f),
				new PointF((float)rectangle.X + (float)rectangle.Width * 0.77f, (float)rectangle.Y + (float)rectangle.Height * 0.32f)
			});
		}
		string text = ((c.Size < 0.0) ? "" : Fmt(c.Size));
		int num = ((c.Size < 0.0) ? Theme.S(56f) : (Theme.Measure(text, Theme.Body).Width + Theme.S(4f)));
		if (c.Size < 0.0)
		{
			Skeleton.Bar(g, new RectangleF(r.Right - Theme.S(14f) - num, r.Y + r.Height / 2 - Theme.S(6f), num, Theme.S(12f)), this);
		}
		int num2 = rectangle.Right + Theme.S(12f);
		int width = r.Right - Theme.S(14f) - num - num2 - Theme.S(8f);
		TextRenderer.DrawText(g, c.Name, Theme.Body, new Rectangle(num2, r.Y + Theme.S(7f), width, Theme.S(20f)), c.On ? Theme.Text : Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, c.Desc, Theme.Tiny, new Rectangle(num2, r.Y + Theme.S(27f), width, Theme.S(17f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		Color fore = ((c.Size >= 1073741824.0) ? Theme.Yellow : ((c.Size > 0.0) ? Theme.Text : Theme.Dim));
		TextRenderer.DrawText(g, text, Theme.Body, new Rectangle(r.Right - Theme.S(14f) - num, r.Y, num, r.Height), fore, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	private int Hit(Point p)
	{
		for (int i = 0; i < Cats.Count; i++)
		{
			if (Cats[i].R.Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = Hit(e.Location);
		if (num != _hot)
		{
			_hot = num;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		int num = Hit(e.Location);
		if (num >= 0 && e.Button == MouseButtons.Left)
		{
			Cats[num].On = !Cats[num].On;
			Invalidate();
		}
		base.OnMouseClick(e);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Skeleton.Track(this, !_scanned);
	}
}
