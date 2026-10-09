// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	// Overview card: big status icon (or a score ring), a headline, a row of stat tiles and stable action buttons.
	// Data comes from one engine call returning @{ Title; Sub; Tone; Score; Tiles = @(@{ L; V; H; T; P }) }.
	internal class StatusHero : Card
	{
		private class TileData
		{
			public string L = "";
			public string V = "…";
			public string H = "";
			public string T = "info";
			public float P = -1f;
			public Rectangle R;
		}

		private readonly Host _host;

		private readonly string _glyph;

		private readonly string _script;

		private readonly string _loadingTitle;

		private readonly bool _ring;

		private readonly TileData[] _tiles;

		public readonly List<FButton> Buttons = new List<FButton>();

		private string _title;

		private string _sub = "";

		private string _tone = "info";

		private float _score = -1f;

		private bool _loaded;

		private int _hot = -1;

		private Rectangle _iconR;

		private Rectangle _textR;

		private int _btnY;

		private readonly Tween _ringT;

		public event Action<int> TileClicked;

		public Hashtable Data;

		public StatusHero(Host host, string glyph, string script, string loadingTitle, string[] tileLabels, bool ring, params ActionDef[] actions)
		{
			_host = host;
			_glyph = glyph;
			_script = script;
			_loadingTitle = loadingTitle;
			_title = loadingTitle;
			_ring = ring;
			_ringT = new Tween(this, 0f, 0.1f).Timed(900);
			_tiles = new TileData[tileLabels.Length];
			for (int i = 0; i < tileLabels.Length; i++)
			{
				_tiles[i] = new TileData { L = tileLabels[i] };
			}
			foreach (ActionDef a in actions)
			{
				FButton b = new FButton { Text = a.Text, Kind = a.Kind, Glyph = a.Glyph, Font = Theme.Body };
				b.AutoWidth(Theme.S(32f));
				b.Tag = a;
				b.Click += delegate(object s, EventArgs e)
				{
					Run((ActionDef)((FButton)s).Tag, (FButton)s);
				};
				Buttons.Add(b);
				Controls.Add(b);
			}
		}

		private void Run(ActionDef a, FButton b)
		{
			if (a.Confirm != null && !WDialog.Ask(FindForm(), a.ConfirmTitle ?? _title, a.Confirm, a.ConfirmYes ?? "Продолжить", "Отмена", DlgIcon.Warn))
			{
				return;
			}
			if (a.Ui != null)
			{
				a.Ui();
				return;
			}
			if (!_host.TryBegin(title: a.Text))
			{
				return;
			}
			b.Enabled = false;
			_host.Worker.Run(delegate(IEngine e)
			{
				object r = (a.Work != null) ? a.Work(e) : null;
				return new object[2] { r, Fetch(e) };
			}, delegate(object o)
			{
				b.Enabled = true;
				object[] arr = (object[])o;
				Apply(arr[1]);
				a.Done?.Invoke(arr[0]);
				_host.End();
			});
		}

		public override object Fetch(IEngine e)
		{
			if (_script == null)
			{
				return null;
			}
			return e.Eval(_script);
		}

		private static string Str(object o, string d = "")
		{
			return (o == null || o.ToString() == "") ? d : o.ToString();
		}

		public override void Apply(object v)
		{
			Hashtable h = v as Hashtable;
			if (h == null)
			{
				return;
			}
			Data = h;
			_title = Str(h["Title"], _loadingTitle);
			_sub = Str(h["Sub"]);
			_tone = Str(h["Tone"], "info");
			try
			{
				_score = (h["Score"] != null) ? Convert.ToSingle(h["Score"]) : (-1f);
			}
			catch
			{
				_score = -1f;
			}
			if (_score >= 0f)
			{
				_ringT.Target = _score;
			}
			object[] arr = h["Tiles"] as object[];
			if (arr == null && h["Tiles"] is Hashtable)
			{
				arr = new object[1] { h["Tiles"] };
			}
			if (arr != null)
			{
				for (int i = 0; i < _tiles.Length && i < arr.Length; i++)
				{
					Hashtable t = arr[i] as Hashtable;
					if (t == null)
					{
						continue;
					}
					_tiles[i].L = Str(t["L"], _tiles[i].L);
					_tiles[i].V = Str(t["V"], "—");
					_tiles[i].H = Str(t["H"]);
					_tiles[i].T = Str(t["T"], "info");
					try
					{
						_tiles[i].P = (t["P"] != null) ? Convert.ToSingle(t["P"]) : (-1f);
					}
					catch
					{
						_tiles[i].P = -1f;
					}
				}
			}
			_loaded = true;
			Skeleton.Track(this, false);
			Invalidate();
		}

		private int Cols(int w)
		{
			int inner = w - Pad * 2;
			int gap = Theme.S(10f);
			int n = _tiles.Length;
			if (n == 0)
			{
				return 1;
			}
			int cols = n;
			while (cols > 1 && (inner - gap * (cols - 1)) / cols < Theme.S(150f))
			{
				cols = (cols > 3 && n % 2 == 0) ? (cols / 2) : (cols - 1);
			}
			return cols;
		}

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Pad + Theme.S(2f);
			int ic = Theme.S(64f);
			_iconR = new Rectangle(pad, pad, ic, ic);
			int tx = _iconR.Right + Theme.S(18f);
			_textR = new Rectangle(tx, pad, w - tx - pad, ic);
			int cy = _iconR.Bottom + Theme.S(16f);
			// buttons: a stable row under the headline
			if (Buttons.Count > 0)
			{
				int bx = pad;
				int bh = Theme.S(36f);
				foreach (FButton b in Buttons)
				{
					if (bx + b.Width > w - pad && bx > pad)
					{
						bx = pad;
						cy += bh + Theme.S(8f);
					}
					b.SetBounds(bx, cy, b.Width, bh);
					bx += b.Width + Theme.S(8f);
				}
				_btnY = cy;
				cy += bh + Theme.S(16f);
			}
			int gap = Theme.S(10f);
			int cols = Cols(w);
			int rows = (_tiles.Length + cols - 1) / Math.Max(1, cols);
			int tw = (w - pad * 2 - gap * (cols - 1)) / Math.Max(1, cols);
			int th = Theme.S(78f);
			for (int i = 0; i < _tiles.Length; i++)
			{
				int c = i % cols;
				int r = i / cols;
				int ww = (c == cols - 1) ? (w - pad * 2 - (tw + gap) * (cols - 1)) : tw;
				_tiles[i].R = new Rectangle(pad + c * (tw + gap), cy + r * (th + gap), ww, th);
			}
			if (_tiles.Length > 0)
			{
				cy += rows * (th + gap) - gap;
			}
			else
			{
				cy -= Theme.S(16f);
			}
			int h = cy + pad;
			SetBounds(x, y, w, h);
			Invalidate();
			return h;
		}

		private static Color ToneC(string t)
		{
			switch (t)
			{
			case "ok":
				return Theme.Green;
			case "warn":
				return Theme.Yellow;
			case "err":
				return Theme.Red;
			default:
				return Theme.Silver2;
			}
		}

		public override void PaintSurface(Graphics g)
		{
			base.PaintSurface(g);
			// soft glow in the tone color behind the icon
			if (_loaded)
			{
				Color c = ToneC(_tone);
				Rectangle gr = new Rectangle(-Theme.S(60f), -Theme.S(80f), Theme.S(360f), Theme.S(260f));
				using (GraphicsPath gp = new GraphicsPath())
				{
					gp.AddEllipse(gr);
					using (PathGradientBrush pb = new PathGradientBrush(gp))
					{
						pb.CenterColor = Color.FromArgb(26, c);
						pb.SurroundColors = new Color[1] { Color.FromArgb(0, c) };
						using (GraphicsPath clip = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.S(12f)))
						{
							Region old = g.Clip;
							g.SetClip(clip, CombineMode.Intersect);
							g.FillEllipse(pb, gr);
							g.Clip = old;
							old.Dispose();
						}
					}
				}
			}
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			Color tc = _loaded ? ToneC(_tone) : Theme.Silver2;
			if (_ring && _score >= 0f)
			{
				float th = Theme.S(6f);
				RectangleF ar = new RectangleF(_iconR.X + th / 2f, _iconR.Y + th / 2f, _iconR.Width - th, _iconR.Height - th);
				using (Pen bg = new Pen(Color.FromArgb(40, 255, 255, 255), th))
				{
					g.DrawEllipse(bg, ar);
				}
				float v = _ringT.Value;
				if (v > 0.005f)
				{
					using (LinearGradientBrush lb = new LinearGradientBrush(_iconR, Theme.Silver1, tc, 45f))
					{
						using (Pen fg = new Pen(lb, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
						{
							g.DrawArc(fg, ar, -90f, 360f * v);
						}
					}
				}
				using (Font pf = Theme.F(14f, semi: true))
				{
					TextRenderer.DrawText(g, ((int)Math.Round(v * 100f)).ToString(), pf, _iconR, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				}
			}
			else
			{
				using (GraphicsPath p = Theme.Round(_iconR, Theme.S(16f)))
				{
					Theme.FillGradient(g, p, _iconR, Theme.Mix(Color.FromArgb(62, 64, 74), tc, 0.2f), Color.FromArgb(30, 30, 36));
					using (Pen pen = new Pen(Color.FromArgb(70, tc)))
					{
						g.DrawPath(pen, p);
					}
				}
				Theme.DrawIcon(g, _glyph, Theme.IconBig, _iconR, tc);
			}
			using (Font tf = Theme.F(15f, semi: true))
			{
				int th2 = Theme.Measure("Ag", tf).Height;
				int ty = _textR.Y + Theme.S(6f);
				TextRenderer.DrawText(g, _title, tf, new Rectangle(_textR.X, ty, _textR.Width, th2 + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				TextRenderer.DrawText(g, _sub, Theme.Small, new Rectangle(_textR.X, ty + th2 + Theme.S(6f), _textR.Width, _textR.Bottom - ty - th2 - Theme.S(4f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			for (int i = 0; i < _tiles.Length; i++)
			{
				PaintTile(g, _tiles[i], i == _hot && TileClicked != null);
			}
		}

		private void PaintTile(Graphics g, TileData t, bool hot)
		{
			Rectangle r = t.R;
			using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(hot ? 26 : 14, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(hot ? 60 : 34, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			Color c = _loaded ? ToneC(t.T) : Theme.Dim;
			int bar = Theme.S(3f);
			using (GraphicsPath bp = Theme.Round(new RectangleF(r.X + Theme.S(14f), r.Y + Theme.S(16f), bar, r.Height - Theme.S(32f)), bar / 2f))
			{
				using (SolidBrush b2 = new SolidBrush(c))
				{
					g.FillPath(b2, bp);
				}
			}
			int tx = r.X + Theme.S(28f);
			int tw = r.Right - tx - Theme.S(10f);
			TextRenderer.DrawText(g, t.L, Theme.Tiny, new Rectangle(tx, r.Y + Theme.S(10f), tw, Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			using (Font vf = Theme.F(13f, semi: true))
			{
				if (_loaded)
				{
					TextRenderer.DrawText(g, t.V, vf, new Rectangle(tx, r.Y + Theme.S(27f), tw, Theme.S(24f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				}
				else
				{
					Skeleton.Bar(g, new RectangleF(tx, r.Y + Theme.S(31f), Math.Min(tw, Theme.S(90f)), Theme.S(14f)), this);
				}
			}
			if (t.P >= 0f && _loaded)
			{
				RectangleF br = new RectangleF(tx, r.Bottom - Theme.S(15f), tw - Theme.S(4f), Theme.S(4f));
				using (GraphicsPath tp = Theme.Round(br, br.Height / 2f))
				{
					using (SolidBrush tb = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
					{
						g.FillPath(tb, tp);
					}
				}
				float fw = Math.Max(br.Height, br.Width * Math.Min(1f, t.P));
				using (GraphicsPath fp = Theme.Round(new RectangleF(br.X, br.Y, fw, br.Height), br.Height / 2f))
				{
					using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(br.X - 1f, br.Y, fw + 2f, br.Height), Theme.Silver1, c, 0f))
					{
						g.FillPath(fb, fp);
					}
				}
			}
			else
			{
				TextRenderer.DrawText(g, _loaded ? t.H : "", Theme.Tiny, new Rectangle(tx, r.Bottom - Theme.S(23f), tw, Theme.S(16f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			if (hot)
			{
				using (Pen ap = new Pen(Theme.Sub, Math.Max(1.4f, Theme.S(1.5f))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				{
					float ax = r.Right - Theme.S(16f);
					float ay = r.Y + Theme.S(18f);
					float s = Theme.S(3.5f);
					g.DrawLines(ap, new PointF[3] { new PointF(ax - s / 2f, ay - s), new PointF(ax + s / 2f, ay), new PointF(ax - s / 2f, ay + s) });
				}
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			if (TileClicked != null)
			{
				for (int i = 0; i < _tiles.Length; i++)
				{
					if (_tiles[i].R.Contains(e.Location))
					{
						h = i;
					}
				}
			}
			if (h != _hot)
			{
				_hot = h;
				Cursor = (h >= 0) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (_hot != -1)
			{
				_hot = -1;
				Invalidate();
			}
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			if (_hot >= 0 && e.Button == MouseButtons.Left)
			{
				TileClicked?.Invoke(_hot);
			}
			base.OnMouseClick(e);
		}
	
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Skeleton.Track(this, !_loaded);
		}
	}
}
