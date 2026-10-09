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
	// Network dashboard: current connection, live ping to the router and the internet, DNS picker with a speed test
	internal class NetView : Card
	{
		private class Target
		{
			public string Host;
			public string Label;
			public string Sub;
			public readonly List<int> Samples = new List<int>();
			public Rectangle R;
		}

		private class Dns
		{
			public int Idx;
			public string Name;
			public string Ip;
			public string Tag;
			public Rectangle R;
		}

		private const int Rounds = 20;

		private readonly Host _host;

		private Hashtable _info;

		private int _dns = -1;

		private bool _loaded;

		private bool _autoDone;

		private readonly List<Target> _targets = new List<Target>();

		private readonly List<Dns> _dnsList = new List<Dns>();

		private readonly Dictionary<int, int> _dnsMs = new Dictionary<int, int>();

		private bool _pinging;

		private bool _dnsTesting;

		private int _round;

		private int _hotDns = -1;

		private float _phase;

		private readonly Timer _anim;

		private readonly Timer _next;

		private readonly FButton _pingBtn;

		private readonly FButton _dnsBtn;

		private readonly FButton _refBtn;

		private readonly FButton _hideBtn;

		private bool _hide = Prefs.HideAddr;

		private Rectangle _vpnR;

		private Rectangle _headR;

		private Rectangle _cellsR;

		private int _pingHead;

		private int _dnsHead;

		private Rectangle _noteR;

		public event Action<int> DnsPicked;

		public NetView(Host host)
		{
			_host = host;
			_pingBtn = Btn("Проверить пинг", BtnKind.Silver, null);
			_pingBtn.Click += delegate
			{
				if (_pinging)
				{
					StopPing();
				}
				else
				{
					StartPing();
				}
			};
			_dnsBtn = Btn("Тест скорости DNS", BtnKind.Ghost, null);
			_dnsBtn.Click += delegate
			{
				TestDns();
			};
			_refBtn = Btn("Обновить", BtnKind.Ghost, Glyph.Refresh);
			_refBtn.Click += delegate
			{
				if (base.Parent is Canvas)
				{
					((Canvas)base.Parent).Page.Refresh(_host.Worker);
				}
			};
			_hideBtn = Btn(_hide ? "Показать адреса" : "Скрыть адреса", BtnKind.Ghost, null);
			_hideBtn.Click += delegate
			{
				_hide = !_hide;
				Prefs.HideAddr = _hide;
				_hideBtn.Text = _hide ? "Показать адреса" : "Скрыть адреса";
				_hideBtn.AutoWidth(Theme.S(26f));
				foreach (Target t in _targets)
				{
					if (t.Label == "Роутер")
					{
						t.Sub = "шлюз " + Mask(t.Host);
					}
				}
				RelayoutButtons();
				Invalidate();
			};
			_dnsList.Add(new Dns { Idx = 0, Name = "Авто", Ip = "от провайдера", Tag = "как было по умолчанию" });
			_dnsList.Add(new Dns { Idx = 1, Name = "Cloudflare", Ip = "1.1.1.1", Tag = "обычно самый быстрый" });
			_dnsList.Add(new Dns { Idx = 2, Name = "Google", Ip = "8.8.8.8", Tag = "стабильный и надёжный" });
			_dnsList.Add(new Dns { Idx = 3, Name = "Quad9", Ip = "9.9.9.9", Tag = "блокирует опасные сайты" });
			_dnsList.Add(new Dns { Idx = 4, Name = "AdGuard", Ip = "94.140.14.14", Tag = "режет рекламу и трекеры" });
			_anim = new Timer { Interval = 33 };
			_anim.Tick += delegate
			{
				_phase = (float)(Anim.Now % 890.0 / 890.0);
				Invalidate();
			};
			_next = new Timer { Interval = 280 };
			_next.Tick += delegate
			{
				_next.Stop();
				PingStep();
			};
		}

		private FButton Btn(string text, BtnKind kind, string glyph)
		{
			FButton b = new FButton { Text = text, Kind = kind, Font = Theme.Body, Glyph = glyph };
			b.AutoWidth(Theme.S(26f));
			base.Controls.Add(b);
			return b;
		}

		private static string Str(Hashtable h, string k)
		{
			if (h == null || h[k] == null)
			{
				return "";
			}
			return h[k].ToString();
		}

		private static int Int(object o, int d)
		{
			if (o == null)
			{
				return d;
			}
			try
			{
				return Convert.ToInt32(o);
			}
			catch
			{
				return d;
			}
		}

		private bool Online => _info != null && _info["Ok"] is bool && (bool)_info["Ok"];

		private bool Wifi => Online && _info["Wifi"] is bool && (bool)_info["Wifi"];

		private bool Vpn => Online && _info["Vpn"] is bool && (bool)_info["Vpn"];

		private bool Phys => Online && _info["Phys"] is bool && (bool)_info["Phys"];

		// local addresses and the network name are hidden on demand (screenshots, streams)
		private string Mask(string ip)
		{
			if (!_hide || string.IsNullOrEmpty(ip))
			{
				return ip;
			}
			return "•••.•••.•.••";
		}

		// the VPN answers ICMP itself: 0–1 ms to the internet means the packets never left the PC
		private bool FakePing
		{
			get
			{
				if (!Vpn)
				{
					return false;
				}
				int n = 0;
				int sum = 0;
				foreach (Target t in _targets)
				{
					if (t.Label == "Роутер")
					{
						continue;
					}
					Stats(t.Samples, out var _, out var _, out var avg, out var _, out var _);
					if (avg >= 0)
					{
						sum += avg;
						n++;
					}
				}
				return n > 0 && sum / n <= 1;
			}
		}

		public override object Fetch(IEngine e)
		{
			return new object[2]
			{
				e.Eval("Get-FNetInfo"),
				e.As(e.Eval("Get-FDns"), -1)
			};
		}

		public override void Apply(object v)
		{
			object[] a = v as object[];
			if (a == null)
			{
				return;
			}
			_info = a[0] as Hashtable;
			_dns = (a[1] is int) ? ((int)a[1]) : (-1);
			_loaded = true;
			string gw = Online ? Str(_info, "Gw") : "";
			// keep samples when the targets did not change
			List<string> want = new List<string>();
			if (gw.Length > 0 && gw != "0.0.0.0")
			{
				want.Add(gw);
			}
			want.Add("1.1.1.1");
			want.Add("8.8.8.8");
			bool same = want.Count == _targets.Count;
			for (int i = 0; same && i < want.Count; i++)
			{
				same = _targets[i].Host == want[i];
			}
			if (!same)
			{
				_targets.Clear();
				foreach (string h in want)
				{
					_targets.Add(new Target
					{
						Host = h,
						Label = (h == gw) ? "Роутер" : ((h == "1.1.1.1") ? "Cloudflare" : "Google"),
						Sub = (h == gw) ? ("шлюз " + Mask(h)) : h
					});
				}
			}
			if (base.Parent is Canvas)
			{
				((Canvas)base.Parent).Page.RelayoutSoon();
			}
			Invalidate();
			if (!_autoDone && Online)
			{
				_autoDone = true;
				StartPing();
			}
		}

		// ---------- ping ----------
		private void StartPing()
		{
			if (!Online)
			{
				_host.Status("warn", "Нет подключения к сети");
				return;
			}
			foreach (Target t in _targets)
			{
				t.Samples.Clear();
			}
			_round = 0;
			_pinging = true;
			_pingBtn.Text = "Остановить";
			_pingBtn.Invalidate();
			_anim.Start();
			PingStep();
		}

		private void StopPing()
		{
			_pinging = false;
			_next.Stop();
			_pingBtn.Text = "Проверить ещё раз";
			_pingBtn.AutoWidth(Theme.S(26f));
			if (!_dnsTesting)
			{
				_anim.Stop();
			}
			RelayoutButtons();
			Invalidate();
		}

		private void PingStep()
		{
			if (!_pinging || base.IsDisposed)
			{
				return;
			}
			List<string> hosts = new List<string>();
			foreach (Target t in _targets)
			{
				hosts.Add(t.Host);
			}
			string list = string.Join(",", hosts.ToArray());
			_host.Worker.Run((IEngine e) => e.Eval("Get-FPing $fa1", new Hashtable { { "fa1", list } }), delegate(object r)
			{
				if (!_pinging)
				{
					return;
				}
				Hashtable h = r as Hashtable;
				foreach (Target t in _targets)
				{
					t.Samples.Add((h != null && h.ContainsKey(t.Host)) ? Int(h[t.Host], -1) : (-1));
				}
				_round++;
				Invalidate();
				if (_round >= Rounds)
				{
					StopPing();
				}
				else
				{
					_next.Start();
				}
			});
		}

		private static void Stats(List<int> s, out int last, out int min, out int avg, out int jit, out int loss)
		{
			last = -1;
			min = -1;
			avg = -1;
			jit = 0;
			loss = 0;
			if (s.Count == 0)
			{
				return;
			}
			last = s[s.Count - 1];
			int n = 0;
			int sum = 0;
			int lost = 0;
			int prev = -1;
			int jsum = 0;
			int jn = 0;
			foreach (int v in s)
			{
				if (v < 0)
				{
					lost++;
					continue;
				}
				if (min < 0 || v < min)
				{
					min = v;
				}
				sum += v;
				n++;
				if (prev >= 0)
				{
					jsum += Math.Abs(v - prev);
					jn++;
				}
				prev = v;
			}
			avg = (n > 0) ? (sum / n) : (-1);
			jit = (jn > 0) ? (jsum / jn) : 0;
			loss = lost * 100 / s.Count;
		}

		private static Color MsColor(int ms)
		{
			if (ms < 0)
			{
				return Theme.Red;
			}
			if (ms <= 40)
			{
				return Theme.Green;
			}
			if (ms <= 90)
			{
				return Theme.Yellow;
			}
			return Theme.Red;
		}

		private void Verdict(out string text, out Color c)
		{
			int sum = 0;
			int n = 0;
			int jit = 0;
			int loss = 0;
			int cnt = 0;
			foreach (Target t in _targets)
			{
				if (t.Label == "Роутер")
				{
					continue;
				}
				Stats(t.Samples, out var _, out var _, out var avg, out var j, out var l);
				cnt = Math.Max(cnt, t.Samples.Count);
				if (avg >= 0)
				{
					sum += avg;
					n++;
				}
				jit = Math.Max(jit, j);
				loss = Math.Max(loss, l);
			}
			if (cnt == 0)
			{
				text = Online ? "ещё не проверяли" : "нет сети";
				c = Theme.Dim;
				return;
			}
			if (FakePing)
			{
				text = "VPN отвечает сам — замер неточный";
				c = Theme.Yellow;
				return;
			}
			if (n == 0)
			{
				text = "интернет не отвечает";
				c = Theme.Red;
				return;
			}
			int a = sum / n;
			if (loss > 5)
			{
				text = "теряются пакеты — " + loss + "%";
				c = Theme.Red;
			}
			else if (a <= 40 && jit <= 8)
			{
				text = Vpn ? "отлично (через VPN)" : "отлично для игр и звонков";
				c = Theme.Green;
			}
			else if (a <= 90 && jit <= 25)
			{
				text = "нормально, без рекордов";
				c = Theme.Yellow;
			}
			else
			{
				text = "высокий пинг или скачки";
				c = Theme.Red;
			}
		}

		// ---------- DNS ----------
		private void TestDns()
		{
			if (_dnsTesting)
			{
				return;
			}
			if (!Online)
			{
				_host.Status("warn", "Нет подключения к сети");
				return;
			}
			_dnsTesting = true;
			_dnsMs.Clear();
			_dnsBtn.Text = "Проверяю…";
			_dnsBtn.Invalidate();
			_anim.Start();
			Invalidate();
			_host.Worker.Run((IEngine e) => e.Eval("Test-FDnsSpeed"), delegate(object r)
			{
				_dnsTesting = false;
				_dnsBtn.Text = "Тест скорости DNS";
				if (!_pinging)
				{
					_anim.Stop();
				}
				Hashtable h = r as Hashtable;
				if (h != null)
				{
					foreach (DictionaryEntry de in h)
					{
						int k = Int(de.Key, -1);
						if (k >= 0)
						{
							_dnsMs[k] = Int(de.Value, -1);
						}
					}
				}
				int best = Fastest();
				if (best >= 0)
				{
					_host.Status("ok", "Быстрее всех отвечает " + _dnsList[best].Name + " — " + _dnsMs[best] + " мс");
				}
				Invalidate();
			});
		}

		private int Fastest()
		{
			int best = -1;
			foreach (KeyValuePair<int, int> kv in _dnsMs)
			{
				if (kv.Value >= 0 && (best < 0 || kv.Value < _dnsMs[best]))
				{
					best = kv.Key;
				}
			}
			return best;
		}

		// ---------- layout ----------
		private void RelayoutButtons()
		{
			int pad = base.Pad;
			if (_headR.Width > 0)
			{
				_refBtn.Location = new Point(base.Width - pad - _refBtn.Width, _headR.Y + (_headR.Height - _refBtn.Height) / 2);
				_hideBtn.Location = new Point(_refBtn.Left - Theme.S(8f) - _hideBtn.Width, _refBtn.Top);
				_pingBtn.Location = new Point(base.Width - pad - _pingBtn.Width, _pingHead + (Theme.S(28f) - _pingBtn.Height) / 2);
				_dnsBtn.Location = new Point(base.Width - pad - _dnsBtn.Width, _dnsHead + (Theme.S(28f) - _dnsBtn.Height) / 2);
			}
		}

		public override int DoLayout(int x, int y, int w)
		{
			int pad = base.Pad;
			int gap = Theme.S(10f);
			int iw = w - pad * 2;
			_headR = new Rectangle(pad, pad, iw, Theme.S(64f));
			int cy = _headR.Bottom + Theme.S(16f);
			_cellsR = new Rectangle(pad, cy, iw, Theme.S(64f));
			cy = _cellsR.Bottom + Theme.S(12f);
			if (Vpn)
			{
				_vpnR = new Rectangle(pad, cy, iw, Theme.S(54f));
				cy = _vpnR.Bottom + Theme.S(12f);
			}
			else
			{
				_vpnR = Rectangle.Empty;
			}
			cy += Theme.S(10f);
			_pingHead = cy;
			cy += Theme.S(28f) + Theme.S(10f);
			int n = Math.Max(1, _targets.Count);
			int tw = (iw - gap * (n - 1)) / n;
			for (int i = 0; i < _targets.Count; i++)
			{
				_targets[i].R = new Rectangle(pad + i * (tw + gap), cy, (i == n - 1) ? (iw - (tw + gap) * (n - 1)) : tw, Theme.S(136f));
			}
			cy += Theme.S(136f) + Theme.S(22f);
			_dnsHead = cy;
			cy += Theme.S(28f) + Theme.S(10f);
			int cols = Math.Max(2, Math.Min(5, (iw + gap) / (Theme.S(150f) + gap)));
			int dw = (iw - gap * (cols - 1)) / cols;
			int dh = Theme.S(100f);
			for (int i = 0; i < _dnsList.Count; i++)
			{
				int c = i % cols;
				int r = i / cols;
				_dnsList[i].R = new Rectangle(pad + c * (dw + gap), cy + r * (dh + gap), dw, dh);
			}
			int rows = (_dnsList.Count + cols - 1) / cols;
			cy += rows * dh + (rows - 1) * gap + Theme.S(12f);
			_noteR = new Rectangle(pad, cy, iw, Theme.S(18f));
			cy = _noteR.Bottom;
			int h = cy + pad;
			SetBounds(x, y, w, h);
			RelayoutButtons();
			return h;
		}

		// ---------- paint ----------
		private void Tile(Graphics g, Rectangle r, int alpha = 14, Color? border = null)
		{
			using (GraphicsPath p = Theme.Round(r, Theme.S(12f)))
			{
				using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
				{
					g.FillPath(b, p);
				}
				using (Pen pen = new Pen(border ?? Color.FromArgb(34, 255, 255, 255), (border != null) ? Theme.S(1.4f) : 1f))
				{
					g.DrawPath(pen, p);
				}
			}
		}

		private void SectionHead(Graphics g, int y, string text, string right, Color rc, int rightLimit)
		{
			int pad = base.Pad;
			TextRenderer.DrawText(g, text, Theme.Tiny, new Point(pad, y + Theme.S(7f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int tw = TextRenderer.MeasureText(g, text, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
			int lx = pad + tw + Theme.S(12f);
			if (!string.IsNullOrEmpty(right))
			{
				int pw = Theme.PillW(right, Theme.Tiny);
				Theme.Pill(g, new Rectangle(lx, y + Theme.S(4f), pw, Theme.S(20f)), right, Theme.Tiny, rc, 26);
				lx += pw + Theme.S(12f);
			}
			int rx = rightLimit - Theme.S(14f);
			if (rx > lx)
			{
				using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(lx, 0, rx - lx, 1), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(6, 255, 255, 255), 0f))
				{
					g.FillRectangle(lb, lx, y + Theme.S(14f), rx - lx, 1);
				}
			}
		}

		// vector Wi-Fi arcs (lit by signal) or an ethernet plug
		private void ConnIcon(Graphics g, Rectangle r)
		{
			using (GraphicsPath p = Theme.Round(r, Theme.S(16f)))
			{
				Color top = Online ? Color.FromArgb(70, 72, 84) : Color.FromArgb(60, 50, 52);
				Theme.FillGradient(g, p, r, top, Color.FromArgb(32, 32, 38));
				using (Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255)))
				{
					g.DrawPath(pen, p);
				}
			}
			if (_pinging || _dnsTesting)
			{
				// soft radar ring while testing
				float k = _phase;
				int a = (int)(90f * (1f - k));
				float grow = Theme.S(10f) * k;
				using (GraphicsPath rp = Theme.Round(new RectangleF(r.X - grow, r.Y - grow, r.Width + grow * 2f, r.Height + grow * 2f), Theme.S(16f) + grow))
				{
					using (Pen rpen = new Pen(Color.FromArgb(Math.Max(0, a), 220, 220, 230), Theme.S(1.5f)))
					{
						g.DrawPath(rpen, rp);
					}
				}
			}
			float cx = r.X + r.Width / 2f;
			float u = Theme.S(1f);
			Color on = Online ? Theme.Silver1 : Theme.Red;
			Color off = Color.FromArgb(70, 255, 255, 255);
			if (Vpn)
			{
				// lock badge in the corner
				float bs = Theme.S(22f);
				RectangleF br = new RectangleF(r.Right - bs + Theme.S(4f), r.Bottom - bs + Theme.S(4f), bs, bs);
				using (SolidBrush bb = new SolidBrush(Color.FromArgb(125, 165, 235)))
				{
					g.FillEllipse(bb, br);
				}
				using (Pen bp = new Pen(Theme.Card2, Theme.S(2f)))
				{
					g.DrawEllipse(bp, br);
				}
				using (Pen lp = new Pen(Theme.OnSilver, Theme.S(1.5f)))
				{
					float lx = br.X + br.Width / 2f;
					float ly = br.Y + br.Height / 2f;
					g.DrawArc(lp, lx - 3.2f * u, ly - 6.5f * u, 6.4f * u, 7f * u, 180f, 180f);
					using (SolidBrush lb = new SolidBrush(Theme.OnSilver))
					{
						g.FillRectangle(lb, lx - 4.6f * u, ly - 2.2f * u, 9.2f * u, 7f * u);
					}
				}
			}
			if (!Online || Wifi)
			{
				int sig = Online ? Int(_info["Signal"], 100) : 0;
				if (sig < 0)
				{
					sig = 100;
				}
				float by = r.Y + r.Height * 0.70f;
				for (int i = 0; i < 3; i++)
				{
					float rad = (7 + i * 7) * u;
					bool lit = Online && sig >= 15 + i * 30;
					using (Pen pen = new Pen(lit ? on : off, 2.6f * u))
					{
						pen.StartCap = LineCap.Round;
						pen.EndCap = LineCap.Round;
						g.DrawArc(pen, cx - rad, by - rad, rad * 2f, rad * 2f, 225f, 90f);
					}
				}
				using (SolidBrush b = new SolidBrush(on))
				{
					g.FillEllipse(b, cx - 2.6f * u, by - 2.6f * u, 5.2f * u, 5.2f * u);
				}
			}
			else
			{
				// ethernet plug
				float w = 20f * u;
				float h = 16f * u;
				float x0 = cx - w / 2f;
				float y0 = r.Y + r.Height / 2f - h / 2f - 2f * u;
				using (Pen pen = new Pen(on, 2.2f * u))
				{
					pen.LineJoin = LineJoin.Round;
					g.DrawPolygon(pen, new PointF[]
					{
						new PointF(x0, y0), new PointF(x0 + w, y0), new PointF(x0 + w, y0 + h - 4f * u), new PointF(x0 + w - 4f * u, y0 + h - 4f * u),
						new PointF(x0 + w - 4f * u, y0 + h), new PointF(x0 + 4f * u, y0 + h), new PointF(x0 + 4f * u, y0 + h - 4f * u), new PointF(x0, y0 + h - 4f * u)
					});
					for (int i = 0; i < 3; i++)
					{
						float px = x0 + 6f * u + i * 4f * u;
						g.DrawLine(pen, px, y0 + 3.5f * u, px, y0 + 7f * u);
					}
					g.DrawLine(pen, cx, y0 + h, cx, y0 + h + 6f * u);
				}
			}
		}

		private static string SpeedText(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return "—";
			}
			return s.Replace("Gbps", "Гбит/с").Replace("Mbps", "Мбит/с").Replace("Kbps", "Кбит/с");
		}

		private string DnsName(string ips)
		{
			if (ips.Contains("1.1.1.1"))
			{
				return "Cloudflare";
			}
			if (ips.Contains("8.8.8.8"))
			{
				return "Google";
			}
			if (ips.Contains("9.9.9.9"))
			{
				return "Quad9";
			}
			if (ips.Contains("94.140.14.14"))
			{
				return "AdGuard";
			}
			return "от провайдера";
		}

		private void Cell(Graphics g, Rectangle r, string label, string value, string hint)
		{
			Tile(g, r);
			int x0 = r.X + Theme.S(14f);
			TextRenderer.DrawText(g, label, Theme.Tiny, new Rectangle(x0, r.Y + Theme.S(10f), r.Width - Theme.S(20f), Theme.S(16f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			using (Font f = Theme.F(11.5f, semi: true))
			{
				TextRenderer.DrawText(g, value, f, new Rectangle(x0, r.Y + Theme.S(27f), r.Width - Theme.S(20f), Theme.S(22f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			if (!string.IsNullOrEmpty(hint))
			{
				int vw = Theme.Measure(value, Theme.FC(11.5f, semi: true)).Width;
				int hx = x0 + vw + Theme.S(6f);
				if (hx < r.Right - Theme.S(30f))
				{
					TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(hx, r.Y + Theme.S(30f), r.Right - hx - Theme.S(8f), Theme.S(18f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				}
			}
		}

		private void PingTile(Graphics g, Target t)
		{
			Rectangle r = t.R;
			Tile(g, r);
			Stats(t.Samples, out var last, out var min, out var avg, out var jit, out var loss);
			int x0 = r.X + Theme.S(14f);
			TextRenderer.DrawText(g, t.Label, Theme.Title, new Point(x0, r.Y + Theme.S(11f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int lw = Theme.Measure(t.Label, Theme.Title).Width;
			TextRenderer.DrawText(g, t.Sub, Theme.Tiny, new Rectangle(x0 + lw + Theme.S(4f), r.Y + Theme.S(14f), r.Right - x0 - lw - Theme.S(90f), Theme.S(16f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			// big number on the right
			string big = (t.Samples.Count == 0) ? "—" : ((avg < 0) ? "нет" : (avg.ToString() ?? ""));
			Color bc = (t.Samples.Count == 0) ? Theme.Dim : ((Vpn && t.Label != "Роутер" && avg >= 0 && avg <= 1) ? Theme.Yellow : MsColor(avg));
			using (Font bf = Theme.F(20f, semi: true))
			{
				Size bs = TextRenderer.MeasureText(g, big, bf, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				int ux = r.Right - Theme.S(14f);
				string unit = (t.Samples.Count == 0 || avg < 0) ? "" : "мс";
				int uw = (unit.Length > 0) ? TextRenderer.MeasureText(g, unit, Theme.Small, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width : 0;
				if (uw > 0)
				{
					TextRenderer.DrawText(g, unit, Theme.Small, new Point(ux - uw, r.Y + Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				}
				TextRenderer.DrawText(g, big, bf, new Point(ux - uw - Theme.S(3f) - bs.Width, r.Y + Theme.S(6f)), bc, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			// sparkline
			Rectangle sr = new Rectangle(r.X + Theme.S(12f), r.Y + Theme.S(46f), r.Width - Theme.S(24f), Theme.S(52f));
			using (Pen gp = new Pen(Color.FromArgb(18, 255, 255, 255)))
			{
				gp.DashStyle = DashStyle.Dot;
				g.DrawLine(gp, sr.X, sr.Bottom, sr.Right, sr.Bottom);
				g.DrawLine(gp, sr.X, sr.Y + sr.Height / 2, sr.Right, sr.Y + sr.Height / 2);
			}
			int maxV = 30;
			foreach (int v in t.Samples)
			{
				maxV = Math.Max(maxV, v);
			}
			maxV = (int)(maxV * 1.15f);
			if (t.Samples.Count > 0)
			{
				float step = sr.Width / (float)(Rounds - 1);
				List<PointF> pts = new List<PointF>();
				for (int i = 0; i < t.Samples.Count; i++)
				{
					int v = t.Samples[i];
					float px = sr.X + i * step;
					if (v < 0)
					{
						// lost packet: red tick
						using (Pen lp = new Pen(Color.FromArgb(200, Theme.Red), Theme.S(2f)))
						{
							g.DrawLine(lp, px, sr.Bottom - Theme.S(8f), px, sr.Bottom);
						}
						continue;
					}
					pts.Add(new PointF(px, sr.Bottom - Math.Max(1f, sr.Height * (v / (float)maxV))));
				}
				Color lc = (Vpn && t.Label != "Роутер" && avg >= 0 && avg <= 1) ? Theme.Yellow : MsColor(avg);
				if (pts.Count >= 2)
				{
					using (GraphicsPath area = new GraphicsPath())
					{
						area.AddLine(pts[0].X, sr.Bottom, pts[0].X, pts[0].Y);
						area.AddLines(pts.ToArray());
						area.AddLine(pts[pts.Count - 1].X, pts[pts.Count - 1].Y, pts[pts.Count - 1].X, sr.Bottom);
						area.CloseFigure();
						using (LinearGradientBrush ab = new LinearGradientBrush(new RectangleF(sr.X, sr.Y - 1, sr.Width, sr.Height + 2), Color.FromArgb(70, lc), Color.FromArgb(0, lc), 90f))
						{
							g.FillPath(ab, area);
						}
					}
					using (Pen lp2 = new Pen(lc, Theme.S(1.8f)))
					{
						lp2.LineJoin = LineJoin.Round;
						g.DrawLines(lp2, pts.ToArray());
					}
				}
				if (pts.Count > 0)
				{
					PointF e = pts[pts.Count - 1];
					float pr = Theme.S(3.5f);
					if (_pinging)
					{
						float gr = pr + Theme.S(6f) * _phase;
						using (SolidBrush gb = new SolidBrush(Color.FromArgb((int)(110f * (1f - _phase)), lc)))
						{
							g.FillEllipse(gb, e.X - gr, e.Y - gr, gr * 2f, gr * 2f);
						}
					}
					using (SolidBrush db = new SolidBrush(lc))
					{
						g.FillEllipse(db, e.X - pr, e.Y - pr, pr * 2f, pr * 2f);
					}
				}
			}
			else
			{
				TextRenderer.DrawText(g, Online ? "Нажми «Проверить пинг»" : "Нет подключения", Theme.Small, sr, Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
			}
			string st = (t.Samples.Count == 0) ? "" : ("мин " + ((min < 0) ? "—" : min.ToString()) + "  •  разброс " + jit + "  •  потери " + loss + "%" + (_pinging ? ("  •  " + t.Samples.Count + "/" + Rounds) : ""));
			TextRenderer.DrawText(g, st, Theme.Tiny, new Rectangle(x0, r.Bottom - Theme.S(28f), r.Width - Theme.S(24f), Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}

		private void DnsTile(Graphics g, Dns d, int idx, int fastest)
		{
			Rectangle r = d.R;
			bool sel = d.Idx == _dns;
			bool hot = idx == _hotDns;
			Tile(g, r, sel ? 30 : (hot ? 22 : 12), sel ? new Color?(Color.FromArgb(200, Theme.Silver1)) : (hot ? new Color?(Color.FromArgb(70, 255, 255, 255)) : null));
			int x0 = r.X + Theme.S(14f);
			using (Font f = Theme.F(11.5f, semi: true))
			{
				TextRenderer.DrawText(g, d.Name, f, new Point(x0, r.Y + Theme.S(12f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			if (sel)
			{
				// check mark in a silver dot
				float s = Theme.S(18f);
				RectangleF cr = new RectangleF(r.Right - Theme.S(12f) - s, r.Y + Theme.S(12f), s, s);
				using (SolidBrush cb = new SolidBrush(Theme.Silver1))
				{
					g.FillEllipse(cb, cr);
				}
				float u = Theme.S(1f);
				using (Pen cp = new Pen(Theme.OnSilver, Theme.S(1.8f)))
				{
					cp.StartCap = LineCap.Round;
					cp.EndCap = LineCap.Round;
					g.DrawLines(cp, new PointF[]
					{
						new PointF(cr.X + 5f * u, cr.Y + 9.5f * u), new PointF(cr.X + 8f * u, cr.Y + 12.5f * u), new PointF(cr.X + 13.5f * u, cr.Y + 6.5f * u)
					});
				}
			}
			TextRenderer.DrawText(g, d.Ip, Theme.Small, new Rectangle(x0, r.Y + Theme.S(34f), r.Width - Theme.S(24f), Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, d.Tag, Theme.Tiny, new Rectangle(x0, r.Y + Theme.S(53f), r.Width - Theme.S(24f), Theme.S(16f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			int by = r.Bottom - Theme.S(28f);
			if (_dnsTesting)
			{
				// shimmering placeholder bar
				float k = (_phase + idx * 0.15f) % 1f;
				int a = 20 + (int)(30f * (float)Math.Sin(k * Math.PI));
				using (GraphicsPath pp = Theme.Round(new RectangleF(x0, by + Theme.S(3f), Theme.S(54f), Theme.S(14f)), Theme.S(7f)))
				{
					using (SolidBrush pb = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
					{
						g.FillPath(pb, pp);
					}
				}
			}
			else if (_dnsMs.TryGetValue(d.Idx, out var ms))
			{
				string t = (ms < 0) ? "нет ответа" : (ms + " мс");
				Color c = (ms < 0) ? Theme.Red : ((d.Idx == fastest) ? Theme.Green : ((ms <= 60) ? Theme.Silver2 : Theme.Yellow));
				int pw = Theme.PillW(t, Theme.Tiny);
				Theme.Pill(g, new Rectangle(x0, by, pw, Theme.S(20f)), t, Theme.Tiny, c, 26);
				if (d.Idx == fastest)
				{
					string fx = "быстрее всех";
					int fw = Theme.PillW(fx, Theme.Tiny);
					if (x0 + pw + Theme.S(6f) + fw < r.Right - Theme.S(8f))
					{
						Theme.Pill(g, new Rectangle(x0 + pw + Theme.S(6f), by, fw, Theme.S(20f)), fx, Theme.Tiny, Theme.Green, 18);
					}
				}
			}
			else if (d.Idx == 0 && _dnsMs.Count > 0)
			{
				Theme.Pill(g, new Rectangle(x0, by, Theme.PillW("не мерили", Theme.Tiny), Theme.S(20f)), "не мерили", Theme.Tiny, Theme.Dim, 18);
			}
			else if (sel)
			{
				Theme.Pill(g, new Rectangle(x0, by, Theme.PillW("сейчас", Theme.Tiny), Theme.S(20f)), "сейчас", Theme.Tiny, Theme.Silver1, 26);
			}
		}

		protected override void PaintContent(Graphics g)
		{
			Theme.Smooth(g);
			int pad = base.Pad;
			Rectangle ir = new Rectangle(_headR.X, _headR.Y, Theme.S(64f), Theme.S(64f));
			ConnIcon(g, ir);
			int tx = ir.Right + Theme.S(16f);
			int rlim = _hideBtn.Left - Theme.S(14f);
			string title;
			string sub;
			if (!_loaded)
			{
				title = "Проверяю подключение…";
				sub = "";
			}
			else if (!Online)
			{
				title = "Нет подключения к интернету";
				sub = "Проверь кабель или Wi-Fi. Если не помогает — «Инструменты → Сбросить сеть»";
			}
			else
			{
				string ssid = _hide ? "" : Str(_info, "Ssid");
				string phys = !Phys ? "" : (Wifi ? ("Wi-Fi" + ((ssid.Length > 0) ? (" • " + ssid) : "")) : "Ethernet • кабель");
				if (Vpn)
				{
					string vn = Str(_info, "VpnDesc");
					title = "VPN • " + ((vn.Length > 0) ? vn : Str(_info, "VpnName"));
					sub = Phys ? ("Под туннелем: " + phys + " — " + Str(_info, "Desc")) : "Физическое подключение не найдено";
				}
				else
				{
					title = phys;
					sub = Str(_info, "Desc");
				}
			}
			using (Font tf = Theme.F(15f, semi: true))
			{
				int th = Theme.Measure("Ag", tf).Height;
				int ty = _headR.Y + Theme.S(8f);
				int tw = Math.Min(Theme.Measure(title, tf).Width + 4, Math.Max(Theme.S(80f), rlim - tx - Theme.S(130f)));
				TextRenderer.DrawText(g, title, tf, new Rectangle(tx, ty, tw, th + 2), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				if (_loaded)
				{
					string pill = Vpn ? "трафик через VPN" : (Online ? "подключено" : "нет сети");
					int pw = Theme.PillW(pill, Theme.Tiny);
					int px = tx + tw + Theme.S(10f);
					if (px + pw < rlim)
					{
						Rectangle pr = new Rectangle(px, ty + (th - Theme.S(20f)) / 2 + Theme.S(1f), pw, Theme.S(20f));
						Theme.Pill(g, pr, pill, Theme.Tiny, Vpn ? Color.FromArgb(125, 165, 235) : (Online ? Theme.Green : Theme.Red), 26);
					}
				}
				TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, ty + th + Theme.S(6f), rlim - tx, Theme.S(20f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			// info cells
			int gap = Theme.S(10f);
			int cw = (_cellsR.Width - gap * 3) / 4;
			string sig = (Wifi && Int(_info["Signal"], -1) >= 0) ? ("сигнал " + Int(_info["Signal"], 0) + "%") : "";
			string dns = Phys ? Str(_info, "Dns") : "";
			string dhcp = Phys ? ((Str(_info, "Dhcp") == "Disabled") ? "вручную" : "по DHCP") : "";
			string gwv = Phys ? Str(_info, "Gw") : "";
			Cell(g, new Rectangle(_cellsR.X, _cellsR.Y, cw, _cellsR.Height), "СКОРОСТЬ КАНАЛА", Phys ? SpeedText(Str(_info, "Speed")) : "—", sig);
			Cell(g, new Rectangle(_cellsR.X + (cw + gap), _cellsR.Y, cw, _cellsR.Height), "IP-АДРЕС", Phys ? Mask(Str(_info, "Ip")) : "—", Vpn ? ("VPN " + Mask(Str(_info, "VpnIp"))) : dhcp);
			Cell(g, new Rectangle(_cellsR.X + (cw + gap) * 2, _cellsR.Y, cw, _cellsR.Height), "РОУТЕР", (gwv.Length > 0) ? Mask(gwv) : "—", "");
			if (Vpn)
			{
				string vd = Str(_info, "VpnDns");
				Cell(g, new Rectangle(_cellsR.X + (cw + gap) * 3, _cellsR.Y, _cellsR.Width - (cw + gap) * 3, _cellsR.Height), "DNS", "через VPN", (vd.Length > 0) ? Mask(vd.Split(',')[0].Trim()) : "");
				Rectangle vr = _vpnR;
				using (GraphicsPath vp = Theme.Round(vr, Theme.S(12f)))
				{
					using (SolidBrush vb = new SolidBrush(Color.FromArgb(22, 125, 165, 235)))
					{
						g.FillPath(vb, vp);
					}
					using (Pen vpen = new Pen(Color.FromArgb(70, 125, 165, 235)))
					{
						g.DrawPath(vpen, vp);
					}
				}
				TextRenderer.DrawText(g, "Включён VPN — весь трафик идёт через туннель", Theme.Body, new Rectangle(vr.X + Theme.S(16f), vr.Y + Theme.S(8f), vr.Width - Theme.S(32f), Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
				TextRenderer.DrawText(g, "Пинг и тест DNS идут через VPN («0 мс» — это отвечает сам VPN). Выбранный DNS может не действовать, пока VPN включён", Theme.Tiny, new Rectangle(vr.X + Theme.S(16f), vr.Y + Theme.S(30f), vr.Width - Theme.S(32f), Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			else
			{
				Cell(g, new Rectangle(_cellsR.X + (cw + gap) * 3, _cellsR.Y, _cellsR.Width - (cw + gap) * 3, _cellsR.Height), "DNS", (dns.Length > 0) ? DnsName(dns) : "—", (dns.Length > 0) ? Mask(dns.Split(',')[0].Trim()) : "");
			}
			// ping
			Verdict(out var vt, out var vc);
			SectionHead(g, _pingHead, "ЗАДЕРЖКА", vt, vc, _pingBtn.Left);
			foreach (Target t in _targets)
			{
				PingTile(g, t);
			}
			// dns
			int fast = Fastest();
			SectionHead(g, _dnsHead, "DNS-СЕРВЕР", (_dns >= 0 && _dns < _dnsList.Count) ? ("сейчас: " + _dnsList[_dns].Name) : null, Theme.Silver2, _dnsBtn.Left);
			for (int i = 0; i < _dnsList.Count; i++)
			{
				DnsTile(g, _dnsList[i], i, fast);
			}
			TextRenderer.DrawText(g, Vpn ? "При включённом VPN DNS обычно задаёт сам VPN — выбор здесь сработает, когда VPN выключен. Смена DNS попадает в «Историю»" : "Нажми на сервер, чтобы переключиться. Смена DNS попадает в «Историю» и откатывается в один клик", Theme.Tiny, _noteR, Vpn ? Theme.Yellow : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int h = -1;
			for (int i = 0; i < _dnsList.Count; i++)
			{
				if (_dnsList[i].R.Contains(e.Location))
				{
					h = i;
				}
			}
			if (h != _hotDns)
			{
				_hotDns = h;
				Cursor = (h >= 0) ? Cursors.Hand : Cursors.Default;
				Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (_hotDns != -1)
			{
				_hotDns = -1;
				Cursor = Cursors.Default;
				Invalidate();
			}
			base.OnMouseLeave(e);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (e.Button != MouseButtons.Left || !_loaded)
			{
				return;
			}
			foreach (Dns d in _dnsList)
			{
				if (d.R.Contains(e.Location) && d.Idx != _dns)
				{
					DnsPicked?.Invoke(d.Idx);
					return;
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_anim.Dispose();
				_next.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
