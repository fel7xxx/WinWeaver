using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AC = Avalonia.Controls;
using AI = Avalonia.Input;
using AP = Avalonia.Platform.Storage;
using AT = Avalonia.Threading;
using AV = Avalonia;

namespace WinWeaver;

/// <summary>Таймер UI-потока (аналог System.Windows.Forms.Timer).</summary>
internal sealed class Timer : IDisposable
{
	private readonly AT.DispatcherTimer _t;

	private int _interval = 100;

	public event EventHandler Tick;

	public object Tag { get; set; }

	public Timer()
	{
		_t = new AT.DispatcherTimer(AT.DispatcherPriority.Normal);
		_t.Interval = TimeSpan.FromMilliseconds(_interval);
		_t.Tick += (s, e) =>
		{
			try
			{
				Tick?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				Application.OnThreadException(ex);
			}
		};
	}

	public Timer(System.ComponentModel.IContainer c)
		: this()
	{
	}

	public int Interval
	{
		get => _interval;
		set
		{
			_interval = Math.Max(1, value);
			_t.Interval = TimeSpan.FromMilliseconds(_interval);
		}
	}

	public bool Enabled
	{
		get => _t.IsEnabled;
		set
		{
			if (value) _t.Start();
			else _t.Stop();
		}
	}

	public void Start() => _t.Start();

	public void Stop() => _t.Stop();

	public void Dispose() => _t.Stop();
}

internal sealed class Cursor
{
	internal readonly AI.Cursor Av;

	internal readonly AI.StandardCursorType Type;

	internal Cursor(AI.StandardCursorType t)
	{
		Type = t;
		try
		{
			Av = new AI.Cursor(t);
		}
		catch
		{
			Av = null;
		}
	}

	private static Point _pos;

	internal static void TrackPosition(Point p) => _pos = p;

	public static Point Position
	{
		get => _pos;
		set => _pos = value;
	}

	public static Cursor Current { get; set; }
}

internal static class Cursors
{
	public static readonly Cursor Default = new Cursor(AI.StandardCursorType.Arrow);
	public static readonly Cursor Arrow = Default;
	public static readonly Cursor Hand = new Cursor(AI.StandardCursorType.Hand);
	public static readonly Cursor IBeam = new Cursor(AI.StandardCursorType.Ibeam);
	public static readonly Cursor WaitCursor = new Cursor(AI.StandardCursorType.Wait);
	public static readonly Cursor AppStarting = new Cursor(AI.StandardCursorType.AppStarting);
	public static readonly Cursor No = new Cursor(AI.StandardCursorType.No);
	public static readonly Cursor SizeAll = new Cursor(AI.StandardCursorType.SizeAll);
	public static readonly Cursor SizeNS = new Cursor(AI.StandardCursorType.SizeNorthSouth);
	public static readonly Cursor SizeWE = new Cursor(AI.StandardCursorType.SizeWestEast);
	public static readonly Cursor HSplit = SizeNS;
	public static readonly Cursor VSplit = SizeWE;
	public static readonly Cursor Help = new Cursor(AI.StandardCursorType.Help);
	public static readonly Cursor Cross = new Cursor(AI.StandardCursorType.Cross);
}

internal sealed class Screen
{
	public Rectangle Bounds { get; }

	public Rectangle WorkingArea { get; }

	public bool Primary { get; }

	private Screen(Rectangle b, Rectangle wa, bool primary)
	{
		Bounds = b;
		WorkingArea = wa;
		Primary = primary;
	}

	/// <summary>Коэффициент перевода физических пикселей в DIP (по основному экрану).</summary>
	internal static double Scale
	{
		get
		{
			var s = Info()?.Primary;
			return s != null && s.Scaling > 0 ? s.Scaling : 1.0;
		}
	}

	private static AC.Window _probe;

	private static AC.Screens Info()
	{
		try
		{
			foreach (var f in Form.OpenFormsList)
			{
				if (f.Window.Screens != null)
				{
					return f.Window.Screens;
				}
			}
			_probe ??= new AC.Window();
			return _probe.Screens;
		}
		catch
		{
			return null;
		}
	}

	private static Screen From(AV.Platform.Screen s)
	{
		if (s == null)
		{
			return new Screen(new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), true);
		}
		double k = s.Scaling > 0 ? s.Scaling : 1.0;
		Rectangle R(AV.PixelRect r) => new Rectangle((int)Math.Round(r.X / k), (int)Math.Round(r.Y / k), (int)Math.Round(r.Width / k), (int)Math.Round(r.Height / k));
		return new Screen(R(s.Bounds), R(s.WorkingArea), s.IsPrimary);
	}

	public static Screen PrimaryScreen => From(Info()?.Primary);

	public static Screen[] AllScreens
	{
		get
		{
			var info = Info();
			if (info == null)
			{
				return new[] { PrimaryScreen };
			}
			var l = new List<Screen>();
			foreach (var s in info.All)
			{
				l.Add(From(s));
			}
			return l.ToArray();
		}
	}

	public static Screen FromControl(Control c)
	{
		var f = c?.FindForm();
		if (f != null)
		{
			try
			{
				var s = f.Window.Screens.ScreenFromWindow(f.Window);
				if (s != null)
				{
					return From(s);
				}
			}
			catch
			{
			}
		}
		return PrimaryScreen;
	}

	public static Screen FromPoint(Point p) => PrimaryScreen;

	public static Screen FromRectangle(Rectangle r) => PrimaryScreen;
}

internal sealed class ThreadExceptionEventArgs : EventArgs
{
	public Exception Exception { get; }

	public ThreadExceptionEventArgs(Exception e)
	{
		Exception = e;
	}
}

internal delegate void ThreadExceptionEventHandler(object sender, ThreadExceptionEventArgs e);

/// <summary>Цикл сообщений и список окон (аналог System.Windows.Forms.Application) поверх диспетчера Avalonia.</summary>
internal static class Application
{
	public static event ThreadExceptionEventHandler ThreadException;

	internal static Icon AppIcon;

	public static IReadOnlyList<Form> OpenForms => Form.OpenFormsList;

	internal static void OnThreadException(Exception ex)
	{
		var h = ThreadException;
		if (h != null)
		{
			h(null, new ThreadExceptionEventArgs(ex));
		}
		else
		{
			Program.Log("UI ERROR " + ex);
		}
	}

	public static void Run(Form f)
	{
		var frame = new AT.DispatcherFrame();
		f.FormClosed += (s, e) => frame.Continue = false;
		f.Show();
		if (f.IsShownInternal)
		{
			AT.Dispatcher.UIThread.PushFrame(frame);
		}
	}

	public static void DoEvents()
	{
		try
		{
			AT.Dispatcher.UIThread.RunJobs();
		}
		catch
		{
		}
	}

	public static void Exit()
	{
		foreach (var f in new List<Form>(Form.OpenFormsList))
		{
			f.Close();
		}
	}

	public static string ExecutablePath => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

	public static string StartupPath => System.IO.Path.GetDirectoryName(ExecutablePath) ?? "";

	/// <summary>Синхронно дождаться задачи, продолжая обрабатывать UI.</summary>
	internal static T Wait<T>(Task<T> task)
	{
		if (!task.IsCompleted)
		{
			var frame = new AT.DispatcherFrame();
			task.ContinueWith(_ => AT.Dispatcher.UIThread.Post(() => frame.Continue = false));
			AT.Dispatcher.UIThread.PushFrame(frame);
		}
		return task.GetAwaiter().GetResult();
	}

	internal static void Wait(Task task)
	{
		if (!task.IsCompleted)
		{
			var frame = new AT.DispatcherFrame();
			task.ContinueWith(_ => AT.Dispatcher.UIThread.Post(() => frame.Continue = false));
			AT.Dispatcher.UIThread.PushFrame(frame);
		}
		task.GetAwaiter().GetResult();
	}

	internal static AC.TopLevel AnyTopLevel()
	{
		if (Form.ActiveForm != null)
		{
			return Form.ActiveForm.Window;
		}
		foreach (var f in Form.OpenFormsList)
		{
			return f.Window;
		}
		return null;
	}
}

internal static class Clipboard
{
	public static void SetText(string text)
	{
		var top = Application.AnyTopLevel();
		if (top?.Clipboard == null)
		{
			return;
		}
		try
		{
			Application.Wait(top.Clipboard.SetTextAsync(text ?? ""));
		}
		catch
		{
		}
	}

	public static string GetText()
	{
		var top = Application.AnyTopLevel();
		if (top?.Clipboard == null)
		{
			return "";
		}
		try
		{
			return Application.Wait(top.Clipboard.GetTextAsync()) ?? "";
		}
		catch
		{
			return "";
		}
	}
}

internal abstract class FileDialog : IDisposable
{
	public string FileName { get; set; } = "";

	public string Filter { get; set; } = "";

	public string Title { get; set; }

	public string DefaultExt { get; set; }

	public string InitialDirectory { get; set; }

	public bool AddExtension { get; set; } = true;

	public bool OverwritePrompt { get; set; } = true;

	public bool CheckFileExists { get; set; }

	public bool RestoreDirectory { get; set; }

	protected List<AP.FilePickerFileType> Types()
	{
		var l = new List<AP.FilePickerFileType>();
		if (string.IsNullOrEmpty(Filter))
		{
			return l;
		}
		var parts = Filter.Split('|');
		for (int i = 0; i + 1 < parts.Length; i += 2)
		{
			l.Add(new AP.FilePickerFileType(L.T(parts[i])) { Patterns = parts[i + 1].Split(';') });
		}
		return l;
	}

	protected static AC.TopLevel Top(IWin32Window owner) => (owner as Control)?.FindForm()?.Window ?? Application.AnyTopLevel();

	public abstract DialogResult ShowDialog(IWin32Window owner = null);

	public void Dispose()
	{
	}
}

internal sealed class SaveFileDialog : FileDialog
{
	public override DialogResult ShowDialog(IWin32Window owner = null)
	{
		var top = Top(owner);
		if (top == null)
		{
			return DialogResult.Cancel;
		}
		var opts = new AP.FilePickerSaveOptions
		{
			Title = L.T(Title ?? ""),
			SuggestedFileName = System.IO.Path.GetFileName(FileName ?? ""),
			DefaultExtension = DefaultExt,
			FileTypeChoices = Types(),
			ShowOverwritePrompt = OverwritePrompt,
		};
		var f = Application.Wait(top.StorageProvider.SaveFilePickerAsync(opts));
		var p = (f == null ? null : AP.StorageProviderExtensions.TryGetLocalPath(f));
		if (string.IsNullOrEmpty(p))
		{
			return DialogResult.Cancel;
		}
		FileName = p;
		return DialogResult.OK;
	}
}

internal sealed class OpenFileDialog : FileDialog
{
	public bool Multiselect { get; set; }

	public override DialogResult ShowDialog(IWin32Window owner = null)
	{
		var top = Top(owner);
		if (top == null)
		{
			return DialogResult.Cancel;
		}
		var opts = new AP.FilePickerOpenOptions
		{
			Title = L.T(Title ?? ""),
			AllowMultiple = false,
			FileTypeFilter = Types(),
		};
		var fs = Application.Wait(top.StorageProvider.OpenFilePickerAsync(opts));
		if (fs == null || fs.Count == 0)
		{
			return DialogResult.Cancel;
		}
		var p = AP.StorageProviderExtensions.TryGetLocalPath(fs[0]);
		if (string.IsNullOrEmpty(p))
		{
			return DialogResult.Cancel;
		}
		FileName = p;
		return DialogResult.OK;
	}
}

/// <summary>Простая панель-контейнер.</summary>
internal class Panel : Control
{
	public BorderStyle BorderStyle { get; set; }

	public bool AutoScroll { get; set; }
}

/// <summary>Поле ввода: настоящий Avalonia TextBox внутри контрола.</summary>
internal class TextBox : Control
{
	internal readonly AC.TextBox Box;

	private bool _syncing;

	public TextBox()
	{
		Box = new AC.TextBox
		{
			BorderThickness = new AV.Thickness(0),
			Padding = new AV.Thickness(0),
			MinHeight = 0,
			Background = Avalonia.Media.Brushes.Transparent,
			VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
			CaretBrush = Avalonia.Media.Brushes.White,
			AcceptsReturn = false,
		};
		Box.Resources["TextControlBackgroundPointerOver"] = Avalonia.Media.Brushes.Transparent;
		Box.Resources["TextControlBackgroundFocused"] = Avalonia.Media.Brushes.Transparent;
		Box.Resources["TextControlBorderBrushPointerOver"] = Avalonia.Media.Brushes.Transparent;
		Box.Resources["TextControlBorderBrushFocused"] = Avalonia.Media.Brushes.Transparent;
		View.Native = Box;
		SyncChildren();
		Box.PropertyChanged += (s, e) =>
		{
			if (e.Property == AC.TextBox.TextProperty && !_syncing)
			{
				_syncing = true;
				try
				{
					base.Text = Box.Text ?? "";
				}
				finally
				{
					_syncing = false;
				}
			}
		};
		Cursor = Cursors.IBeam;
		ApplyFont();
	}

	internal override bool CanFocusInternal => true;

	public override string Text
	{
		get => base.Text;
		set
		{
			if (_syncing)
			{
				base.Text = value;
				return;
			}
			_syncing = true;
			try
			{
				Box.Text = value ?? "";
				base.Text = value ?? "";
			}
			finally
			{
				_syncing = false;
			}
		}
	}

	public override Font Font
	{
		get => base.Font;
		set
		{
			base.Font = value;
			ApplyFont();
		}
	}

	private void ApplyFont()
	{
		var f = Font;
		if (f == null)
		{
			return;
		}
		Box.FontFamily = f.Typeface.FontFamily;
		Box.FontWeight = f.Typeface.Weight;
		Box.FontSize = f.PixelSize;
	}

	public override Color ForeColor
	{
		get => base.ForeColor;
		set
		{
			base.ForeColor = value;
			Box.Foreground = Conv.Solid(value);
			Box.Resources["TextControlForeground"] = Conv.Solid(value);
			Box.Resources["TextControlForegroundPointerOver"] = Conv.Solid(value);
			Box.Resources["TextControlForegroundFocused"] = Conv.Solid(value);
		}
	}

	public override Color BackColor
	{
		get => base.BackColor;
		set
		{
			base.BackColor = value;
			var b = Conv.Solid(value.A == 0 ? Color.Transparent : value);
			Box.Background = b;
			Box.Resources["TextControlBackground"] = b;
			Box.Resources["TextControlBackgroundPointerOver"] = b;
			Box.Resources["TextControlBackgroundFocused"] = b;
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	public BorderStyle BorderStyle { get; set; }

	public bool Multiline
	{
		get => Box.AcceptsReturn;
		set
		{
			Box.AcceptsReturn = value;
			Box.TextWrapping = value ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap;
			Box.VerticalContentAlignment = value ? Avalonia.Layout.VerticalAlignment.Top : Avalonia.Layout.VerticalAlignment.Center;
		}
	}

	public bool WordWrap
	{
		get => Box.TextWrapping != Avalonia.Media.TextWrapping.NoWrap;
		set => Box.TextWrapping = value ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap;
	}

	public bool ReadOnly
	{
		get => Box.IsReadOnly;
		set => Box.IsReadOnly = value;
	}

	public ScrollBars ScrollBars
	{
		get => ScrollBars.None;
		set
		{
			AC.ScrollViewer.SetVerticalScrollBarVisibility(Box, (value & ScrollBars.Vertical) != 0 ? AC.Primitives.ScrollBarVisibility.Auto : AC.Primitives.ScrollBarVisibility.Disabled);
		}
	}

	public int MaxLength
	{
		get => Box.MaxLength;
		set => Box.MaxLength = value;
	}

	public HorizontalAlignment TextAlign
	{
		get => HorizontalAlignment.Left;
		set => Box.TextAlignment = value switch
		{
			HorizontalAlignment.Center => Avalonia.Media.TextAlignment.Center,
			HorizontalAlignment.Right => Avalonia.Media.TextAlignment.Right,
			_ => Avalonia.Media.TextAlignment.Left,
		};
	}

	public string PlaceholderText
	{
		get => Box.Watermark;
		set => Box.Watermark = L.T(value ?? "");
	}

	public bool UseSystemPasswordChar
	{
		get => Box.PasswordChar != default(char);
		set => Box.PasswordChar = value ? '●' : default(char);
	}

	public bool HideSelection { get; set; }

	public bool ShortcutsEnabled { get; set; } = true;

	public int SelectionStart
	{
		get => Box.SelectionStart;
		set
		{
			Box.SelectionStart = value;
			Box.SelectionEnd = value;
			Box.CaretIndex = value;
		}
	}

	public int SelectionLength
	{
		get => Math.Abs(Box.SelectionEnd - Box.SelectionStart);
		set => Box.SelectionEnd = Box.SelectionStart + value;
	}

	public int TextLength => (Box.Text ?? "").Length;

	public void SelectAll() => Box.SelectAll();

	public void Select(int start, int length)
	{
		Box.SelectionStart = start;
		Box.SelectionEnd = start + length;
	}

	public void Clear() => Text = "";

	public void AppendText(string s) => Text = Text + s;

	public void ScrollToCaret()
	{
	}

	/// <summary>Высота однострочного поля как в WinForms (без рамки): высота строки шрифта + 1.</summary>
	public int PreferredHeight => (Font?.Height ?? 16) + (BorderStyle == BorderStyle.None ? 1 : 7);
}

internal static class SystemInformation
{
	public static int MouseWheelScrollLines => 3;
}
