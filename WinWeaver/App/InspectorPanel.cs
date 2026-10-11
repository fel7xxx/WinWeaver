using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class InspectorPanel : Control
{
	private readonly Page _page;

	public SettingCard Card;

	private int _scroll;

	private int _max;

	private int _titleMid;

	private int _boost0 = -100000;

	private int _blink = -1;

	private Rectangle _bTech;

	private Rectangle _bRec;

	private int _hot = -1;

	private readonly Timer _tick;

	private readonly Tween _fade;

	private Bitmap _cache;

	private bool _dirty = true;

	private Rectangle _cursorR;

	private PointF _cursorAt;

	private Bitmap _body;

	private bool _bodyDirty = true;

	private PointF _bodyCursor = new PointF(float.NaN, float.NaN);

	public static int Strip => Theme.S(44f);

	private static int PadX => Theme.S(26f);

	private static int PadTop => Theme.S(24f);

	private static int PadBot => Theme.S(20f);

	private static int BtnH => Theme.S(46f);

	private Rectangle CardR => new Rectangle(Strip, 0, Math.Max(1, base.Width - Strip), base.Height);

	public InspectorPanel(Page page)
	{
		_page = page;
		SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
		_fade = new Tween(this, 1f, 0.2f, Redraw).Timed(260);
		_tick = new Timer
		{
			Interval = 33
		};
		_tick.Tick += delegate
		{
			if (base.Visible && Card != null && !base.IsDisposed)
			{
				Invalidate(new Rectangle(0, 0, Strip + Theme.S(2f), base.Height));
				int num = Environment.TickCount / 450 % 2;
				if (num != _blink)
				{
					_blink = num;
					if (_cursorR.Width > 0)
					{
						Invalidate(_cursorR);
					}
				}
			}
		};
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		base.OnVisibleChanged(e);
		_tick.Enabled = base.Visible && Anim.Enabled;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_tick.Dispose();
			_cache?.Dispose();
			_body?.Dispose();
		}
		base.Dispose(disposing);
	}

	public void Dirty()
	{
		_bodyDirty = true;
		_dirty = true;
		Invalidate();
	}

	private void Redraw()
	{
		_dirty = true;
		Invalidate();
	}

	protected override void OnResize(EventArgs e)
	{
		_dirty = true;
		base.OnResize(e);
	}

	public void Show(SettingCard c)
	{
		if (Card != c)
		{
			if (Card != null && Card.InspectorHost == this)
			{
				Card.InspectorHost = null;
			}
			Card = c;
			_scroll = 0;
			_hot = -1;
			if (c != null)
			{
				c.InspectorHost = this;
				c.PanelOpened();
			}
			_boost0 = Environment.TickCount;
			_dirty = true;
			_bodyDirty = true;
			if (Anim.Enabled && base.Visible)
			{
				_fade.Snap(0.35f);
				_fade.Target = 1f;
			}
			Invalidate();
		}
	}

	public int DesiredH(int width)
	{
		if (Card == null)
		{
			return Theme.S(300f);
		}
		int iw = width - Strip - PadX * 2;
		return PadTop + Card.PanelHeadH(iw) + Theme.S(14f) + Card.BodyH(iw, panel: true) + Theme.S(18f) + BtnH + PadBot;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		// Avalonia: рисуем сразу в контекст (команды кэширует композитор на GPU);
		// промежуточный битмап перерисовывался бы целиком на каждом кадре прокрутки.
		_dirty = false;
		{
			var st = graphics.Save();
			Render(graphics);
			graphics.Restore(st);
		}
		if (Card != null)
		{
			Theme.Smooth(graphics);
			if (_cursorR.Width > 0 && _blink == 0)
			{
				Theme.Dot(graphics, _cursorAt.X, _cursorAt.Y, Theme.S(3f), Theme.Accent, 0.7f);
			}

		}
	}

	private void Render(Graphics g)
	{
		g.Clear(Theme.Bg);
		_cursorR = Rectangle.Empty;
		Theme.Smooth(g);
		Rectangle cardR = CardR;
		Theme.Card(g, cardR, 0f, 17f);
		if (Card == null)
		{
			TextRenderer.DrawText(g, Theme.Up("Выбери параметр слева"), Theme.Tiny, cardR, Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			return;
		}
		int num = cardR.X + PadX;
		int num2 = cardR.Width - PadX * 2;
		int num3 = Card.PanelHead(g, num, PadTop, num2, out _titleMid);
		int num4 = PadTop + num3 + Theme.S(14f);
		int num5 = base.Height - PadBot - BtnH;
		int num6 = num5 - Theme.S(12f);
		int num7 = Card.BodyH(num2, panel: true);
		_max = Math.Max(0, num7 + Theme.S(6f) - (num6 - num4));
		_scroll = Math.Max(0, Math.Min(_max, _scroll));
		if (num6 > num4)
		{
			int num8 = cardR.Width - 4;
			int num9 = num7 + Theme.S(8f);
			if (_body == null || _bodyDirty || _body.Width != num8 || _body.Height != num9)
			{
				if (_body == null || _body.Width != num8 || _body.Height != num9)
				{
					_body?.Dispose();
					_body = new Bitmap(Math.Max(1, num8), Math.Max(1, num9), PixelFormat.Format32bppRgb);
				}
				_bodyDirty = false;
				using (Graphics graphics = Graphics.FromImage(_body))
				{
					graphics.Clear(Theme.Card1);
					Card.PaintBody(graphics, num - (cardR.X + 2), 0, num2, panel: true);
				}
				_bodyCursor = Card.CursorAt;
			}
			Region clip = g.Clip;
			g.SetClip(new Rectangle(cardR.X + 2, num4, cardR.Width - 4, num6 - num4), CombineMode.Intersect);
			int num10 = Math.Min(num6 - num4, Math.Max(0, _body.Height - _scroll));
			if (num10 > 0)
			{
				g.DrawImage(_body, new Rectangle(cardR.X + 2, num4, num8, num10), new Rectangle(0, _scroll, num8, num10), GraphicsUnit.Pixel);
			}
			PointF cursorAt = (float.IsNaN(_bodyCursor.X) ? _bodyCursor : new PointF(_bodyCursor.X + (float)cardR.X + 2f, _bodyCursor.Y + (float)num4 - (float)_scroll));
			if (!float.IsNaN(cursorAt.X) && cursorAt.Y > (float)num4 && cursorAt.Y < (float)num6)
			{
				_cursorAt = cursorAt;
				int num11 = Theme.S(12f);
				_cursorR = new Rectangle((int)cursorAt.X - num11, (int)cursorAt.Y - num11, num11 * 2, num11 * 2);
			}
			int num12 = Theme.S(18f);
			if (_scroll > 0)
			{
				using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(cardR.X, num4 - 1, cardR.Width, num12 + 2), Theme.Card1, Color.FromArgb(0, Theme.Card1), 90f);
				g.FillRectangle(brush, cardR.X + 2, num4, cardR.Width - 4, num12);
			}
			if (_scroll < _max)
			{
				using LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(cardR.X, num6 - num12 - 1, cardR.Width, num12 + 2), Color.FromArgb(0, Theme.Card1), Theme.Card1, 90f);
				g.FillRectangle(brush2, cardR.X + 2, num6 - num12, cardR.Width - 4, num12);
			}
			g.Clip = clip;
			clip.Dispose();
			if (_max > 0)
			{
				int trackH = num6 - num4 - Theme.S(8f);
				int thumbH = Math.Max(Theme.S(28f), (int)((float)trackH * (float)(num6 - num4) / (float)(num6 - num4 + _max)));
				float ty = (float)(num4 + Theme.S(4f)) + (float)(trackH - thumbH) * ((float)_scroll / (float)_max);
				using GraphicsPath tp = Theme.Round(new RectangleF(cardR.Right - Theme.S(8f), ty, Theme.S(4f), thumbH), Theme.S(2f));
				using SolidBrush brush3 = new SolidBrush(Color.FromArgb(60, 60, 68));
				g.FillPath(brush3, tp);
			}
		}
		int num17 = Theme.S(10f);
		int num18 = (num2 - num17) / 2;
		_bTech = new Rectangle(num, num5, num18, BtnH);
		_bRec = new Rectangle(num + num18 + num17, num5, num2 - num18 - num17, BtnH);
		bool panelHasTech = Card.PanelHasTech;
		SettingCard.PillButton(g, _bTech, Theme.Up(Card.PanelTech ? "Проще" : "Подробнее"), Card.PanelTech, _hot == 0, panelHasTech);
		bool enabled;
		string s = Card.PanelRecButton(out enabled);
		SettingCard.PillButton(g, _bRec, Theme.Up(s), on: false, _hot == 1, enabled, primary: true);

		if (_fade.Value < 0.999f)
		{
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb((int)(255f * (1f - _fade.Value)), Theme.Card1)))
			{
				g.FillRectangle(brush4, cardR.X + 2, PadTop + Theme.S(28f), cardR.Width - 4, num5 - PadTop - Theme.S(34f));
			}
			_cursorR = Rectangle.Empty;
		}
	}

	private void PaintConnector(Graphics g)
	{
		Control parent = Card.Parent;
		if (parent == null || !Card.Visible)
		{
			return;
		}
		Rectangle listRect = _page.ListRect;
		int num = parent.Top + Card.Top + Card.Height / 2 - base.Top;
		int val = parent.Top + listRect.Top - base.Top + Theme.S(8f);
		int val2 = parent.Top + listRect.Bottom - base.Top - Theme.S(8f);
		val = Math.Max(val, Theme.S(8f) - base.Top);
		val2 = Math.Min(val2, _page.Height - base.Top - Theme.S(8f));
		bool num2 = num > val && num < val2;
		num = Math.Max(val, Math.Min(val2, num));
		num = Math.Max(Theme.S(6f), Math.Min(base.Height - Theme.S(6f), num));
		float u = Theme.S(1f);
		float arg = 2f * u;
		float num3 = 20f * u;
		float num4 = 42f * u;
		float num5 = _titleMid;
		List<PointF> pts = new List<PointF>();
		Action<float, float, float, float> obj = delegate(float x0, float y0, float x1, float y1)
		{
			float num12 = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
			int num13 = Math.Max(1, (int)Math.Round(num12 / (5f * u)));
			for (int j = 0; j < num13; j++)
			{
				pts.Add(new PointF(x0 + (x1 - x0) * (float)j / (float)num13, y0 + (y1 - y0) * (float)j / (float)num13));
			}
		};
		obj(arg, num, num3, num);
		obj(num3, num, num3, num5);
		obj(num3, num5, num4, num5);
		pts.Add(new PointF(num4, num5));
		float num6 = (num2 ? 1f : 0.35f);
		int tickCount = Environment.TickCount;
		float num7 = Math.Max(0f, 1f - (float)(tickCount - _boost0) / 700f);
		float num8 = (Anim.Enabled ? ((float)tickCount / 14f % (float)(pts.Count + 24)) : (-100f));
		float num9 = 1.25f * u;
		for (int i = 0; i < pts.Count; i++)
		{
			float num10 = num8 - (float)i;
			float num11 = 0.2f;
			if (num10 >= 0f && num10 < 9f)
			{
				num11 = Math.Max(num11, 0.95f * (1f - num10 / 9f));
			}
			if (num7 > 0f && (float)i / (float)pts.Count < 1f - num7)
			{
				num11 = Math.Max(num11, 0.9f * num7 + 0.2f);
			}
			num11 = Math.Min(1f, num11) * num6;
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)(255f * num11), 255, 255, 255));
			g.FillEllipse(brush, pts[i].X - num9, pts[i].Y - num9, num9 * 2f, num9 * 2f);
		}
		Color c = Color.FromArgb((int)(255f * num6), Theme.Accent);
		Theme.Dot(g, 3f * u, num, 2.6f * u, c, num6);
		Theme.Dot(g, 41f * u, num5, 2.6f * u, c, num6);
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if (_max > 0)
		{
			int num = -Page.WheelPx(e.Delta);
			int num2 = Math.Max(0, Math.Min(_max, _scroll + num));
			if (num2 != _scroll)
			{
				_scroll = num2;
				Redraw();
			}
		}
		else
		{
			_page.SmoothBy(-Page.WheelPx(e.Delta));
		}
		if (e is HandledMouseEventArgs)
		{
			((HandledMouseEventArgs)e).Handled = true;
		}
	}

	private int Hit(Point p)
	{
		if (Card == null)
		{
			return -1;
		}
		if (_bTech.Contains(p) && Card.PanelHasTech)
		{
			return 0;
		}
		Card.PanelRecButton(out var enabled);
		if (_bRec.Contains(p) && enabled)
		{
			return 1;
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
			Redraw();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (_hot != -1)
		{
			_hot = -1;
			Redraw();
		}
		base.OnMouseLeave(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Left && Card != null)
		{
			switch (Hit(e.Location))
			{
			case 0:
				Card.PanelToggleTech();
				_scroll = 0;
				Dirty();
				break;
			case 1:
				Card.PanelApplyRec();
				Dirty();
				break;
			}
		}
	}
}
