// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	// Dialog in the common style (DlgChrome header + footer band) with a free content area between them.
	// Subclasses put their controls into Content and call Finish(contentHeight).
	internal class PanelDialog : Form, ISurface
	{
		protected readonly string TitleText;

		protected readonly string BodyText;

		protected readonly DlgIcon IconKind;

		protected readonly Panel Content;

		protected readonly FButton Yes;

		protected readonly FButton No;

		protected FButton Extra;

		private DlgChrome.Head _head;

		private int _footerTop;

		protected int DlgW;

		protected int ContentTop => _head.Bottom + Theme.S(20f);

		protected int M => DlgChrome.M;

		protected int InnerW => DlgW - M * 2;

		protected PanelDialog(string title, string body, string yes, string no, DlgIcon icon, float width = 560f)
		{
			TitleText = title;
			BodyText = body;
			IconKind = icon;
			DlgW = Theme.S(width);
			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.Manual;
			ShowInTaskbar = false;
			AutoScaleMode = AutoScaleMode.None;
			BackColor = Theme.Card2;
			KeyPreview = true;
			Text = title;
			if (WDialog.AppIcon != null)
			{
				Icon = WDialog.AppIcon;
			}
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			_head = DlgChrome.Layout(DlgW, title, body);
			Content = new SurfacePanel(this) { Location = new Point(0, ContentTop), Width = DlgW };
			Controls.Add(Content);
			Yes = new FButton { Text = yes, Kind = DlgChrome.YesKind(yes, icon), Font = Theme.Body };
			Yes.AutoWidth(Theme.S(44f));
			Yes.Click += delegate
			{
				if (Validate())
				{
					DialogResult = DialogResult.Yes;
					Close();
				}
			};
			Controls.Add(Yes);
			if (!string.IsNullOrEmpty(no))
			{
				No = new FButton { Text = no, Kind = BtnKind.Ghost, Font = Theme.Body };
				No.AutoWidth(Theme.S(44f));
				No.Click += delegate
				{
					DialogResult = DialogResult.No;
					Close();
				};
				Controls.Add(No);
			}
		}

		// adds a left-aligned ghost button in the footer (e.g. "Choose steps…")
		protected FButton AddExtra(string text, EventHandler click)
		{
			Extra = new FButton { Text = text, Kind = BtnKind.Ghost, Font = Theme.Body };
			Extra.AutoWidth(Theme.S(30f));
			Extra.Click += click;
			Controls.Add(Extra);
			return Extra;
		}

		protected new virtual bool Validate()
		{
			return true;
		}

		protected void Finish(int contentH)
		{
			Content.Height = contentH;
			_footerTop = Content.Bottom + Theme.S(22f);
			Size = new Size(DlgW, _footerTop + DlgChrome.FooterH);
			DlgChrome.PlaceButtons(DlgW, _footerTop, Yes, No);
			if (Extra != null)
			{
				Extra.SetBounds(M, _footerTop + DlgChrome.FooterPad, Math.Max(Theme.S(112f), Extra.Width), DlgChrome.BtnH);
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			DlgChrome.Round(this);
			DlgChrome.FadeIn(this);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape && No != null)
			{
				DialogResult = DialogResult.No;
				Close();
			}
			base.OnKeyDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			DlgChrome.Paint(e.Graphics, Size, IconKind, _footerTop, closeHot: false);
			DlgChrome.DrawHead(e.Graphics, _head, IconKind, TitleText);
		}

		public void PaintSurface(Graphics g)
		{
			DlgChrome.Bg(g, Size, IconKind, _footerTop);
		}

		// content panel: paints the dialog background under itself so children blend in
		private class SurfacePanel : Panel, ISurface
		{
			private readonly PanelDialog _d;

			public SurfacePanel(PanelDialog d)
			{
				_d = d;
				SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
			}

			public void PaintSurface(Graphics g)
			{
				GraphicsState st = g.Save();
				g.TranslateTransform(-Left, -Top);
				_d.PaintSurface(g);
				g.Restore(st);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				PaintSurface(e.Graphics);
				_d.PaintContent(e.Graphics);
			}
		}

		// subclasses draw labels and static texts here (coordinates of the content panel)
		protected virtual void PaintContent(Graphics g)
		{
		}

		protected void Label(Graphics g, string text, int y, string right = null)
		{
			TextRenderer.DrawText(g, text.ToUpperInvariant(), Theme.FC(8f, semi: true), new Point(M, y), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			if (right != null)
			{
				TextRenderer.DrawText(g, right, Theme.Tiny, new Rectangle(M, y, InnerW, Theme.S(16f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.Right);
			}
		}

		public DialogResult ShowFor(IWin32Window owner)
		{
			Form form = owner as Form;
			Dimmer dimmer = null;
			if (form != null && form.Visible && form.WindowState != FormWindowState.Minimized)
			{
				Location = new Point(form.Left + (form.Width - Width) / 2, form.Top + Math.Max(0, (form.Height - Height) / 2));
				dimmer = new Dimmer(form);
				dimmer.Show(form);
			}
			else
			{
				Rectangle wa = Screen.PrimaryScreen.WorkingArea;
				Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + Math.Max(0, (wa.Height - Height) / 2));
				TopMost = true;
				ShowInTaskbar = true;
			}
			try
			{
				return ShowDialog((IWin32Window)dimmer ?? owner);
			}
			finally
			{
				if (dimmer != null)
				{
					dimmer.Close();
					dimmer.Dispose();
				}
				form?.Activate();
			}
		}
	}

	// dark rounded text field (optionally a password field)
	internal class FField : Control, ISurface
	{
		public readonly TextBox Box;

		public string Placeholder;

		private bool _focus;

		public FField(string placeholder, bool password = false, int maxLen = 64)
		{
			Placeholder = placeholder;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
			Height = Theme.S(40f);
			Box = new TextBox
			{
				BorderStyle = BorderStyle.None,
				BackColor = Color.FromArgb(30, 32, 38),
				ForeColor = Theme.Text,
				Font = Theme.Body,
				MaxLength = maxLen,
				UseSystemPasswordChar = password
			};
			Box.GotFocus += delegate { _focus = true; Invalidate(); };
			Box.LostFocus += delegate { _focus = false; Box.Visible = Box.Text.Length > 0; Invalidate(); };
			Box.TextChanged += delegate { Invalidate(); OnTextChanged(EventArgs.Empty); };
			Box.Visible = false;
			Controls.Add(Box);
			Cursor = Cursors.IBeam;
			SetStyle(ControlStyles.Selectable, value: true);
			TabStop = true;
		}

		public string Value
		{
			get { return Box.Text; }
			set { Box.Text = value; Box.Visible = _focus || Box.Text.Length > 0; }
		}

		// the text box is hidden while the field is empty so the placeholder stays visible
		private void Activate()
		{
			Box.Visible = true;
			Box.Focus();
		}

		protected override void OnClick(EventArgs e)
		{
			Activate();
			base.OnClick(e);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);
			Activate();
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			base.OnLayout(e);
			if (Box == null)
			{
				return;
			}
			int pad = Theme.S(14f);
			Box.SetBounds(pad, (Height - Box.PreferredHeight) / 2 + 1, Width - pad * 2, Box.PreferredHeight);
		}

		public void PaintSurface(Graphics g)
		{
			if (Parent is ISurface s)
			{
				GraphicsState st = g.Save();
				g.TranslateTransform(-Left, -Top);
				s.PaintSurface(g);
				g.Restore(st);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			PaintSurface(g);
			Theme.Smooth(g);
			RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
			using (GraphicsPath p = Theme.Round(r, Theme.S(10f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 32, 38)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(_focus ? Theme.Ice : Theme.BorderHi, _focus ? 1.4f : 1f))
				{
					g.DrawPath(pen, p);
				}
			}
			if (Box.Text.Length == 0 && !string.IsNullOrEmpty(Placeholder))
			{
				TextRenderer.DrawText(g, Placeholder, Theme.Body, new Rectangle(Theme.S(14f), 0, Width - Theme.S(28f), Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
		}
	}

	// row of selectable tiles: picture (optional), title and hint; ice outline on the selected one
	internal class TileChoice : Control, ISurface
	{
		public class Tile
		{
			public string Title;
			public string Hint;
			public Image Picture;
			public string Glyph;
			public Rectangle R;
		}

		public readonly List<Tile> Tiles = new List<Tile>();

		public int Selected;

		public int PictureH;

		public event Action<int> Changed;

		private int _hot = -1;

		public TileChoice()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
			Cursor = Cursors.Hand;
		}

		public Tile Add(string title, string hint, Image pic = null, string glyph = null)
		{
			Tile t = new Tile { Title = title, Hint = hint, Picture = pic, Glyph = glyph };
			Tiles.Add(t);
			return t;
		}

		public int Measure(int w)
		{
			int textH = Theme.S(hintLines * 16f + 34f);
			return ((PictureH > 0) ? (PictureH + Theme.S(10f)) : 0) + textH;
		}

		public int hintLines = 1;

		private void Calc()
		{
			int n = Math.Max(1, Tiles.Count);
			int gap = Theme.S(10f);
			int tw = (Width - gap * (n - 1)) / n;
			for (int i = 0; i < Tiles.Count; i++)
			{
				Tiles[i].R = new Rectangle(i * (tw + gap), 0, tw, Height - 1);
			}
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			Calc();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			for (int i = 0; i < Tiles.Count; i++)
			{
				if (Tiles[i].R.Contains(e.Location))
				{
					h = i;
				}
			}
			if (h != _hot)
			{
				_hot = h;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = -1;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			for (int i = 0; i < Tiles.Count; i++)
			{
				if (Tiles[i].R.Contains(e.Location) && i != Selected)
				{
					Selected = i;
					Invalidate();
					Changed?.Invoke(i);
				}
			}
			base.OnMouseDown(e);
		}

		public void PaintSurface(Graphics g)
		{
			if (Parent is ISurface s)
			{
				GraphicsState st = g.Save();
				g.TranslateTransform(-Left, -Top);
				s.PaintSurface(g);
				g.Restore(st);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			PaintSurface(g);
			Calc();
			Theme.Smooth(g);
			for (int i = 0; i < Tiles.Count; i++)
			{
				Tile t = Tiles[i];
				bool sel = i == Selected;
				bool hot = i == _hot;
				RectangleF r = new RectangleF(t.R.X + 0.5f, t.R.Y + 0.5f, t.R.Width - 1f, t.R.Height - 1f);
				using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
				{
					Theme.FillGradient(g, p, r, sel ? Theme.Mix(Theme.CardHot1, Theme.Ice, 0.08f) : (hot ? Theme.CardHot1 : Theme.Card1), sel ? Theme.CardHot2 : Theme.Card2);
					using (Pen pen = new Pen(sel ? Theme.Ice : (hot ? Theme.BorderHi : Theme.Border), sel ? 1.6f : 1f))
					{
						g.DrawPath(pen, p);
					}
				}
				int pad = Theme.S(10f);
				int y = t.R.Y + pad;
				if (PictureH > 0)
				{
					Rectangle pr = new Rectangle(t.R.X + pad, y, t.R.Width - pad * 2, PictureH);
					using (GraphicsPath pp = Theme.Round(pr, Theme.S(8f)))
					{
						Region old = g.Clip;
						g.SetClip(pp, CombineMode.Intersect);
						if (t.Picture != null)
						{
							g.InterpolationMode = InterpolationMode.HighQualityBicubic;
							// cover: crop to the tile ratio
							float ir = (float)t.Picture.Width / t.Picture.Height;
							float rr = (float)pr.Width / pr.Height;
							RectangleF src = (ir > rr) ? new RectangleF((t.Picture.Width - t.Picture.Height * rr) / 2f, 0, t.Picture.Height * rr, t.Picture.Height) : new RectangleF(0, (t.Picture.Height - t.Picture.Width / rr) / 2f, t.Picture.Width, t.Picture.Width / rr);
							g.DrawImage(t.Picture, pr, src, GraphicsUnit.Pixel);
						}
						else
						{
							using (SolidBrush b = new SolidBrush(Theme.Control2))
							{
								g.FillRectangle(b, pr);
							}
							if (t.Glyph != null)
							{
								Theme.DrawIcon(g, t.Glyph, Theme.IconBig, pr, Theme.Sub);
							}
						}
						g.Clip = old;
						old.Dispose();
					}
					y = pr.Bottom + Theme.S(8f);
				}
				int tx = t.R.X + pad + Theme.S(2f);
				int tw = t.R.Width - pad * 2 - Theme.S(26f);
				TextRenderer.DrawText(g, t.Title, Theme.Title, new Rectangle(tx, y, tw, Theme.S(20f)), sel ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, 0.6f), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				if (!string.IsNullOrEmpty(t.Hint))
				{
					TextRenderer.DrawText(g, t.Hint, Theme.Tiny, new Rectangle(tx, y + Theme.S(21f), t.R.Width - pad * 2, Theme.S(16f) * hintLines), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
				}
				// selection dot
				Rectangle dot = new Rectangle(t.R.Right - pad - Theme.S(16f), y + Theme.S(2f), Theme.S(16f), Theme.S(16f));
				using (Pen dp = new Pen(sel ? Theme.Ice : Theme.BorderHi, 1.4f))
				{
					g.DrawEllipse(dp, dot);
				}
				if (sel)
				{
					using (SolidBrush db = new SolidBrush(Theme.Ice))
					{
						g.FillEllipse(db, Rectangle.Inflate(dot, -Theme.S(4f), -Theme.S(4f)));
					}
				}
			}
		}
	}
}
