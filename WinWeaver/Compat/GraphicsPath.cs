using System;
using System.Collections.Generic;
using AM = Avalonia.Media;
using AV = Avalonia;

namespace WinWeaver;

/// <summary>Аналог System.Drawing.Drawing2D.GraphicsPath, собирающий Avalonia StreamGeometry.</summary>
internal sealed class GraphicsPath : IDisposable
{
	private enum Kind { Line, Bezier }

	private struct Seg
	{
		public Kind K;
		public PointF A, B, C;
	}

	private sealed class Figure
	{
		public PointF Start;
		public readonly List<Seg> Segs = new List<Seg>();
		public bool Closed;
		public PointF Last => Segs.Count == 0 ? Start : (Segs[Segs.Count - 1].K == Kind.Line ? Segs[Segs.Count - 1].A : Segs[Segs.Count - 1].C);
	}

	private readonly List<Figure> _figs = new List<Figure>();

	private readonly List<AM.Geometry> _extra = new List<AM.Geometry>();

	private bool _newFigure = true;

	private AM.Geometry _geo;

	public FillMode FillMode { get; set; }

	public GraphicsPath()
	{
	}

	public GraphicsPath(FillMode mode)
	{
		FillMode = mode;
	}

	private Figure Cur(PointF start)
	{
		_geo = null;
		if (_newFigure || _figs.Count == 0 || _figs[_figs.Count - 1].Closed)
		{
			var f = new Figure { Start = start };
			_figs.Add(f);
			_newFigure = false;
			return f;
		}
		var cur = _figs[_figs.Count - 1];
		PointF last = cur.Last;
		if (Math.Abs(last.X - start.X) > 0.001f || Math.Abs(last.Y - start.Y) > 0.001f)
		{
			cur.Segs.Add(new Seg { K = Kind.Line, A = start });
		}
		return cur;
	}

	public void StartFigure()
	{
		_newFigure = true;
	}

	public void CloseFigure()
	{
		if (_figs.Count > 0)
		{
			_figs[_figs.Count - 1].Closed = true;
		}
		_newFigure = true;
		_geo = null;
	}

	public void CloseAllFigures()
	{
		foreach (var f in _figs)
		{
			f.Closed = true;
		}
		_newFigure = true;
		_geo = null;
	}

	public void Reset()
	{
		_figs.Clear();
		_extra.Clear();
		_newFigure = true;
		_geo = null;
	}

	public void AddLine(float x1, float y1, float x2, float y2)
	{
		var f = Cur(new PointF(x1, y1));
		f.Segs.Add(new Seg { K = Kind.Line, A = new PointF(x2, y2) });
	}

	public void AddLine(PointF a, PointF b) => AddLine(a.X, a.Y, b.X, b.Y);

	public void AddLine(Point a, Point b) => AddLine(a.X, a.Y, b.X, b.Y);

	public void AddLine(int x1, int y1, int x2, int y2) => AddLine((float)x1, y1, x2, y2);

	public void AddLines(PointF[] pts)
	{
		if (pts == null || pts.Length == 0)
		{
			return;
		}
		var f = Cur(pts[0]);
		for (int i = 1; i < pts.Length; i++)
		{
			f.Segs.Add(new Seg { K = Kind.Line, A = pts[i] });
		}
	}

	public void AddLines(Point[] pts)
	{
		if (pts == null)
		{
			return;
		}
		var p = new PointF[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			p[i] = pts[i];
		}
		AddLines(p);
	}

	public void AddPolygon(PointF[] pts)
	{
		StartFigure();
		AddLines(pts);
		CloseFigure();
	}

	public void AddPolygon(Point[] pts)
	{
		StartFigure();
		AddLines(pts);
		CloseFigure();
	}

	public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
	{
		var f = Cur(new PointF(x1, y1));
		f.Segs.Add(new Seg { K = Kind.Bezier, A = new PointF(x2, y2), B = new PointF(x3, y3), C = new PointF(x4, y4) });
	}

	public void AddBezier(PointF a, PointF b, PointF c, PointF d) => AddBezier(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y);

	public void AddArc(float x, float y, float w, float h, float startAngle, float sweepAngle)
	{
		if (w <= 0 || h <= 0)
		{
			return;
		}
		double rx = w / 2.0, ry = h / 2.0, cx = x + rx, cy = y + ry;
		PointF At(double deg)
		{
			double t = ParamAngle(deg, rx, ry);
			return new PointF((float)(cx + rx * Math.Cos(t)), (float)(cy + ry * Math.Sin(t)));
		}
		var f = Cur(At(startAngle));
		int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepAngle) / 90.0 - 1e-6));
		double step = sweepAngle / (double)n;
		double a0 = startAngle;
		for (int i = 0; i < n; i++)
		{
			double a1 = a0 + step;
			double t0 = ParamAngle(a0, rx, ry), t1 = ParamAngle(a1, rx, ry);
			if (step > 0 && t1 < t0) t1 += Math.PI * 2;
			if (step < 0 && t1 > t0) t1 -= Math.PI * 2;
			double k = 4.0 / 3.0 * Math.Tan((t1 - t0) / 4.0);
			double c0 = Math.Cos(t0), s0 = Math.Sin(t0), c1 = Math.Cos(t1), s1 = Math.Sin(t1);
			var p1 = new PointF((float)(cx + rx * (c0 - k * s0)), (float)(cy + ry * (s0 + k * c0)));
			var p2 = new PointF((float)(cx + rx * (c1 + k * s1)), (float)(cy + ry * (s1 - k * c1)));
			var p3 = new PointF((float)(cx + rx * c1), (float)(cy + ry * s1));
			f.Segs.Add(new Seg { K = Kind.Bezier, A = p1, B = p2, C = p3 });
			a0 = a1;
		}
	}

	// GDI+ задаёт углы дуги в пространстве эллипса, а не параметрически.
	private static double ParamAngle(double deg, double rx, double ry)
	{
		double r = deg * Math.PI / 180.0;
		if (Math.Abs(rx - ry) < 1e-9)
		{
			return r;
		}
		double turns = Math.Floor(r / (Math.PI * 2));
		double rr = r - turns * Math.PI * 2;
		double t = Math.Atan2(Math.Sin(rr) * rx, Math.Cos(rr) * ry);
		if (t < 0) t += Math.PI * 2;
		return t + turns * Math.PI * 2;
	}

	public void AddArc(RectangleF r, float start, float sweep) => AddArc(r.X, r.Y, r.Width, r.Height, start, sweep);

	public void AddArc(Rectangle r, float start, float sweep) => AddArc(r.X, r.Y, r.Width, r.Height, start, sweep);

	public void AddArc(int x, int y, int w, int h, float start, float sweep) => AddArc((float)x, y, w, h, start, sweep);

	public void AddEllipse(float x, float y, float w, float h)
	{
		StartFigure();
		AddArc(x, y, w, h, 0, 360);
		CloseFigure();
	}

	public void AddEllipse(RectangleF r) => AddEllipse(r.X, r.Y, r.Width, r.Height);

	public void AddEllipse(Rectangle r) => AddEllipse(r.X, r.Y, r.Width, r.Height);

	public void AddEllipse(int x, int y, int w, int h) => AddEllipse((float)x, y, w, h);

	public void AddRectangle(RectangleF r)
	{
		StartFigure();
		AddLines(new[] { new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom) });
		CloseFigure();
	}

	public void AddRectangle(Rectangle r) => AddRectangle((RectangleF)r);

	public void AddPath(GraphicsPath other, bool connect)
	{
		if (other == null)
		{
			return;
		}
		foreach (var f in other._figs)
		{
			var nf = new Figure { Start = f.Start, Closed = f.Closed };
			nf.Segs.AddRange(f.Segs);
			_figs.Add(nf);
		}
		_extra.AddRange(other._extra);
		_newFigure = true;
		_geo = null;
	}

	public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat format)
	{
		if (string.IsNullOrEmpty(s))
		{
			return;
		}
		var font = new Font(family, emSize, (FontStyle)style, GraphicsUnit.Pixel);
		var ft = new AM.FormattedText(s, System.Globalization.CultureInfo.CurrentCulture, AM.FlowDirection.LeftToRight, font.Typeface, emSize, AM.Brushes.White);
		var g = ft.BuildGeometry(new AV.Point(origin.X, origin.Y));
		if (g != null)
		{
			_extra.Add(g);
		}
		_newFigure = true;
		_geo = null;
	}

	public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat format)
		=> AddString(s, family, style, emSize, (PointF)origin, format);

	public RectangleF GetBounds()
	{
		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
		void Acc(PointF p)
		{
			if (p.X < minX) minX = p.X;
			if (p.Y < minY) minY = p.Y;
			if (p.X > maxX) maxX = p.X;
			if (p.Y > maxY) maxY = p.Y;
		}
		foreach (var f in _figs)
		{
			Acc(f.Start);
			foreach (var s in f.Segs)
			{
				Acc(s.A);
				if (s.K == Kind.Bezier)
				{
					Acc(s.B);
					Acc(s.C);
				}
			}
		}
		foreach (var g in _extra)
		{
			var b = g.Bounds;
			Acc(new PointF((float)b.X, (float)b.Y));
			Acc(new PointF((float)b.Right, (float)b.Bottom));
		}
		if (minX > maxX)
		{
			return RectangleF.Empty;
		}
		return new RectangleF(minX, minY, maxX - minX, maxY - minY);
	}

	public PointF[] PathPoints
	{
		get
		{
			var l = new List<PointF>();
			foreach (var f in _figs)
			{
				l.Add(f.Start);
				foreach (var s in f.Segs)
				{
					l.Add(s.K == Kind.Line ? s.A : s.C);
				}
			}
			return l.ToArray();
		}
	}

	public int PointCount => PathPoints.Length;

	internal AM.Geometry ToGeometry()
	{
		if (_geo != null)
		{
			return _geo;
		}
		var sg = new AM.StreamGeometry();
		using (var ctx = sg.Open())
		{
			ctx.SetFillRule(FillMode == FillMode.Winding ? AM.FillRule.NonZero : AM.FillRule.EvenOdd);
			foreach (var f in _figs)
			{
				ctx.BeginFigure(Conv.P(f.Start), true);
				foreach (var s in f.Segs)
				{
					if (s.K == Kind.Line)
					{
						ctx.LineTo(Conv.P(s.A));
					}
					else
					{
						ctx.CubicBezierTo(Conv.P(s.A), Conv.P(s.B), Conv.P(s.C));
					}
				}
				ctx.EndFigure(f.Closed);
			}
		}
		if (_extra.Count == 0)
		{
			_geo = sg;
		}
		else
		{
			var grp = new AM.GeometryGroup { FillRule = AM.FillRule.NonZero };
			if (_figs.Count > 0)
			{
				grp.Children.Add(sg);
			}
			foreach (var e in _extra)
			{
				grp.Children.Add(e);
			}
			_geo = grp;
		}
		return _geo;
	}

	public bool IsVisible(float x, float y) => ToGeometry().FillContains(new AV.Point(x, y));

	public bool IsVisible(Point p) => IsVisible(p.X, p.Y);

	public bool IsVisible(PointF p) => IsVisible(p.X, p.Y);

	public GraphicsPath Clone()
	{
		var g = new GraphicsPath(FillMode);
		g.AddPath(this, false);
		return g;
	}

	public void Dispose()
	{
	}
}

internal sealed class StringFormat : IDisposable
{
	public StringAlignment Alignment { get; set; }

	public StringAlignment LineAlignment { get; set; }

	public StringTrimming Trimming { get; set; }

	public StringFormatFlags FormatFlags { get; set; }

	public static StringFormat GenericTypographic => new StringFormat();

	public static StringFormat GenericDefault => new StringFormat();

	public void Dispose()
	{
	}
}

/// <summary>Матрица преобразования (аналог Drawing2D.Matrix).</summary>
internal sealed class Matrix : IDisposable
{
	internal AV.Matrix M;

	public Matrix()
	{
		M = AV.Matrix.Identity;
	}

	internal Matrix(AV.Matrix m)
	{
		M = m;
	}

	public Matrix(float m11, float m12, float m21, float m22, float dx, float dy)
	{
		M = new AV.Matrix(m11, m12, m21, m22, dx, dy);
	}

	public float OffsetX => (float)M.M31;

	public float OffsetY => (float)M.M32;

	public bool IsIdentity => M.IsIdentity;

	public void Translate(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
	{
		var t = AV.Matrix.CreateTranslation(dx, dy);
		M = order == MatrixOrder.Prepend ? t * M : M * t;
	}

	public void Scale(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend)
	{
		var t = AV.Matrix.CreateScale(sx, sy);
		M = order == MatrixOrder.Prepend ? t * M : M * t;
	}

	public void Rotate(float deg, MatrixOrder order = MatrixOrder.Prepend)
	{
		var t = AV.Matrix.CreateRotation(deg * Math.PI / 180.0);
		M = order == MatrixOrder.Prepend ? t * M : M * t;
	}

	public Matrix Clone() => new Matrix(M);

	public void Dispose()
	{
	}
}

/// <summary>Область отсечения: пересечение набора геометрий в координатах устройства (null — бесконечная).</summary>
internal sealed class Region : IDisposable
{
	internal readonly List<AM.Geometry> Parts = new List<AM.Geometry>();

	internal bool Empty;

	public Region()
	{
	}

	public Region(Rectangle r)
	{
		Parts.Add(new AM.RectangleGeometry(Conv.R(r)));
	}

	public Region(RectangleF r)
	{
		Parts.Add(new AM.RectangleGeometry(Conv.R(r)));
	}

	public Region(GraphicsPath p)
	{
		Parts.Add(p.ToGeometry());
	}

	internal Region(IEnumerable<AM.Geometry> parts, bool empty)
	{
		Parts.AddRange(parts);
		Empty = empty;
	}

	public bool IsInfinite(Graphics g) => Parts.Count == 0 && !Empty;

	public bool IsEmpty(Graphics g) => Empty;

	public Region Clone() => new Region(Parts, Empty);

	public void Intersect(Rectangle r) => Parts.Add(new AM.RectangleGeometry(Conv.R(r)));

	public void Intersect(RectangleF r) => Parts.Add(new AM.RectangleGeometry(Conv.R(r)));

	public void Intersect(GraphicsPath p) => Parts.Add(p.ToGeometry());

	public void Exclude(Rectangle r) => Parts.Add(Graphics.Excluding(new AM.RectangleGeometry(Conv.R(r))));

	public void Exclude(GraphicsPath p) => Parts.Add(Graphics.Excluding(p.ToGeometry()));

	public void MakeInfinite()
	{
		Parts.Clear();
		Empty = false;
	}

	public void MakeEmpty()
	{
		Parts.Clear();
		Empty = true;
	}

	internal AM.Geometry Single()
	{
		if (Parts.Count == 0)
		{
			return null;
		}
		AM.Geometry g = Parts[0];
		for (int i = 1; i < Parts.Count; i++)
		{
			g = new AM.CombinedGeometry(AM.GeometryCombineMode.Intersect, g, Parts[i]);
		}
		return g;
	}

	public void Dispose()
	{
	}
}
