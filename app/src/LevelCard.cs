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
	// One auto-tune level. The whole card is clickable; while the level runs it shows a live progress bar.
	internal class LevelCard : Card
	{
		public readonly string Key;

		public readonly string Title;

		public readonly string Tagline;

		public readonly string GlyphText;

		public readonly string[] Points;

		public readonly bool Recommended;

		// yellow badge for levels that trade comfort for speed
		public string Caution;

		public readonly FButton Btn;

		public bool Current;

		public event Action<LevelCard> Activated;

		private Rectangle _titleR;

		private Rectangle _tagR;

		private readonly List<Rectangle> _pointR = new List<Rectangle>();

		private int _iconBox;

		private bool _running;

		private float _phase;

		private string _step = "";

		private int _i;

		private int _n;

		private readonly Tween _prog;

		private readonly Tween _runT;

		private Timer _shimmer;

		public bool Running => _running;

		public LevelCard(string key, string glyph, string title, string tagline, string[] points, bool recommended)
		{
			Key = key;
			GlyphText = glyph;
			Title = title;
			Tagline = tagline;
			Points = points ?? new string[0];
			Recommended = recommended;
			HoverFx = true;
			Cursor = Cursors.Hand;
			_prog = new Tween(this, 0f, 0.15f).Timed(450);
			_runT = new Tween(this, 0f, 0.2f);
			Btn = new FButton
			{
				Text = "Настроить…",
				Kind = (recommended ? BtnKind.Silver : BtnKind.Ghost),
				Font = Theme.Body,
				Glyph = "\ue945"
			};
			Btn.Click += delegate
			{
				Fire();
			};
			base.Controls.Add(Btn);
		}

		private void Fire()
		{
			if (!_running)
			{
				Activated?.Invoke(this);
			}
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				Fire();
			}
			base.OnMouseClick(e);
		}

		public void StartProgress()
		{
			_running = true;
			_i = 0;
			_n = 0;
			_step = "Подготовка…";
			_prog.Snap(0f);
			_runT.Target = 1f;
			Btn.Visible = false;
			Cursor = Cursors.AppStarting;
			if (_shimmer == null)
			{
				_shimmer = new Timer
				{
					Interval = 33
				};
				_shimmer.Tick += delegate
				{
					_phase = (float)(Anim.Now % 1670.0 / 1670.0);
					Invalidate();
				};
			}
			if (Anim.Enabled)
			{
				_shimmer.Start();
			}
			Invalidate();
		}

		public void SetProgress(int i, int n, string step)
		{
			if (!_running)
			{
				return;
			}
			_i = i;
			_n = n;
			if (!string.IsNullOrEmpty(step))
			{
				_step = step;
			}
			_prog.Target = (n > 0) ? Math.Max(0.02f, (i - 0.5f) / n) : 0f;
			Invalidate();
		}

		public void EndProgress()
		{
			_running = false;
			_shimmer?.Stop();
			_runT.Snap(0f);
			Btn.Visible = true;
			Cursor = Cursors.Hand;
			Invalidate();
		}

		private Rectangle _meterR;

		private int _divY;

		private static readonly Font TitleBig = Theme.F(14f, semi: true);

		private int Depth => (Key == "light") ? 1 : ((Key == "gamer") ? 3 : 2);

		private string DepthText => (Depth == 1) ? "Бережно" : ((Depth == 3) ? "Максимум" : "Сбалансированно");

		public override int DoLayout(int x, int y, int w)
		{
			int pad = base.Pad;
			_iconBox = Theme.S(46f);
			int tw = Math.Max(Theme.S(60f), w - pad * 2);
			int top = pad + _iconBox + Theme.S(14f);
			int th = Theme.Measure(Title, TitleBig, tw, wrap: true).Height;
			_titleR = new Rectangle(pad, top, tw, th);
			int gh = Math.Max(Theme.Measure(Tagline, Theme.Small, tw, wrap: true).Height, Theme.Measure("A\nA", Theme.Small, tw, wrap: true).Height);
			_tagR = new Rectangle(pad, _titleR.Bottom + Theme.S(3f), tw, gh);
			top = _tagR.Bottom + Theme.S(18f);
			int px = pad + Theme.S(28f);
			int pw = Math.Max(Theme.S(60f), w - px - pad);
			_pointR.Clear();
			foreach (string p in Points)
			{
				int ph = Theme.Measure(p, Theme.Small, pw, wrap: true).Height;
				_pointR.Add(new Rectangle(px, top, pw, ph));
				top += Math.Max(ph, Theme.S(18f)) + Theme.S(10f);
			}
			_divY = top + Theme.S(4f);
			_meterR = new Rectangle(pad, _divY + Theme.S(14f), tw, Theme.S(16f));
			int bh = Theme.S(40f);
			int h = _meterR.Bottom + Theme.S(16f) + bh + pad;
			SetBounds(x, y, w, h);
			PlaceButton();
			Invalidate();
			return h;
		}

		private void PlaceButton()
		{
			int pad = base.Pad;
			int bh = Theme.S(40f);
			Btn.SetBounds(pad, base.Height - pad - bh, Math.Max(Theme.S(80f), base.Width - pad * 2), bh);
			// footer is pinned to the bottom so the meters line up across cards of the same row
			_meterR = new Rectangle(pad, base.Height - pad - bh - Theme.S(16f) - Theme.S(16f), Math.Max(Theme.S(60f), base.Width - pad * 2), Theme.S(16f));
			_divY = _meterR.Y - Theme.S(14f);
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			PlaceButton();
		}

		public override object Fetch(IEngine e)
		{
			return e.Str(e.Eval("Get-FLevelId"));
		}

		public override void Apply(object v)
		{
			bool cur = string.Equals(v as string, Key, StringComparison.OrdinalIgnoreCase);
			if (cur != Current)
			{
				Current = cur;
				Invalidate();
			}
		}

		private Color Accent => Current && !_running ? Theme.Green : Theme.Silver1;

		public override void PaintSurface(Graphics g)
		{
			base.PaintSurface(g);
			float hot = HotT.Value;
			Theme.Smooth(g);
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
			using (GraphicsPath path = Theme.Round(r, Theme.S(12f)))
			{
				// glow at the top: stronger for the recommended level, tinted green for the applied one
				float glow = (Recommended ? 0.75f : 0.3f) + 0.5f * hot + (_running ? 0.4f : 0f);
				Color tone = Current && !_running ? Theme.Green : Color.FromArgb(220, 222, 232);
				RectangleF gr = new RectangleF(r.X, r.Y, r.Width, Theme.S(130f));
				using (LinearGradientBrush gb = new LinearGradientBrush(new RectangleF(gr.X, gr.Y - 1f, gr.Width, gr.Height + 2f), Color.FromArgb((int)Math.Min(60f, 26f * glow), tone), Color.FromArgb(0, tone), 90f))
				{
					Region old = g.Clip;
					g.SetClip(path, CombineMode.Intersect);
					g.FillRectangle(gb, gr);
					g.Clip = old;
					old.Dispose();
				}
				int a1 = (int)Math.Min(255f, (Recommended ? 150 : 50) + 80f * hot + (_running ? 60 : 0) + (Current ? 40 : 0));
				using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1f, r.Width, r.Height + 2f), Color.FromArgb(a1, tone), Color.FromArgb(24, tone), 90f))
				{
					using (Pen pen = new Pen(brush, 1f))
					{
						g.DrawPath(pen, path);
					}
				}
			}
		}

		private static void Check(Graphics g, Rectangle c, Color col, bool plus)
		{
			using (SolidBrush b = new SolidBrush(Color.FromArgb(plus ? 22 : 34, col)))
			{
				g.FillEllipse(b, c);
			}
			using (Pen pen = new Pen(Color.FromArgb(plus ? 80 : 120, col)))
			{
				g.DrawEllipse(pen, c);
			}
			using (Pen ck = new Pen(col, Math.Max(1.4f, Theme.S(1.6f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
			{
				if (plus)
				{
					float k = c.Width * 0.24f;
					PointF m = new PointF(c.X + c.Width / 2f, c.Y + c.Height / 2f);
					g.DrawLine(ck, m.X - k, m.Y, m.X + k, m.Y);
					g.DrawLine(ck, m.X, m.Y - k, m.X, m.Y + k);
				}
				else
				{
					g.DrawLines(ck, new PointF[3]
					{
						new PointF(c.X + c.Width * 0.28f, c.Y + c.Height * 0.53f),
						new PointF(c.X + c.Width * 0.44f, c.Y + c.Height * 0.68f),
						new PointF(c.X + c.Width * 0.73f, c.Y + c.Height * 0.35f)
					});
				}
			}
		}

		protected override void PaintContent(Graphics g)
		{
			int pad = base.Pad;
			float hot = HotT.Value;
			Theme.Smooth(g);
			Rectangle icon = new Rectangle(pad, pad - (int)(hot * Theme.S(2f)), _iconBox, _iconBox);
			Theme.IconBox(g, icon, GlyphText, Recommended || Current, hot);
			string badge = (_running ? "Идёт настройка" : (Current ? "Применена" : (Recommended ? "Рекомендуем" : Caution)));
			if (badge != null)
			{
				int bw = Theme.PillW(badge, Theme.Tiny);
				Rectangle br = new Rectangle(base.Width - pad - bw, pad + (_iconBox - Theme.S(22f)) / 2, bw, Theme.S(22f));
				Theme.Pill(g, br, badge, Theme.Tiny, (badge == Caution && !Current && !_running) ? Theme.Yellow : Accent);
			}
			TextRenderer.DrawText(g, Title, TitleBig, _titleR, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			TextRenderer.DrawText(g, Tagline, Theme.Small, _tagR, Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			// footer: divider + depth meter in one row ("Глубина  Сбалансированно" ●●○)
			using (Pen dp = new Pen(Color.FromArgb(26, 255, 255, 255)))
			{
				g.DrawLine(dp, pad, _divY, base.Width - pad, _divY);
			}
			int gap = Theme.S(4f);
			float segW = Theme.S(22f);
			float segX = _meterR.Right - (segW * 3 + gap * 2);
			string dl = "Глубина";
			int dlw = Theme.Measure(dl, Theme.Tiny).Width + Theme.S(6f);
			TextRenderer.DrawText(g, dl, Theme.Tiny, new Rectangle(_meterR.X, _meterR.Y, dlw + Theme.S(4f), _meterR.Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, DepthText, Theme.Tiny, new Rectangle(_meterR.X + dlw, _meterR.Y, Math.Max(10, (int)segX - _meterR.X - dlw - Theme.S(8f)), _meterR.Height), Theme.Mix(Theme.Sub, Theme.Text, hot), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
			for (int i = 0; i < 3; i++)
			{
				RectangleF sr = new RectangleF(segX + i * (segW + gap), _meterR.Y + (_meterR.Height - Theme.S(6f)) / 2f, segW, Theme.S(6f));
				using (GraphicsPath sp = Theme.Round(sr, Theme.S(3f)))
				{
					if (i < Depth)
					{
						Color c1 = Current && !_running ? Theme.Mix(Theme.Green, Color.White, 0.15f) : Theme.Mix(Theme.Silver2, Theme.Silver1, 0.4f + 0.6f * hot);
						Theme.FillGradient(g, sp, Rectangle.Round(sr), c1, Theme.Mix(c1, Color.Black, 0.25f));
					}
					else
					{
						using (SolidBrush eb = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
						{
							g.FillPath(eb, sp);
						}
					}
				}
			}
			int lh = Theme.Measure("Ag", Theme.Small).Height;
			for (int i = 0; i < _pointR.Count && i < Points.Length; i++)
			{
				Rectangle pr = _pointR[i];
				bool inherit = i == 0 && Key != "light";
				int d = Theme.S(18f);
				Rectangle c = new Rectangle(pad, pr.Top + (lh - d) / 2, d, d);
				Check(g, c, inherit ? Theme.Sub : (Current && !_running ? Theme.Green : Theme.Mix(Theme.Silver2, Theme.Silver1, 0.5f + 0.5f * hot)), inherit);
				TextRenderer.DrawText(g, Points[i], Theme.Small, pr, inherit ? Theme.Sub : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			}
			if (_running)
			{
				int bh = Theme.S(40f);
				Rectangle area = new Rectangle(pad, base.Height - pad - bh, base.Width - pad * 2, bh);
				string head = (_n > 0) ? ("Шаг " + _i + " из " + _n) : "Запуск…";
				string pct = (_n > 0) ? ((int)Math.Round(_prog.Value * 100f) + "%") : "";
				TextRenderer.DrawText(g, head, Theme.Tiny, new Rectangle(area.X, area.Y, area.Width, Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				TextRenderer.DrawText(g, pct, Theme.Tiny, new Rectangle(area.X, area.Y, area.Width, Theme.S(16f)), Theme.Silver1, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right);
				Theme.Progress(g, new RectangleF(area.X, area.Y + Theme.S(19f), area.Width, Theme.S(6f)), _prog.Value, _phase);
				TextRenderer.DrawText(g, _step, Theme.Tiny, new Rectangle(area.X, area.Y + Theme.S(27f), area.Width, Theme.S(16f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
		}
	}
}
