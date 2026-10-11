using System;
using System.Collections.Generic;
using AM = Avalonia.Media;
using AV = Avalonia;

namespace WinWeaver;

internal interface IDeviceContext : IDisposable
{
}

internal sealed class GraphicsState
{
	internal AV.Matrix M;

	internal Region Clip;
}

/// <summary>
/// Аналог System.Drawing.Graphics поверх Avalonia DrawingContext.
/// Хранит текущие преобразование и отсечение и применяет их к контексту перед каждой операцией.
/// </summary>
internal sealed class Graphics : IDeviceContext
{
	internal readonly AM.DrawingContext Dc;

	private readonly bool _owns;

	private readonly Bitmap _bitmap;

	private readonly List<AM.DrawingContext.PushedState> _pushed = new List<AM.DrawingContext.PushedState>();

	private AV.Matrix _m = AV.Matrix.Identity;

	/// <summary>Базовый масштаб поверхности (HiDPI-битмап). Рисуем в логических единицах,
	/// а масштаб применяем сами: у масштабированного контекста RenderTargetBitmap в Skia
	/// PushTransform дважды учитывает DPI.</summary>
	private AV.Matrix _base = AV.Matrix.Identity;

	private AV.Matrix Eff => _m * _base;

	private Region _clip;

	private bool _dirty = true;

	private static readonly AV.Rect Huge = new AV.Rect(-100000, -100000, 200000, 200000);

	public SmoothingMode SmoothingMode { get; set; }

	public PixelOffsetMode PixelOffsetMode { get; set; }

	public InterpolationMode InterpolationMode { get; set; }

	public CompositingQuality CompositingQuality { get; set; }

	public CompositingMode CompositingMode { get; set; }

	public TextRenderingHint TextRenderingHint { get; set; }

	public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Display;

	public float DpiX => 96f;

	public float DpiY => 96f;

	/// <summary>Размер области (для Clear и отсечения по умолчанию).</summary>
	internal AV.Size Area;

	internal Graphics(AM.DrawingContext dc, AV.Size area, bool owns = false, Bitmap bmp = null)
	{
		Dc = dc;
		Area = area;
		_owns = owns;
		_bitmap = bmp;
	}

	public static Graphics FromImage(Image img)
	{
		if (!(img is Bitmap b))
		{
			throw new ArgumentException("Нужен Bitmap");
		}
		var rt = b.EnsureTarget();
		var dc = rt.CreateDrawingContext(false);
		var g = new Graphics(dc, new AV.Size(b.Width, b.Height), owns: true, b);
		if (b.Scale != 1)
		{
			g._base = AV.Matrix.CreateScale(b.Scale, b.Scale);
			g._dirty = true;
		}
		return g;
	}

	internal static AM.Geometry Excluding(AM.Geometry g)
	{
		return new AM.CombinedGeometry(AM.GeometryCombineMode.Exclude, new AM.RectangleGeometry(Huge), g);
	}

	internal static readonly int[] OpCount = new int[4];

	private void Sync()
	{
		if (!_dirty)
		{
			return;
		}
		for (int i = _pushed.Count - 1; i >= 0; i--)
		{
			_pushed[i].Dispose();
		}
		_pushed.Clear();
		if (_clip != null)
		{
			if (_clip.Empty)
			{
				_pushed.Add(Dc.PushClip(new AV.Rect(0, 0, 0, 0)));
			}
			foreach (var part in _clip.Parts)
			{
				if (part is AM.RectangleGeometry rg && rg.Transform == null)
				{
					_pushed.Add(Dc.PushClip(rg.Rect));
				}
				else
				{
					_pushed.Add(Dc.PushGeometryClip(part)); OpCount[0]++;
				}
			}
		}
		var eff = Eff;
		if (!eff.IsIdentity)
		{
			_pushed.Add(Dc.PushTransform(eff));
		}
		_dirty = false;
	}

	private AM.Geometry ToDevice(AM.Geometry g)
	{
		if (Eff.IsIdentity)
		{
			return g;
		}
		var c = g.Clone();
		c.Transform = new AM.MatrixTransform(Eff);
		return c;
	}

	private AM.Geometry ToDevice(AV.Rect r)
	{
		if (Eff.IsIdentity)
		{
			return new AM.RectangleGeometry(r);
		}
		if (Eff.M12 == 0 && Eff.M21 == 0)
		{
			return new AM.RectangleGeometry(r.TransformToAABB(Eff));
		}
		return new AM.RectangleGeometry(r) { Transform = new AM.MatrixTransform(Eff) };
	}

	// ---------- Состояние ----------

	public Region Clip
	{
		get => _clip == null ? new Region() : _clip.Clone();
		set
		{
			_clip = (value == null || value.IsInfinite(this)) ? null : value.Clone();
			_dirty = true;
		}
	}

	public RectangleF ClipBounds
	{
		get
		{
			if (_clip == null || _clip.Parts.Count == 0)
			{
				return new RectangleF(0, 0, (float)Area.Width, (float)Area.Height);
			}
			var b = _clip.Single().Bounds;
			return new RectangleF((float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
		}
	}

	private void Combine(AM.Geometry dev, CombineMode mode)
	{
		switch (mode)
		{
		case CombineMode.Replace:
			_clip = new Region();
			_clip.Parts.Add(dev);
			break;
		case CombineMode.Intersect:
			_clip = _clip == null ? new Region() : _clip.Clone();
			_clip.Parts.Add(dev);
			break;
		case CombineMode.Exclude:
			_clip = _clip == null ? new Region() : _clip.Clone();
			_clip.Parts.Add(Excluding(dev));
			break;
		case CombineMode.Union:
			if (_clip == null)
			{
				break;
			}
			{
				var cur = _clip.Single();
				_clip = new Region();
				_clip.Parts.Add(cur == null ? dev : new AM.CombinedGeometry(AM.GeometryCombineMode.Union, cur, dev));
			}
			break;
		default:
			_clip = new Region();
			_clip.Parts.Add(dev);
			break;
		}
		_dirty = true;
	}

	public void SetClip(Rectangle r, CombineMode mode = CombineMode.Replace) => Combine(ToDevice(Conv.R(r)), mode);

	public void SetClip(RectangleF r, CombineMode mode = CombineMode.Replace) => Combine(ToDevice(Conv.R(r)), mode);

	public void SetClip(GraphicsPath p, CombineMode mode = CombineMode.Replace) => Combine(ToDevice(p.ToGeometry()), mode);

	public void SetClip(Region r, CombineMode mode)
	{
		if (r == null || r.IsInfinite(this))
		{
			if (mode == CombineMode.Replace)
			{
				ResetClip();
			}
			return;
		}
		var g = r.Single();
		if (g != null)
		{
			Combine(g, mode);
		}
	}

	public void IntersectClip(Rectangle r) => SetClip(r, CombineMode.Intersect);

	public void IntersectClip(RectangleF r) => SetClip(r, CombineMode.Intersect);

	public void ExcludeClip(Rectangle r) => SetClip(r, CombineMode.Exclude);

	public void ResetClip()
	{
		_clip = null;
		_dirty = true;
	}

	public Matrix Transform
	{
		get => new Matrix(_m);
		set
		{
			_m = value?.M ?? AV.Matrix.Identity;
			_dirty = true;
		}
	}

	public void TranslateTransform(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
	{
		var t = AV.Matrix.CreateTranslation(dx, dy);
		_m = order == MatrixOrder.Prepend ? t * _m : _m * t;
		_dirty = true;
	}

	public void ScaleTransform(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend)
	{
		var t = AV.Matrix.CreateScale(sx, sy);
		_m = order == MatrixOrder.Prepend ? t * _m : _m * t;
		_dirty = true;
	}

	public void RotateTransform(float deg, MatrixOrder order = MatrixOrder.Prepend)
	{
		var t = AV.Matrix.CreateRotation(deg * Math.PI / 180.0);
		_m = order == MatrixOrder.Prepend ? t * _m : _m * t;
		_dirty = true;
	}

	public void ResetTransform()
	{
		_m = AV.Matrix.Identity;
		_dirty = true;
	}

	public GraphicsState Save() => new GraphicsState { M = _m, Clip = _clip?.Clone() };

	public void Restore(GraphicsState s)
	{
		if (s == null)
		{
			return;
		}
		_m = s.M;
		_clip = s.Clip?.Clone();
		_dirty = true;
	}

	internal AV.Matrix CurrentMatrix => _m;

	// ---------- Заливки ----------

	public void Clear(Color c)
	{
		Sync();
		using (Dc.PushTransform(_m.IsIdentity ? AV.Matrix.Identity : InvertOrIdentity(_m)))
		{
			Dc.FillRectangle(Conv.Solid(c), new AV.Rect(0, 0, Math.Max(Area.Width, 1), Math.Max(Area.Height, 1)));
		}
	}

	private static AV.Matrix InvertOrIdentity(AV.Matrix m) => m.TryInvert(out var inv) ? inv : AV.Matrix.Identity;

	public void FillRectangle(Brush b, RectangleF r)
	{
		if (r.Width <= 0 || r.Height <= 0)
		{
			return;
		}
		Sync();
		Dc.DrawRectangle(b.ToAvalonia(), null, Conv.R(r));
	}

	public void FillRectangle(Brush b, Rectangle r) => FillRectangle(b, (RectangleF)r);

	public void FillRectangle(Brush b, float x, float y, float w, float h) => FillRectangle(b, new RectangleF(x, y, w, h));

	public void FillRectangle(Brush b, int x, int y, int w, int h) => FillRectangle(b, new RectangleF(x, y, w, h));

	public void FillRectangles(Brush b, Rectangle[] rs)
	{
		foreach (var r in rs)
		{
			FillRectangle(b, r);
		}
	}

	public void FillPath(Brush b, GraphicsPath p)
	{
		Sync();
		Dc.DrawGeometry(b.ToAvalonia(), null, p.ToGeometry());
	}

	public void FillRegion(Brush b, Region r)
	{
		var g = r?.Single();
		if (g == null)
		{
			return;
		}
		Sync();
		using (Dc.PushTransform(InvertOrIdentity(Eff)))
		{
			Dc.DrawGeometry(b.ToAvalonia(), null, g);
		}
	}

	public void FillEllipse(Brush b, float x, float y, float w, float h)
	{
		if (w <= 0 || h <= 0)
		{
			return;
		}
		Sync();
		Dc.DrawEllipse(b.ToAvalonia(), null, new AV.Point(x + w / 2.0, y + h / 2.0), w / 2.0, h / 2.0);
	}

	public void FillEllipse(Brush b, RectangleF r) => FillEllipse(b, r.X, r.Y, r.Width, r.Height);

	public void FillEllipse(Brush b, Rectangle r) => FillEllipse(b, r.X, r.Y, r.Width, r.Height);

	public void FillEllipse(Brush b, int x, int y, int w, int h) => FillEllipse(b, (float)x, y, w, h);

	public void FillPolygon(Brush b, PointF[] pts)
	{
		using var p = new GraphicsPath();
		p.AddPolygon(pts);
		FillPath(b, p);
	}

	public void FillPolygon(Brush b, Point[] pts)
	{
		using var p = new GraphicsPath();
		p.AddPolygon(pts);
		FillPath(b, p);
	}

	// ---------- Линии ----------

	public void DrawPath(Pen pen, GraphicsPath p)
	{
		Sync();
		Dc.DrawGeometry(null, pen.ToAvalonia(), p.ToGeometry());
	}

	public void DrawRectangle(Pen pen, float x, float y, float w, float h)
	{
		Sync();
		Dc.DrawRectangle(null, pen.ToAvalonia(), new AV.Rect(x, y, Math.Max(0, w), Math.Max(0, h)));
	}

	public void DrawRectangle(Pen pen, Rectangle r) => DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

	public void DrawRectangle(Pen pen, RectangleF r) => DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

	public void DrawRectangle(Pen pen, int x, int y, int w, int h) => DrawRectangle(pen, (float)x, y, w, h);

	public void DrawEllipse(Pen pen, float x, float y, float w, float h)
	{
		if (w <= 0 || h <= 0)
		{
			return;
		}
		Sync();
		Dc.DrawEllipse(null, pen.ToAvalonia(), new AV.Point(x + w / 2.0, y + h / 2.0), w / 2.0, h / 2.0);
	}

	public void DrawEllipse(Pen pen, RectangleF r) => DrawEllipse(pen, r.X, r.Y, r.Width, r.Height);

	public void DrawEllipse(Pen pen, Rectangle r) => DrawEllipse(pen, r.X, r.Y, r.Width, r.Height);

	public void DrawEllipse(Pen pen, int x, int y, int w, int h) => DrawEllipse(pen, (float)x, y, w, h);

	public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
	{
		Sync();
		Dc.DrawLine(pen.ToAvalonia(), new AV.Point(x1, y1), new AV.Point(x2, y2));
	}

	public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => DrawLine(pen, (float)x1, y1, x2, y2);

	public void DrawLine(Pen pen, PointF a, PointF b) => DrawLine(pen, a.X, a.Y, b.X, b.Y);

	public void DrawLine(Pen pen, Point a, Point b) => DrawLine(pen, a.X, a.Y, b.X, b.Y);

	public void DrawLines(Pen pen, PointF[] pts)
	{
		if (pts == null || pts.Length < 2)
		{
			return;
		}
		using var p = new GraphicsPath();
		p.AddLines(pts);
		DrawPath(pen, p);
	}

	public void DrawLines(Pen pen, Point[] pts)
	{
		if (pts == null || pts.Length < 2)
		{
			return;
		}
		using var p = new GraphicsPath();
		p.AddLines(pts);
		DrawPath(pen, p);
	}

	public void DrawPolygon(Pen pen, PointF[] pts)
	{
		using var p = new GraphicsPath();
		p.AddPolygon(pts);
		DrawPath(pen, p);
	}

	public void DrawPolygon(Pen pen, Point[] pts)
	{
		using var p = new GraphicsPath();
		p.AddPolygon(pts);
		DrawPath(pen, p);
	}

	public void DrawArc(Pen pen, float x, float y, float w, float h, float start, float sweep)
	{
		using var p = new GraphicsPath();
		p.AddArc(x, y, w, h, start, sweep);
		DrawPath(pen, p);
	}

	public void DrawArc(Pen pen, RectangleF r, float start, float sweep) => DrawArc(pen, r.X, r.Y, r.Width, r.Height, start, sweep);

	public void DrawArc(Pen pen, Rectangle r, float start, float sweep) => DrawArc(pen, r.X, r.Y, r.Width, r.Height, start, sweep);

	public void DrawArc(Pen pen, int x, int y, int w, int h, int start, int sweep) => DrawArc(pen, (float)x, y, w, h, start, sweep);

	public void DrawBezier(Pen pen, PointF a, PointF b, PointF c, PointF d)
	{
		using var p = new GraphicsPath();
		p.AddBezier(a, b, c, d);
		DrawPath(pen, p);
	}

	// ---------- Картинки ----------

	private void Img(Image img, AV.Rect dest, AV.Rect? src, float opacity = 1f)
	{
		var s = img?.AvSource;
		if (s == null || dest.Width <= 0 || dest.Height <= 0)
		{
			return;
		}
		if (img is Bitmap b && b == _bitmap)
		{
			return;
		}
		Sync();
		AM.DrawingContext.PushedState? op = null;
		if (opacity < 0.999f)
		{
			op = Dc.PushOpacity(Math.Max(0, opacity)); OpCount[1]++;
		}
		AM.DrawingContext.PushedState? ro = null;
		if (InterpolationMode == InterpolationMode.NearestNeighbor)
		{
			ro = Dc.PushRenderOptions(new AM.RenderOptions { BitmapInterpolationMode = AM.Imaging.BitmapInterpolationMode.None });
		}
		// Avalonia/Skia трактует sourceRect в пикселях источника.
		double k = img is Bitmap bs ? bs.Scale : 1;
		if (src.HasValue)
		{
			var r = src.Value;
			Dc.DrawImage(s, new AV.Rect(r.X * k, r.Y * k, r.Width * k, r.Height * k), dest);
		}
		else if (img is Bitmap bp)
		{
			Dc.DrawImage(s, new AV.Rect(0, 0, bp.PixelWidth, bp.PixelHeight), dest);
		}
		else
		{
			Dc.DrawImage(s, dest);
		}
		ro?.Dispose();
		op?.Dispose();
	}

	public void DrawImage(Image img, float x, float y) => Img(img, new AV.Rect(x, y, img.Width, img.Height), null);

	public void DrawImage(Image img, int x, int y) => Img(img, new AV.Rect(x, y, img.Width, img.Height), null);

	public void DrawImage(Image img, Point p) => DrawImage(img, p.X, p.Y);

	public void DrawImage(Image img, PointF p) => DrawImage(img, p.X, p.Y);

	public void DrawImage(Image img, float x, float y, float w, float h) => Img(img, new AV.Rect(x, y, Math.Max(0, w), Math.Max(0, h)), null);

	public void DrawImage(Image img, int x, int y, int w, int h) => Img(img, new AV.Rect(x, y, Math.Max(0, w), Math.Max(0, h)), null);

	public void DrawImage(Image img, Rectangle dest) => Img(img, Conv.R(dest), null);

	public void DrawImage(Image img, RectangleF dest) => Img(img, Conv.R(dest), null);

	public void DrawImage(Image img, Rectangle dest, Rectangle src, GraphicsUnit unit) => Img(img, Conv.R(dest), Conv.R(src));

	public void DrawImage(Image img, RectangleF dest, RectangleF src, GraphicsUnit unit) => Img(img, Conv.R(dest), Conv.R(src));

	public void DrawImage(Image img, Rectangle dest, int sx, int sy, int sw, int sh, GraphicsUnit unit) => Img(img, Conv.R(dest), new AV.Rect(sx, sy, sw, sh));

	public void DrawImage(Image img, Rectangle dest, int sx, int sy, int sw, int sh, GraphicsUnit unit, ImageAttributes attr)
		=> Img(img, Conv.R(dest), new AV.Rect(sx, sy, sw, sh), attr?.Opacity ?? 1f);

	public void DrawImage(Image img, Rectangle dest, float sx, float sy, float sw, float sh, GraphicsUnit unit, ImageAttributes attr)
		=> Img(img, Conv.R(dest), new AV.Rect(sx, sy, sw, sh), attr?.Opacity ?? 1f);

	public void DrawImageUnscaled(Image img, int x, int y) => DrawImage(img, x, y);

	public void DrawImageUnscaled(Image img, Point p) => DrawImage(img, p.X, p.Y);

	public void DrawImageUnscaled(Image img, Rectangle r) => DrawImage(img, r.X, r.Y);

	public void DrawImageUnscaledAndClipped(Image img, Rectangle r)
	{
		var st = Save();
		SetClip(r, CombineMode.Intersect);
		DrawImage(img, r.X, r.Y);
		Restore(st);
	}

	public void DrawIcon(Icon icon, Rectangle r) => DrawImage(icon.ToBitmap(), r);

	public void DrawIcon(Icon icon, int x, int y) => DrawImage(icon.ToBitmap(), x, y);

	// ---------- Текст ----------

	internal void DrawLayout(AM.TextFormatting.TextLayout layout, double x, double y, double alpha = 1)
	{
		Sync();
		if (alpha < 0.999)
		{
			OpCount[2]++;
			using (Dc.PushOpacity(Math.Max(0, alpha)))
			{
				layout.Draw(Dc, new AV.Point(x, y));
			}
			return;
		}
		OpCount[3]++;
		layout.Draw(Dc, new AV.Point(x, y));
	}

	internal void PushRawClip(AV.Rect r, Action draw)
	{
		Sync();
		using (Dc.PushClip(r))
		{
			draw();
		}
	}

	public void Flush()
	{
	}

	public void Dispose()
	{
		for (int i = _pushed.Count - 1; i >= 0; i--)
		{
			_pushed[i].Dispose();
		}
		_pushed.Clear();
		_dirty = true;
		if (_owns)
		{
			Dc.Dispose();
		}
	}
}
