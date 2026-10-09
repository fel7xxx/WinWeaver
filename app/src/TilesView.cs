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
	// Grid of painted one-click tiles: icon, title, two lines of text, a hint and a pill button with live state
	internal class TilesView : Control, IBlock
	{
		public class Tile
		{
			public string Glyph;
			public string Title;
			public string Desc;
			public string Btn = "Исправить";
			public string Hint;
			public string Confirm;
			public string ConfirmTitle;
			public Func<IEngine, object> Work;
			public Action Ui;
			public Action<object> Done;
			public bool Danger;
			public Rectangle R;
			public Rectangle BtnR;
			public int State; // 0 idle, 1 running, 2 done, 3 failed
			public DateTime DoneAt;
		}

		private readonly Host _host;

		public readonly List<Tile> Items = new List<Tile>();

		public int MinColW = 300;

		public int TileH = 138;

		private int _hot = -1;

		private bool _hotBtn;

		private readonly Timer _spin;

		private float _phase;

		private readonly Tween _hotT;

		public TilesView(Host host)
		{
			_host = host;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			_hotT = new Tween(this, 0f, 0.25f);
			_spin = new Timer { Interval = 33 };
			_spin.Tick += delegate
			{
				_phase += 0.025f;
				if (_phase > 1f)
				{
					_phase -= 1f;
				}
				bool running = false;
				bool waiting = false;
				foreach (Tile t in Items)
				{
					if (t.State == 1)
					{
						running = true;
					}
					else if (t.State >= 2)
					{
						if ((DateTime.Now - t.DoneAt).TotalSeconds > 6.0)
						{
							t.State = 0;
							Invalidate();
						}
						else
						{
							waiting = true;
						}
					}
				}
				if (running)
				{
					Invalidate();
				}
				else if (!waiting)
				{
					_spin.Stop();
				}
			};
		}

		public Tile Add(string glyph, string title, string desc, string hint, Func<IEngine, object> work, string btn = "Исправить")
		{
			Tile t = new Tile { Glyph = glyph, Title = title, Desc = desc, Hint = hint, Work = work, Btn = btn };
			Items.Add(t);
			return t;
		}

		public Tile AddUi(string glyph, string title, string desc, string hint, Action ui, string btn = "Открыть")
		{
			Tile t = new Tile { Glyph = glyph, Title = title, Desc = desc, Hint = hint, Ui = ui, Btn = btn };
			Items.Add(t);
			return t;
		}

		public int DoLayout(int x, int y, int w)
		{
			int gap = Theme.S(14f);
			int cols = Math.Max(1, Math.Min(Items.Count, (w + gap) / (Theme.S(MinColW) + gap)));
			int cw = (w - gap * (cols - 1)) / cols;
			int th = Theme.S(TileH);
			for (int i = 0; i < Items.Count; i++)
			{
				int c = i % cols;
				int r = i / cols;
				Items[i].R = new Rectangle(c * (cw + gap), r * (th + gap), (c == cols - 1) ? (w - (cw + gap) * (cols - 1)) : cw, th);
			}
			int rows = (Items.Count + cols - 1) / cols;
			int h = Math.Max(1, rows * (th + gap) - gap);
			SetBounds(x, y, w, h);
			Invalidate();
			return h;
		}

		public object Fetch(IEngine e)
		{
			return null;
		}

		public void Apply(object v)
		{
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			Theme.Smooth(g);
			for (int i = 0; i < Items.Count; i++)
			{
				PaintTile(g, Items[i], i == _hot);
			}
		}

		private void PaintTile(Graphics g, Tile t, bool hot)
		{
			Rectangle r = t.R;
			float hv = hot ? _hotT.Value : 0f;
			Theme.Card(g, r, hv);
			int pad = Theme.S(18f);
			int ic = Theme.S(38f);
			Rectangle ir = new Rectangle(r.X + pad, r.Y + pad, ic, ic);
			Color accent = (t.State == 2) ? Theme.Green : ((t.State == 3) ? Theme.Red : Theme.Silver1);
			Theme.IconBox(g, ir, t.Glyph, bright: false, hv, Theme.IconSmall);
			int tx = ir.Right + Theme.S(12f);
			int tw = r.Right - pad - tx;
			TextRenderer.DrawText(g, t.Title, Theme.Title, new Rectangle(tx, ir.Y - Theme.S(1f), tw, Theme.S(22f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(g, t.Desc ?? "", Theme.Small, new Rectangle(tx, ir.Y + Theme.S(21f), tw, Theme.S(36f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
			string bt = (t.State == 1) ? "Работаю…" : ((t.State == 2) ? "Готово" : ((t.State == 3) ? "Не вышло" : t.Btn));
			int bw = Math.Max(Theme.S(96f), Theme.Measure(bt, Theme.Small).Width + Theme.S(34f));
			int bh = Theme.S(32f);
			t.BtnR = new Rectangle(r.Right - pad - bw, r.Bottom - pad - bh + Theme.S(4f), bw, bh);
			bool bhot = hot && _hotBtn && t.State == 0;
			using (GraphicsPath bp = Theme.Round(t.BtnR, bh / 2f))
			{
				if (t.State == 1)
				{
					using (SolidBrush b0 = new SolidBrush(Theme.Control2))
					{
						g.FillPath(b0, bp);
					}
					Region old = g.Clip;
					g.SetClip(bp, CombineMode.Intersect);
					float sw = bw * 0.45f;
					float sx = t.BtnR.X - sw + (bw + sw) * _phase;
					using (LinearGradientBrush sb = new LinearGradientBrush(new RectangleF(sx - 1f, t.BtnR.Y, sw + 2f, bh), Color.Transparent, Color.Transparent, 0f))
					{
						ColorBlend cb = new ColorBlend();
						cb.Positions = new float[3] { 0f, 0.5f, 1f };
						cb.Colors = new Color[3] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(50, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
						sb.InterpolationColors = cb;
						g.FillRectangle(sb, sx, t.BtnR.Y, sw, bh);
					}
					g.Clip = old;
					using (Pen p0 = new Pen(Theme.BorderHi))
					{
						g.DrawPath(p0, bp);
					}
				}
				else if (t.State >= 2)
				{
					using (SolidBrush b2 = new SolidBrush(Color.FromArgb(30, accent)))
					{
						g.FillPath(b2, bp);
					}
					using (Pen p2 = new Pen(Color.FromArgb(120, accent)))
					{
						g.DrawPath(p2, bp);
					}
				}
				else
				{
					Color c1 = t.Danger ? Color.FromArgb(70, 44, 46) : (bhot ? Theme.Mix(Theme.Control1, Color.White, 0.14f) : Theme.Control1);
					Color c2 = t.Danger ? Color.FromArgb(52, 34, 36) : Theme.Control2;
					Theme.FillGradient(g, bp, t.BtnR, c1, c2);
					using (Pen p3 = new Pen(bhot ? Theme.BorderHi : Theme.Border))
					{
						g.DrawPath(p3, bp);
					}
				}
			}
			Color btc = (t.State >= 2) ? accent : ((t.State == 1) ? Theme.Sub : (t.Danger ? Theme.Red : Theme.Text));
			TextRenderer.DrawText(g, bt, Theme.Small, t.BtnR, btc, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
			if (!string.IsNullOrEmpty(t.Hint))
			{
				int hx = r.X + pad;
				int hw = t.BtnR.Left - Theme.S(10f) - hx;
				int d = Theme.S(5f);
				int cy = t.BtnR.Y + bh / 2;
				using (SolidBrush db = new SolidBrush(Theme.Dim))
				{
					g.FillEllipse(db, hx, cy - d / 2, d, d);
				}
				TextRenderer.DrawText(g, t.Hint, Theme.Tiny, new Rectangle(hx + d + Theme.S(7f), cy - Theme.S(9f), Math.Max(10, hw - d - Theme.S(7f)), Theme.S(18f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
		}

		private void RunTile(Tile t)
		{
			if (t.State == 1)
			{
				return;
			}
			if (t.Confirm != null && !WDialog.Ask(FindForm(), t.ConfirmTitle ?? (t.Title + "?"), t.Confirm, t.Btn, "Отмена", DlgIcon.Warn))
			{
				return;
			}
			if (t.Ui != null)
			{
				t.Ui();
				return;
			}
			if (t.Work == null || !_host.TryBegin(title: t.Title))
			{
				return;
			}
			t.State = 1;
			_spin.Start();
			Invalidate();
			_host.Worker.Run((IEngine e) => t.Work(e), delegate(object r)
			{
				t.State = (r is bool && !(bool)r) ? 3 : 2;
				t.DoneAt = DateTime.Now;
				_spin.Start();
				Invalidate();
				_host.End();
				t.Done?.Invoke(r);
			});
		}

		private int Hit(Point p, out bool onBtn)
		{
			onBtn = false;
			for (int i = 0; i < Items.Count; i++)
			{
				if (Items[i].R.Contains(p))
				{
					onBtn = Items[i].BtnR.Contains(p);
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			bool ob;
			int h = Hit(e.Location, out ob);
			if (h != _hot || ob != _hotBtn)
			{
				if (h != _hot)
				{
					_hotT.Snap(0f);
					_hotT.Target = 1f;
				}
				_hot = h;
				_hotBtn = ob;
				Cursor = (h >= 0) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hot = -1;
			_hotBtn = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			bool ob;
			int h = Hit(e.Location, out ob);
			if (h >= 0 && e.Button == MouseButtons.Left)
			{
				RunTile(Items[h]);
			}
			base.OnMouseClick(e);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_spin.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}