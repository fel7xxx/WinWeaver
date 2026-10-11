using System;
using System.Collections.Generic;
using AC = Avalonia.Controls;
using AL = Avalonia.Layout;
using AM = Avalonia.Media;
using AV = Avalonia;

namespace WinWeaver;

/// <summary>Пункт меню (минимальный аналог ToolStripMenuItem) поверх Avalonia MenuItem.</summary>
internal class ToolStripMenuItem
{
	internal readonly AC.MenuItem Mi = new AC.MenuItem();

	public ToolStripItemCollection DropDownItems { get; }

	public event EventHandler Click;

	public object Tag { get; set; }

	public ToolStripMenuItem()
	{
		DropDownItems = new ToolStripItemCollection(Mi.Items);
		Mi.Click += (s, e) =>
		{
			if (Mi.Items.Count == 0)
			{
				Click?.Invoke(this, EventArgs.Empty);
			}
		};
	}

	public bool Enabled
	{
		get => Mi.IsEnabled;
		set => Mi.IsEnabled = value;
	}

	private bool _checked;

	public bool Checked
	{
		get => _checked;
		set
		{
			_checked = value;
			DarkMenu.Decorate(this);
		}
	}

	internal string TextValue = "";

	internal string GlyphText;

	internal string Hint;

	internal bool Danger;

	public string Text
	{
		get => TextValue;
		set
		{
			TextValue = value ?? "";
			DarkMenu.Decorate(this);
		}
	}

	public bool HasDropDownItems => Mi.Items.Count > 0;
}

internal sealed class ToolStripItemCollection
{
	private readonly AC.ItemCollection _items;

	internal ToolStripItemCollection(AC.ItemCollection items)
	{
		_items = items;
	}

	public int Count => _items.Count;

	public void Add(ToolStripMenuItem it) => _items.Add(it.Mi);

	internal void AddRaw(object o) => _items.Add(o);

	public void Clear() => _items.Clear();
}

/// <summary>Тёмное контекстное меню WinWeaver (Avalonia ContextMenu со стилем программы).</summary>
internal class DarkMenu
{
	private static readonly Color Bg = Color.FromArgb(30, 30, 36);

	private static readonly Color BorderC = Color.FromArgb(66, 66, 76);

	private static readonly Color HoverC = Color.FromArgb(50, 50, 58);

	private readonly AC.ContextMenu _menu = new AC.ContextMenu();

	public ToolStripItemCollection Items { get; }

	public Font Font { get; set; } = Theme.Body;

	public DarkMenu()
	{
		Items = new ToolStripItemCollection(_menu.Items);
		_menu.Background = Conv.Solid(Bg);
		_menu.BorderBrush = Conv.Solid(BorderC);
		_menu.BorderThickness = new AV.Thickness(1);
		_menu.CornerRadius = new AV.CornerRadius(Theme.S(10f));
		_menu.Padding = new AV.Thickness(0, Theme.S(6f), 0, Theme.S(6f));
		_menu.Resources["MenuFlyoutItemBackgroundPointerOver"] = Conv.Solid(HoverC);
		_menu.Resources["MenuFlyoutItemBackgroundPressed"] = Conv.Solid(HoverC);
		_menu.Resources["MenuFlyoutSubItemBackgroundPointerOver"] = Conv.Solid(HoverC);
		_menu.Resources["MenuFlyoutPresenterBackground"] = Conv.Solid(Bg);
		_menu.Resources["MenuFlyoutPresenterBorderBrush"] = Conv.Solid(BorderC);
		_menu.Resources["MenuFlyoutItemForeground"] = Conv.Solid(Theme.Text);
		_menu.Resources["MenuFlyoutItemForegroundPointerOver"] = Conv.Solid(Color.White);
		_menu.Resources["MenuFlyoutSeparatorBackground"] = Conv.Solid(Color.FromArgb(52, 52, 60));
		_menu.FontFamily = Theme.Body.Typeface.FontFamily;
		_menu.FontSize = Theme.Body.PixelSize;
		_menu.Foreground = Conv.Solid(Theme.Text);
	}

	internal static int IconX => Theme.S(12f);

	internal static int IconW => Theme.S(24f);

	internal static int TextX => Theme.S(46f);

	internal static int RightPad => Theme.S(22f);

	private static AC.TextBlock Tb(string s, Font f, Color c) => new AC.TextBlock
	{
		Text = L.T(s ?? ""),
		FontFamily = f.Typeface.FontFamily,
		FontWeight = f.Typeface.Weight,
		FontSize = f.PixelSize,
		Foreground = Conv.Solid(c),
	};

	internal static void Decorate(ToolStripMenuItem it)
	{
		Color main = it.Danger ? Theme.Red : Theme.Text;
		var stack = new AC.StackPanel { Orientation = AL.Orientation.Vertical, Spacing = 1, Margin = new AV.Thickness(0, Theme.S(3f), Theme.S(8f), Theme.S(3f)) };
		stack.Children.Add(Tb(it.TextValue, Theme.Body, main));
		if (!string.IsNullOrEmpty(it.Hint))
		{
			stack.Children.Add(Tb(it.Hint, Theme.Small, it.Danger ? Theme.Mix(Theme.Red, Theme.Sub, 0.4f) : Theme.Sub));
		}
		it.Mi.Header = stack;
		string glyph = it.Checked ? "\ue73e" : it.GlyphText;
		if (!string.IsNullOrEmpty(glyph) && Theme.Icon != null)
		{
			var icon = new AC.TextBlock
			{
				Text = glyph,
				FontFamily = Theme.IconSmall.Typeface.FontFamily,
				FontSize = Theme.IconSmall.PixelSize,
				Foreground = Conv.Solid(it.Danger ? Theme.Red : (it.Checked ? Theme.Accent : Theme.Sub)),
				HorizontalAlignment = AL.HorizontalAlignment.Center,
				VerticalAlignment = AL.VerticalAlignment.Center,
			};
			it.Mi.Icon = icon;
		}
		else
		{
			it.Mi.Icon = null;
		}
		it.Mi.MinHeight = Theme.S(34f);
	}

	public void Header(string title, string sub = null)
	{
		var stack = new AC.StackPanel { Orientation = AL.Orientation.Vertical, Margin = new AV.Thickness(Theme.S(14f), Theme.S(8f), Theme.S(18f), Theme.S(4f)), IsHitTestVisible = false };
		stack.Children.Add(Tb(title, Theme.Title, Theme.Text));
		if (!string.IsNullOrEmpty(sub))
		{
			stack.Children.Add(Tb(sub, Theme.Small, Theme.Sub));
		}
		Items.AddRaw(stack);
		Sep();
	}

	public ToolStripMenuItem Item(string text, Action click, bool danger = false, bool check = false)
	{
		return Item(Items, text, click, danger, check);
	}

	public ToolStripMenuItem Item(string text, string glyph, string hint, Action click, bool danger = false)
	{
		var it = Item(Items, text, click, danger);
		it.GlyphText = glyph;
		it.Hint = hint;
		Decorate(it);
		return it;
	}

	public static ToolStripMenuItem Item(ToolStripItemCollection into, string text, Action click, bool danger = false, bool check = false)
	{
		var it = new ToolStripMenuItem { Danger = danger, Tag = danger ? "danger" : null };
		it.TextValue = text ?? "";
		it.Checked = check;
		if (click != null)
		{
			it.Click += delegate
			{
				click();
			};
		}
		Decorate(it);
		into.Add(it);
		return it;
	}

	public ToolStripMenuItem Sub(string text, string glyph = null, string hint = null)
	{
		var it = Item(text, null);
		it.GlyphText = glyph;
		it.Hint = hint;
		Decorate(it);
		return it;
	}

	public void Sep()
	{
		Items.AddRaw(new AC.Separator { Margin = new AV.Thickness(Theme.S(10f), Theme.S(4f)) });
	}

	/// <summary>Показать меню у указателя (точка в экранных координатах, как у WinForms).</summary>
	public void Show(Point screen)
	{
		var top = Application.AnyTopLevel() as AC.Window;
		if (top == null)
		{
			return;
		}
		_menu.Placement = AC.PlacementMode.Pointer;
		_menu.Open(top.Content as AC.Control ?? top);
	}

	public void Show(Control c, Point local)
	{
		var f = c?.FindForm();
		_menu.Placement = AC.PlacementMode.Pointer;
		_menu.Open(c?.View ?? (AC.Control)f?.Window);
	}

	public void Dispose()
	{
	}
}
