using System;

namespace WinWeaver;

internal class FCheck : Control
{
	public bool Checked = true;

	public FCheck(string text)
	{
		Text = text;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		Font = Theme.Small;
		base.Height = Theme.S(26f);
		base.Width = Theme.Measure(text, Theme.Small).Width + Theme.S(34f);
	}

	protected override void OnClick(EventArgs e)
	{
		Checked = !Checked;
		Invalidate();
		base.OnClick(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		Rectangle rectangle = new Rectangle(1, (base.Height - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
		using (GraphicsPath path = Theme.Round(rectangle, (float)rectangle.Width / 2f))
		{
			using (SolidBrush brush = new SolidBrush(Checked ? Theme.Accent : Theme.Control2))
			{
				graphics.FillPath(brush, path);
			}
			using Pen pen = new Pen(Checked ? Theme.Accent : Theme.BorderHi, Theme.BW);
			graphics.DrawPath(pen, path);
		}
		if (Checked)
		{
			if (Theme.IconSmall != null)
			{
				TextRenderer.DrawText(graphics, "\ue73e", Theme.IconSmall, rectangle, Theme.OnSilver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
			else
			{
				using Pen pen2 = new Pen(Theme.OnSilver, Theme.S(2f));
				graphics.DrawLines(pen2, new Point[3]
				{
					new Point(rectangle.X + Theme.S(4f), rectangle.Y + Theme.S(9f)),
					new Point(rectangle.X + Theme.S(8f), rectangle.Y + Theme.S(13f)),
					new Point(rectangle.X + Theme.S(14f), rectangle.Y + Theme.S(5f))
				});
			}
		}
		TextRenderer.DrawText(graphics, Text, Font, new Rectangle(rectangle.Right + Theme.S(10f), 0, base.Width - rectangle.Right - Theme.S(10f), base.Height), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
	}
}
