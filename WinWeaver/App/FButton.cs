using System;

namespace WinWeaver;

internal class FButton : Control
{
	public BtnKind Kind = BtnKind.Ghost;

	public string Glyph;

	private bool _hot;

	private bool _down;

	private readonly Tween _hotT;

	private readonly Tween _downT;

	public FButton()
	{
		_hotT = new Tween(this, 0f, 0.25f);
		_downT = new Tween(this, 0f, 0.35f);
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.StandardClick | ControlStyles.Selectable | ControlStyles.StandardDoubleClick, value: false);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.TabStop = false;
		base.Height = Theme.S(36f);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hot = true;
		_hotT.Target = 1f;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hot = false;
		_down = false;
		_hotT.Target = 0f;
		_downT.Target = 0f;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			_down = true;
			_downT.Target = 1f;
			Invalidate();
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		bool num = _down && e.Button == MouseButtons.Left && base.ClientRectangle.Contains(e.Location);
		_down = false;
		_downT.Target = 0f;
		Invalidate();
		base.OnMouseUp(e);
		if (num && base.Enabled)
		{
			OnClick(EventArgs.Empty);
		}
	}

	public void PerformClick()
	{
		if (base.Enabled && base.Visible)
		{
			OnClick(EventArgs.Empty);
		}
	}

	protected override void OnTextChanged(EventArgs e)
	{
		Invalidate();
		base.OnTextChanged(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		PaintParent(this, e.Graphics);
	}

	internal static void PaintParent(Control c, Graphics g)
	{
		Control parent = c.Parent;
		if (parent is ISurface surface)
		{
			GraphicsState gstate = g.Save();
			try
			{
				g.TranslateTransform(-c.Left, -c.Top);
				surface.PaintSurface(g);
				return;
			}
			finally
			{
				g.Restore(gstate);
			}
		}
		Color color = Theme.Bg;
		for (Control control = parent; control != null; control = control.Parent)
		{
			if (control.BackColor.A == byte.MaxValue)
			{
				color = control.BackColor;
				break;
			}
		}
		g.Clear(color);
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		if (!base.Visible)
		{
			_hot = false;
			_down = false;
			_hotT.Snap(0f);
			_downT.Snap(0f);
		}
		base.OnVisibleChanged(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		_hot = false;
		_down = false;
		_hotT.Snap(0f);
		_downT.Snap(0f);
		Invalidate();
		base.OnEnabledChanged(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		float num = _downT.Value * 1.2f;
		RectangleF r = new RectangleF(0.5f + num, 0.5f + num, (float)base.Width - 1f - num * 2f, (float)base.Height - 1f - num * 2f);
		float t = _hotT.Value * 0.08f + _downT.Value * 0.08f;
		Color fore;
		if (!base.Enabled)
		{
			_ = Theme.Control2;
			_ = Theme.Control2;
			_ = Theme.Border;
			fore = Theme.Dim;
		}
		else if (Kind == BtnKind.Silver)
		{
			Theme.Mix(Theme.Silver1, Color.White, t);
			Theme.Mix(Theme.Silver2, Color.FromArgb(120, 120, 132), t);
			Color.FromArgb(210, 210, 220);
			fore = Theme.OnSilver;
		}
		else if (Kind == BtnKind.Danger)
		{
			Theme.Mix(Color.FromArgb(70, 44, 46), Color.FromArgb(90, 54, 56), t);
			Theme.Mix(Color.FromArgb(52, 34, 36), Color.FromArgb(70, 42, 44), t);
			Color.FromArgb(120, 70, 72);
			fore = Theme.Red;
		}
		else
		{
			Theme.Mix(Theme.Control1, Theme.BorderHi, t);
			Theme.Mix(Theme.Control2, Theme.Control1, t);
			Theme.Mix(Theme.Border, Theme.BorderHi, _hotT.Value);
			fore = Theme.Text;
		}
		using (GraphicsPath path = Theme.Round(r, r.Height / 2f))
		{
			if (base.Enabled && Kind == BtnKind.Silver)
			{
				using SolidBrush brush = new SolidBrush(Theme.Mix(Theme.Mix(Theme.Accent, Color.FromArgb(255, 92, 104), _hotT.Value), Color.FromArgb(214, 40, 54), _downT.Value));
				graphics.FillPath(brush, path);
			}
			else
			{
				if (_hotT.Value > 0.01f)
				{
					using SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(12f * _hotT.Value), 255, 255, 255));
					graphics.FillPath(brush2, path);
				}
				using Pen pen = new Pen((!base.Enabled) ? Theme.Border : ((Kind == BtnKind.Danger) ? Theme.Mix(Color.FromArgb(150, 30, 38), Theme.Accent, _hotT.Value) : Theme.Mix(Color.FromArgb(46, 46, 53), Color.FromArgb(72, 72, 82), _hotT.Value)), Theme.BW);
				graphics.DrawPath(pen, path);
			}
		}
		if (base.Enabled && Kind == BtnKind.Danger)
		{
			fore = Theme.IceHi;
		}
		_ = Glyph;
		string text = Theme.Up(Text);
		_ = Theme.IconSmall;
		TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
		int num2 = Theme.S(10f);
		int num3;
		int num4;
		if (base.Enabled)
		{
			num3 = ((Kind == BtnKind.Silver) ? 1 : 0);
			num3 = 0;
		}
		else
		{
			num3 = 0;
		}
		num4 = 0;
		goto IL_035f;
		IL_035f:
		int num5 = num4;
		int width = Theme.Measure(text, Font).Width;
		int num6 = Math.Max(num2, (base.Width - (width + num5)) / 2);
		if (num3 != 0)
		{
			Theme.Dot(graphics, num6 + Theme.S(4.5f), (float)base.Height / 2f, Theme.S(4.5f), Theme.Accent, 0.8f);
			num6 += num5;
		}
		TextRenderer.DrawText(graphics, text, Font, new Rectangle(num6, 0, Math.Max(1, base.Width - num6 - num2 + Theme.S(2f)), base.Height), fore, flags);
	}

	public void AutoWidth(int pad)
	{
		int num = 0;
		base.Width = Theme.Measure(Theme.Up(Text), Font).Width + num + pad + Theme.S(12f);
	}
}
