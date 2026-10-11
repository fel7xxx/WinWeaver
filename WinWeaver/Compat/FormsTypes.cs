using System;
using System.ComponentModel;

namespace WinWeaver;

internal interface IWin32Window
{
	IntPtr Handle { get; }
}

internal enum DialogResult { None = 0, OK = 1, Cancel = 2, Abort = 3, Retry = 4, Ignore = 5, Yes = 6, No = 7 }

[Flags]
internal enum MouseButtons { None = 0, Left = 0x100000, Right = 0x200000, Middle = 0x400000, XButton1 = 0x800000, XButton2 = 0x1000000 }

internal enum FormBorderStyle { None = 0, FixedSingle = 1, Fixed3D = 2, FixedDialog = 3, Sizable = 4, FixedToolWindow = 5, SizableToolWindow = 6 }
internal enum FormStartPosition { Manual = 0, CenterScreen = 1, WindowsDefaultLocation = 2, WindowsDefaultBounds = 3, CenterParent = 4 }
internal enum FormWindowState { Normal = 0, Minimized = 1, Maximized = 2 }
internal enum AutoScaleMode { None = 0, Font = 1, Dpi = 2, Inherit = 3 }
internal enum DockStyle { None = 0, Top = 1, Bottom = 2, Left = 3, Right = 4, Fill = 5 }
internal enum CloseReason { None = 0, WindowsShutDown = 1, MdiFormClosing = 2, UserClosing = 3, TaskManagerClosing = 4, FormOwnerClosing = 5, ApplicationExitCall = 6 }
internal enum BorderStyle { None = 0, FixedSingle = 1, Fixed3D = 2 }
internal enum ScrollBars { None = 0, Horizontal = 1, Vertical = 2, Both = 3 }
internal enum UnhandledExceptionMode { Automatic = 0, ThrowException = 1, CatchException = 2 }
internal enum HorizontalAlignment { Left = 0, Right = 1, Center = 2 }
[Flags] internal enum BoundsSpecified { None = 0, X = 1, Y = 2, Location = 3, Width = 4, Height = 8, Size = 12, All = 15 }
[Flags] internal enum AnchorStyles { None = 0, Top = 1, Bottom = 2, Left = 4, Right = 8 }

[Flags]
internal enum ControlStyles
{
	ContainerControl = 1, UserPaint = 2, Opaque = 4, ResizeRedraw = 16, FixedWidth = 32, FixedHeight = 64,
	StandardClick = 256, Selectable = 512, UserMouse = 1024, SupportsTransparentBackColor = 2048,
	StandardDoubleClick = 4096, AllPaintingInWmPaint = 8192, CacheText = 16384, EnableNotifyMessage = 32768,
	DoubleBuffer = 65536, OptimizedDoubleBuffer = 131072, UseTextForAccessibility = 262144
}

[Flags]
internal enum Keys
{
	None = 0, LButton = 1, RButton = 2, Cancel = 3, Back = 8, Tab = 9, Clear = 12, Return = 13, Enter = 13,
	ShiftKey = 16, ControlKey = 17, Menu = 18, Pause = 19, Capital = 20, Escape = 27, Space = 32,
	Prior = 33, PageUp = 33, Next = 34, PageDown = 34, End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40,
	Insert = 45, Delete = 46,
	D0 = 48, D1, D2, D3, D4, D5, D6, D7, D8, D9,
	A = 65, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
	NumPad0 = 96, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
	F1 = 112, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
	KeyCode = 0xFFFF, Shift = 0x10000, Control = 0x20000, Alt = 0x40000, Modifiers = -65536
}

internal class MouseEventArgs : EventArgs
{
	public MouseButtons Button { get; }

	public int Clicks { get; }

	public int X { get; }

	public int Y { get; }

	public int Delta { get; }

	public Point Location => new Point(X, Y);

	public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
	{
		Button = button;
		Clicks = clicks;
		X = x;
		Y = y;
		Delta = delta;
	}
}

internal class HandledMouseEventArgs : MouseEventArgs
{
	public bool Handled { get; set; }

	public HandledMouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
		: base(button, clicks, x, y, delta)
	{
	}
}

internal class KeyEventArgs : EventArgs
{
	public Keys KeyData { get; }

	public Keys KeyCode => KeyData & Keys.KeyCode;

	public Keys Modifiers => KeyData & Keys.Modifiers;

	public int KeyValue => (int)KeyCode;

	public bool Control => (KeyData & Keys.Control) != 0;

	public bool Shift => (KeyData & Keys.Shift) != 0;

	public bool Alt => (KeyData & Keys.Alt) != 0;

	public bool Handled { get; set; }

	public bool SuppressKeyPress
	{
		get => Handled;
		set => Handled = value;
	}

	public KeyEventArgs(Keys keyData)
	{
		KeyData = keyData;
	}
}

internal class KeyPressEventArgs : EventArgs
{
	public char KeyChar { get; set; }

	public bool Handled { get; set; }

	public KeyPressEventArgs(char c)
	{
		KeyChar = c;
	}
}

internal class PaintEventArgs : EventArgs, IDisposable
{
	public Graphics Graphics { get; }

	public Rectangle ClipRectangle { get; }

	public PaintEventArgs(Graphics g, Rectangle clip)
	{
		Graphics = g;
		ClipRectangle = clip;
	}

	public void Dispose()
	{
	}
}

internal class ControlEventArgs : EventArgs
{
	public Control Control { get; }

	public ControlEventArgs(Control c)
	{
		Control = c;
	}
}

internal class LayoutEventArgs : EventArgs
{
	public Control AffectedControl { get; }

	public string AffectedProperty { get; }

	public LayoutEventArgs(Control c, string prop)
	{
		AffectedControl = c;
		AffectedProperty = prop;
	}
}

internal class FormClosingEventArgs : CancelEventArgs
{
	public CloseReason CloseReason { get; }

	public FormClosingEventArgs(CloseReason r, bool cancel)
		: base(cancel)
	{
		CloseReason = r;
	}
}

internal class FormClosedEventArgs : EventArgs
{
	public CloseReason CloseReason { get; }

	public FormClosedEventArgs(CloseReason r)
	{
		CloseReason = r;
	}
}

internal delegate void MouseEventHandler(object sender, MouseEventArgs e);
internal delegate void KeyEventHandler(object sender, KeyEventArgs e);
internal delegate void PaintEventHandler(object sender, PaintEventArgs e);
internal delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);
internal delegate void FormClosedEventHandler(object sender, FormClosedEventArgs e);
internal delegate void ControlEventHandler(object sender, ControlEventArgs e);
internal delegate void LayoutEventHandler(object sender, LayoutEventArgs e);

internal struct Padding
{
	public int Left, Top, Right, Bottom;

	public Padding(int all)
	{
		Left = Top = Right = Bottom = all;
	}

	public Padding(int l, int t, int r, int b)
	{
		Left = l;
		Top = t;
		Right = r;
		Bottom = b;
	}

	public int All
	{
		get => Left;
		set => Left = Top = Right = Bottom = value;
	}

	public int Horizontal => Left + Right;

	public int Vertical => Top + Bottom;

	public static readonly Padding Empty = default(Padding);
}
