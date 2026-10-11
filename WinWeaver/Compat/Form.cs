using System;
using System.Collections.Generic;
using System.ComponentModel;
using AC = Avalonia.Controls;
using AI = Avalonia.Input;
using AM = Avalonia.Media;
using AT = Avalonia.Threading;
using AV = Avalonia;

namespace WinWeaver;

/// <summary>Окно в стиле WinForms поверх Avalonia Window.</summary>
internal class Form : Control
{
	internal readonly AC.Window Window;

	private static readonly List<Form> _open = new List<Form>();

	private DialogResult _dr;

	private FormBorderStyle _border = FormBorderStyle.Sizable;

	private Icon _icon;

	private double _opacity = 1.0;

	private bool _closing;

	private bool _shownOnce;

	private bool _previewRaised;

	private AT.DispatcherFrame _frame;

	internal bool IsShownInternal;

	public static Form ActiveForm { get; private set; }

	internal static IReadOnlyList<Form> OpenFormsList => _open;

	public FormStartPosition StartPosition { get; set; } = FormStartPosition.WindowsDefaultLocation;

	public AutoScaleMode AutoScaleMode { get; set; }

	public bool KeyPreview { get; set; }

	public bool Modal { get; private set; }

	public Form Owner { get; set; }

	public bool MaximizeBox { get; set; } = true;

	public bool MinimizeBox { get; set; } = true;

	public bool ControlBox { get; set; } = true;

	public Color TransparencyKey { get; set; }

	public bool HelpButton { get; set; }

	protected virtual bool ShowWithoutActivation => false;

	public Form()
	{
		Window = new AC.Window
		{
			Content = View,
			Background = AM.Brushes.Black,
			SizeToContent = AC.SizeToContent.Manual,
			Title = "",
		};
		Visible = false;
		View.IsVisible = true;
		Window.PropertyChanged += (s, e) =>
		{
			if (e.Property == AC.TopLevel.ClientSizeProperty)
			{
				var cs = Window.ClientSize;
				SetSizeFromHost((int)Math.Round(cs.Width), (int)Math.Round(cs.Height));
			}
			else if (e.Property == AC.Window.WindowStateProperty)
			{
				OnResize(EventArgs.Empty);
			}
		};
		Window.PositionChanged += (s, e) =>
		{
			double sc = Screen.Scale;
			SetLocationFromHost((int)Math.Round(e.Point.X / sc), (int)Math.Round(e.Point.Y / sc));
		};
		Window.Activated += (s, e) =>
		{
			ActiveForm = this;
			OnActivated(EventArgs.Empty);
		};
		Window.Deactivated += (s, e) =>
		{
			if (ActiveForm == this)
			{
				ActiveForm = null;
			}
			OnDeactivate(EventArgs.Empty);
		};
		Window.Opened += (s, e) =>
		{
			Bitmap.RenderScale = Math.Max(Bitmap.RenderScale, Window.RenderScaling);
			if (!_shownOnce)
			{
				_shownOnce = true;
				AT.Dispatcher.UIThread.Post(() =>
				{
					if (!IsDisposed)
					{
						OnShown(EventArgs.Empty);
					}
				}, AT.DispatcherPriority.Background);
			}
		};
		Window.Closing += (s, e) =>
		{
			var args = new FormClosingEventArgs(CloseReason.UserClosing, false);
			OnFormClosing(args);
			e.Cancel = args.Cancel;
			if (args.Cancel)
			{
				_closing = false;
				if (Modal)
				{
					_dr = DialogResult.None;
				}
			}
		};
		Window.Closed += (s, e) =>
		{
			IsShownInternal = false;
			_open.Remove(this);
			if (ActiveForm == this)
			{
				ActiveForm = null;
			}
			OnFormClosed(new FormClosedEventArgs(CloseReason.UserClosing));
			OnClosed(EventArgs.Empty);
			if (_frame != null)
			{
				_frame.Continue = false;
			}
			if (!Modal)
			{
				Dispose();
			}
		};
		Window.AddHandler(AI.InputElement.KeyDownEvent, OnWindowKeyDownTunnel, AV.Interactivity.RoutingStrategies.Tunnel);
		Window.AddHandler(AI.InputElement.KeyDownEvent, OnWindowKeyDownBubble, AV.Interactivity.RoutingStrategies.Bubble);
		Window.AddHandler(AI.InputElement.PointerMovedEvent, (s, e) => TrackPointer(e), AV.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
		Window.AddHandler(AI.InputElement.PointerPressedEvent, (s, e) => ControlView.LastPress = e, AV.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
		if (Application.AppIcon != null)
		{
			Window.Icon = Application.AppIcon.ToWindowIcon();
		}
	}

	private void TrackPointer(AI.PointerEventArgs e)
	{
		try
		{
			var sp = AV.VisualExtensions.PointToScreen(Window, e.GetPosition(Window));
			double s = Screen.Scale;
			WinWeaver.Cursor.TrackPosition(new Point((int)Math.Round(sp.X / s), (int)Math.Round(sp.Y / s)));
			ModifierKeysValue = Keymap.Mods(e.KeyModifiers);
		}
		catch
		{
		}
	}

	private Control FocusedControl()
	{
		var fe = Window.FocusManager?.GetFocusedElement() as AV.Visual;
		while (fe != null)
		{
			if (fe is ControlView cv)
			{
				return cv.Owner;
			}
			fe = AV.VisualTree.VisualExtensions.GetVisualParent(fe);
		}
		return null;
	}

	private void OnWindowKeyDownTunnel(object sender, AI.KeyEventArgs e)
	{
		_previewRaised = false;
		if (e.Key == AI.Key.F12 && e.KeyModifiers == (AI.KeyModifiers.Control | AI.KeyModifiers.Shift))
		{
			// Ctrl+Shift+F12 — счётчик FPS и графики времени рендера/раскладки (диагностика).
			var d = Window.RendererDiagnostics;
			d.DebugOverlays = d.DebugOverlays == AV.Rendering.RendererDebugOverlays.None
				? AV.Rendering.RendererDebugOverlays.Fps | AV.Rendering.RendererDebugOverlays.RenderTimeGraph | AV.Rendering.RendererDebugOverlays.LayoutTimeGraph
				: AV.Rendering.RendererDebugOverlays.None;
			e.Handled = true;
			return;
		}
		ModifierKeysValue = Keymap.Mods(e.KeyModifiers);
		Keys k = Keymap.ToKeys(e.Key, e.KeyModifiers);
		Control focused = FocusedControl() ?? this;
		bool isText = focused is TextBox;
		if (!(isText && (k & Keys.KeyCode) != Keys.Escape && (k & Keys.KeyCode) != Keys.Enter && (k & Keys.KeyCode) != Keys.Tab))
		{
			if (focused.ProcessDialogKeyInternal(k))
			{
				e.Handled = true;
				return;
			}
		}
		if (KeyPreview)
		{
			_previewRaised = true;
			var a = new KeyEventArgs(k);
			RaiseKeyDown(a);
			if (a.Handled)
			{
				e.Handled = true;
			}
		}
	}

	private void OnWindowKeyDownBubble(object sender, AI.KeyEventArgs e)
	{
		if (e.Handled || _previewRaised)
		{
			return;
		}
		if (FocusedControl() == null)
		{
			var a = new KeyEventArgs(Keymap.ToKeys(e.Key, e.KeyModifiers));
			RaiseKeyDown(a);
			if (a.Handled)
			{
				e.Handled = true;
			}
		}
	}

	protected override bool ProcessDialogKey(Keys keyData) => false;

	// ---------- Свойства окна ----------

	public override string Text
	{
		get => base.Text;
		set
		{
			base.Text = value;
			Window.Title = L.T(value ?? "");
		}
	}

	public Icon Icon
	{
		get => _icon;
		set
		{
			_icon = value;
			try
			{
				Window.Icon = value?.ToWindowIcon();
			}
			catch
			{
			}
		}
	}

	public FormBorderStyle FormBorderStyle
	{
		get => _border;
		set
		{
			_border = value;
			if (value == FormBorderStyle.None)
			{
				Window.SystemDecorations = AC.SystemDecorations.None;
				Window.CanResize = false;
				Window.ExtendClientAreaToDecorationsHint = false;
			}
			else
			{
				Window.SystemDecorations = AC.SystemDecorations.Full;
				Window.CanResize = value == FormBorderStyle.Sizable || value == FormBorderStyle.SizableToolWindow;
			}
		}
	}

	public bool ShowInTaskbar
	{
		get => Window.ShowInTaskbar;
		set => Window.ShowInTaskbar = value;
	}

	public bool TopMost
	{
		get => Window.Topmost;
		set => Window.Topmost = value;
	}

	public double Opacity
	{
		get => _opacity;
		set
		{
			_opacity = Math.Max(0, Math.Min(1, value));
			if (_opacity < 1)
			{
				EnsureTransparent();
			}
			Window.Opacity = _opacity;
		}
	}

	private bool _transparent;

	internal void EnsureTransparent()
	{
		if (_transparent)
		{
			return;
		}
		_transparent = true;
		Window.TransparencyLevelHint = new[] { AC.WindowTransparencyLevel.Transparent };
		Window.Background = AM.Brushes.Transparent;
	}

	public override Color BackColor
	{
		get => base.BackColor;
		set
		{
			base.BackColor = value;
			if (!_transparent)
			{
				Window.Background = Conv.Solid(value.A == 0 ? Color.Black : value);
			}
		}
	}

	internal override void OnRegionChanged()
	{
		if (Region != null)
		{
			EnsureTransparent();
		}
	}

	public new Size MinimumSize
	{
		get => base.MinimumSize;
		set
		{
			base.MinimumSize = value;
			Window.MinWidth = value.Width;
			Window.MinHeight = value.Height;
		}
	}

	public new Size MaximumSize
	{
		get => base.MaximumSize;
		set
		{
			base.MaximumSize = value;
			if (value.Width > 0) Window.MaxWidth = value.Width;
			if (value.Height > 0) Window.MaxHeight = value.Height;
		}
	}

	public FormWindowState WindowState
	{
		get => Window.WindowState switch
		{
			AC.WindowState.Minimized => FormWindowState.Minimized,
			AC.WindowState.Maximized => FormWindowState.Maximized,
			AC.WindowState.FullScreen => FormWindowState.Maximized,
			_ => FormWindowState.Normal,
		};
		set => Window.WindowState = value switch
		{
			FormWindowState.Minimized => AC.WindowState.Minimized,
			FormWindowState.Maximized => AC.WindowState.Maximized,
			_ => AC.WindowState.Normal,
		};
	}

	public Rectangle RestoreBounds => Bounds;

	public Rectangle DesktopBounds => Bounds;

	public override IntPtr Handle
	{
		get
		{
			EnsureHandle();
			try
			{
				return Window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
			}
			catch
			{
				return IntPtr.Zero;
			}
		}
	}

	protected override void SetBoundsCore(int x, int y, int w, int h, BoundsSpecified spec)
	{
		bool resized = w != Width || h != Height;
		bool moved = x != Left || y != Top;
		base.SetBoundsCore(x, y, w, h, spec);
		if (resized)
		{
			Window.Width = Math.Max(1, w);
			Window.Height = Math.Max(1, h);
		}
		if (moved)
		{
			double sc = Screen.Scale;
			Window.Position = new AV.PixelPoint((int)Math.Round(x * sc), (int)Math.Round(y * sc));
		}
	}

	public DialogResult DialogResult
	{
		get => _dr;
		set
		{
			_dr = value;
			if (Modal && value != DialogResult.None && IsShownInternal)
			{
				Close();
			}
		}
	}

	protected override void SetVisibleCore(bool value)
	{
		if (value && !IsShownInternal && !_inShow)
		{
			ShowCore(Owner, modal: false);
		}
		else if (!value && IsShownInternal)
		{
			Window.Hide();
			IsShownInternal = false;
		}
	}

	private bool _inShow;

	// ---------- Показ ----------

	public new void Show()
	{
		ShowCore(Owner, modal: false);
	}

	public void Show(IWin32Window owner)
	{
		ShowCore((owner as Control)?.FindForm(), modal: false);
	}

	private void ShowCore(Form owner, bool modal)
	{
		if (IsDisposed)
		{
			return;
		}
		if (IsShownInternal)
		{
			Window.Activate();
			return;
		}
		_inShow = true;
		try
		{
			Owner = owner ?? Owner;
			EnsureHandle();
			if (!_loaded)
			{
				_loaded = true;
				OnLoad(EventArgs.Empty);
			}
			Window.Width = Math.Max(1, Width);
			Window.Height = Math.Max(1, Height);
			ApplyStartPosition();
			Window.ShowActivated = !ShowWithoutActivation;
			base.Visible = true;
			IsShownInternal = true;
			if (!_open.Contains(this))
			{
				_open.Add(this);
			}
			var ow = Owner?.Window;
			if (modal && ow != null && Owner.IsShownInternal)
			{
				_ = Window.ShowDialog(ow);
			}
			else if (ow != null && Owner.IsShownInternal)
			{
				Window.Show(ow);
			}
			else
			{
				Window.Show();
			}
		}
		finally
		{
			_inShow = false;
		}
	}

	private bool _loaded;

	private void ApplyStartPosition()
	{
		Rectangle wa = Screen.PrimaryScreen.WorkingArea;
		switch (StartPosition)
		{
		case FormStartPosition.CenterParent:
			if (Owner != null && Owner.IsShownInternal)
			{
				Location = new Point(Owner.Left + (Owner.Width - Width) / 2, Owner.Top + (Owner.Height - Height) / 2);
				break;
			}
			goto case FormStartPosition.CenterScreen;
		case FormStartPosition.CenterScreen:
		case FormStartPosition.WindowsDefaultLocation:
		case FormStartPosition.WindowsDefaultBounds:
			Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
			break;
		}
		double sc = Screen.Scale;
		Window.WindowStartupLocation = AC.WindowStartupLocation.Manual;
		Window.Position = new AV.PixelPoint((int)Math.Round(Left * sc), (int)Math.Round(Top * sc));
	}

	public DialogResult ShowDialog() => ShowDialog(null);

	public DialogResult ShowDialog(IWin32Window owner)
	{
		Form of = (owner as Control)?.FindForm() ?? Owner ?? ActiveForm;
		if (of == this)
		{
			of = null;
		}
		Modal = true;
		_dr = DialogResult.None;
		_frame = new AT.DispatcherFrame();
		ShowCore(of, modal: true);
		if (IsShownInternal)
		{
			AT.Dispatcher.UIThread.PushFrame(_frame);
		}
		_frame = null;
		Modal = false;
		if (_dr == DialogResult.None)
		{
			_dr = DialogResult.Cancel;
		}
		of?.Window.Activate();
		return _dr;
	}

	public void Close()
	{
		if (!IsShownInternal || _closing)
		{
			if (!IsShownInternal && !Modal && !IsDisposed && _loaded)
			{
				Dispose();
			}
			return;
		}
		_closing = true;
		Window.Close();
		_closing = false;
	}

	public void Activate()
	{
		if (IsShownInternal)
		{
			Window.Activate();
		}
	}

	public new void Hide()
	{
		Visible = false;
	}

	// ---------- События формы ----------

	public event EventHandler Load;
	public event EventHandler Shown;
	public event FormClosingEventHandler FormClosing;
	public event FormClosedEventHandler FormClosed;
	public event EventHandler Closed;
	public event EventHandler Activated;
	public event EventHandler Deactivate;

	protected virtual void OnLoad(EventArgs e) => Load?.Invoke(this, e);
	protected virtual void OnShown(EventArgs e) => Shown?.Invoke(this, e);
	protected virtual void OnFormClosing(FormClosingEventArgs e) => FormClosing?.Invoke(this, e);
	protected virtual void OnFormClosed(FormClosedEventArgs e) => FormClosed?.Invoke(this, e);
	protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);
	protected virtual void OnActivated(EventArgs e) => Activated?.Invoke(this, e);
	protected virtual void OnDeactivate(EventArgs e) => Deactivate?.Invoke(this, e);

	/// <summary>Начать перетаскивание окна мышью (замена WM_NCLBUTTONDOWN).</summary>
	public void BeginDrag()
	{
		var p = ControlView.LastPress;
		if (p != null)
		{
			try
			{
				Window.BeginMoveDrag(p);
			}
			catch
			{
			}
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && IsShownInternal)
		{
			try
			{
				_closing = true;
				Window.Close();
			}
			catch
			{
			}
		}
		base.Dispose(disposing);
	}
}
