using System;

namespace WinWeaver;

internal class TabStrip : Control, IBlock
{
	public readonly string[] Items;

	public int Selected;

	private int _hover = -1;

	private Rectangle[] _r = new Rectangle[0];

	private readonly Tween _pos;

	private readonly Tween _hov;

	private int[] _counts;

	private bool[] _pend;

	private static Font TabF => Theme.FC(8f);

	public event Action<int> Changed;

	public TabStrip(string[] items)
	{
		Items = items ?? new string[0];
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		_pos = new Tween(this, 0f, 0.24f);
		_hov = new Tween(this, 0f, 0.25f);
	}

	public void Select(int i)
	{
		Selected = i;
		_pos.Target = i;
		Invalidate();
	}

	public void SetCounts(int[] counts, bool[] pend)
	{
		bool flag = _counts != null && _counts.Length == counts.Length;
		if (flag)
		{
			for (int i = 0; i < counts.Length; i++)
			{
				if (_counts[i] != counts[i] || _pend[i] != pend[i])
				{
					flag = false;
					break;
				}
			}
		}
		if (!flag)
		{
			_counts = counts;
			_pend = pend;
			Invalidate();
		}
	}

	private string CountText(int i)
	{
		if (_counts == null || i >= _counts.Length || _counts[i] <= 0)
		{
			return "";
		}
		return _counts[i].ToString();
	}

	public int DoLayout(int x, int y, int w)
	{
		int num = Theme.S(40f);
		int num2 = Theme.S(2f);
		_r = new Rectangle[Items.Length];
		int num3 = 0;
		for (int i = 0; i < Items.Length; i++)
		{
			int num4 = Theme.Measure(Theme.Up(Items[i]), TabF).Width + Theme.S(32f);
			string text = CountText(i);
			if (text.Length > 0)
			{
				num4 += Theme.Measure(text, Theme.Tiny).Width + Theme.S(8f);
			}
			if (_pend != null && i < _pend.Length && _pend[i])
			{
				num4 += Theme.S(12f);
			}
			_r[i] = new Rectangle(num3, num2, num4, num - num2 * 2);
			num3 += num4 + Theme.S(6f);
		}
		SetBounds(x, y, Math.Min(w, Math.Max(1, num3 - Theme.S(6f) + Theme.S(2f))), num);
		Invalidate();
		return num + Theme.S(4f);
	}

	private RectangleF Pill()
	{
		if (_r.Length == 0)
		{
			return RectangleF.Empty;
		}
		float num = Math.Max(0f, Math.Min(_r.Length - 1, _pos.Value));
		int num2 = (int)Math.Floor(num);
		int num3 = Math.Min(_r.Length - 1, num2 + 1);
		float num4 = num - (float)num2;
		Rectangle rectangle = _r[num2];
		Rectangle rectangle2 = _r[num3];
		return new RectangleF((float)rectangle.X + (float)(rectangle2.X - rectangle.X) * num4, rectangle.Y, (float)rectangle.Width + (float)(rectangle2.Width - rectangle.Width) * num4, rectangle.Height);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		if (!(base.Parent is Canvas))
		{
			FButton.PaintParent(this, graphics);
		}
		else
		{
			graphics.Clear(Theme.Card1);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		if (_hover >= 0 && _hover < _r.Length && _hover != Selected)
		{
			using GraphicsPath path = Theme.Round(new RectangleF((float)_r[_hover].X + 0.75f, (float)_r[_hover].Y + 0.75f, (float)_r[_hover].Width - 1.5f, (float)_r[_hover].Height - 1.5f), Theme.S(9f));
			using SolidBrush brush = new SolidBrush(Color.FromArgb(8, 255, 255, 255));
			graphics.FillPath(brush, path);
		}
		RectangleF rectangleF = Pill();
		if (rectangleF.Width > 0f)
		{
			RectangleF r = new RectangleF(rectangleF.X + 0.75f, rectangleF.Y + 0.75f, rectangleF.Width - 1.5f, rectangleF.Height - 1.5f);
			using GraphicsPath path2 = Theme.Round(r, Theme.S(9f));
			using (SolidBrush pb = new SolidBrush(Color.FromArgb(31, 31, 37))) graphics.FillPath(pb, path2);
			using Pen pen = new Pen(Color.FromArgb(58, 58, 66), Theme.BW);
			graphics.DrawPath(pen, path2);
		}
		for (int i = 0; i < _r.Length; i++)
		{
			float t = 1f - Math.Min(1f, Math.Abs(_pos.Value - (float)i));
			Color fore = Theme.Mix((i == _hover) ? Theme.Text : Theme.Sub, Color.White, t);
			string text = Theme.Up(Items[i]);
			int width = Theme.Measure(text, TabF).Width;
			int height = Theme.Measure(text, TabF).Height;
			int num = _r[i].X + Theme.S(16f);
			int y = _r[i].Y + (_r[i].Height - height) / 2;
			TextRenderer.DrawText(graphics, text, TabF, new Point(num, y), fore, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			num += width + Theme.S(8f);
			string text2 = CountText(i);
			if (text2.Length > 0)
			{
				int height2 = Theme.Measure(text2, Theme.Tiny).Height;
				TextRenderer.DrawText(graphics, text2, Theme.Tiny, new Point(num, _r[i].Y + (_r[i].Height - height2) / 2 + Theme.S(1f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				num += Theme.Measure(text2, Theme.Tiny).Width + Theme.S(8f);
			}
			if (_pend != null && i < _pend.Length && _pend[i])
			{
				Theme.Dot(graphics, num + Theme.S(3f), (float)_r[i].Y + (float)_r[i].Height / 2f, Theme.S(3f), Theme.Accent, 0.8f);
			}
		}
	}

	private int Hit(Point p)
	{
		for (int i = 0; i < _r.Length; i++)
		{
			if (_r[i].Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = Hit(e.Location);
		if (num != _hover)
		{
			_hover = num;
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		int num = Hit(e.Location);
		if (e.Button == MouseButtons.Left && num >= 0 && num != Selected)
		{
			this.Changed?.Invoke(num);
		}
		base.OnMouseDown(e);
	}

	public object Fetch(IEngine e)
	{
		return null;
	}

	public void Apply(object v)
	{
	}
}
