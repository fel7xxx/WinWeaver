using System;

namespace WinWeaver;

internal class Segment : Control
{
	public string[] Items = new string[0];

	private int _sel = -1;

	private int _hot = -1;

	private readonly Tween _pos;

	public int Selected
	{
		get
		{
			return _sel;
		}
		set
		{
			if (_sel < 0 || value < 0)
			{
				_pos.Snap(Math.Max(0, value));
			}
			else
			{
				_pos.Target = value;
			}
			_sel = value;
			Invalidate();
		}
	}

	public bool Busy { get; set; }

	private float[] _x = new float[0];

	private float[] _w = new float[0];

	private Font _font;

	private int _layW = -1;

	private string _layKey;

	private static Font _small2;

	private static Font _small3;

	// Ширина каждого сегмента — по тексту; если не помещается, шрифт уменьшается.
	private void Lay()
	{
		string key = string.Join("\u0001", Items);
		if (_layW == base.Width && _layKey == key && _x.Length == Items.Length)
		{
			return;
		}
		_layW = base.Width;
		_layKey = key;
		int n = Items.Length;
		_x = new float[n];
		_w = new float[n];
		if (n == 0)
		{
			return;
		}
		if (_small2 == null)
		{
			_small2 = Theme.F(7.6f);
			_small3 = Theme.F(7f);
		}
		float avail = base.Width - Theme.S(6f);
		float pad = Theme.S(22f);
		float[] tw = new float[n];
		Font[] fonts = { Theme.Small, _small2, _small3 };
		foreach (Font f in fonts)
		{
			_font = f;
			float sum = 0f;
			for (int i = 0; i < n; i++)
			{
				tw[i] = Theme.Measure(Theme.Up(Items[i]), f).Width;
				sum += tw[i];
			}
			pad = Math.Max(Theme.S(12f), Math.Min(Theme.S(22f), (avail - sum) / n));
			if (sum + pad * n <= avail)
			{
				break;
			}
		}
		float total = 0f;
		for (int i = 0; i < n; i++)
		{
			_w[i] = tw[i] + pad;
			total += _w[i];
		}
		// Остаток делим поровну, чтобы сегменты занимали всю ширину.
		float extra = (avail - total) / n;
		if (extra < 0f)
		{
			float k = avail / total;
			for (int i = 0; i < n; i++) _w[i] *= k;
			extra = 0f;
		}
		float x = Theme.S(3f);
		for (int i = 0; i < n; i++)
		{
			_w[i] += extra;
			_x[i] = x;
			x += _w[i];
		}
	}

	private int HitIndex(int px)
	{
		Lay();
		for (int i = 0; i < _x.Length; i++)
		{
			if (px >= _x[i] && px < _x[i] + _w[i])
			{
				return i;
			}
		}
		return -1;
	}

	private void PillAt(float v, out float x, out float w)
	{
		int n = _x.Length;
		v = Math.Max(0f, Math.Min(n - 1, v));
		int a = (int)Math.Floor(v);
		int b = Math.Min(n - 1, a + 1);
		float t = v - a;
		x = _x[a] + (_x[b] - _x[a]) * t;
		w = _w[a] + (_w[b] - _w[a]) * t;
	}

	/// <summary>Минимальная ширина, при которой все подписи помещаются обычным шрифтом.</summary>
	public int PreferredWidth
	{
		get
		{
			float sum = Theme.S(6f);
			foreach (string it in Items)
			{
				sum += Theme.Measure(Theme.Up(it), Theme.Small).Width + Theme.S(22f);
			}
			return (int)Math.Ceiling(sum);
		}
	}

	public event EventHandler<int> Changed;

	public Segment()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		base.Height = Theme.S(34f);
		Cursor = Cursors.Hand;
		_pos = new Tween(this, 0f, 0.24f);
	}

	public void SimSelect(int i)
	{
		if (!Busy && i != _sel)
		{
			Selected = i;
			Invalidate();
			this.Changed?.Invoke(this, i);
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = HitIndex(e.X);
		if (num != _hot)
		{
			_hot = num;
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

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (!Busy)
		{
			int num = HitIndex(e.X);
			if (num >= 0 && num < Items.Length && num != _sel)
			{
				Selected = num;
				Invalidate();
				this.Changed?.Invoke(this, num);
			}
		}
		base.OnMouseDown(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		FButton.PaintParent(this, e.Graphics);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		RectangleF r = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
		using (GraphicsPath path = Theme.Round(r, Theme.S(9f)))
		{
			using (SolidBrush bb = new SolidBrush(Color.FromArgb(16, 16, 19))) graphics.FillPath(bb, path);
			using Pen pen = new Pen(Color.FromArgb(46, 46, 53), Theme.BW);
			graphics.DrawPath(pen, path);
		}
		Lay();
		if (_sel >= 0 && Items.Length != 0)
		{
			PillAt(_pos.Value, out var px, out var pw);
			RectangleF r2 = new RectangleF(px, Theme.S(3f), pw, base.Height - Theme.S(6f));
			using GraphicsPath path2 = Theme.Round(r2, Theme.S(7f));
			using SolidBrush brush = new SolidBrush(Color.FromArgb(40, 40, 48));
			graphics.FillPath(brush, path2);
		}
		for (int i = 0; i < Items.Length; i++)
		{
			Rectangle r3 = new Rectangle((int)_x[i], 0, (int)Math.Ceiling(_w[i]), base.Height);
			float t = Math.Max(0f, 1f - Math.Abs(_pos.Value - (float)i));
			Color fore = ((_sel < 0) ? ((i == _hot) ? Theme.Text : Theme.Sub) : Theme.Mix((i == _hot) ? Theme.Text : Theme.Sub, Color.White, t));
			TextRenderer.DrawText(graphics, Theme.Up(Items[i]), _font ?? Theme.Small, r3, fore, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}
}
