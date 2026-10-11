using System;

namespace WinWeaver;

internal class SectionLabel : Control, IBlock
{
	public string Hint;

	public SectionLabel(string t)
	{
		Text = t;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
	}

	public int DoLayout(int x, int y, int w)
	{
		int num = Theme.S(42f);
		SetBounds(x, y + Theme.S(6f), w, num - Theme.S(6f));
		return num;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		string title = L.T(Text);
		Size size = Theme.Measure(title, Theme.H2);
		TextRenderer.DrawText(graphics, title, Theme.H2, new Rectangle(Theme.S(4f), 0, Math.Max(0, Width - Theme.S(8f)), Height), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		int num = size.Width + Theme.S(16f);
		int y = base.Height / 2;
		int num2 = base.Width;
		if (!string.IsNullOrEmpty(Hint))
		{
			string hint = L.T(Hint);
			Size size2 = Theme.Measure(hint, Theme.Tiny);
			if (num + size2.Width + Theme.S(40f) < base.Width)
			{
				TextRenderer.DrawText(graphics, hint, Theme.Tiny, new Point(base.Width - size2.Width - Theme.S(4f), (base.Height - size2.Height) / 2), Theme.Dim, TextFormatFlags.NoPrefix);
				num2 = base.Width - size2.Width - Theme.S(16f);
			}
		}
		if (num < num2)
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(num, y, Math.Max(2, num2 - num), 2), Theme.Border, Color.FromArgb(0, Theme.Border), 0f))
			{
				graphics.FillRectangle(brush, num, y, num2 - num, 1);
			}
		}
	}

	public object Fetch(IEngine e)
	{
		return null;
	}

	public void Apply(object v)
	{
	}
}
