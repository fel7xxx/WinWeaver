using System;

namespace WinWeaver;

internal class FField : Control, ISurface
{
	public readonly TextBox Box;

	public string Placeholder;

	private bool _focus;

	public string Value
	{
		get
		{
			return Box.Text;
		}
		set
		{
			Box.Text = value;
			Box.Visible = _focus || Box.Text.Length > 0;
		}
	}

	public FField(string placeholder, bool password = false, int maxLen = 64)
	{
		Placeholder = placeholder;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.Height = Theme.S(40f);
		Box = new TextBox
		{
			BorderStyle = BorderStyle.None,
			BackColor = Color.FromArgb(30, 32, 38),
			ForeColor = Theme.Text,
			Font = Theme.Body,
			MaxLength = maxLen,
			UseSystemPasswordChar = password
		};
		Box.GotFocus += delegate
		{
			_focus = true;
			Invalidate();
		};
		Box.LostFocus += delegate
		{
			_focus = false;
			Box.Visible = Box.Text.Length > 0;
			Invalidate();
		};
		Box.TextChanged += delegate
		{
			Invalidate();
			OnTextChanged(EventArgs.Empty);
		};
		Box.Visible = false;
		base.Controls.Add(Box);
		Cursor = Cursors.IBeam;
		SetStyle(ControlStyles.Selectable, value: true);
		base.TabStop = true;
	}

	private void Activate()
	{
		Box.Visible = true;
		Box.Focus();
	}

	protected override void OnClick(EventArgs e)
	{
		Activate();
		base.OnClick(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		base.OnGotFocus(e);
		Activate();
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		base.OnLayout(e);
		if (Box != null)
		{
			int num = Theme.S(14f);
			Box.SetBounds(num, (base.Height - Box.PreferredHeight) / 2 + 1, base.Width - num * 2, Box.PreferredHeight);
		}
	}

	public void PaintSurface(Graphics g)
	{
		if (base.Parent is ISurface surface)
		{
			GraphicsState gstate = g.Save();
			g.TranslateTransform(-base.Left, -base.Top);
			surface.PaintSurface(g);
			g.Restore(gstate);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		PaintSurface(graphics);
		Theme.Smooth(graphics);
		using (GraphicsPath path = Theme.Round(new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f), Theme.S(10f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 32, 38)))
			{
				graphics.FillPath(brush, path);
			}
			using Pen pen = new Pen(_focus ? Theme.Ice : Theme.BorderHi, _focus ? 1.4f : 1f);
			graphics.DrawPath(pen, path);
		}
		if (Box.Text.Length == 0 && !string.IsNullOrEmpty(Placeholder))
		{
			TextRenderer.DrawText(graphics, Placeholder, Theme.Body, new Rectangle(Theme.S(14f), 0, base.Width - Theme.S(28f), base.Height), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}
}
