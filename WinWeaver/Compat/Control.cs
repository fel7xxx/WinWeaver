using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AC = Avalonia.Controls;
using AI = Avalonia.Input;
using AM = Avalonia.Media;
using AR = Avalonia.Rendering;
using AT = Avalonia.Threading;
using AV = Avalonia;
using AVis = Avalonia.VisualTree;

namespace WinWeaver;

internal delegate void MethodInvoker();

/// <summary>
/// Визуальный узел Avalonia, который рисует связанный Control через Graphics-обёртку,
/// размещает дочерние контролы по их Bounds и переводит события указателя/клавиатуры.
/// </summary>
internal sealed class ControlView : AC.Control, AR.ICustomHitTest
{
	internal sealed class Kids : IEnumerable<AC.Control>
	{
		private readonly ControlView _v;

		private readonly List<AC.Control> _l = new List<AC.Control>();

		public Kids(ControlView v)
		{
			_v = v;
		}

		public int Count => _l.Count;

		public AC.Control this[int i] => _l[i];

		public int IndexOf(AC.Control c) => _l.IndexOf(c);

		public bool Contains(AC.Control c) => _l.Contains(c);

		public void Insert(int i, AC.Control c)
		{
			_l.Insert(i, c);
			_v.VisualChildren.Insert(i, c);
			_v.LogicalChildren.Insert(i, c);
		}

		public void Remove(AC.Control c)
		{
			int i = _l.IndexOf(c);
			if (i < 0)
			{
				return;
			}
			_l.RemoveAt(i);
			_v.VisualChildren.Remove(c);
			_v.LogicalChildren.Remove(c);
		}

		public void Move(int from, int to)
		{
			var c = _l[from];
			Remove(c);
			Insert(to, c);
		}

		public IEnumerator<AC.Control> GetEnumerator() => _l.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => _l.GetEnumerator();
	}

	internal readonly Kids Children;

	internal readonly Control Owner;

	internal AC.Control Native;

	internal static AI.PointerPressedEventArgs LastPress;

	private MouseButtons _pressed;

	public ControlView(Control owner)
	{
		Owner = owner;
		Children = new Kids(this);
		ClipToBounds = true;
		Focusable = false;
	}

	public bool HitTest(AV.Point p) => Owner.Enabled && new AV.Rect(Bounds.Size).Contains(p);

	protected override AV.Size MeasureOverride(AV.Size availableSize)
	{
		foreach (var c in Children)
		{
			if (c is ControlView cv)
			{
				cv.Measure(new AV.Size(Math.Max(0, cv.Owner.Width), Math.Max(0, cv.Owner.Height)));
			}
			else
			{
				c.Measure(new AV.Size(Math.Max(0, Owner.Width), Math.Max(0, Owner.Height)));
			}
		}
		return new AV.Size(Math.Max(0, Owner.Width), Math.Max(0, Owner.Height));
	}

	protected override AV.Size ArrangeOverride(AV.Size finalSize)
	{
		foreach (var c in Children)
		{
			if (c is ControlView cv)
			{
				cv.Arrange(new AV.Rect(cv.Owner.Left, cv.Owner.Top, Math.Max(0, cv.Owner.Width), Math.Max(0, cv.Owner.Height)));
			}
			else
			{
				c.Arrange(new AV.Rect(0, 0, Math.Max(0, Owner.Width), Math.Max(0, Owner.Height)));
			}
		}
		return finalSize;
	}

	public override void Render(AM.DrawingContext context)
	{
		Owner.PaintInternal(context, Bounds.Size);
	}

	private bool IsMine(AI.PointerEventArgs e)
	{
		var v = e.Source as AV.Visual;
		while (v != null && !(v is ControlView))
		{
			if (Native != null && v == Native)
			{
				return false;
			}
			v = AVis.VisualExtensions.GetVisualParent(v);
		}
		return v == this;
	}

	private static MouseButtons Buttons(AI.PointerPointProperties p)
	{
		MouseButtons b = MouseButtons.None;
		if (p.IsLeftButtonPressed) b |= MouseButtons.Left;
		if (p.IsRightButtonPressed) b |= MouseButtons.Right;
		if (p.IsMiddleButtonPressed) b |= MouseButtons.Middle;
		return b;
	}

	private void Track(AI.PointerEventArgs e)
	{
		try
		{
			var top = AC.TopLevel.GetTopLevel(this);
			if (top != null)
			{
				var sp = AV.VisualExtensions.PointToScreen(top, e.GetPosition(top));
				double s = Screen.Scale;
				WinWeaver.Cursor.TrackPosition(new Point((int)Math.Round(sp.X / s), (int)Math.Round(sp.Y / s)));
			}
			Control.ModifierKeysValue = Keymap.Mods(e.KeyModifiers);
		}
		catch
		{
		}
	}

	protected override void OnPointerPressed(AI.PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);
		Track(e);
		if (!IsMine(e))
		{
			return;
		}
		LastPress = e;
		var pt = e.GetPosition(this);
		var props = e.GetCurrentPoint(this).Properties;
		MouseButtons b = props.PointerUpdateKind switch
		{
			AI.PointerUpdateKind.RightButtonPressed => MouseButtons.Right,
			AI.PointerUpdateKind.MiddleButtonPressed => MouseButtons.Middle,
			_ => MouseButtons.Left,
		};
		_pressed = b;
		e.Pointer.Capture(this);
		if (Owner.CanFocusInternal)
		{
			Focus();
		}
		Owner.RaiseMouseDown(new MouseEventArgs(b, e.ClickCount, (int)pt.X, (int)pt.Y, 0));
		e.Handled = true;
	}

	protected override void OnPointerReleased(AI.PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		Track(e);
		if (!IsMine(e) && _pressed == MouseButtons.None)
		{
			return;
		}
		var pt = e.GetPosition(this);
		MouseButtons b = e.InitialPressMouseButton switch
		{
			AI.MouseButton.Right => MouseButtons.Right,
			AI.MouseButton.Middle => MouseButtons.Middle,
			_ => MouseButtons.Left,
		};
		bool wasPressed = _pressed != MouseButtons.None;
		_pressed = MouseButtons.None;
		if (e.Pointer.Captured == this)
		{
			e.Pointer.Capture(null);
		}
		var args = new MouseEventArgs(b, 1, (int)pt.X, (int)pt.Y, 0);
		if (wasPressed && !Owner.IsDisposed && new AV.Rect(Bounds.Size).Contains(pt))
		{
			Owner.RaiseClick(args);
		}
		if (!Owner.IsDisposed)
		{
			Owner.RaiseMouseUp(args);
		}
		e.Handled = true;
	}

	protected override void OnPointerMoved(AI.PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		Track(e);
		if (!IsMine(e) && _pressed == MouseButtons.None)
		{
			return;
		}
		var pt = e.GetPosition(this);
		Owner.RaiseMouseMove(new MouseEventArgs(Buttons(e.GetCurrentPoint(this).Properties), 0, (int)pt.X, (int)pt.Y, 0));
	}

	protected override void OnPointerEntered(AI.PointerEventArgs e)
	{
		base.OnPointerEntered(e);
		Track(e);
		Owner.RaiseMouseEnter();
	}

	protected override void OnPointerExited(AI.PointerEventArgs e)
	{
		base.OnPointerExited(e);
		Track(e);
		Owner.RaiseMouseLeave();
	}

	protected override void OnPointerCaptureLost(AI.PointerCaptureLostEventArgs e)
	{
		base.OnPointerCaptureLost(e);
		_pressed = MouseButtons.None;
	}

	protected override void OnPointerWheelChanged(AI.PointerWheelEventArgs e)
	{
		base.OnPointerWheelChanged(e);
		if (e.Handled)
		{
			return;
		}
		Track(e);
		var pt = e.GetPosition(this);
		var args = new HandledMouseEventArgs(MouseButtons.None, 0, (int)pt.X, (int)pt.Y, (int)Math.Round(e.Delta.Y * 120.0));
		Owner.RaiseMouseWheel(args);
		if (args.Handled)
		{
			e.Handled = true;
		}
	}

	protected override void OnKeyDown(AI.KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Handled)
		{
			return;
		}
		var args = new KeyEventArgs(Keymap.ToKeys(e.Key, e.KeyModifiers));
		Owner.RaiseKeyDown(args);
		if (args.Handled)
		{
			e.Handled = true;
		}
	}

	protected override void OnKeyUp(AI.KeyEventArgs e)
	{
		base.OnKeyUp(e);
		var args = new KeyEventArgs(Keymap.ToKeys(e.Key, e.KeyModifiers));
		Owner.RaiseKeyUp(args);
	}

	protected override void OnTextInput(AI.TextInputEventArgs e)
	{
		base.OnTextInput(e);
		if (e.Handled || string.IsNullOrEmpty(e.Text))
		{
			return;
		}
		foreach (char ch in e.Text)
		{
			var a = new KeyPressEventArgs(ch);
			Owner.RaiseKeyPress(a);
			if (a.Handled)
			{
				e.Handled = true;
			}
		}
	}

	protected override void OnGotFocus(AI.GotFocusEventArgs e)
	{
		base.OnGotFocus(e);
		Owner.RaiseGotFocus();
	}

	protected override void OnLostFocus(AV.Interactivity.RoutedEventArgs e)
	{
		base.OnLostFocus(e);
		Owner.RaiseLostFocus();
	}
}

internal static class Keymap
{
	public static Keys Mods(AI.KeyModifiers m)
	{
		Keys k = Keys.None;
		if ((m & AI.KeyModifiers.Shift) != 0) k |= Keys.Shift;
		if ((m & AI.KeyModifiers.Control) != 0) k |= Keys.Control;
		if ((m & AI.KeyModifiers.Alt) != 0) k |= Keys.Alt;
		return k;
	}

	public static Keys ToKeys(AI.Key key, AI.KeyModifiers m)
	{
		Keys code = Keys.None;
		if (key >= AI.Key.A && key <= AI.Key.Z)
		{
			code = Keys.A + (key - AI.Key.A);
		}
		else if (key >= AI.Key.D0 && key <= AI.Key.D9)
		{
			code = Keys.D0 + (key - AI.Key.D0);
		}
		else if (key >= AI.Key.NumPad0 && key <= AI.Key.NumPad9)
		{
			code = Keys.NumPad0 + (key - AI.Key.NumPad0);
		}
		else if (key >= AI.Key.F1 && key <= AI.Key.F12)
		{
			code = Keys.F1 + (key - AI.Key.F1);
		}
		else
		{
			code = key switch
			{
				AI.Key.Escape => Keys.Escape,
				AI.Key.Enter => Keys.Enter,
				AI.Key.Space => Keys.Space,
				AI.Key.Tab => Keys.Tab,
				AI.Key.Back => Keys.Back,
				AI.Key.Delete => Keys.Delete,
				AI.Key.Insert => Keys.Insert,
				AI.Key.Left => Keys.Left,
				AI.Key.Right => Keys.Right,
				AI.Key.Up => Keys.Up,
				AI.Key.Down => Keys.Down,
				AI.Key.Home => Keys.Home,
				AI.Key.End => Keys.End,
				AI.Key.PageUp => Keys.PageUp,
				AI.Key.PageDown => Keys.PageDown,
				AI.Key.LeftShift or AI.Key.RightShift => Keys.ShiftKey,
				AI.Key.LeftCtrl or AI.Key.RightCtrl => Keys.ControlKey,
				AI.Key.LeftAlt or AI.Key.RightAlt => Keys.Menu,
				_ => Keys.None,
			};
		}
		return code | Mods(m);
	}
}

/// <summary>Коллекция дочерних контролов (порядок как в WinForms: индекс 0 — поверх остальных).</summary>
internal sealed class ControlCollection : IEnumerable<Control>
{
	private readonly Control _owner;

	private readonly List<Control> _list = new List<Control>();

	internal ControlCollection(Control owner)
	{
		_owner = owner;
	}

	public int Count => _list.Count;

	public Control this[int i] => _list[i];

	public void Add(Control c)
	{
		if (c == null)
		{
			return;
		}
		if (c.Parent == _owner && _list.Contains(c))
		{
			return;
		}
		c.Parent?.Controls.Remove(c);
		_list.Add(c);
		c.SetParentInternal(_owner);
		_owner.SyncChildren();
		_owner.OnControlAddedInternal(c);
	}

	public void AddRange(Control[] cs)
	{
		foreach (var c in cs)
		{
			Add(c);
		}
	}

	public void Remove(Control c)
	{
		if (c == null || !_list.Remove(c))
		{
			return;
		}
		c.SetParentInternal(null);
		_owner.SyncChildren();
		_owner.OnControlRemovedInternal(c);
	}

	public void RemoveAt(int i) => Remove(_list[i]);

	public void Clear()
	{
		foreach (var c in _list.ToArray())
		{
			Remove(c);
		}
	}

	public bool Contains(Control c) => _list.Contains(c);

	public int IndexOf(Control c) => _list.IndexOf(c);

	public int GetChildIndex(Control c) => _list.IndexOf(c);

	public void SetChildIndex(Control c, int index)
	{
		if (!_list.Remove(c))
		{
			return;
		}
		index = Math.Max(0, Math.Min(index, _list.Count));
		_list.Insert(index, c);
		_owner.SyncChildren();
	}

	internal List<Control> List => _list;

	public IEnumerator<Control> GetEnumerator() => ((IEnumerable<Control>)_list.ToArray()).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}


/// <summary>
/// Базовый контрол в стиле WinForms, реализованный поверх Avalonia.
/// Координаты целочисленные, в DIP Avalonia (масштабирование экрана делает Avalonia).
/// </summary>
internal class Control : IWin32Window, IDisposable
{
	internal readonly ControlView View;

	private int _x, _y, _w, _h;

	private bool _visible = true;

	private bool _enabled = true;

	private string _text = "";

	private Font _font;

	private Color _back = Color.Empty;

	private Color _fore = Color.Empty;

	private Cursor _cursor;

	private bool _handle;

	private int _layoutSuspend;

	private Region _region;

	internal static Keys ModifierKeysValue;

	public ControlCollection Controls { get; }

	public Control Parent { get; private set; }

	public string Name { get; set; }

	public object Tag { get; set; }

	public bool IsDisposed { get; private set; }

	public bool Disposing { get; private set; }

	public bool TabStop { get; set; } = true;

	public int TabIndex { get; set; }

	public Padding Padding { get; set; }

	public Padding Margin { get; set; }

	public DockStyle Dock { get; set; }

	public AnchorStyles Anchor { get; set; } = AnchorStyles.Top | AnchorStyles.Left;

	public bool AutoSize { get; set; }

	public Size MinimumSize { get; set; }

	public Size MaximumSize { get; set; }

	public bool AllowDrop { get; set; }

	public string AccessibleName { get; set; }

	protected bool DoubleBuffered { get; set; }

	protected bool ResizeRedraw { get; set; }

	internal virtual bool CanFocusInternal => false;

	public Control()
	{
		View = new ControlView(this);
		Controls = new ControlCollection(this);
	}

	public Control(string text)
		: this()
	{
		_text = text ?? "";
	}

	// ---------- Геометрия ----------

	public int Left
	{
		get => _x;
		set => SetBounds(value, _y, _w, _h);
	}

	public int Top
	{
		get => _y;
		set => SetBounds(_x, value, _w, _h);
	}

	public virtual int Width
	{
		get => _w;
		set => SetBounds(_x, _y, value, _h);
	}

	public virtual int Height
	{
		get => _h;
		set => SetBounds(_x, _y, _w, value);
	}

	public int Right => _x + _w;

	public int Bottom => _y + _h;

	public Point Location
	{
		get => new Point(_x, _y);
		set => SetBounds(value.X, value.Y, _w, _h);
	}

	public virtual Size Size
	{
		get => new Size(_w, _h);
		set => SetBounds(_x, _y, value.Width, value.Height);
	}

	public virtual Size ClientSize
	{
		get => new Size(_w, _h);
		set => SetBounds(_x, _y, value.Width, value.Height);
	}

	public Rectangle Bounds
	{
		get => new Rectangle(_x, _y, _w, _h);
		set => SetBounds(value.X, value.Y, value.Width, value.Height);
	}

	public Rectangle ClientRectangle => new Rectangle(0, 0, _w, _h);

	public Rectangle DisplayRectangle => ClientRectangle;

	public void SetBounds(int x, int y, int w, int h)
	{
		SetBoundsCore(x, y, Math.Max(0, w), Math.Max(0, h), BoundsSpecified.All);
	}

	public void SetBounds(int x, int y, int w, int h, BoundsSpecified spec)
	{
		if ((spec & BoundsSpecified.X) == 0) x = _x;
		if ((spec & BoundsSpecified.Y) == 0) y = _y;
		if ((spec & BoundsSpecified.Width) == 0) w = _w;
		if ((spec & BoundsSpecified.Height) == 0) h = _h;
		SetBoundsCore(x, y, Math.Max(0, w), Math.Max(0, h), spec);
	}

	protected virtual void SetBoundsCore(int x, int y, int w, int h, BoundsSpecified spec)
	{
		bool moved = x != _x || y != _y;
		bool resized = w != _w || h != _h;
		if (!moved && !resized)
		{
			return;
		}
		_x = x;
		_y = y;
		_w = w;
		_h = h;
		OnBoundsChangedInternal(resized);
		if (resized)
		{
			View.InvalidateMeasure();
			View.InvalidateArrange();
		}
		Parent?.View.InvalidateArrange();
		if (moved)
		{
			OnLocationChanged(EventArgs.Empty);
			OnMove(EventArgs.Empty);
		}
		if (resized)
		{
			// В WinForms изменение размера вызывает WM_PAINT; в Avalonia перерисовку надо запросить явно.
			View.InvalidateVisual();
			OnSizeChanged(EventArgs.Empty);
			PerformLayout();
		}
	}

	internal void SetLocationFromHost(int x, int y)
	{
		if (x == _x && y == _y)
		{
			return;
		}
		_x = x;
		_y = y;
		OnLocationChanged(EventArgs.Empty);
		OnMove(EventArgs.Empty);
	}

	internal virtual void OnBoundsChangedInternal(bool resized)
	{
	}

	/// <summary>Обновляет размер без вызова SetBoundsCore у наследников (для окна).</summary>
	internal void SetSizeFromHost(int w, int h)
	{
		if (w == _w && h == _h)
		{
			return;
		}
		_w = w;
		_h = h;
		View.InvalidateMeasure();
		View.InvalidateArrange();
		View.InvalidateVisual();
		OnSizeChanged(EventArgs.Empty);
		PerformLayout();
	}

	public virtual Size GetPreferredSize(Size proposed) => Size;

	public Size PreferredSize => GetPreferredSize(Size.Empty);

	// ---------- Иерархия ----------

	internal void SetParentInternal(Control p)
	{
		Parent = p;
		if (p != null && p._handle)
		{
			EnsureHandle();
		}
		OnParentChanged(EventArgs.Empty);
	}

	internal void SyncChildren()
	{
		var list = Controls.List;
		var kids = View.Children;
		var want = new List<AC.Control>(list.Count + 1);
		if (View.Native != null)
		{
			want.Add(View.Native);
		}
		for (int i = list.Count - 1; i >= 0; i--)
		{
			want.Add(list[i].View);
		}
		bool same = want.Count == kids.Count;
		for (int i = 0; same && i < want.Count; i++)
		{
			same = kids[i] == want[i];
		}
		if (same)
		{
			return;
		}
		foreach (var k in new List<AC.Control>(kids))
		{
			if (!want.Contains(k))
			{
				kids.Remove(k);
			}
		}
		for (int i = 0; i < want.Count; i++)
		{
			var w = want[i];
			int cur = kids.IndexOf(w);
			if (cur == i)
			{
				continue;
			}
			if (cur >= 0)
			{
				kids.Move(cur, i);
			}
			else
			{
				kids.Insert(i, w);
			}
		}
		View.InvalidateArrange();
	}

	internal void OnControlAddedInternal(Control c)
	{
		OnControlAdded(new ControlEventArgs(c));
		PerformLayout();
	}

	internal void OnControlRemovedInternal(Control c)
	{
		OnControlRemoved(new ControlEventArgs(c));
		PerformLayout();
	}

	public void BringToFront()
	{
		Parent?.Controls.SetChildIndex(this, 0);
		if (this is Form f)
		{
			f.Activate();
		}
	}

	public void SendToBack()
	{
		Parent?.Controls.SetChildIndex(this, int.MaxValue);
	}

	public Form FindForm()
	{
		Control c = this;
		while (c != null)
		{
			if (c is Form f)
			{
				return f;
			}
			c = c.Parent;
		}
		return null;
	}

	public Control TopLevelControl => FindForm();

	public bool Contains(Control c)
	{
		while (c != null)
		{
			c = c.Parent;
			if (c == this)
			{
				return true;
			}
		}
		return false;
	}

	public Control GetChildAtPoint(Point p)
	{
		foreach (var c in Controls.List)
		{
			if (c.Visible && c.Bounds.Contains(p))
			{
				return c;
			}
		}
		return null;
	}

	// ---------- Свойства ----------

	public bool Visible
	{
		get
		{
			if (!_visible)
			{
				return false;
			}
			if (this is Form f)
			{
				return f.IsShownInternal;
			}
			return Parent == null || Parent.Visible;
		}
		set
		{
			if (_visible == value)
			{
				return;
			}
			_visible = value;
			View.IsVisible = value;
			if (value)
			{
				// Avalonia не перерисовывает невидимые визуалы; при показе
				// перерисовываем поддерево, чтобы не остался устаревший кадр.
				Invalidate(true);
			}
			SetVisibleCore(value);
			OnVisibleChanged(EventArgs.Empty);
			Parent?.PerformLayout();
		}
	}

	internal bool VisibleSelf => _visible;

	// Видимость по цепочке родителей без учёта показа формы: скрытые
	// страницы и вкладки не должны перерисовываться (при показе Visible
	// инвалидирует всё поддерево).
	private bool ChainVisible()
	{
		for (Control c = this; c != null; c = c.Parent)
		{
			if (!c._visible) return false;
			if (c is Form) return true;
		}
		return true;
	}

	protected virtual void SetVisibleCore(bool value)
	{
	}

	public void Show() => Visible = true;

	public void Hide() => Visible = false;

	public bool Enabled
	{
		get => _enabled && (Parent == null || Parent.Enabled);
		set
		{
			if (_enabled == value)
			{
				return;
			}
			_enabled = value;
			View.IsEnabled = value;
			OnEnabledChanged(EventArgs.Empty);
			Invalidate();
		}
	}

	public virtual string Text
	{
		get => _text;
		set
		{
			value = value ?? "";
			if (_text == value)
			{
				return;
			}
			_text = value;
			OnTextChanged(EventArgs.Empty);
			Invalidate();
		}
	}

	public virtual Font Font
	{
		get => _font ?? Parent?.Font ?? Theme.Body ?? DefaultFont;
		set
		{
			_font = value;
			OnFontChanged(EventArgs.Empty);
			Invalidate();
		}
	}

	private static Font _defaultFont;

	public static Font DefaultFont => _defaultFont ??= new Font("Segoe UI", 9f);

	public virtual Color BackColor
	{
		get => _back.IsEmpty ? (Parent?.BackColor ?? Color.Black) : _back;
		set
		{
			_back = value;
			Invalidate();
		}
	}

	public virtual Color ForeColor
	{
		get => _fore.IsEmpty ? (Parent?.ForeColor ?? Color.White) : _fore;
		set
		{
			_fore = value;
			Invalidate();
		}
	}

	internal Color BackColorSelf => _back;

	public virtual Cursor Cursor
	{
		get => _cursor ?? Parent?.Cursor ?? Cursors.Default;
		set
		{
			_cursor = value;
			View.Cursor = value?.Av;
		}
	}

	public Region Region
	{
		get => _region;
		set
		{
			_region = value;
			View.Clip = value?.Single();
			OnRegionChanged();
		}
	}

	internal virtual void OnRegionChanged()
	{
	}

	public bool Capture
	{
		get => false;
		set
		{
		}
	}

	public bool Focused => View.IsFocused || (View.Native?.IsFocused ?? false);

	public bool ContainsFocus => View.IsKeyboardFocusWithin;

	public bool CanFocus => Visible && Enabled;

	public bool Focus()
	{
		if (View.Native != null)
		{
			return View.Native.Focus();
		}
		View.Focusable = true;
		return View.Focus();
	}

	public bool Select()
	{
		return Focus();
	}

	public static Keys ModifierKeys => ModifierKeysValue;

	public static Point MousePosition => Cursor.Position;

	public static MouseButtons MouseButtons => MouseButtons.None;

	// ---------- Handle / потоки ----------

	public bool IsHandleCreated => _handle;

	public virtual IntPtr Handle
	{
		get
		{
			EnsureHandle();
			var f = FindForm();
			if (f != null && f != this)
			{
				return f.Handle;
			}
			return new IntPtr(1);
		}
	}

	internal void EnsureHandle()
	{
		if (_handle || IsDisposed)
		{
			return;
		}
		_handle = true;
		try
		{
			OnHandleCreated(EventArgs.Empty);
		}
		catch (Exception ex)
		{
			Program.Log("HANDLE ERR " + ex);
		}
		foreach (var c in Controls.List.ToArray())
		{
			c.EnsureHandle();
		}
	}

	public void CreateControl() => EnsureHandle();

	public bool InvokeRequired => !AT.Dispatcher.UIThread.CheckAccess();

	public IAsyncResult BeginInvoke(Delegate d, params object[] args)
	{
		AT.Dispatcher.UIThread.Post(() =>
		{
			if (IsDisposed)
			{
				return;
			}
			try
			{
				Call(d, args);
			}
			catch (Exception ex)
			{
				Application.OnThreadException(ex);
			}
		});
		return null;
	}

	public IAsyncResult BeginInvoke(Action a)
	{
		return BeginInvoke((Delegate)a);
	}

	public object Invoke(Delegate d, params object[] args)
	{
		if (AT.Dispatcher.UIThread.CheckAccess())
		{
			return Call(d, args);
		}
		return AT.Dispatcher.UIThread.Invoke(() => Call(d, args));
	}

	public object Invoke(Action a) => Invoke((Delegate)a);

	private static object Call(Delegate d, object[] args)
	{
		if (d is Action a && (args == null || args.Length == 0))
		{
			a();
			return null;
		}
		if (d is MethodInvoker mi)
		{
			mi();
			return null;
		}
		try
		{
			return d.DynamicInvoke(args);
		}
		catch (TargetInvocationException tie) when (tie.InnerException != null)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
			throw;
		}
	}

	// ---------- Отрисовка ----------

	public void Invalidate()
	{
		if (!IsDisposed && _w > 0 && _h > 0 && ChainVisible())
		{
			View.InvalidateVisual();
		}
	}

	public void Invalidate(bool invalidateChildren)
	{
		Invalidate();
		if (invalidateChildren)
		{
			foreach (var c in Controls.List)
			{
				c.Invalidate(true);
			}
		}
	}

	public void Invalidate(Rectangle r) => Invalidate();

	public void Invalidate(Rectangle r, bool children) => Invalidate(children);

	public void Invalidate(Region r) => Invalidate();

	public void Update()
	{
	}

	public virtual void Refresh() => Invalidate(true);

	protected void SetStyle(ControlStyles flags, bool value)
	{
		if ((flags & ControlStyles.Selectable) != 0)
		{
			_selectable = value;
		}
	}

	private bool _selectable = true;

	protected bool GetStyle(ControlStyles flags) => (flags & ControlStyles.Selectable) != 0 ? _selectable : true;

	protected void UpdateStyles()
	{
	}

	internal static Dictionary<string, double[]> PaintStats;

	internal void PaintInternal(AM.DrawingContext ctx, AV.Size size)
	{
		var g = new Graphics(ctx, size);
		long t0 = PaintStats != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		try
		{
			var pe = new PaintEventArgs(g, new Rectangle(0, 0, _w, _h));
			OnPaintBackground(pe);
			OnPaint(pe);
			if (PaintStats != null)
			{
				string k = GetType().Name + " " + _w + "x" + _h;
				if (!PaintStats.TryGetValue(k, out var a)) PaintStats[k] = a = new double[2];
				a[0]++;
				a[1] += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
			}
		}
		catch (Exception ex)
		{
			Program.Log("PAINT ERR " + GetType().Name + ": " + ex);
		}
		finally
		{
			g.Dispose();
		}
	}

	protected virtual void OnPaintBackground(PaintEventArgs e)
	{
		Color c = _back;
		if (!c.IsEmpty && c.A > 0)
		{
			using var b = new SolidBrush(c);
			e.Graphics.FillRectangle(b, ClientRectangle);
		}
	}

	protected virtual void OnPaint(PaintEventArgs e)
	{
		Paint?.Invoke(this, e);
	}

	protected void InvokePaintBackground(Control c, PaintEventArgs e) => c.OnPaintBackground(e);

	protected void InvokePaint(Control c, PaintEventArgs e) => c.OnPaint(e);

	public void DrawToBitmap(Bitmap bmp, Rectangle target)
	{
		try
		{
			// RenderTargetBitmap.Render рисует визуал со смещением его Bounds в родителе,
			// поэтому рендерим на поверхность с запасом и вырезаем нужную область.
			// DPI оставляем 96: при другом DPI Avalonia 11.2 теряет часть текста.
			var off = View.Bounds.Position;
			int ox = Math.Max(0, (int)Math.Round(off.X));
			int oy = Math.Max(0, (int)Math.Round(off.Y));
			int w = Math.Max(1, bmp.Width);
			int h = Math.Max(1, bmp.Height);
			using var tmp = new AM.Imaging.RenderTargetBitmap(new AV.PixelSize(ox + w, oy + h), new AV.Vector(96, 96));
			tmp.Render(View);
			var rt = bmp.EnsureTarget();
			using var dc = rt.CreateDrawingContext(true);
			using (dc.PushRenderOptions(new AM.RenderOptions { BitmapInterpolationMode = AM.Imaging.BitmapInterpolationMode.HighQuality }))
			{
				dc.DrawImage(tmp, new AV.Rect(ox, oy, w, h), new AV.Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
			}
		}
		catch (Exception ex)
		{
			Program.Log("DRAWTOBITMAP " + ex.Message);
		}
	}

	// ---------- Раскладка ----------

	public void SuspendLayout()
	{
		_layoutSuspend++;
	}

	public void ResumeLayout()
	{
		ResumeLayout(true);
	}

	public void ResumeLayout(bool performLayout)
	{
		if (_layoutSuspend > 0)
		{
			_layoutSuspend--;
		}
		if (performLayout && _layoutSuspend == 0)
		{
			PerformLayout();
		}
	}

	public void PerformLayout()
	{
		if (_layoutSuspend > 0 || IsDisposed)
		{
			return;
		}
		OnLayout(new LayoutEventArgs(this, null));
	}

	public void PerformLayout(Control affected, string prop) => PerformLayout();

	// ---------- Координаты ----------

	public Point PointToScreen(Point p)
	{
		try
		{
			var top = AC.TopLevel.GetTopLevel(View);
			if (top != null)
			{
				var sp = AV.VisualExtensions.PointToScreen(View, new AV.Point(p.X, p.Y));
				double s = Screen.Scale;
				return new Point((int)Math.Round(sp.X / s), (int)Math.Round(sp.Y / s));
			}
		}
		catch
		{
		}
		var o = OffsetInForm();
		var f = FindForm();
		return new Point(p.X + o.X + (f?.Left ?? 0), p.Y + o.Y + (f?.Top ?? 0));
	}

	public Point PointToClient(Point p)
	{
		var z = PointToScreen(Point.Empty);
		return new Point(p.X - z.X, p.Y - z.Y);
	}

	public Rectangle RectangleToScreen(Rectangle r)
	{
		var p = PointToScreen(r.Location);
		return new Rectangle(p, r.Size);
	}

	public Rectangle RectangleToClient(Rectangle r)
	{
		var p = PointToClient(r.Location);
		return new Rectangle(p, r.Size);
	}

	private Point OffsetInForm()
	{
		int x = 0, y = 0;
		Control c = this;
		while (c != null && !(c is Form))
		{
			x += c._x;
			y += c._y;
			c = c.Parent;
		}
		return new Point(x, y);
	}

	// ---------- События ----------

	public event EventHandler Click;
	public event EventHandler DoubleClick;
	public event MouseEventHandler MouseClick;
	public event MouseEventHandler MouseDown;
	public event MouseEventHandler MouseUp;
	public event MouseEventHandler MouseMove;
	public event MouseEventHandler MouseWheel;
	public event EventHandler MouseEnter;
	public event EventHandler MouseLeave;
	public event PaintEventHandler Paint;
	public event EventHandler Resize;
	public event EventHandler SizeChanged;
	public event EventHandler LocationChanged;
	public event EventHandler Move;
	public event EventHandler VisibleChanged;
	public event EventHandler EnabledChanged;
	public event EventHandler TextChanged;
	public event EventHandler FontChanged;
	public event EventHandler ParentChanged;
	public event KeyEventHandler KeyDown;
	public event KeyEventHandler KeyUp;
	public event EventHandler<KeyPressEventArgs> KeyPress;
	public event EventHandler GotFocus;
	public event EventHandler LostFocus;
	public event EventHandler Enter;
	public event EventHandler Leave;
	public event EventHandler HandleCreated;
	public event EventHandler Disposed;
	public event ControlEventHandler ControlAdded;
	public event ControlEventHandler ControlRemoved;
	public event LayoutEventHandler Layout;

	private bool _hover;

	internal void RaiseMouseDown(MouseEventArgs e)
	{
		if (Enabled)
		{
			OnMouseDown(e);
		}
	}

	internal void RaiseMouseUp(MouseEventArgs e)
	{
		if (Enabled)
		{
			OnMouseUp(e);
		}
	}

	internal void RaiseClick(MouseEventArgs e)
	{
		if (!Enabled)
		{
			return;
		}
		OnClick(e);
		if (!IsDisposed)
		{
			OnMouseClick(e);
		}
	}

	internal void RaiseMouseMove(MouseEventArgs e)
	{
		if (!_hover)
		{
			RaiseMouseEnter();
		}
		OnMouseMove(e);
	}

	internal void RaiseMouseEnter()
	{
		if (_hover)
		{
			return;
		}
		_hover = true;
		OnMouseEnter(EventArgs.Empty);
	}

	internal void RaiseMouseLeave()
	{
		if (!_hover)
		{
			return;
		}
		_hover = false;
		OnMouseLeave(EventArgs.Empty);
	}

	internal void RaiseMouseWheel(HandledMouseEventArgs e) => OnMouseWheel(e);

	internal void RaiseKeyDown(KeyEventArgs e) => OnKeyDown(e);

	internal void RaiseKeyUp(KeyEventArgs e) => OnKeyUp(e);

	internal void RaiseKeyPress(KeyPressEventArgs e) => OnKeyPress(e);

	internal void RaiseGotFocus()
	{
		OnGotFocus(EventArgs.Empty);
		OnEnter(EventArgs.Empty);
	}

	internal void RaiseLostFocus()
	{
		OnLostFocus(EventArgs.Empty);
		OnLeave(EventArgs.Empty);
	}

	protected virtual void OnClick(EventArgs e) => Click?.Invoke(this, e);
	protected virtual void OnDoubleClick(EventArgs e) => DoubleClick?.Invoke(this, e);
	protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);
	protected virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(this, e);
	protected virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(this, e);
	protected virtual void OnMouseMove(MouseEventArgs e) => MouseMove?.Invoke(this, e);
	protected virtual void OnMouseWheel(MouseEventArgs e) => MouseWheel?.Invoke(this, e);
	protected virtual void OnMouseEnter(EventArgs e) => MouseEnter?.Invoke(this, e);
	protected virtual void OnMouseLeave(EventArgs e) => MouseLeave?.Invoke(this, e);
	protected virtual void OnSizeChanged(EventArgs e)
	{
		OnResize(e);
		SizeChanged?.Invoke(this, e);
	}
	protected virtual void OnResize(EventArgs e)
	{
		Invalidate();
		Resize?.Invoke(this, e);
	}
	protected virtual void OnLocationChanged(EventArgs e) => LocationChanged?.Invoke(this, e);
	protected virtual void OnMove(EventArgs e) => Move?.Invoke(this, e);
	protected virtual void OnVisibleChanged(EventArgs e)
	{
		VisibleChanged?.Invoke(this, e);
		foreach (var c in Controls.List.ToArray())
		{
			if (c._visible)
			{
				c.OnParentVisibleChanged(e);
			}
		}
	}
	protected virtual void OnParentVisibleChanged(EventArgs e)
	{
		OnVisibleChanged(e);
	}
	protected virtual void OnEnabledChanged(EventArgs e) => EnabledChanged?.Invoke(this, e);
	protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);
	protected virtual void OnFontChanged(EventArgs e) => FontChanged?.Invoke(this, e);
	protected virtual void OnParentChanged(EventArgs e) => ParentChanged?.Invoke(this, e);
	protected virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);
	protected virtual void OnKeyUp(KeyEventArgs e) => KeyUp?.Invoke(this, e);
	protected virtual void OnKeyPress(KeyPressEventArgs e) => KeyPress?.Invoke(this, e);
	protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);
	protected virtual void OnLostFocus(EventArgs e) => LostFocus?.Invoke(this, e);
	protected virtual void OnEnter(EventArgs e) => Enter?.Invoke(this, e);
	protected virtual void OnLeave(EventArgs e) => Leave?.Invoke(this, e);
	protected virtual void OnHandleCreated(EventArgs e) => HandleCreated?.Invoke(this, e);
	protected virtual void OnControlAdded(ControlEventArgs e) => ControlAdded?.Invoke(this, e);
	protected virtual void OnControlRemoved(ControlEventArgs e) => ControlRemoved?.Invoke(this, e);
	protected virtual void OnLayout(LayoutEventArgs e) => Layout?.Invoke(this, e);

	protected virtual bool ProcessDialogKey(Keys keyData) => Parent?.ProcessDialogKey(keyData) ?? false;

	internal bool ProcessDialogKeyInternal(Keys k) => ProcessDialogKey(k);

	protected virtual bool IsInputKey(Keys keyData) => false;

	// ---------- Освобождение ----------

	public void Dispose()
	{
		if (IsDisposed)
		{
			return;
		}
		Disposing = true;
		Dispose(true);
		IsDisposed = true;
		Disposed?.Invoke(this, EventArgs.Empty);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposing)
		{
			return;
		}
		foreach (var c in Controls.List.ToArray())
		{
			c.Dispose();
		}
		Parent?.Controls.Remove(this);
	}
}
