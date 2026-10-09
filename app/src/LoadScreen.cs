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
	// Full-window start screen: the app stays closed until the engine is up and every score is counted.
	// Logo, spinner, current step and a progress bar in the centre; nothing behind it can be clicked.
	internal class LoadScreen : Control
	{
		private readonly Timer _t = new Timer { Interval = 16 };

		private readonly DateTime _start = DateTime.Now;

		private double _last = Anim.Now;

		private float _ang;

		private float _p;

		private float _shown;

		private float _target;

		private string _step = "Запускаю движок…";

		private string _hint = "";

		private float _fade = 1f;

		private bool _closing;

		private Action _closed;

		public Image Logo;

		// "skip": appears once the home page is ready; the rest is read in the background
		public event Action Skip;

		private bool _canSkip;

		private float _skipA;

		private bool _skipHot;

		private Rectangle _skipR;

		public void AllowSkip()
		{
			if (_closing)
			{
				return;
			}
			_canSkip = true;
			if (!Anim.Enabled)
			{
				_skipA = 1f;
			}
			Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			bool hot = _canSkip && !_closing && _skipR.Contains(e.Location);
			if (hot != _skipHot)
			{
				_skipHot = hot;
				Cursor = hot ? Cursors.Hand : Cursors.AppStarting;
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			if (_skipHot)
			{
				_skipHot = false;
				Cursor = Cursors.AppStarting;
				Invalidate();
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (e.Button == MouseButtons.Left && _canSkip && !_closing && _skipR.Contains(e.Location))
			{
				_canSkip = false;
				_skipHot = false;
				Cursor = Cursors.Default;
				Skip?.Invoke();
			}
		}

		public LoadScreen()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
			BackColor = Theme.Bg;
			Dock = DockStyle.Fill;
			Cursor = Cursors.AppStarting;
			_t.Tick += delegate
			{
				Step();
			};
			_t.Start();
		}

		public void SetStep(string step, float p, string hint = null)
		{
			_step = step ?? "";
			_target = Math.Max(_target, Math.Min(1f, p));
			if (hint != null)
			{
				_hint = hint;
			}
			if (!Anim.Enabled)
			{
				_shown = _target;
			}
			Invalidate();
		}

		// fade away, then remove itself
		public void Finish(Action closed)
		{
			_target = 1f;
			_step = "Готово";
			_closing = true;
			_closed = closed;
			if (!Anim.Enabled)
			{
				Close();
			}
		}

		private void Close()
		{
			_t.Stop();
			Control p = Parent;
			p?.Controls.Remove(this);
			_closed?.Invoke();
			Dispose();
		}

		private void Step()
		{
			double now = Anim.Now;
			// capped: while the pages are being built a frame can come 0.5 s late, and the closing fade
			// then ended in one jump instead of fading
			float dt = Math.Min(40f, (float)(now - _last));
			_last = now;
			_ang = (_ang + dt * 0.4f) % 1800f;
			if (_canSkip && _skipA < 1f)
			{
				_skipA = Math.Min(1f, _skipA + dt / 300f);
			}
			_shown += (_target - _shown) * Math.Min(1f, dt / 180f);
			if (_closing && _shown > 0.985f)
			{
				_fade -= dt / 260f;
				if (_fade <= 0f)
				{
					Close();
					return;
				}
			}
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			Theme.Smooth(g);
			using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, -1, Width, Height + 2), Color.FromArgb(24, 24, 28), Theme.Bg, 90f))
			{
				g.FillRectangle(b, ClientRectangle);
			}
			int cx = Width / 2;
			int cy = Height / 2 - Theme.S(30f);
			// soft glow behind the logo
			using (GraphicsPath gp = new GraphicsPath())
			{
				int gr = Theme.S(220f);
				gp.AddEllipse(cx - gr, cy - gr, gr * 2, gr * 2);
				using (PathGradientBrush pg = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(26, 220, 220, 235), SurroundColors = new Color[1] { Color.FromArgb(0, 220, 220, 235) } })
				{
					g.FillPath(pg, gp);
				}
			}
			int a = (int)(255f * Math.Max(0f, Math.Min(1f, _fade)));
			int ls = Theme.S(50f);
			Rectangle lr = new Rectangle(cx - ls / 2, cy - ls - Theme.S(30f), ls, ls);
			if (Logo != null)
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(Logo, lr);
			}
			// spinner ring around the logo
			Rectangle sr = Rectangle.Inflate(lr, Theme.S(21f), Theme.S(21f));
			using (Pen tp = new Pen(Color.FromArgb(a / 8, 255, 255, 255), Theme.S(2f)))
			{
				g.DrawEllipse(tp, sr);
			}
			using (Pen ap = new Pen(Color.FromArgb(a, Theme.Silver1), Theme.S(2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				float sweep = 70f + 50f * (float)Math.Sin(_ang * Math.PI / 180.0);
				g.DrawArc(ap, sr, (_ang * 1.4f) % 360f, sweep + 40f);
			}
			int y = sr.Bottom + Theme.S(22f);
			TextFormatFlags c = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
			TextRenderer.DrawText(g, Brand.Name, Theme.H1, new Rectangle(0, y, Width, Theme.S(40f)), Color.FromArgb(a, Theme.Text), c);
			y += Theme.S(42f);
			TextRenderer.DrawText(g, _step, Theme.Body, new Rectangle(0, y, Width, Theme.S(22f)), Color.FromArgb(a, Theme.Sub), c);
			y += Theme.S(34f);
			int bw = Math.Min(Theme.S(360f), Width - Theme.S(80f));
			RectangleF br = new RectangleF(cx - bw / 2f, y, bw, Theme.S(4f));
			using (GraphicsPath tp = Theme.Round(br, Theme.S(2f)))
			{
				using (SolidBrush tb = new SolidBrush(Color.FromArgb(a / 9, 255, 255, 255)))
				{
					g.FillPath(tb, tp);
				}
			}
			float fw = Math.Max(Theme.S(4f), br.Width * Math.Max(0f, Math.Min(1f, _shown)));
			using (GraphicsPath fp = Theme.Round(new RectangleF(br.X, br.Y, fw, br.Height), Theme.S(2f)))
			{
				using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(br.X - 1f, br.Y, fw + 2f, br.Height), Color.FromArgb(a, Theme.Silver2), Color.FromArgb(a, Theme.Silver1), 0f))
				{
					g.FillPath(fb, fp);
				}
			}
			y += Theme.S(14f);
			string pct = (int)Math.Round(_shown * 100f) + "%";
			string left = string.IsNullOrEmpty(_hint) ? "" : _hint;
			TextRenderer.DrawText(g, left, Theme.Tiny, new Rectangle((int)br.X, y, bw - Theme.S(40f), Theme.S(18f)), Color.FromArgb(a, Theme.Dim), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, pct, Theme.Tiny, new Rectangle((int)br.X, y, bw, Theme.S(18f)), Color.FromArgb(a, Theme.Dim), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right | TextFormatFlags.NoPadding);
			// skip button (pill) + note under it
			y += Theme.S(46f);
			int sa = (int)(a * Math.Max(0f, Math.Min(1f, _skipA)));
			if (_canSkip || (_closing && _skipA > 0f))
			{
				string st = "Пропустить";
				int sw = Theme.Measure(st, Theme.Body).Width + Theme.S(44f);
				int sh = Theme.S(34f);
				_skipR = new Rectangle(cx - sw / 2, y, sw, sh);
				using (GraphicsPath pp = Theme.Round(_skipR, sh / 2))
				{
					using (SolidBrush pb = new SolidBrush(Color.FromArgb(sa * (_skipHot ? 34 : 18) / 255, 255, 255, 255)))
					{
						g.FillPath(pb, pp);
					}
					using (Pen pn = new Pen(Color.FromArgb(sa * (_skipHot ? 90 : 50) / 255, 255, 255, 255), 1f))
					{
						g.DrawPath(pn, pp);
					}
				}
				TextRenderer.DrawText(g, st, Theme.Body, _skipR, Color.FromArgb(sa, _skipHot ? Theme.Text : Theme.Sub), c);
				TextRenderer.DrawText(g, "остальные разделы дочитаются в фоне, пока ты работаешь", Theme.Tiny, new Rectangle(0, _skipR.Bottom + Theme.S(8f), Width, Theme.S(18f)), Color.FromArgb(sa * 3 / 4, Theme.Dim), c);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_t.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
