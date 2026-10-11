using System;
using System.Runtime.InteropServices;

namespace WinWeaver;

internal class ToastWindow : Form
{
	private readonly StatusBar _bar;

	private int Glow => Theme.S(26f);

	protected override bool ShowWithoutActivation => true;

	public ToastWindow(StatusBar bar)
	{
		_bar = bar;
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.Manual;
		base.AutoScaleMode = AutoScaleMode.None;
		EnsureTransparent();
	}

	public void ShowOn(Form owner)
	{
		if (owner != null)
		{
			base.Owner = owner;
		}
		Show();
		Render();
	}

	public void Render()
	{
		if (base.IsDisposed || _bar.Width <= 0 || _bar.Height <= 0)
		{
			return;
		}
		int glow = Glow;
		Rectangle r = _bar.ScreenRect;
		SetBounds(r.X - glow, r.Y - glow, r.Width + glow * 2, r.Height + glow * 2);
		Opacity = Math.Max(0.0, Math.Min(1.0, _bar.Fade));
		Invalidate();
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		int glow = Glow;
		Rectangle screenRect = _bar.ScreenRect;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		int num = 13;
		for (int i = 0; i < num; i++)
		{
			float num2 = (float)i / (float)(num - 1);
			float num3 = (float)glow * (1f - num2);
			using GraphicsPath path = Theme.Round(new RectangleF((float)glow - num3, (float)glow - num3 + (float)Theme.S(5f) * (1f - num2 * 0.6f), (float)screenRect.Width + num3 * 2f, (float)screenRect.Height + num3 * 2f), (float)Theme.S(12f) + num3);
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)(6f + 16f * num2 * num2), 0, 0, 0));
			graphics.FillPath(brush, path);
		}
		using GraphicsPath path2 = Theme.Round(new RectangleF(glow, glow, screenRect.Width, screenRect.Height), Theme.S(12f));
		GraphicsState st = graphics.Save();
		graphics.SetClip(path2);
		graphics.TranslateTransform(glow, glow);
		_bar.PaintTo(graphics);
		graphics.Restore(st);
	}

	private Point Local(Point p)
	{
		return new Point(p.X - Glow, p.Y - Glow);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		_bar.WinMove(Local(e.Location));
		Cursor = _bar.Cursor;
		base.OnMouseMove(e);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_bar.WinEnter();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_bar.WinLeave();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		Point p = Local(e.Location);
		if (p.X >= 0 && p.Y >= 0 && p.X < _bar.Width && p.Y < _bar.Height)
		{
			_bar.WinClick(p, e.Button);
		}
		base.OnMouseClick(e);
	}
}
