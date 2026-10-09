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
	internal class Sidebar : Control
	{
		public class Item
		{
			public string Key;

			public string Text;

			public string Glyph;

			public bool Bottom;

			public int Badge;

			public Rectangle R;
		}

		public readonly List<Item> Items = new List<Item>();

		public string Selected;

		private int _hot = -1;

		public Image Logo;

		public string Version = "v" + Brand.Version;

		private Rectangle _credR;

		private bool _credHot;

		private readonly Font _fLogo = Theme.F(16.5f, semi: true, FontStyle.Bold);

		private readonly Font _fNav = Theme.F(10f);

		private readonly Font _fNavSel = Theme.F(10f, semi: true);

		private readonly Font _fBadge = Theme.F(7.5f, semi: true);

		private readonly Tween _pillY;

		private readonly Tween _hotA;

		private int _hotLast = -1;

		private bool _placed;

		private int Top0 => Theme.S(112f);

		private int ItemH => Theme.S(44f);

		public event Action<string> Navigate;

		// "restart needed" card above the bottom items; null = hidden
		public string RebootText;

		public string RebootSub;

		public bool RebootScheduled;

		public event Action<Point> RebootClicked;

		private Rectangle _rbR;

		private bool _rbHot;

		private int RbH => (RebootText != null) ? Theme.S(62f) : 0;

		// "new version" card (ice accent) above the restart card; null = hidden
		public string UpdateText;

		public string UpdateSub;

		public event Action UpdateClicked;

		public event Action SupportClicked;

		private Rectangle _upR;

		private bool _upHot;

		private Rectangle _heartR;

		private bool _heartHot;

		private int UpH => (UpdateText != null) ? Theme.S(62f) : 0;

		public void SetUpdate(string text, string sub)
		{
			if (text == UpdateText && sub == UpdateSub)
			{
				return;
			}
			UpdateText = text;
			UpdateSub = sub;
			Calc();
			Invalidate();
		}

		public void SetReboot(string text, string sub, bool scheduled)
		{
			if (text == RebootText && sub == RebootSub && scheduled == RebootScheduled)
			{
				return;
			}
			RebootText = text;
			RebootSub = sub;
			RebootScheduled = scheduled;
			Calc();
			Invalidate();
		}

		public Sidebar()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			Cursor = Cursors.Default;
			_pillY = new Tween(this, 0f, 0.22f);
			_hotA = new Tween(this, 0f, 0.3f);
		}

		public void Add(string key, string text, string glyph, bool bottom = false)
		{
			Items.Add(new Item
			{
				Key = key,
				Text = text,
				Glyph = glyph,
				Bottom = bottom
			});
		}

		public void SetBadge(string key, int n)
		{
			foreach (Item item in Items)
			{
				if (item.Key == key && item.Badge != n)
				{
					item.Badge = n;
					Invalidate();
				}
			}
		}

		private void Calc()
		{
			int y = Top0;
			int h = ItemH;
			// shrink rows a little when the window is too short for every item
			int nTop = 0;
			int nBot = 0;
			foreach (Item it in Items)
			{
				if (it.Bottom)
				{
					nBot++;
				}
				else
				{
					nTop++;
				}
			}
			int avail = base.Height - Theme.S(58f) - Top0 - Theme.S(24f) - ((RbH > 0) ? (RbH + Theme.S(20f)) : 0) - ((UpH > 0) ? (UpH + Theme.S(10f)) : 0);
			if (nTop + nBot > 0 && (nTop + nBot) * h > avail)
			{
				h = Math.Max(Theme.S(34f), avail / (nTop + nBot));
			}
			int x = Theme.S(14f);
			int w = base.Width - Theme.S(28f);
			foreach (Item item in Items)
			{
				if (!item.Bottom)
				{
					item.R = new Rectangle(x, y, w, h - Theme.S(6f));
					y += h;
				}
			}
			int yb = base.Height - Theme.S(58f);
			for (int i = Items.Count - 1; i >= 0; i--)
			{
				Item item2 = Items[i];
				if (item2.Bottom)
				{
					yb -= h;
					item2.R = new Rectangle(x, yb, w, h - Theme.S(6f));
				}
			}
			_rbR = (RbH > 0) ? new Rectangle(x, yb - Theme.S(22f) - RbH, w, RbH) : Rectangle.Empty;
			int upBottom = (RbH > 0) ? (_rbR.Y - Theme.S(10f)) : (yb - Theme.S(22f));
			_upR = (UpH > 0) ? new Rectangle(x, upBottom - UpH, w, UpH) : Rectangle.Empty;
		}

		private Item Sel()
		{
			foreach (Item item in Items)
			{
				if (item.Key == Selected)
				{
					return item;
				}
			}
			return null;
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			Calc();
			Item item = Sel();
			if (item != null)
			{
				_pillY.Snap(item.R.Y);
			}
			Invalidate();
		}

		private int Hit(Point p)
		{
			for (int i = 0; i < Items.Count; i++)
			{
				if (Items[i].R.Contains(p))
				{
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int num = Hit(e.Location);
			bool rh = !_rbR.IsEmpty && _rbR.Contains(e.Location);
			if (rh != _rbHot)
			{
				_rbHot = rh;
				Invalidate();
			}
			bool uh = !_upR.IsEmpty && _upR.Contains(e.Location);
			bool hh = _heartR.Contains(e.Location);
			if (uh != _upHot || hh != _heartHot)
			{
				_upHot = uh;
				_heartHot = hh;
				Invalidate();
			}
			bool ch = _credR.Contains(e.Location);
			if (ch != _credHot)
			{
				_credHot = ch;
				Invalidate();
			}
			if (num != _hot)
			{
				_hot = num;
				if (num >= 0)
				{
					_hotLast = num;
					_hotA.Snap(0f);
					_hotA.Target = 1f;
				}
				else
				{
					_hotA.Target = 0f;
				}
				Invalidate();
			}
			// one place for the cursor: the update banner and the heart never switched it to the hand before
			Cursor want = (num >= 0 || _credHot || _rbHot || _upHot || _heartHot) ? Cursors.Hand : Cursors.Default;
			if (Cursor != want)
			{
				Cursor = want;
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = -1;
			_credHot = false;
			_rbHot = false;
			_upHot = false;
			_heartHot = false;
			_hotA.Target = 0f;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (_credR.Contains(e.Location))
			{
				Shell.Open(Brand.GitHub);
				return;
			}
			if (_heartR.Contains(e.Location))
			{
				SupportClicked?.Invoke();
				return;
			}
			if (!_upR.IsEmpty && _upR.Contains(e.Location))
			{
				UpdateClicked?.Invoke();
				return;
			}
			if (!_rbR.IsEmpty && _rbR.Contains(e.Location))
			{
				RebootClicked?.Invoke(PointToScreen(new Point(_rbR.X, _rbR.Bottom + Theme.S(4f))));
				return;
			}
			int num = Hit(e.Location);
			if (num >= 0)
			{
				this.Navigate?.Invoke(Items[num].Key);
			}
			base.OnMouseDown(e);
		}

		public void Select(string key)
		{
			Selected = key;
			Calc();
			Item item = Sel();
			if (item != null)
			{
				if (!_placed)
				{
					_pillY.Snap(item.R.Y);
					_placed = true;
				}
				else
				{
					_pillY.Target = item.R.Y;
				}
			}
			Invalidate();
		}

		private void PaintUpdate(Graphics g)
		{
			Color tone = Theme.Ice;
			Rectangle r = _upR;
			using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
			{
				Theme.FillGradient(g, p, r, Theme.Mix(Color.FromArgb(34, 38, 46), tone, _upHot ? 0.18f : 0.11f), Theme.Mix(Color.FromArgb(26, 28, 34), tone, 0.04f), 0f);
				using (Pen pen = new Pen(Color.FromArgb(_upHot ? 170 : 100, tone)))
				{
					g.DrawPath(pen, p);
				}
			}
			int ic = Theme.S(32f);
			Rectangle ir = new Rectangle(r.X + Theme.S(12f), r.Y + (r.Height - ic) / 2, ic, ic);
			using (SolidBrush ib = new SolidBrush(Color.FromArgb(44, tone)))
			{
				g.FillEllipse(ib, ir);
			}
			// arrow down into a tray
			float cx = ir.X + ir.Width / 2f;
			float u = Theme.S(1f);
			using (Pen ap = new Pen(tone, Math.Max(1.6f, Theme.S(1.8f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				g.DrawLine(ap, cx, ir.Y + 9 * u, cx, ir.Y + 19 * u);
				g.DrawLines(ap, new PointF[] { new PointF(cx - 4.5f * u, ir.Y + 14.5f * u), new PointF(cx, ir.Y + 19 * u), new PointF(cx + 4.5f * u, ir.Y + 14.5f * u) });
				g.DrawLines(ap, new PointF[] { new PointF(ir.X + 9 * u, ir.Y + 20 * u), new PointF(ir.X + 9 * u, ir.Y + 23 * u), new PointF(ir.Right - 9 * u, ir.Y + 23 * u), new PointF(ir.Right - 9 * u, ir.Y + 20 * u) });
			}
			int tx = ir.Right + Theme.S(10f);
			int tw = r.Right - tx - Theme.S(8f);
			TextRenderer.DrawText(g, UpdateText, _fNavSel, new Rectangle(tx, r.Y + Theme.S(11f), tw, Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, UpdateSub ?? "", Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(32f), tw, Theme.S(18f)), Theme.Mix(tone, Theme.Sub, 0.35f), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		private void PaintReboot(Graphics g)
		{
			Color tone = RebootScheduled ? Theme.Silver1 : Theme.Yellow;
			Rectangle r = _rbR;
			using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
			{
				Theme.FillGradient(g, p, r, Theme.Mix(Color.FromArgb(40, 40, 46), tone, _rbHot ? 0.16f : 0.1f), Theme.Mix(Color.FromArgb(30, 30, 35), tone, 0.04f), 0f);
				using (Pen pen = new Pen(Color.FromArgb(_rbHot ? 150 : 90, tone)))
				{
					g.DrawPath(pen, p);
				}
			}
			int ic = Theme.S(32f);
			Rectangle ir = new Rectangle(r.X + Theme.S(12f), r.Y + (r.Height - ic) / 2, ic, ic);
			using (SolidBrush ib = new SolidBrush(Color.FromArgb(40, tone)))
			{
				g.FillEllipse(ib, ir);
			}
			// circular arrow
			RectangleF ar = RectangleF.Inflate(ir, -Theme.S(9f), -Theme.S(9f));
			using (Pen ap = new Pen(tone, Math.Max(1.6f, Theme.S(1.8f))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawArc(ap, ar, -60f, 300f);
				float cx = ar.X + ar.Width / 2f;
				float cy = ar.Y + ar.Height / 2f;
				double a = -60.0 * Math.PI / 180.0;
				PointF tip = new PointF(cx + (float)Math.Cos(a) * ar.Width / 2f, cy + (float)Math.Sin(a) * ar.Height / 2f);
				float k = Theme.S(3.5f);
				g.DrawLine(ap, tip.X, tip.Y, tip.X - k, tip.Y - k * 0.2f);
				g.DrawLine(ap, tip.X, tip.Y, tip.X + k * 0.1f, tip.Y + k);
			}
			int tx = ir.Right + Theme.S(10f);
			int tw = r.Right - tx - Theme.S(8f);
			TextRenderer.DrawText(g, RebootText, _fNavSel, new Rectangle(tx, r.Y + Theme.S(11f), tw, Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, RebootSub ?? "", Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(32f), tw, Theme.S(18f)), Theme.Mix(tone, Theme.Sub, 0.4f), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			Theme.Smooth(g);
			Calc();
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, -1, base.Width, base.Height + 2), Theme.Side1, Theme.Side2, 90f))
			{
				g.FillRectangle(brush, base.ClientRectangle);
			}
			using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(base.Width - 1, 0, 1, base.Height + 1), Color.FromArgb(56, 56, 64), Color.FromArgb(30, 30, 35), 90f))
			{
				g.FillRectangle(brush2, base.Width - 1, 0, 1, base.Height);
			}
			int lx = Theme.S(24f);
			int ly = Theme.S(32f);
			int ls = Theme.S(42f);
			if (Logo != null)
			{
				g.DrawImage(Logo, new Rectangle(lx, ly, ls, ls));
			}
			Theme.GradientText(g, Brand.Short, _fLogo, new PointF(lx + ls + Theme.S(11f), ly - Theme.S(6f)));
			TextRenderer.DrawText(g, "настройка Windows 11", Theme.Tiny, new Point(lx + ls + Theme.S(13f), ly + Theme.S(26f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			Item sel = Sel();
			int divY = -1;
			foreach (Item item in Items)
			{
				if (item.Bottom)
				{
					divY = item.R.Y - Theme.S(12f);
					break;
				}
			}
			if (!_rbR.IsEmpty)
			{
				PaintReboot(g);
				divY = _rbR.Y - Theme.S(12f);
			}
			if (!_upR.IsEmpty)
			{
				PaintUpdate(g);
				divY = _upR.Y - Theme.S(12f);
			}
			if (divY > 0)
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(Theme.S(14f), divY, base.Width - Theme.S(28f), 1), Color.FromArgb(70, 70, 80), Color.FromArgb(0, 70, 70, 80), 0f))
				{
					g.FillRectangle(lb, Theme.S(18f), divY, base.Width - Theme.S(36f), 1);
				}
			}
			if (sel != null)
			{
				Rectangle pr = new Rectangle(sel.R.X, (int)Math.Round(_pillY.Value), sel.R.Width, sel.R.Height);
				using (GraphicsPath path = Theme.Round(pr, Theme.S(10f)))
				{
					Theme.FillGradient(g, path, pr, Theme.Mix(Color.FromArgb(58, 60, 68), Theme.Ice, 0.07f), Color.FromArgb(40, 42, 49), 0f);
					using (Pen pen = new Pen(Theme.Mix(Color.FromArgb(76, 78, 88), Theme.Ice, 0.18f)))
					{
						g.DrawPath(pen, path);
					}
				}
				RectangleF bar = new RectangleF(pr.X + Theme.S(1f), (float)pr.Y + (float)pr.Height * 0.26f, Theme.S(3f), (float)pr.Height * 0.48f);
				using (GraphicsPath path2 = Theme.Round(bar, bar.Width / 2f))
				{
					Theme.FillGradient(g, path2, bar, Theme.IceHi, Theme.Ice);
				}
				// soft ice glow behind the accent bar
				using (GraphicsPath glow = new GraphicsPath())
				{
					RectangleF gr = new RectangleF(pr.X - Theme.S(6f), bar.Y - Theme.S(6f), Theme.S(18f), bar.Height + Theme.S(12f));
					glow.AddEllipse(gr);
					using (PathGradientBrush gb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(70, Theme.Ice), SurroundColors = new Color[] { Color.FromArgb(0, Theme.Ice) } })
					{
						g.FillPath(gb, glow);
					}
				}
			}
			if (_hotLast >= 0 && _hotLast < Items.Count && _hotA.Value > 0.01f && Items[_hotLast] != sel)
			{
				using (GraphicsPath path3 = Theme.Round(Items[_hotLast].R, Theme.S(10f)))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(20f * _hotA.Value), 255, 255, 255)))
					{
						g.FillPath(b, path3);
					}
				}
			}
			for (int i = 0; i < Items.Count; i++)
			{
				Item item2 = Items[i];
				bool isSel = item2 == sel;
				float hot = ((i == _hotLast) ? _hotA.Value : 0f);
				Color color = (isSel ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, hot));
				int nudge = (int)(hot * (float)Theme.S(2f));
				Rectangle ir = new Rectangle(item2.R.X + Theme.S(14f) + nudge, item2.R.Y, Theme.S(22f), item2.R.Height);
				Theme.DrawIcon(g, item2.Glyph, Theme.IconSmall, ir, isSel ? Theme.Silver1 : color);
				int right = item2.R.Right - Theme.S(12f);
				if (item2.Badge > 0)
				{
					string bt = (item2.Badge > 99) ? "99+" : item2.Badge.ToString();
					int bw = Math.Max(Theme.S(22f), Theme.Measure(bt, _fBadge).Width + Theme.S(12f));
					Rectangle br = new Rectangle(right - bw, item2.R.Y + (item2.R.Height - Theme.S(20f)) / 2, bw, Theme.S(20f));
					using (GraphicsPath bp = Theme.Round(br, (float)br.Height / 2f))
					{
						Theme.FillGradient(g, bp, br, isSel ? Theme.Silver1 : Color.FromArgb(78, 78, 90), isSel ? Theme.Silver2 : Color.FromArgb(58, 58, 68));
					}
					TextRenderer.DrawText(g, bt, _fBadge, br, isSel ? Theme.OnSilver : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					right = br.X - Theme.S(6f);
				}
				TextRenderer.DrawText(g, item2.Text, isSel ? _fNavSel : _fNav, new Rectangle(ir.Right + Theme.S(11f), item2.R.Y, Math.Max(1, right - ir.Right - Theme.S(11f)), item2.R.Height), color, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
			}
			// footer: version, GitHub link and a heart that opens "Support the project"
			int fy = base.Height - Theme.S(40f);
			string cred = "GitHub";
			Size vs = TextRenderer.MeasureText(g, Version, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			Size cs = TextRenderer.MeasureText(g, cred, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int fx = Theme.S(26f);
			TextRenderer.DrawText(g, Version, Theme.Tiny, new Point(fx, fy + Theme.S(5f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			_credR = new Rectangle(fx + vs.Width + Theme.S(8f), fy, cs.Width + Theme.S(32f), Theme.S(24f));
			using (GraphicsPath cp = Theme.Round(_credR, (float)_credR.Height / 2f))
			{
				using (SolidBrush cb = new SolidBrush(_credHot ? Color.FromArgb(46, Theme.Ice) : Color.FromArgb(22, 255, 255, 255)))
				{
					g.FillPath(cb, cp);
				}
			}
			Color tg = _credHot ? Theme.IceHi : Theme.Sub;
			// small "code" mark: </>
			float px = _credR.X + Theme.S(9f), py = _credR.Y + _credR.Height / 2f;
			float u = Theme.S(1f);
			using (Pen gp = new Pen(tg, Math.Max(1.2f, 1.4f * u)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				g.DrawLines(gp, new PointF[] { new PointF(px + 3 * u, py - 3.5f * u), new PointF(px, py), new PointF(px + 3 * u, py + 3.5f * u) });
				g.DrawLines(gp, new PointF[] { new PointF(px + 9 * u, py - 3.5f * u), new PointF(px + 12 * u, py), new PointF(px + 9 * u, py + 3.5f * u) });
				g.DrawLine(gp, px + 7.2f * u, py - 4.5f * u, px + 4.8f * u, py + 4.5f * u);
			}
			TextRenderer.DrawText(g, cred, Theme.Tiny, new Point(_credR.X + Theme.S(24f), _credR.Y + (_credR.Height - cs.Height) / 2), tg, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			// heart: support the project
			_heartR = new Rectangle(_credR.Right + Theme.S(6f), fy, Theme.S(30f), Theme.S(24f));
			using (GraphicsPath hp = Theme.Round(_heartR, (float)_heartR.Height / 2f))
			{
				using (SolidBrush hb = new SolidBrush(_heartHot ? Color.FromArgb(52, 236, 110, 140) : Color.FromArgb(22, 255, 255, 255)))
				{
					g.FillPath(hb, hp);
				}
			}
			Theme.Heart(g, new RectangleF(_heartR.X + (_heartR.Width - 12 * u) / 2f, _heartR.Y + (_heartR.Height - 11 * u) / 2f, 12 * u, 11 * u), _heartHot ? Color.FromArgb(246, 120, 150) : Theme.Sub);
		}
	}
}
