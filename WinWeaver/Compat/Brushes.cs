using System;
using System.Collections.Generic;
using AM = Avalonia.Media;
using AMI = Avalonia.Media.Immutable;
using AV = Avalonia;

namespace WinWeaver;

internal static class Conv
{
	public static AM.Color C(Color c) => AM.Color.FromArgb(c.A, c.R, c.G, c.B);

	public static AV.Rect R(Rectangle r) => new AV.Rect(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height));

	public static AV.Rect R(RectangleF r) => new AV.Rect(r.X, r.Y, Math.Max(0f, r.Width), Math.Max(0f, r.Height));

	public static AV.Point P(PointF p) => new AV.Point(p.X, p.Y);

	public static AV.Point P(Point p) => new AV.Point(p.X, p.Y);

	private static readonly Dictionary<int, AMI.ImmutableSolidColorBrush> _solid = new Dictionary<int, AMI.ImmutableSolidColorBrush>();

	public static AM.IBrush Solid(Color c)
	{
		int key = c.ToArgb();
		lock (_solid)
		{
			if (!_solid.TryGetValue(key, out var b))
			{
				if (_solid.Count > 4096)
				{
					_solid.Clear();
				}
				b = new AMI.ImmutableSolidColorBrush(C(c));
				_solid[key] = b;
			}
			return b;
		}
	}
}

internal class ColorBlend
{
	public Color[] Colors;

	public float[] Positions;

	public ColorBlend()
	{
		Colors = new Color[0];
		Positions = new float[0];
	}

	public ColorBlend(int count)
	{
		Colors = new Color[count];
		Positions = new float[count];
	}
}

internal class Blend
{
	public float[] Factors;

	public float[] Positions;

	public Blend()
	{
		Factors = new float[0];
		Positions = new float[0];
	}

	public Blend(int count)
	{
		Factors = new float[count];
		Positions = new float[count];
	}
}

internal abstract class Brush : IDisposable
{
	internal abstract AM.IBrush ToAvalonia();

	public virtual void Dispose()
	{
	}
}

internal sealed class SolidBrush : Brush
{
	public Color Color { get; set; }

	public SolidBrush(Color c)
	{
		Color = c;
	}

	internal override AM.IBrush ToAvalonia() => Conv.Solid(Color);
}

internal sealed class LinearGradientBrush : Brush
{
	private readonly AV.Point _p1;

	private readonly AV.Point _p2;

	private readonly Color _c1;

	private readonly Color _c2;

	public ColorBlend InterpolationColors { get; set; }

	public Blend Blend { get; set; }

	public WrapMode WrapMode { get; set; }

	public bool GammaCorrection { get; set; }

	public Color[] LinearColors => new[] { _c1, _c2 };

	public LinearGradientBrush(RectangleF r, Color c1, Color c2, float angle, bool isAngleScaleable = false)
	{
		double a = angle * Math.PI / 180.0;
		double cos = Math.Cos(a), sin = Math.Sin(a);
		double cx = r.X + r.Width / 2.0, cy = r.Y + r.Height / 2.0;
		double half = (Math.Abs(r.Width * cos) + Math.Abs(r.Height * sin)) / 2.0;
		_p1 = new AV.Point(cx - cos * half, cy - sin * half);
		_p2 = new AV.Point(cx + cos * half, cy + sin * half);
		_c1 = c1;
		_c2 = c2;
	}

	public LinearGradientBrush(Rectangle r, Color c1, Color c2, float angle, bool isAngleScaleable = false)
		: this((RectangleF)r, c1, c2, angle)
	{
	}

	public LinearGradientBrush(RectangleF r, Color c1, Color c2, LinearGradientMode mode)
		: this(r, c1, c2, mode switch
		{
			LinearGradientMode.Vertical => 90f,
			LinearGradientMode.ForwardDiagonal => 45f,
			LinearGradientMode.BackwardDiagonal => 135f,
			_ => 0f,
		})
	{
	}

	public LinearGradientBrush(Rectangle r, Color c1, Color c2, LinearGradientMode mode)
		: this((RectangleF)r, c1, c2, mode)
	{
	}

	public LinearGradientBrush(PointF p1, PointF p2, Color c1, Color c2)
	{
		_p1 = Conv.P(p1);
		_p2 = Conv.P(p2);
		_c1 = c1;
		_c2 = c2;
	}

	public LinearGradientBrush(Point p1, Point p2, Color c1, Color c2)
		: this((PointF)p1, (PointF)p2, c1, c2)
	{
	}

	public void SetSigmaBellShape(float focus, float scale = 1f)
	{
		Blend = new Blend(3) { Positions = new[] { 0f, focus, 1f }, Factors = new[] { 0f, scale, 0f } };
	}

	public void SetBlendTriangularShape(float focus, float scale = 1f)
	{
		Blend = new Blend(3) { Positions = new[] { 0f, focus, 1f }, Factors = new[] { 0f, scale, 0f } };
	}

	internal override AM.IBrush ToAvalonia()
	{
		var stops = new List<AMI.ImmutableGradientStop>();
		if (InterpolationColors != null && InterpolationColors.Colors != null && InterpolationColors.Colors.Length > 0)
		{
			var ic = InterpolationColors;
			for (int i = 0; i < ic.Colors.Length; i++)
			{
				float pos = (ic.Positions != null && i < ic.Positions.Length) ? ic.Positions[i] : (ic.Colors.Length == 1 ? 0f : (float)i / (ic.Colors.Length - 1));
				stops.Add(new AMI.ImmutableGradientStop(pos, Conv.C(ic.Colors[i])));
			}
		}
		else if (Blend != null && Blend.Factors != null && Blend.Factors.Length > 0)
		{
			for (int i = 0; i < Blend.Factors.Length; i++)
			{
				stops.Add(new AMI.ImmutableGradientStop(Blend.Positions[i], Conv.C(Theme.Mix(_c1, _c2, Blend.Factors[i]))));
			}
		}
		else
		{
			stops.Add(new AMI.ImmutableGradientStop(0, Conv.C(_c1)));
			stops.Add(new AMI.ImmutableGradientStop(1, Conv.C(_c2)));
		}
		return new AMI.ImmutableLinearGradientBrush(stops, 1.0, null, null, AM.GradientSpreadMethod.Pad,
			new AV.RelativePoint(_p1, AV.RelativeUnit.Absolute), new AV.RelativePoint(_p2, AV.RelativeUnit.Absolute));
	}
}

internal sealed class PathGradientBrush : Brush
{
	private readonly RectangleF _bounds;

	public Color CenterColor { get; set; } = Color.Black;

	public Color[] SurroundColors { get; set; } = new[] { Color.White };

	public ColorBlend InterpolationColors { get; set; }

	public PointF FocusScales { get; set; }

	public PointF CenterPoint { get; set; }

	public WrapMode WrapMode { get; set; }

	public PathGradientBrush(GraphicsPath path)
	{
		_bounds = path.GetBounds();
		CenterPoint = new PointF(_bounds.X + _bounds.Width / 2f, _bounds.Y + _bounds.Height / 2f);
	}

	internal override AM.IBrush ToAvalonia()
	{
		var stops = new List<AM.GradientStop>();
		if (InterpolationColors != null && InterpolationColors.Colors != null && InterpolationColors.Colors.Length > 0)
		{
			// В GDI+ позиция 0 — край фигуры, 1 — центр.
			var ic = InterpolationColors;
			for (int i = ic.Colors.Length - 1; i >= 0; i--)
			{
				stops.Add(new AM.GradientStop(Conv.C(ic.Colors[i]), 1.0 - ic.Positions[i]));
			}
		}
		else
		{
			Color surround = (SurroundColors != null && SurroundColors.Length > 0) ? SurroundColors[0] : Color.Transparent;
			stops.Add(new AM.GradientStop(Conv.C(CenterColor), 0));
			float f = Math.Max(FocusScales.X, FocusScales.Y);
			if (f > 0f && f < 1f)
			{
				stops.Add(new AM.GradientStop(Conv.C(CenterColor), f));
			}
			stops.Add(new AM.GradientStop(Conv.C(surround), 1));
		}
		var center = new AV.RelativePoint(CenterPoint.X, CenterPoint.Y, AV.RelativeUnit.Absolute);
		var b = new AM.RadialGradientBrush
		{
			Center = center,
			GradientOrigin = center,
			RadiusX = new AV.RelativeScalar(Math.Max(0.5, _bounds.Width / 2.0), AV.RelativeUnit.Absolute),
			RadiusY = new AV.RelativeScalar(Math.Max(0.5, _bounds.Height / 2.0), AV.RelativeUnit.Absolute),
			SpreadMethod = AM.GradientSpreadMethod.Pad,
		};
		foreach (var s in stops)
		{
			b.GradientStops.Add(s);
		}
		return b;
	}
}

internal sealed class TextureBrush : Brush
{
	private readonly Image _img;

	public WrapMode WrapMode { get; set; }

	public TextureBrush(Image img)
	{
		_img = img;
	}

	public TextureBrush(Image img, WrapMode mode)
	{
		_img = img;
		WrapMode = mode;
	}

	internal override AM.IBrush ToAvalonia()
	{
		var src = _img?.AvSource;
		if (src == null)
		{
			return Conv.Solid(Color.Transparent);
		}
		return new AM.ImageBrush((AM.IImageBrushSource)src)
		{
			TileMode = AM.TileMode.Tile,
			Stretch = AM.Stretch.None,
			AlignmentX = AM.AlignmentX.Left,
			AlignmentY = AM.AlignmentY.Top,
			DestinationRect = new AV.RelativeRect(0, 0, _img.Width, _img.Height, AV.RelativeUnit.Absolute),
		};
	}
}

internal sealed class Pen : IDisposable
{
	public Color Color { get; set; }

	public Brush Brush { get; set; }

	public float Width { get; set; }

	public LineCap StartCap { get; set; }

	public LineCap EndCap { get; set; }

	public LineJoin LineJoin { get; set; }

	public DashStyle DashStyle { get; set; }

	public float[] DashPattern { get; set; }

	public PenAlignment Alignment { get; set; }

	public Pen(Color c, float width = 1f)
	{
		Color = c;
		Width = width;
	}

	public Pen(Brush b, float width = 1f)
	{
		Brush = b;
		Color = (b as SolidBrush)?.Color ?? Color.White;
		Width = width;
	}

	public void SetLineCap(LineCap start, LineCap end, int dashCap)
	{
		StartCap = start;
		EndCap = end;
	}

	internal AM.IPen ToAvalonia()
	{
		AM.IBrush brush = Brush != null ? Brush.ToAvalonia() : Conv.Solid(Color);
		AM.PenLineCap cap = (StartCap == LineCap.Round || EndCap == LineCap.Round || StartCap == LineCap.RoundAnchor) ? AM.PenLineCap.Round
			: (StartCap == LineCap.Square || EndCap == LineCap.Square) ? AM.PenLineCap.Square : AM.PenLineCap.Flat;
		AM.PenLineJoin join = LineJoin switch
		{
			LineJoin.Round => AM.PenLineJoin.Round,
			LineJoin.Bevel => AM.PenLineJoin.Bevel,
			_ => AM.PenLineJoin.Miter,
		};
		AMI.ImmutableDashStyle dash = null;
		switch (DashStyle)
		{
		case DashStyle.Dash:
			dash = new AMI.ImmutableDashStyle(new double[] { 3, 1 }, 0);
			break;
		case DashStyle.Dot:
			dash = new AMI.ImmutableDashStyle(new double[] { 1, 1 }, 0);
			break;
		case DashStyle.DashDot:
			dash = new AMI.ImmutableDashStyle(new double[] { 3, 1, 1, 1 }, 0);
			break;
		case DashStyle.DashDotDot:
			dash = new AMI.ImmutableDashStyle(new double[] { 3, 1, 1, 1, 1, 1 }, 0);
			break;
		case DashStyle.Custom:
			if (DashPattern != null)
			{
				var d = new double[DashPattern.Length];
				for (int i = 0; i < d.Length; i++)
				{
					d[i] = DashPattern[i];
				}
				dash = new AMI.ImmutableDashStyle(d, 0);
			}
			break;
		}
		var ib = brush as AM.IImmutableBrush ?? AM.BrushExtensions.ToImmutable(brush);
		return new AMI.ImmutablePen(ib, Width, dash, cap, join);
	}

	public void Dispose()
	{
	}
}
