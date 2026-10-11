// Примитивы System.Drawing (Color, Point, Rectangle…) — кроссплатформенные структуры.
// Всё остальное (Graphics, Font, Brush, Control, Form…) реализовано поверх Avalonia в папке Compat.
global using Color = System.Drawing.Color;
global using Point = System.Drawing.Point;
global using PointF = System.Drawing.PointF;
global using Size = System.Drawing.Size;
global using SizeF = System.Drawing.SizeF;
global using Rectangle = System.Drawing.Rectangle;
global using RectangleF = System.Drawing.RectangleF;
