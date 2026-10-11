using System;

namespace WinWeaver;

[Flags] internal enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
internal enum GraphicsUnit { World = 0, Display = 1, Pixel = 2, Point = 3, Inch = 4, Document = 5, Millimeter = 6 }
internal enum SmoothingMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, AntiAlias = 4 }
internal enum PixelOffsetMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, Half = 4 }
internal enum InterpolationMode { Invalid = -1, Default = 0, Low = 1, High = 2, Bilinear = 3, Bicubic = 4, NearestNeighbor = 5, HighQualityBilinear = 6, HighQualityBicubic = 7 }
internal enum CompositingQuality { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, GammaCorrected = 3, AssumeLinear = 4 }
internal enum CompositingMode { SourceOver = 0, SourceCopy = 1 }
internal enum TextRenderingHint { SystemDefault = 0, SingleBitPerPixelGridFit = 1, SingleBitPerPixel = 2, AntiAliasGridFit = 3, AntiAlias = 4, ClearTypeGridFit = 5 }
internal enum LineCap { Flat = 0, Square = 1, Round = 2, Triangle = 3, NoAnchor = 16, SquareAnchor = 17, RoundAnchor = 18, DiamondAnchor = 19, ArrowAnchor = 20, AnchorMask = 240, Custom = 255 }
internal enum LineJoin { Miter = 0, Bevel = 1, Round = 2, MiterClipped = 3 }
internal enum DashStyle { Solid = 0, Dash = 1, Dot = 2, DashDot = 3, DashDotDot = 4, Custom = 5 }
internal enum PenAlignment { Center = 0, Inset = 1, Outset = 2, Left = 3, Right = 4 }
internal enum CombineMode { Replace = 0, Intersect = 1, Union = 2, Xor = 3, Exclude = 4, Complement = 5 }
internal enum FillMode { Alternate = 0, Winding = 1 }
internal enum LinearGradientMode { Horizontal = 0, Vertical = 1, ForwardDiagonal = 2, BackwardDiagonal = 3 }
internal enum WrapMode { Tile = 0, TileFlipX = 1, TileFlipY = 2, TileFlipXY = 3, Clamp = 4 }
internal enum MatrixOrder { Prepend = 0, Append = 1 }
internal enum ColorMatrixFlag { Default = 0, SkipGrays = 1, AltGrays = 2 }
internal enum ColorAdjustType { Default = 0, Bitmap = 1, Brush = 2, Pen = 3, Text = 4, Count = 5, Any = 6 }
internal enum PixelFormat
{
	DontCare = 0, Format24bppRgb = 137224, Format32bppRgb = 139273, Format32bppArgb = 2498570, Format32bppPArgb = 925707
}
internal enum StringAlignment { Near = 0, Center = 1, Far = 2 }
internal enum StringTrimming { None = 0, Character = 1, Word = 2, EllipsisCharacter = 3, EllipsisWord = 4, EllipsisPath = 5 }
[Flags] internal enum StringFormatFlags { NoWrap = 4096, LineLimit = 8192, NoClip = 16384 }

[Flags]
internal enum TextFormatFlags
{
	Default = 0, GlyphOverhangPadding = 0, Left = 0, Top = 0,
	HorizontalCenter = 1, Right = 2, VerticalCenter = 4, Bottom = 8, WordBreak = 16, SingleLine = 32,
	ExpandTabs = 64, NoClipping = 256, ExternalLeading = 512, NoPrefix = 2048, Internal = 4096,
	TextBoxControl = 8192, PathEllipsis = 16384, EndEllipsis = 32768, ModifyString = 65536,
	RightToLeft = 131072, WordEllipsis = 262144, NoFullWidthCharacterBreak = 524288, HidePrefix = 1048576,
	PrefixOnly = 2097152, PreserveGraphicsClipping = 16777216, PreserveGraphicsTranslateTransform = 33554432,
	NoPadding = 268435456, LeftAndRightPadding = 536870912
}
