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
	// BitLocker: every drive with its state and a live percentage while it encrypts / decrypts
	internal class BitLockerView : Card
	{
		private class Vol
		{
			public string Drive;
			public string Status;
			public double Pct;
			public double Size;
			public string Method;
			public Tween Shown;
			public double Rate; // percent per minute (moving)
			public Rectangle R;
		}

		private readonly Host _host;

		private readonly List<Vol> _vols = new List<Vol>();

		private readonly Dictionary<string, Vol> _prev = new Dictionary<string, Vol>();

		private DateTime _prevAt = DateTime.MinValue;

		private bool _loaded;

		private bool _auto;

		private Rectangle _headR;

		private int _listTop;

		private readonly FButton _decrypt;

		private readonly FButton _block;

		private readonly Timer _poll;

		private readonly Timer _shine;

		private float _phase;

		private bool _polling;

		public event Action DecryptClicked;

		public event Action BlockClicked;

		// background progress for the task toast: title, details, 0..1 (or -1), still running
		public event Action<string, string, float, bool> Progress;

		private bool _wasBusy;

		private int _w;

		public BitLockerView(Host host)
		{
			_host = host;
			_decrypt = new FButton { Text = "Расшифровать все диски", Kind = BtnKind.Silver, Font = Theme.Body, Glyph = Glyph.Lock };
			_decrypt.AutoWidth(Theme.S(30f));
			_decrypt.Click += delegate
			{
				DecryptClicked?.Invoke();
			};
			_block = new FButton { Text = "Запретить автошифрование", Kind = BtnKind.Ghost, Font = Theme.Body };
			_block.AutoWidth(Theme.S(30f));
			_block.Click += delegate
			{
				BlockClicked?.Invoke();
			};
			Controls.Add(_decrypt);
			Controls.Add(_block);
			_decrypt.Enabled = false;
			_block.Enabled = false;
			_poll = new Timer { Interval = 4000 };
			_poll.Tick += delegate
			{
				Poll();
			};
			_shine = new Timer { Interval = 30 };
			_shine.Tick += delegate
			{
				_phase = (_phase + 0.012f) % 1f;
				Invalidate();
			};
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_poll.Dispose();
				_shine.Dispose();
			}
			base.Dispose(disposing);
		}

		private static bool Busy(string s)
		{
			return s == "DecryptionInProgress" || s == "EncryptionInProgress";
		}

		private bool AnyBusy()
		{
			foreach (Vol v in _vols)
			{
				if (Busy(v.Status))
				{
					return true;
				}
			}
			return false;
		}

		private void Poll()
		{
			if (_polling || _host.IsBusy || _host.Worker == null)
			{
				return;
			}
			_polling = true;
			_host.Worker.Run((IEngine e) => Fetch(e), delegate(object v)
			{
				_polling = false;
				Apply(v);
			});
		}

		public override object Fetch(IEngine e)
		{
			return e.Eval("Get-FBLInfo");
		}

		public override void Apply(object v)
		{
			Hashtable h = v as Hashtable;
			if (h == null)
			{
				return;
			}
			_auto = h["Auto"] is bool && (bool)h["Auto"];
			object[] arr = h["Vols"] as object[];
			if (arr == null && h["Vols"] is Hashtable)
			{
				arr = new object[1] { h["Vols"] };
			}
			DateTime now = DateTime.Now;
			double mins = (_prevAt == DateTime.MinValue) ? 0 : (now - _prevAt).TotalMinutes;
			Dictionary<string, Vol> old = new Dictionary<string, Vol>();
			foreach (Vol o in _vols)
			{
				old[o.Drive] = o;
			}
			int count = _vols.Count;
			_vols.Clear();
			if (arr != null)
			{
				foreach (object o in arr)
				{
					Hashtable x = o as Hashtable;
					if (x == null)
					{
						continue;
					}
					Vol nv = new Vol
					{
						Drive = (x["Drive"] ?? "?").ToString(),
						Status = (x["Status"] ?? "").ToString(),
						Pct = Num(x["Pct"]),
						Size = Num(x["Size"]),
						Method = (x["Method"] ?? "").ToString()
					};
					Vol ov;
					if (old.TryGetValue(nv.Drive, out ov))
					{
						nv.Shown = ov.Shown;
						nv.Rate = ov.Rate;
						if (mins > 0.02 && Busy(nv.Status))
						{
							double r = Math.Abs(nv.Pct - ov.Pct) / mins;
							nv.Rate = (ov.Rate <= 0) ? r : (ov.Rate * 0.6 + r * 0.4);
						}
					}
					else
					{
						nv.Shown = new Tween(this, (float)(nv.Pct / 100.0), 0.12f).Timed(1200);
					}
					nv.Shown.Target = (float)(nv.Pct / 100.0);
					_vols.Add(nv);
				}
			}
			_prevAt = now;
			_loaded = true;
			LayoutRows();
			bool enc = false;
			foreach (Vol x in _vols)
			{
				if (x.Status != "FullyDecrypted" && !Busy(x.Status) || x.Status == "EncryptionInProgress")
				{
					enc = true;
				}
			}
			_decrypt.Enabled = enc;
			_block.Enabled = !_auto;
			_block.Text = _auto ? "Автошифрование запрещено" : "Запретить автошифрование";
			if (AnyBusy())
			{
				_poll.Start();
				_shine.Start();
			}
			else
			{
				_poll.Stop();
				_shine.Stop();
			}
			if (count != _vols.Count && Parent is Canvas)
			{
				((Canvas)Parent).Page.RelayoutSoon();
			}
			ReportProgress();
			Invalidate();
		}

		// every poll builds new Vol objects: their rectangles must be placed again,
		// otherwise the rows are drawn at (0, 0) over the header
		private void LayoutRows()
		{
			int w = (_w > 0) ? _w : Width;
			for (int i = 0; i < _vols.Count; i++)
			{
				_vols[i].R = new Rectangle(Pad, _listTop + i * (RowH + Theme.S(8f)), w - Pad * 2, RowH);
			}
		}

		private void ReportProgress()
		{
			Action<string, string, float, bool> pr = Progress;
			if (pr == null)
			{
				return;
			}
			foreach (Vol v in _vols)
			{
				if (!Busy(v.Status))
				{
					continue;
				}
				bool dec = v.Status == "DecryptionInProgress";
				double done = dec ? (100 - v.Pct) : v.Pct;
				string sub = (dec ? "расшифровано " : "зашифровано ") + (int)Math.Round(done) + "%";
				if (v.Rate > 0.01)
				{
					sub += "  •  осталось ≈ " + Eta((dec ? v.Pct : (100 - v.Pct)) / v.Rate);
				}
				sub += "  •  можно пользоваться ПК";
				pr((dec ? "Расшифровка BitLocker — диск " : "Шифрование BitLocker — диск ") + v.Drive, sub, (float)(done / 100.0), true);
				_wasBusy = true;
				return;
			}
			if (_wasBusy)
			{
				_wasBusy = false;
				pr("BitLocker", null, 1f, false);
			}
		}

		private static double Num(object o)
		{
			try
			{
				return (o == null) ? 0.0 : Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture);
			}
			catch
			{
				return 0.0;
			}
		}

		private int RowH => Theme.S(62f);

		public override int DoLayout(int x, int y, int w)
		{
			int pad = Pad;
			_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(48f));
			int cy = _headR.Bottom + Theme.S(14f);
			_decrypt.Location = new Point(pad, cy);
			_block.Location = new Point(_decrypt.Right + Theme.S(8f), cy);
			cy += _decrypt.Height + Theme.S(16f);
			_listTop = cy;
			_w = w;
			int n = _loaded ? Math.Max(1, _vols.Count) : 1;
			LayoutRows();
			cy += n * (RowH + Theme.S(8f)) - Theme.S(8f);
			int h = cy + pad;
			SetBounds(x, y, w, h);
			return h;
		}

		private static string StatusText(Vol v)
		{
			switch (v.Status)
			{
			case "DecryptionInProgress":
				return "Расшифровывается";
			case "EncryptionInProgress":
				return "Шифруется";
			case "FullyEncrypted":
				return "Зашифрован";
			case "FullyDecrypted":
				return "Не зашифрован";
			case "EncryptionPaused":
				return "Шифрование на паузе";
			case "DecryptionPaused":
				return "Расшифровка на паузе";
			default:
				return (v.Status == "") ? "Нет данных" : v.Status;
			}
		}

		private string Summary()
		{
			if (!_loaded)
			{
				return "Проверяю диски…";
			}
			foreach (Vol v in _vols)
			{
				if (v.Status == "DecryptionInProgress")
				{
					return "Идёт расшифровка " + v.Drive + " — " + (int)Math.Round(100 - v.Pct) + "% готово. Компьютером можно пользоваться";
				}
				if (v.Status == "EncryptionInProgress")
				{
					return "Идёт шифрование " + v.Drive + " — " + (int)Math.Round(v.Pct) + "%";
				}
			}
			int enc = 0;
			foreach (Vol v in _vols)
			{
				if (v.Status != "FullyDecrypted")
				{
					enc++;
				}
			}
			string a = _auto ? "автошифрование запрещено" : "Windows может зашифровать диск сама";
			if (_vols.Count == 0)
			{
				return "BitLocker недоступен в этой редакции Windows";
			}
			return ((enc == 0) ? "Диски не зашифрованы" : ("Зашифровано дисков: " + enc)) + " • " + a;
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			int ic = Theme.S(44f);
			bool busy = AnyBusy();
			Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - ic) / 2, ic, ic), Glyph.Lock, busy);
			int tx = _headR.X + ic + Theme.S(14f);
			int th = Theme.Measure("Ag", Theme.Title).Height;
			int ty = _headR.Y + (_headR.Height - th - Theme.S(22f)) / 2;
			TextRenderer.DrawText(g, "BitLocker", Theme.Title, new Point(tx, ty), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, Summary(), Theme.Small, new Rectangle(tx, ty + th + Theme.S(4f), _headR.Right - tx, Theme.S(20f)), busy ? Theme.Yellow : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			if (!_loaded)
			{
				Rectangle sk = new Rectangle(Pad, _listTop, Width - Pad * 2, RowH);
				Skeleton.Row(g, sk, this);
				return;
			}
			foreach (Vol v in _vols)
			{
				Rectangle r = v.R;
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
				Rectangle dr = new Rectangle(r.X + Theme.S(12f), r.Y + (r.Height - Theme.S(38f)) / 2, Theme.S(38f), Theme.S(38f));
				using (GraphicsPath dp = Theme.Round(dr, Theme.S(9f)))
				{
					Theme.FillGradient(g, dp, dr, Color.FromArgb(62, 62, 72), Color.FromArgb(40, 40, 47));
				}
				using (Font df = Theme.F(11f, semi: true))
				{
					TextRenderer.DrawText(g, v.Drive.TrimEnd(':', '\\'), df, dr, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				}
				int x0 = dr.Right + Theme.S(14f);
				int right = r.Right - Theme.S(16f);
				bool b2 = Busy(v.Status);
				bool enc = v.Status != "FullyDecrypted";
				Color sc = b2 ? Theme.Yellow : (enc ? Theme.Silver1 : Theme.Green);
				string name = "Диск " + v.Drive + ((v.Size > 0) ? ("  •  " + CleanView.Fmt(v.Size)) : "");
				TextRenderer.DrawText(g, name, Theme.Body, new Point(x0, r.Y + Theme.S(11f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				string st = StatusText(v);
				int sw = Theme.PillW(st, Theme.Tiny);
				Theme.Pill(g, new Rectangle(right - sw, r.Y + Theme.S(9f), sw, Theme.S(20f)), st, Theme.Tiny, sc, 22);
				float shown = v.Shown != null ? v.Shown.Value : (float)(v.Pct / 100.0);
				string pctText;
				float bar;
				if (v.Status == "DecryptionInProgress")
				{
					bar = 1f - shown;
					pctText = "расшифровано " + (int)Math.Round(bar * 100f) + "%";
					if (v.Rate > 0.01)
					{
						pctText += "  •  осталось ≈ " + Eta(v.Pct / v.Rate);
					}
				}
				else if (v.Status == "EncryptionInProgress")
				{
					bar = shown;
					pctText = "зашифровано " + (int)Math.Round(bar * 100f) + "%";
					if (v.Rate > 0.01)
					{
						pctText += "  •  осталось ≈ " + Eta((100 - v.Pct) / v.Rate);
					}
				}
				else
				{
					bar = enc ? shown : 0f;
					pctText = enc ? ("зашифровано " + (int)Math.Round(v.Pct) + "%" + (v.Method != "" && v.Method != "None" ? ("  •  " + v.Method) : "")) : "данные хранятся без шифрования";
				}
				RectangleF track = new RectangleF(x0, r.Bottom - Theme.S(20f), right - x0 - Theme.S(220f), Theme.S(6f));
				if (b2)
				{
					Theme.Progress(g, track, bar, _phase);
				}
				else
				{
					using (GraphicsPath tp = Theme.Round(track, track.Height / 2f))
					{
						using (SolidBrush tb = new SolidBrush(Color.FromArgb(34, 255, 255, 255)))
						{
							g.FillPath(tb, tp);
						}
					}
					if (bar > 0.005f)
					{
						RectangleF fr = new RectangleF(track.X, track.Y, Math.Max(track.Height, track.Width * bar), track.Height);
						using (GraphicsPath fp = Theme.Round(fr, track.Height / 2f))
						{
							using (SolidBrush fb = new SolidBrush(enc ? Theme.Silver2 : Theme.Green))
							{
								g.FillPath(fb, fp);
							}
						}
					}
				}
				TextRenderer.DrawText(g, pctText, Theme.Small, new Rectangle((int)track.Right + Theme.S(12f), (int)track.Y - Theme.S(8f), right - (int)track.Right - Theme.S(12f), Theme.S(22f)), b2 ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
			}
		}

		private static string Eta(double min)
		{
			if (min < 1)
			{
				return "меньше минуты";
			}
			if (min < 90)
			{
				return (int)Math.Ceiling(min) + " мин";
			}
			double h = min / 60.0;
			return h.ToString("0.#", L.Culture) + (L.IsRu ? " ч" : " h");
		}
	
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Skeleton.Track(this, !_loaded);
		}
	}
}
