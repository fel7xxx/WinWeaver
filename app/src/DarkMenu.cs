// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	// dark context menu in the app style: rounded glass window, optional header, icons,
	// second line with a hint under an item, silver hover pill, red danger items, fade-in
	internal class DarkMenu : ContextMenuStrip
	{
		// menu item with an icon glyph and an optional hint line
		internal class DarkItem : ToolStripMenuItem
		{
			public string GlyphText;

			public string Hint;

			public bool Danger;

			public DarkItem(string text) : base(text)
			{
			}

			public override Size GetPreferredSize(Size constrainingSize)
			{
				Size s = base.GetPreferredSize(constrainingSize);
				int tw = Theme.Measure(Text ?? "", Theme.Body).Width;
				if (!string.IsNullOrEmpty(Hint))
				{
					tw = Math.Max(tw, Theme.Measure(Hint, Theme.Small).Width);
					s.Height = Math.Max(s.Height, Theme.S(10f) + Theme.Measure("Ag", Theme.Body).Height + Theme.Measure("Ag", Theme.Small).Height + Theme.S(2f));
				}
				else
				{
					s.Height = Math.Max(s.Height, Theme.S(34f));
				}
				s.Width = TextX + tw + RightPad + (HasDropDownItems ? Theme.S(18f) : 0);
				return s;
			}
		}

		// non-clickable caption at the top of the menu
		internal class HeaderItem : ToolStripLabel
		{
			public string Sub;

			public HeaderItem(string text, string sub) : base(text)
			{
				Sub = sub;
				Enabled = true;
			}

			public override Size GetPreferredSize(Size constrainingSize)
			{
				int w = TextX + Math.Max(Theme.Measure(Text ?? "", Theme.Title).Width, string.IsNullOrEmpty(Sub) ? 0 : Theme.Measure(Sub, Theme.Small).Width) + RightPad;
				int h = Theme.S(12f) + Theme.Measure("Ag", Theme.Title).Height + (string.IsNullOrEmpty(Sub) ? 0 : Theme.Measure("Ag", Theme.Small).Height) + Theme.S(6f);
				return new Size(w, h);
			}
		}

		private class Colors : ProfessionalColorTable
		{
			public override Color MenuBorder => Color.FromArgb(66, 66, 76);
			public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 36);
			public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 36);
			public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 36);
			public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 36);
			public override Color SeparatorDark => Color.FromArgb(52, 52, 60);
			public override Color SeparatorLight => Color.FromArgb(30, 30, 36);
			public override Color MenuItemSelected => Color.FromArgb(50, 50, 58);
			public override Color MenuItemBorder => Color.FromArgb(50, 50, 58);
		}

		// one column grid for every row, measured from the left edge of the menu window
		// (items and labels have different Bounds.X inside a drop-down, so local offsets would not line up)
		internal static int IconX => Theme.S(12f);

		internal static int IconW => Theme.S(24f);

		internal static int TextX => Theme.S(46f);

		internal static int RightPad => Theme.S(22f);

		private static readonly Color Top = Color.FromArgb(38, 38, 45);

		private static readonly Color Bottom = Color.FromArgb(25, 25, 30);

		private class DarkRenderer : ToolStripProfessionalRenderer
		{
			public DarkRenderer() : base(new Colors())
			{
				RoundedEdges = false;
			}

			protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
			{
				Rectangle r = new Rectangle(Point.Empty, e.ToolStrip.Size);
				using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, -1, r.Width, r.Height + 2), Top, Bottom, 90f))
				{
					e.Graphics.FillRectangle(b, r);
				}
				// silver highlight along the top edge
				using (LinearGradientBrush hl = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, r.Width), 2), Color.Transparent, Color.Transparent, 0f))
				{
					ColorBlend cb = new ColorBlend();
					cb.Positions = new float[3] { 0f, 0.5f, 1f };
					cb.Colors = new Color[3] { Color.FromArgb(0, 220, 220, 230), Color.FromArgb(120, 225, 225, 232), Color.FromArgb(0, 220, 220, 230) };
					hl.InterpolationColors = cb;
					e.Graphics.FillRectangle(hl, 0, 0, r.Width, 1);
				}
			}

			protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
			{
			}

			protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
			{
				Rectangle r = new Rectangle(Point.Empty, e.ToolStrip.Size);
				using (Pen pen = new Pen(Color.FromArgb(70, 70, 82)))
				{
					if (e.ToolStrip.Region != null)
					{
						Theme.Smooth(e.Graphics);
						using (GraphicsPath p = Theme.Round(new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f), Theme.S(10f)))
						{
							e.Graphics.DrawPath(pen, p);
						}
					}
					else
					{
						e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
					}
				}
			}

			protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
			{
				int y = e.Item.Height / 2;
				int sw = (e.ToolStrip != null) ? e.ToolStrip.Width : e.Item.Width;
				int x0 = Theme.S(12f) - e.Item.Bounds.X;
				int x1 = sw - Theme.S(12f) - e.Item.Bounds.X;
				using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(x0, y, Math.Max(2, x1 - x0), 1), Color.Transparent, Color.Transparent, 0f))
				{
					ColorBlend cb = new ColorBlend();
					cb.Positions = new float[3] { 0f, 0.5f, 1f };
					cb.Colors = new Color[3] { Color.FromArgb(0, 80, 80, 92), Color.FromArgb(255, 62, 62, 72), Color.FromArgb(0, 80, 80, 92) };
					b.InterpolationColors = cb;
					e.Graphics.FillRectangle(b, x0, y, x1 - x0, 1);
				}
			}

			private static bool IsDanger(ToolStripItem it)
			{
				return (it is DarkItem d && d.Danger) || (it.Tag as string == "danger");
			}

			protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
			{
				Graphics g = e.Graphics;
				Theme.Smooth(g);
				bool danger = IsDanger(e.Item);
				if (e.Item.Selected && e.Item.Enabled)
				{
					int ox = e.Item.Bounds.X;
					int sw = (e.ToolStrip != null) ? e.ToolStrip.Width : (e.Item.Width + ox * 2);
					RectangleF r = new RectangleF(Theme.S(5f) - ox + 0.5f, 1.5f, sw - Theme.S(10f) - 1f, e.Item.Height - 3f);
					using (GraphicsPath p = Theme.Round(r, Theme.S(8f)))
					{
						if (danger)
						{
							Theme.FillGradient(g, p, r, Color.FromArgb(78, 44, 46), Color.FromArgb(62, 36, 38));
						}
						else
						{
							Theme.FillGradient(g, p, r, Color.FromArgb(62, 62, 72), Color.FromArgb(50, 50, 58));
						}
						using (Pen pen = new Pen(danger ? Color.FromArgb(70, Theme.Red) : Color.FromArgb(36, 255, 255, 255)))
						{
							g.DrawPath(pen, p);
						}
					}
				}
				DarkItem di = e.Item as DarkItem;
				if (di != null && !string.IsNullOrEmpty(di.GlyphText) && !di.Checked)
				{
					Rectangle ir = new Rectangle(IconX - e.Item.Bounds.X, 0, IconW, e.Item.Height);
					Color c = !e.Item.Enabled ? Theme.Dim : (danger ? Theme.Red : (e.Item.Selected ? Theme.Text : Theme.Silver2));
					if (Theme.IconSmall != null)
					{
						Theme.DrawIcon(g, di.GlyphText, Theme.IconSmall, ir, c);
					}
				}
			}

			protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
			{
				Graphics g = e.Graphics;
				if (e.Item is HeaderItem h)
				{
					int x = TextX - e.Item.Bounds.X;
					int hy = Theme.S(8f);
					int th = Theme.Measure("Ag", Theme.Title).Height;
					TextRenderer.DrawText(g, h.Text, Theme.Title, new Rectangle(x, hy, e.Item.Width - x, th), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
					if (!string.IsNullOrEmpty(h.Sub))
					{
						TextRenderer.DrawText(g, h.Sub, Theme.Small, new Rectangle(x, hy + th, e.Item.Width - x, Theme.Measure("Ag", Theme.Small).Height), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
					}
					return;
				}
				bool danger = IsDanger(e.Item);
				Color c = !e.Item.Enabled ? Theme.Dim : (danger ? Theme.Red : Theme.Text);
				DarkItem di = e.Item as DarkItem;
				// the invisible width spacer (see Widen) is passed as shortcut text: never drawn
				if (di != null && !(e.Item.Owner is MenuStrip) && e.Text != di.Text)
				{
					return;
				}
				if (di == null || e.Item.Owner is MenuStrip)
				{
					e.TextColor = c;
					base.OnRenderItemText(e);
					return;
				}
				int tx = TextX - e.Item.Bounds.X;
				int right = e.Item.Width - Theme.S(10f) - (di.HasDropDownItems ? Theme.S(16f) : 0);
				TextFormatFlags f = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
				int th1 = Theme.Measure("Ag", Theme.Body).Height;
				if (string.IsNullOrEmpty(di.Hint))
				{
					TextRenderer.DrawText(g, di.Text, Theme.Body, new Rectangle(tx, 0, right - tx, e.Item.Height), c, f | TextFormatFlags.VerticalCenter);
					return;
				}
				int th2 = Theme.Measure("Ag", Theme.Small).Height;
				int y = (e.Item.Height - th1 - th2) / 2;
				TextRenderer.DrawText(g, di.Text, Theme.Body, new Rectangle(tx, y, right - tx, th1), c, f);
				Color hc = !e.Item.Enabled ? Color.FromArgb(80, 80, 90) : (danger ? Color.FromArgb(190, Theme.Red) : (e.Item.Selected ? Color.FromArgb(178, 178, 188) : Theme.Dim));
				TextRenderer.DrawText(g, di.Hint, Theme.Small, new Rectangle(tx, y + th1, right - tx, th2), hc, f);
			}

			protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
			{
				e.ArrowColor = e.Item.Selected ? Theme.Text : Theme.Sub;
				base.OnRenderArrow(e);
			}

			protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
			{
				Rectangle r = new Rectangle(IconX - e.Item.Bounds.X, 0, IconW, e.Item.Height);
				if (Theme.IconSmall != null)
				{
					Theme.DrawIcon(e.Graphics, Glyph.Check, Theme.IconSmall, r, Theme.Green);
				}
				else
				{
					TextRenderer.DrawText(e.Graphics, "✓", Theme.Body, r, Theme.Green, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				}
			}
		}

		private static DarkRenderer _renderer;

		private static DarkRenderer R => _renderer ?? (_renderer = new DarkRenderer());

		public DarkMenu()
		{
			Renderer = R;
			Font = Theme.Body;
			ShowImageMargin = true;
			ImageScalingSize = new Size(Theme.S(18f), Theme.S(18f));
			Padding = new Padding(0, Theme.S(6f), 0, Theme.S(6f));
			DropShadowEnabled = true;
		}

		// caption at the top: "Перезагрузка" + short explanation
		public void Header(string title, string sub = null)
		{
			Items.Add(new HeaderItem(title, sub));
			Sep();
		}

		public ToolStripMenuItem Item(string text, Action click, bool danger = false, bool check = false)
		{
			return Item(Items, text, click, danger, check);
		}

		public ToolStripMenuItem Item(string text, string glyph, string hint, Action click, bool danger = false)
		{
			DarkItem mi = (DarkItem)Item(Items, text, click, danger, false);
			mi.GlyphText = glyph;
			mi.Hint = hint;
			Widen(mi);
			return mi;
		}

		// WinForms sizes menu items by their title only, so a longer grey hint got cut off.
		// A never-drawn shortcut string reserves the missing width.
		private static void Widen(DarkItem mi)
		{
			if (string.IsNullOrEmpty(mi.Hint))
			{
				return;
			}
			int need = Theme.Measure(mi.Hint, Theme.Small).Width - Theme.Measure(mi.Text ?? "", Theme.Body).Width + Theme.S(10f);
			if (need <= 0)
			{
				return;
			}
			int cw = Math.Max(1, Theme.Measure("0000000000", Theme.Body).Width / 10);
			mi.ShowShortcutKeys = true;
			mi.ShortcutKeyDisplayString = new string('0', need / cw + 1);
		}

		public static ToolStripMenuItem Item(ToolStripItemCollection into, string text, Action click, bool danger = false, bool check = false)
		{
			DarkItem mi = new DarkItem(text)
			{
				ForeColor = Theme.Text,
				Padding = new Padding(Theme.S(6f), Theme.S(4f), Theme.S(12f), Theme.S(4f)),
				Checked = check,
				Danger = danger,
				Tag = danger ? "danger" : null
			};
			if (click != null)
			{
				mi.Click += delegate
				{
					click();
				};
			}
			into.Add(mi);
			return mi;
		}

		public ToolStripMenuItem Sub(string text, string glyph = null, string hint = null)
		{
			DarkItem mi = (DarkItem)Item(text, null);
			mi.GlyphText = glyph;
			mi.Hint = hint;
			Widen(mi);
			if (mi.DropDown is ToolStripDropDownMenu dd)
			{
				dd.Renderer = R;
				dd.Font = Font;
				dd.ShowImageMargin = true;
				dd.Padding = new Padding(0, Theme.S(6f), 0, Theme.S(6f));
				dd.HandleCreated += delegate
				{
					Shape(dd);
				};
				dd.Opening += delegate
				{
					FadeIn(dd);
				};
			}
			return mi;
		}

		public void Sep()
		{
			Items.Add(new ToolStripSeparator());
		}

		// Windows 11 rounds the popup itself (with shadow); older systems get a rounded region
		private static void Shape(ToolStripDropDown d)
		{
			try
			{
				if (Win.RoundCorners(d.Handle))
				{
					return;
				}
			}
			catch
			{
			}
			d.SizeChanged -= RegionOnSize;
			d.SizeChanged += RegionOnSize;
			RegionOnSize(d, EventArgs.Empty);
		}

		private static void RegionOnSize(object sender, EventArgs e)
		{
			ToolStripDropDown d = sender as ToolStripDropDown;
			if (d == null || d.Width <= 0 || d.Height <= 0)
			{
				return;
			}
			using (GraphicsPath p = Theme.Round(new RectangleF(0f, 0f, d.Width, d.Height), Theme.S(10f)))
			{
				Region old = d.Region;
				d.Region = new Region(p);
				old?.Dispose();
			}
		}

		private static void FadeIn(ToolStripDropDown d)
		{
			if (!Anim.Enabled)
			{
				return;
			}
			try
			{
				d.Opacity = 0.0;
				Anim.Start(d, 130, delegate(float t)
				{
					try
					{
						d.Opacity = Anim.EaseOut(t);
					}
					catch
					{
					}
				}, delegate
				{
					try
					{
						d.Opacity = 1.0;
					}
					catch
					{
					}
				}, "menu-fade-" + d.GetHashCode());
			}
			catch
			{
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Shape(this);
		}

		protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
		{
			base.OnOpening(e);
			if (!e.Cancel)
			{
				FadeIn(this);
			}
		}

		protected override void OnClosed(ToolStripDropDownClosedEventArgs e)
		{
			base.OnClosed(e);
			BeginInvoke((Action)Dispose);
		}
	}
}
