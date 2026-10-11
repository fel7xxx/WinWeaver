using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class TilesView : Control, IBlock
{
	public class Tile
	{
		public string Glyph;

		public string Title;

		public string Desc;

		public string Btn = "Исправить";

		public string Hint;

		public string Confirm;

		public string ConfirmTitle;

		public Func<IEngine, object> Work;

		public Action Ui;

		public Action<object> Done;

		public bool Danger;

		public Rectangle R;

		public Rectangle BtnR;

		public int State;

		public DateTime DoneAt;
	}

	private readonly Host _host;

	public readonly List<Tile> Items = new List<Tile>();

	public int MinColW = 300;

	public int TileH = 138;

	private int _hot = -1;

	private bool _hotBtn;

	private readonly Timer _spin;

	private float _phase;

	private readonly Tween _hotT;

	public TilesView(Host host)
	{
		_host = host;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		_hotT = new Tween(this, 0f, 0.25f);
		_spin = new Timer
		{
			Interval = 33
		};
		_spin.Tick += delegate
		{
			_phase = (float)(Anim.Now % 1320.0 / 1320.0);
			bool flag = false;
			bool flag2 = false;
			foreach (Tile item in Items)
			{
				if (item.State == 1)
				{
					flag = true;
				}
				else if (item.State >= 2)
				{
					if ((DateTime.Now - item.DoneAt).TotalSeconds > 6.0)
					{
						item.State = 0;
						Invalidate();
					}
					else
					{
						flag2 = true;
					}
				}
			}
			if (flag)
			{
				Invalidate();
			}
			else if (!flag2)
			{
				_spin.Stop();
			}
		};
	}

	public Tile Add(string glyph, string title, string desc, string hint, Func<IEngine, object> work, string btn = "Исправить")
	{
		Tile tile = new Tile
		{
			Glyph = glyph,
			Title = title,
			Desc = desc,
			Hint = hint,
			Work = work,
			Btn = btn
		};
		Items.Add(tile);
		return tile;
	}

	public Tile AddUi(string glyph, string title, string desc, string hint, Action ui, string btn = "Открыть")
	{
		Tile tile = new Tile
		{
			Glyph = glyph,
			Title = title,
			Desc = desc,
			Hint = hint,
			Ui = ui,
			Btn = btn
		};
		Items.Add(tile);
		return tile;
	}

	public int DoLayout(int x, int y, int w)
	{
		int num = Theme.S(14f);
		int num2 = Math.Max(1, Math.Min(Items.Count, (w + num) / (Theme.S(MinColW) + num)));
		int num3 = (w - num * (num2 - 1)) / num2;
		int num4 = Theme.S(TileH);
		for (int i = 0; i < Items.Count; i++)
		{
			int num5 = i % num2;
			int num6 = i / num2;
			Items[i].R = new Rectangle(num5 * (num3 + num), num6 * (num4 + num), (num5 == num2 - 1) ? (w - (num3 + num) * (num2 - 1)) : num3, num4);
		}
		int num7 = (Items.Count + num2 - 1) / num2;
		int num8 = Math.Max(1, num7 * (num4 + num) - num);
		SetBounds(x, y, w, num8);
		Invalidate();
		return num8;
	}

	public object Fetch(IEngine e)
	{
		return null;
	}

	public void Apply(object v)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		for (int i = 0; i < Items.Count; i++)
		{
			PaintTile(graphics, Items[i], i == _hot);
		}
	}

	private void PaintTile(Graphics g, Tile t, bool hot)
	{
		Rectangle r = t.R;
		float num = (hot ? _hotT.Value : 0f);
		Theme.Card(g, r, num);
		int num2 = Theme.S(18f);
		int num3 = Theme.S(38f);
		Rectangle r2 = new Rectangle(r.X + num2, r.Y + num2, num3, num3);
		Color color = ((t.State == 2) ? Theme.Green : ((t.State == 3) ? Theme.Red : Theme.Silver1));
		Theme.IconBox(g, r2, t.Glyph, bright: false, num, Theme.IconSmall);
		int num4 = r2.Right + Theme.S(12f);
		int width = r.Right - num2 - num4;
		TextRenderer.DrawText(g, t.Title, Theme.Title, new Rectangle(num4, r2.Y - Theme.S(1f), width, Theme.S(22f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		TextRenderer.DrawText(g, t.Desc ?? "", Theme.Small, new Rectangle(num4, r2.Y + Theme.S(21f), width, Theme.S(36f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		string text = ((t.State == 1) ? "Работаю…" : ((t.State == 2) ? "Готово" : ((t.State == 3) ? "Не вышло" : t.Btn)));
		int num5 = Math.Max(Theme.S(96f), Theme.Measure(text, Theme.Small).Width + Theme.S(34f));
		int num6 = Theme.S(32f);
		t.BtnR = new Rectangle(r.Right - num2 - num5, r.Bottom - num2 - num6 + Theme.S(4f), num5, num6);
		bool flag = hot && _hotBtn && t.State == 0;
		using (GraphicsPath path = Theme.Round(t.BtnR, (float)num6 / 2f))
		{
			if (t.State == 1)
			{
				using (SolidBrush brush = new SolidBrush(Theme.Control2))
				{
					g.FillPath(brush, path);
				}
				Region clip = g.Clip;
				g.SetClip(path, CombineMode.Intersect);
				float num7 = (float)num5 * 0.45f;
				float num8 = (float)t.BtnR.X - num7 + ((float)num5 + num7) * _phase;
				using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(num8 - 1f, t.BtnR.Y, num7 + 2f, num6), Color.Transparent, Color.Transparent, 0f))
				{
					ColorBlend colorBlend = new ColorBlend();
					colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
					colorBlend.Colors = new Color[3]
					{
						Color.FromArgb(0, 255, 255, 255),
						Color.FromArgb(50, 255, 255, 255),
						Color.FromArgb(0, 255, 255, 255)
					};
					linearGradientBrush.InterpolationColors = colorBlend;
					g.FillRectangle(linearGradientBrush, num8, t.BtnR.Y, num7, num6);
				}
				g.Clip = clip;
				using Pen pen = new Pen(Theme.BorderHi);
				g.DrawPath(pen, path);
			}
			else if (t.State >= 2)
			{
				using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(30, color)))
				{
					g.FillPath(brush2, path);
				}
				using Pen pen2 = new Pen(Color.FromArgb(120, color));
				g.DrawPath(pen2, path);
			}
			else
			{
				Color c = (t.Danger ? Color.FromArgb(70, 44, 46) : (flag ? Theme.Mix(Theme.Control1, Color.White, 0.14f) : Theme.Control1));
				Color c2 = (t.Danger ? Color.FromArgb(52, 34, 36) : Theme.Control2);
				Theme.FillGradient(g, path, t.BtnR, c, c2);
				using Pen pen3 = new Pen(flag ? Theme.BorderHi : Theme.Border);
				g.DrawPath(pen3, path);
			}
		}
		Color fore = ((t.State >= 2) ? color : ((t.State == 1) ? Theme.Sub : (t.Danger ? Theme.Red : Theme.Text)));
		TextRenderer.DrawText(g, text, Theme.Small, t.BtnR, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		if (!string.IsNullOrEmpty(t.Hint))
		{
			int num9 = r.X + num2;
			int num10 = t.BtnR.Left - Theme.S(10f) - num9;
			int num11 = Theme.S(5f);
			int num12 = t.BtnR.Y + num6 / 2;
			using (SolidBrush brush3 = new SolidBrush(Theme.Dim))
			{
				g.FillEllipse(brush3, num9, num12 - num11 / 2, num11, num11);
			}
			TextRenderer.DrawText(g, t.Hint, Theme.Tiny, new Rectangle(num9 + num11 + Theme.S(7f), num12 - Theme.S(9f), Math.Max(10, num10 - num11 - Theme.S(7f)), Theme.S(18f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	private void RunTile(Tile t)
	{
		if (t.State == 1 || (t.Confirm != null && !WDialog.Ask(FindForm(), t.ConfirmTitle ?? (t.Title + "?"), t.Confirm, t.Btn, "Отмена", DlgIcon.Warn)))
		{
			return;
		}
		if (t.Ui != null)
		{
			t.Ui();
		}
		else if (t.Work != null && _host.TryBegin(quick: false, t.Title))
		{
			t.State = 1;
			_spin.Start();
			Invalidate();
			_host.Worker.Run((IEngine e) => t.Work(e), delegate(object r)
			{
				t.State = ((r is bool && !(bool)r) ? 3 : 2);
				t.DoneAt = DateTime.Now;
				_spin.Start();
				Invalidate();
				_host.End();
				t.Done?.Invoke(r);
			});
		}
	}

	private int Hit(Point p, out bool onBtn)
	{
		onBtn = false;
		for (int i = 0; i < Items.Count; i++)
		{
			if (Items[i].R.Contains(p))
			{
				onBtn = Items[i].BtnR.Contains(p);
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		bool onBtn;
		int num = Hit(e.Location, out onBtn);
		if (num != _hot || onBtn != _hotBtn)
		{
			if (num != _hot)
			{
				_hotT.Snap(0f);
				_hotT.Target = 1f;
			}
			_hot = num;
			_hotBtn = onBtn;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = -1;
		_hotBtn = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		bool onBtn;
		int num = Hit(e.Location, out onBtn);
		if (num >= 0 && e.Button == MouseButtons.Left)
		{
			RunTile(Items[num]);
		}
		base.OnMouseClick(e);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_spin.Dispose();
		}
		base.Dispose(disposing);
	}
}
