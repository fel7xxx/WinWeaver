using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class TileChoice : Control, ISurface
{
	public class Tile
	{
		public string Title;

		public string Hint;

		public Image Picture;

		public string Glyph;

		public Rectangle R;
	}

	public readonly List<Tile> Tiles = new List<Tile>();

	public int Selected;

	public int PictureH;

	private int _hot = -1;

	public int hintLines = 1;

	public event Action<int> Changed;

	public TileChoice()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Cursor = Cursors.Hand;
	}

	public Tile Add(string title, string hint, Image pic = null, string glyph = null)
	{
		Tile tile = new Tile
		{
			Title = title,
			Hint = hint,
			Picture = pic,
			Glyph = glyph
		};
		Tiles.Add(tile);
		return tile;
	}

	public int Measure(int w)
	{
		int num = Theme.S((float)hintLines * 16f + 34f);
		return ((PictureH > 0) ? (PictureH + Theme.S(10f)) : 0) + num;
	}

	private void Calc()
	{
		int num = Math.Max(1, Tiles.Count);
		int num2 = Theme.S(10f);
		int num3 = (base.Width - num2 * (num - 1)) / num;
		for (int i = 0; i < Tiles.Count; i++)
		{
			Tiles[i].R = new Rectangle(i * (num3 + num2), 0, num3, base.Height - 1);
		}
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Calc();
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		for (int i = 0; i < Tiles.Count; i++)
		{
			if (Tiles[i].R.Contains(e.Location))
			{
				num = i;
			}
		}
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
		for (int i = 0; i < Tiles.Count; i++)
		{
			if (Tiles[i].R.Contains(e.Location) && i != Selected)
			{
				Selected = i;
				Invalidate();
				this.Changed?.Invoke(i);
			}
		}
		base.OnMouseDown(e);
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
		Calc();
		Theme.Smooth(graphics);
		for (int i = 0; i < Tiles.Count; i++)
		{
			Tile tile = Tiles[i];
			bool flag = i == Selected;
			bool flag2 = i == _hot;
			RectangleF r = new RectangleF((float)tile.R.X + 0.5f, (float)tile.R.Y + 0.5f, (float)tile.R.Width - 1f, (float)tile.R.Height - 1f);
			using (GraphicsPath path = Theme.Round(r, Theme.S(12f)))
			{
				Theme.FillGradient(graphics, path, r, flag ? Theme.Mix(Theme.CardHot1, Theme.Ice, 0.08f) : (flag2 ? Theme.CardHot1 : Theme.Card1), flag ? Theme.CardHot2 : Theme.Card2);
				using Pen pen = new Pen(flag ? Theme.Ice : (flag2 ? Theme.BorderHi : Theme.Border), flag ? 1.6f : 1f);
				graphics.DrawPath(pen, path);
			}
			int num = Theme.S(10f);
			int num2 = tile.R.Y + num;
			if (PictureH > 0)
			{
				Rectangle rectangle = new Rectangle(tile.R.X + num, num2, tile.R.Width - num * 2, PictureH);
				using (GraphicsPath path2 = Theme.Round(rectangle, Theme.S(8f)))
				{
					Region clip = graphics.Clip;
					graphics.SetClip(path2, CombineMode.Intersect);
					if (tile.Picture != null)
					{
						graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
						float num3 = (float)tile.Picture.Width / (float)tile.Picture.Height;
						float num4 = (float)rectangle.Width / (float)rectangle.Height;
						RectangleF srcRect = ((num3 > num4) ? new RectangleF(((float)tile.Picture.Width - (float)tile.Picture.Height * num4) / 2f, 0f, (float)tile.Picture.Height * num4, tile.Picture.Height) : new RectangleF(0f, ((float)tile.Picture.Height - (float)tile.Picture.Width / num4) / 2f, tile.Picture.Width, (float)tile.Picture.Width / num4));
						graphics.DrawImage(tile.Picture, rectangle, srcRect, GraphicsUnit.Pixel);
					}
					else
					{
						using (SolidBrush brush = new SolidBrush(Theme.Control2))
						{
							graphics.FillRectangle(brush, rectangle);
						}
						if (tile.Glyph != null)
						{
							Theme.DrawIcon(graphics, tile.Glyph, Theme.IconBig, rectangle, Theme.Sub);
						}
					}
					graphics.Clip = clip;
					clip.Dispose();
				}
				num2 = rectangle.Bottom + Theme.S(8f);
			}
			int x = tile.R.X + num + Theme.S(2f);
			int width = tile.R.Width - num * 2 - Theme.S(26f);
			TextRenderer.DrawText(graphics, tile.Title, Theme.Title, new Rectangle(x, num2, width, Theme.S(20f)), flag ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, 0.6f), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			if (!string.IsNullOrEmpty(tile.Hint))
			{
				TextRenderer.DrawText(graphics, tile.Hint, Theme.Tiny, new Rectangle(x, num2 + Theme.S(21f), tile.R.Width - num * 2, Theme.S(16f) * hintLines), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			}
			Rectangle rect = new Rectangle(tile.R.Right - num - Theme.S(16f), num2 + Theme.S(2f), Theme.S(16f), Theme.S(16f));
			using (Pen pen2 = new Pen(flag ? Theme.Ice : Theme.BorderHi, 1.4f))
			{
				graphics.DrawEllipse(pen2, rect);
			}
			if (flag)
			{
				using SolidBrush brush2 = new SolidBrush(Theme.Ice);
				graphics.FillEllipse(brush2, Rectangle.Inflate(rect, -Theme.S(4f), -Theme.S(4f)));
			}
		}
	}
}
