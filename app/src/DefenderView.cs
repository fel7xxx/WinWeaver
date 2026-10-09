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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	// Microsoft Defender: state header, 3-way mode switch, live status tiles and quick tools
	internal class DefenderView : Card
	{
		private static readonly string[] ModeNames = { "Включён", "Без реального времени", "Выключен полностью" };

		private static readonly string[] ModeHints = { "полная защита Windows", "сканирует по запросу, не мешает играм", "службы остановлены, без антивируса" };

		private bool _loaded;

		private int _mode;

		private bool _rtp, _tamper, _cloud, _pua;

		private int _sigAge = -1, _scanAge = -1;

		private string _note = "", _other = "";

		private Rectangle _headR;

		private readonly Rectangle[] _modeR = new Rectangle[3];

		private readonly Rectangle[] _tileR = new Rectangle[4];

		private Rectangle _noteR;

		private int _hot = -1;

		private readonly FButton _sig, _scan, _settings;

		public event Action<int> ModeClicked;

		public event Action<string> ToolClicked;

		public DefenderView()
		{
			_sig = Btn("Обновить базы", "sig", Glyph.Sync);
			_scan = Btn("Быстрая проверка", "scan", Glyph.Search);
			_settings = Btn("Настройки защиты", "settings", null);
		}

		private FButton Btn(string text, string key, string glyph)
		{
			FButton b = new FButton { Text = text, Kind = BtnKind.Ghost, Font = Theme.Body, Glyph = glyph };
			b.AutoWidth(Theme.S(30f));
			b.Click += delegate
			{
				ToolClicked?.Invoke(key);
			};
			Controls.Add(b);
			return b;
		}

		public override object Fetch(IEngine e)
		{
			return e.Eval("Get-FDefInfo");
		}

		private static int Int(object o, int d)
		{
			try
			{
				return (o == null) ? d : Convert.ToInt32(o);
			}
			catch
			{
				return d;
			}
		}

		public override void Apply(object v)
		{
			Hashtable h = v as Hashtable;
			if (h == null)
			{
				return;
			}
			int oldCount = ModeCount;
			_mode = Math.Max(0, Math.Min(2, Int(h["Mode"], 0)));
			_rtp = h["Rtp"] is bool && (bool)h["Rtp"];
			_tamper = h["Tamper"] is bool && (bool)h["Tamper"];
			_cloud = h["Cloud"] is bool && (bool)h["Cloud"];
			_pua = h["Pua"] is bool && (bool)h["Pua"];
			_sigAge = Int(h["SigAge"], -1);
			_scanAge = Int(h["ScanAge"], -1);
			_note = (h["Note"] ?? "").ToString();
			_other = (h["Other"] ?? "").ToString();
			bool hadNote = _noteR.Height > 0;
			_loaded = true;
			_sig.Enabled = _mode != 2;
			_scan.Enabled = _mode != 2;
			Skeleton.Track(this, false);
			if ((hadNote != (Note() != "") || oldCount != ModeCount) && Parent is Canvas)
			{
				((Canvas)Parent).Page.RelayoutSoon();
			}
			Invalidate();
		}

		private string Note()
		{
			if (!_loaded)
			{
				return "";
			}
			if (_note != "")
			{
				return _note;
			}
			if (_other != "")
			{
				return "Основной антивирус — " + _other + ". Defender работает в пассивном режиме.";
			}
			if (_tamper && _mode == 0)
			{
				return "Чтобы выключить Defender, сначала отключи «Защиту от подделки» — кнопка «Настройки защиты».";
			}
			return "";
		}

		// "turn off completely" is shown only in expert mode (or when Defender is already off)
		private int ModeCount => (Prefs.Expert || _mode == 2) ? 3 : 2;

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Pad;
			_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(48f));
			int cy = _headR.Bottom + Theme.S(16f);
			int g = Theme.S(8f);
			int nm = ModeCount;
			int mw = (w - pad * 2 - g * (nm - 1)) / nm;
			for (int i = 0; i < 3; i++)
			{
				_modeR[i] = (i < nm) ? new Rectangle(pad + i * (mw + g), cy, (i == nm - 1) ? (w - pad * 2 - (mw + g) * (nm - 1)) : mw, Theme.S(54f)) : Rectangle.Empty;
			}
			cy += Theme.S(54f) + Theme.S(12f);
			int tw = (w - pad * 2 - g * 3) / 4;
			for (int i = 0; i < 4; i++)
			{
				_tileR[i] = new Rectangle(pad + i * (tw + g), cy, tw, Theme.S(58f));
			}
			cy += Theme.S(58f) + Theme.S(12f);
			if (Note() != "")
			{
				_noteR = new Rectangle(pad, cy, w - pad * 2, Theme.S(20f));
				cy += Theme.S(20f) + Theme.S(10f);
			}
			else
			{
				_noteR = Rectangle.Empty;
			}
			_sig.Location = new Point(pad, cy);
			_scan.Location = new Point(_sig.Right + g, cy);
			_settings.Location = new Point(_scan.Right + g, cy);
			cy += _sig.Height;
			int h = cy + pad;
			SetBounds(x, y, w, h);
			return h;
		}

		private static string Days(int d, string never)
		{
			if (d < 0 || d > 3650)
			{
				return never;
			}
			if (d == 0)
			{
				return "сегодня";
			}
			if (d == 1)
			{
				return "вчера";
			}
			int m10 = d % 10, m100 = d % 100;
			string w = (m10 == 1 && m100 != 11) ? "день" : ((m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) ? "дня" : "дней");
			return d + " " + w + " назад";
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			Color tone = !_loaded ? Theme.Sub : (_mode == 0 ? Theme.Green : (_mode == 1 ? Theme.Yellow : Theme.Red));
			int ic = Theme.S(44f);
			Rectangle ir = new Rectangle(_headR.X, _headR.Y + (_headR.Height - ic) / 2, ic, ic);
			using (GraphicsPath p = Theme.Round(ir, ic * 0.27f))
			{
				Theme.FillGradient(g, p, ir, Theme.Mix(Color.FromArgb(58, 58, 67), tone, 0.22f), Theme.Mix(Color.FromArgb(40, 40, 47), tone, 0.08f));
				using (Pen pen = new Pen(Color.FromArgb(110, tone)))
				{
					g.DrawPath(pen, p);
				}
			}
			Theme.DrawIcon(g, Glyph.Shield, Theme.Icon, ir, Theme.Mix(tone, Color.White, 0.3f));
			int tx = ir.Right + Theme.S(14f);
			int th = Theme.Measure("Ag", Theme.Title).Height;
			int ty = _headR.Y + (_headR.Height - th - Theme.S(22f)) / 2;
			TextRenderer.DrawText(g, "Microsoft Defender", Theme.Title, new Point(tx, ty), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			string sum = !_loaded ? "Проверяю защиту…" : (_mode == 0 ? "Компьютер защищён • базы обновлены " + Days(_sigAge, "давно") : (_mode == 1 ? "Защита в реальном времени выключена — файлы не проверяются при открытии" : "Defender выключен полностью — компьютер без антивируса"));
			string pill = !_loaded ? "" : (_mode == 0 ? "Защищает" : (_mode == 1 ? "Частично" : "Выключен"));
			int pw = pill == "" ? 0 : Theme.PillW(pill, Theme.Tiny);
			TextRenderer.DrawText(g, sum, Theme.Small, new Rectangle(tx, ty + th + Theme.S(4f), _headR.Right - tx - pw - Theme.S(12f), Theme.S(20f)), _mode == 0 || !_loaded ? Theme.Sub : tone, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (pw > 0)
			{
				Theme.Pill(g, new Rectangle(_headR.Right - pw, _headR.Y + (_headR.Height - Theme.S(22f)) / 2, pw, Theme.S(22f)), pill, Theme.Tiny, tone, 26);
			}
			if (!_loaded)
			{
				Skeleton.Row(g, new Rectangle(_modeR[0].X, _modeR[0].Y, _modeR[ModeCount - 1].Right - _modeR[0].X, _modeR[0].Height), this);
				Skeleton.Row(g, new Rectangle(_tileR[0].X, _tileR[0].Y, _tileR[3].Right - _tileR[0].X, _tileR[0].Height), this);
				return;
			}
			// mode switch
			for (int i = 0; i < ModeCount; i++)
			{
				Rectangle r = _modeR[i];
				bool sel = i == _mode;
				bool hot = i == _hot && !sel;
				using (GraphicsPath p = Theme.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.S(12f)))
				{
					if (sel)
					{
						Theme.FillGradient(g, p, r, Theme.Silver1, Theme.Silver2);
					}
					else
					{
						Theme.FillGradient(g, p, r, hot ? Theme.CardHot1 : Theme.Card1, hot ? Theme.CardHot2 : Theme.Card2);
						using (Pen pen = new Pen(hot ? Theme.BorderHi : Theme.Border))
						{
							g.DrawPath(pen, p);
						}
					}
				}
				Color dot = i == 0 ? Theme.Green : (i == 1 ? Theme.Yellow : Theme.Red);
				int d = Theme.S(8f);
				using (SolidBrush b = new SolidBrush(sel ? Theme.Mix(dot, Color.Black, 0.25f) : dot))
				{
					g.FillEllipse(b, r.X + Theme.S(14f), r.Y + Theme.S(15f), d, d);
				}
				int x0 = r.X + Theme.S(30f);
				TextRenderer.DrawText(g, ModeNames[i], Theme.Body, new Rectangle(x0, r.Y + Theme.S(8f), r.Right - x0 - Theme.S(10f), Theme.S(22f)), sel ? Theme.OnSilver : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				TextRenderer.DrawText(g, ModeHints[i], Theme.Tiny, new Rectangle(x0, r.Y + Theme.S(30f), r.Right - x0 - Theme.S(10f), Theme.S(18f)), sel ? Color.FromArgb(80, 80, 90) : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			// status tiles
			Tile(g, _tileR[0], "Реальное время", _rtp ? "Включено" : "Выключено", _rtp);
			Tile(g, _tileR[1], "Защита от подделки", _tamper ? "Включена" : "Выключена", _tamper);
			Tile(g, _tileR[2], "Облачная защита", _cloud ? "Включена" : "Выключена", _cloud);
			Tile(g, _tileR[3], "Последняя проверка", Days(_scanAge, "не проводилась"), _scanAge >= 0 && _scanAge <= 7);
			string note = Note();
			if (note != "" && _noteR.Height > 0)
			{
				Rectangle hi = new Rectangle(_noteR.X, _noteR.Y + Theme.S(1f), Theme.S(18f), Theme.S(18f));
				if (Theme.IconSmall != null)
				{
					Theme.DrawIcon(g, Glyph.Info, Theme.IconSmall, hi, Theme.Yellow);
				}
				TextRenderer.DrawText(g, note, Theme.Small, new Rectangle(hi.Right + Theme.S(8f), _noteR.Y, _noteR.Width - Theme.S(26f), _noteR.Height), Theme.Yellow, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
		}

		private void Tile(Graphics g, Rectangle r, string name, string val, bool good)
		{
			using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			int x0 = r.X + Theme.S(14f);
			TextRenderer.DrawText(g, name, Theme.Tiny, new Rectangle(x0, r.Y + Theme.S(10f), r.Width - Theme.S(24f), Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			int d = Theme.S(7f);
			int vy = r.Y + Theme.S(30f);
			using (SolidBrush b = new SolidBrush(good ? Theme.Green : Theme.Dim))
			{
				g.FillEllipse(b, x0, vy + Theme.S(6f), d, d);
			}
			TextRenderer.DrawText(g, val, Theme.Body, new Rectangle(x0 + d + Theme.S(8f), vy, r.Right - x0 - d - Theme.S(18f), Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		private int HitMode(Point p)
		{
			if (!_loaded)
			{
				return -1;
			}
			for (int i = 0; i < ModeCount; i++)
			{
				if (_modeR[i].Contains(p))
				{
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			int h = HitMode(e.Location);
			if (h != _hot)
			{
				_hot = h;
				Cursor = (h >= 0 && h != _mode) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			if (_hot != -1)
			{
				_hot = -1;
				Cursor = Cursors.Default;
				Invalidate();
			}
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			int h = HitMode(e.Location);
			if (e.Button == MouseButtons.Left && h >= 0 && h != _mode)
			{
				ModeClicked?.Invoke(h);
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Skeleton.Track(this, !_loaded);
		}
	}
}
