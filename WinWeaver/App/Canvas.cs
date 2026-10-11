using System;

namespace WinWeaver;

internal class Canvas : Control
{
	public Page Page;

	public int HeaderH => Page.NoHeader ? Theme.S(18f) : Theme.S(24f) + Theme.Measure(Page.Title, Theme.H1).Height + Theme.S(2f) + Theme.Measure("Ag", Theme.Body).Height + Theme.S(22f);

	public Canvas(Page p)
	{
		Page = p;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		Page.SmoothBy(-Page.WheelPx(e.Delta));
		if (e is HandledMouseEventArgs)
		{
			((HandledMouseEventArgs)e).Handled = true;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		int margin = Page.Margin;
		if (!Page.NoHeader && e.ClipRectangle.Top < Page.Body.HeaderH)
		{
			TextRenderer.DrawText(graphics, Theme.Up(Page.Title), Theme.H1, new Point(margin - Theme.S(3f), Theme.S(24f)), Theme.Text, TextFormatFlags.NoPrefix);
			int height = Theme.Measure(Page.Title, Theme.H1).Height;
			TextRenderer.DrawText(graphics, Theme.Up(Page.Subtitle), Theme.Small, new Point(margin - Theme.S(1f), Theme.S(24f) + height + Theme.S(2f)), Theme.Sub, TextFormatFlags.NoPrefix);
		}
		Rectangle rectangle = Rectangle.Inflate(Page.ListRect, -Theme.S(34f), -Theme.S(34f));
		if (!Page.SplitOn || Page.ListRect.Height <= 0 || !e.ClipRectangle.IntersectsWith(Page.ListRect) || rectangle.Contains(e.ClipRectangle))
		{
			return;
		}
		Rectangle listRect = Page.ListRect;
		int num = Theme.S(34f);
		if (e.ClipRectangle.Top >= listRect.Top + num && e.ClipRectangle.Bottom <= listRect.Bottom - num)
		{
			Theme.Smooth(graphics);
			using (SolidBrush fill = new SolidBrush(Theme.Card1))
			{
				graphics.FillRectangle(fill, listRect.X + 1, e.ClipRectangle.Top - 1, listRect.Width - 2, e.ClipRectangle.Height + 2);
			}
			using Pen pen = new Pen(Theme.Border, Theme.BW);
			float num2 = (float)listRect.X + 1f;
			float num3 = (float)listRect.X + 1f + ((float)listRect.Width - 2.5f);
			graphics.DrawLine(pen, num2, e.ClipRectangle.Top - 1, num2, e.ClipRectangle.Bottom + 1);
			graphics.DrawLine(pen, num3, e.ClipRectangle.Top - 1, num3, e.ClipRectangle.Bottom + 1);
		}
		else
		{
			Theme.Card(graphics, listRect, 0f, 17f);
		}
		if (Page.ListLineY >= 0)
		{
			using (SolidBrush brush = new SolidBrush(Theme.Line))
			{
				graphics.FillRectangle(brush, Page.ListRect.X + 1, Page.ListLineY, Page.ListRect.Width - 3, 1);
			}
		}
	}
}
