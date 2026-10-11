using System;
using System.Collections;
using System.Collections.Generic;

namespace WinWeaver;

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

	private bool Online
	{
		get
		{
			if (_info != null && _info["Ok"] is bool)
			{
				return (bool)_info["Ok"];
			}
			return false;
		}
	}

	private bool Wifi
	{
		get
		{
			if (Online && _info["Wifi"] is bool)
			{
				return (bool)_info["Wifi"];
			}
			return false;
		}
	}

	private bool Vpn
	{
		get
		{
			if (Online && _info["Vpn"] is bool)
			{
				return (bool)_info["Vpn"];
			}
			return false;
		}
	}

	private bool Phys
	{
		get
		{
			if (Online && _info["Phys"] is bool)
			{
				return (bool)_info["Phys"];
			}
			return false;
		}
	}

	private bool FakePing
	{
		get
		{
			if (!Vpn)
			{
				return false;
			}
			int num = 0;
			int num2 = 0;
			foreach (Target target in _targets)
			{
				if (!(target.Label == "Роутер"))
				{
					Stats(target.Samples, out var _, out var _, out var avg, out var _, out var _);
					if (avg >= 0)
					{
						num2 += avg;
						num++;
					}
				}
			}
			if (num > 0)
			{
				return num2 / num <= 1;
			}
			return false;
		}
	}

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
		_refBtn = Btn("Обновить", BtnKind.Ghost, "\ue72c");
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
			_hideBtn.Text = (_hide ? "Показать адреса" : "Скрыть адреса");
			_hideBtn.AutoWidth(Theme.S(26f));
			foreach (Target target in _targets)
			{
				if (target.Label == "Роутер")
				{
					target.Sub = "шлюз " + Mask(target.Host);
				}
			}
			RelayoutButtons();
			Invalidate();
		};
		_dnsList.Add(new Dns
		{
			Idx = 0,
			Name = "Авто",
			Ip = "от провайдера",
			Tag = "как было по умолчанию"
		});
		_dnsList.Add(new Dns
		{
			Idx = 1,
			Name = "Cloudflare",
			Ip = "1.1.1.1",
			Tag = "обычно самый быстрый"
		});
		_dnsList.Add(new Dns
		{
			Idx = 2,
			Name = "Google",
			Ip = "8.8.8.8",
			Tag = "стабильный и надёжный"
		});
		_dnsList.Add(new Dns
		{
			Idx = 3,
			Name = "Quad9",
			Ip = "9.9.9.9",
			Tag = "блокирует опасные сайты"
		});
		_dnsList.Add(new Dns
		{
			Idx = 4,
			Name = "AdGuard",
			Ip = "94.140.14.14",
			Tag = "режет рекламу и трекеры"
		});
		_anim = new Timer
		{
			Interval = 33
		};
		_anim.Tick += delegate
		{
			_phase = (float)(Anim.Now % 890.0 / 890.0);
			Invalidate();
		};
		_next = new Timer
		{
			Interval = 280
		};
		_next.Tick += delegate
		{
			_next.Stop();
			PingStep();
		};
	}

	private FButton Btn(string text, BtnKind kind, string glyph)
	{
		FButton fButton = new FButton
		{
			Text = text,
			Kind = kind,
			Font = Theme.Body,
			Glyph = glyph
		};
		fButton.AutoWidth(Theme.S(26f));
		base.Controls.Add(fButton);
		return fButton;
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

	private string Mask(string ip)
	{
		if (!_hide || string.IsNullOrEmpty(ip))
		{
			return ip;
		}
		return "•••.•••.•.••";
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
		if (!(v is object[] array))
		{
			return;
		}
		_info = array[0] as Hashtable;
		_dns = ((array[1] is int) ? ((int)array[1]) : (-1));
		_loaded = true;
		string text = (Online ? Str(_info, "Gw") : "");
		List<string> list = new List<string>();
		if (text.Length > 0 && text != "0.0.0.0")
		{
			list.Add(text);
		}
		list.Add("1.1.1.1");
		list.Add("8.8.8.8");
		bool flag = list.Count == _targets.Count;
		int num = 0;
		while (flag && num < list.Count)
		{
			flag = _targets[num].Host == list[num];
			num++;
		}
		if (!flag)
		{
			_targets.Clear();
			foreach (string item in list)
			{
				_targets.Add(new Target
				{
					Host = item,
					Label = ((item == text) ? "Роутер" : ((item == "1.1.1.1") ? "Cloudflare" : "Google")),
					Sub = ((item == text) ? ("шлюз " + Mask(item)) : item)
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

	private void StartPing()
	{
		if (!Online)
		{
			_host.Status("warn", "Нет подключения к сети");
			return;
		}
		foreach (Target target in _targets)
		{
			target.Samples.Clear();
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
		List<string> list2 = new List<string>();
		foreach (Target target in _targets)
		{
			list2.Add(target.Host);
		}
		string list = string.Join(",", list2.ToArray());
		_host.Worker.Run((IEngine e) => e.Eval("Get-FPing $fa1", new Hashtable { { "fa1", list } }), delegate(object r)
		{
			if (_pinging)
			{
				Hashtable hashtable = r as Hashtable;
				foreach (Target target2 in _targets)
				{
					target2.Samples.Add((hashtable != null && hashtable.ContainsKey(target2.Host)) ? Int(hashtable[target2.Host], -1) : (-1));
				}
				_round++;
				Invalidate();
				if (_round >= 20)
				{
					StopPing();
				}
				else
				{
					_next.Start();
				}
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
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = -1;
		int num5 = 0;
		int num6 = 0;
		foreach (int item in s)
		{
			if (item < 0)
			{
				num3++;
				continue;
			}
			if (min < 0 || item < min)
			{
				min = item;
			}
			num2 += item;
			num++;
			if (num4 >= 0)
			{
				num5 += Math.Abs(item - num4);
				num6++;
			}
			num4 = item;
		}
		avg = ((num > 0) ? (num2 / num) : (-1));
		jit = ((num6 > 0) ? (num5 / num6) : 0);
		loss = num3 * 100 / s.Count;
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
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		foreach (Target target in _targets)
		{
			if (!(target.Label == "Роутер"))
			{
				Stats(target.Samples, out var _, out var _, out var avg, out var jit, out var loss);
				num5 = Math.Max(num5, target.Samples.Count);
				if (avg >= 0)
				{
					num += avg;
					num2++;
				}
				num3 = Math.Max(num3, jit);
				num4 = Math.Max(num4, loss);
			}
		}
		if (num5 == 0)
		{
			text = (Online ? "ещё не проверяли" : "нет сети");
			c = Theme.Dim;
			return;
		}
		if (FakePing)
		{
			text = "VPN отвечает сам — замер неточный";
			c = Theme.Yellow;
			return;
		}
		if (num2 == 0)
		{
			text = "интернет не отвечает";
			c = Theme.Red;
			return;
		}
		int num6 = num / num2;
		if (num4 > 5)
		{
			text = "теряются пакеты — " + num4 + "%";
			c = Theme.Red;
		}
		else if (num6 <= 40 && num3 <= 8)
		{
			text = (Vpn ? "отлично (через VPN)" : "отлично для игр и звонков");
			c = Theme.Green;
		}
		else if (num6 <= 90 && num3 <= 25)
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
			if (r is Hashtable hashtable)
			{
				foreach (DictionaryEntry item in hashtable)
				{
					int num = Int(item.Key, -1);
					if (num >= 0)
					{
						_dnsMs[num] = Int(item.Value, -1);
					}
				}
			}
			int num2 = Fastest();
			if (num2 >= 0)
			{
				_host.Status("ok", "Быстрее всех отвечает " + _dnsList[num2].Name + " — " + _dnsMs[num2] + " мс");
			}
			Invalidate();
		});
	}

	private int Fastest()
	{
		int num = -1;
		foreach (KeyValuePair<int, int> dnsM in _dnsMs)
		{
			if (dnsM.Value >= 0 && (num < 0 || dnsM.Value < _dnsMs[num]))
			{
				num = dnsM.Key;
			}
		}
		return num;
	}

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
		int num = Theme.S(10f);
		int num2 = w - pad * 2;
		_headR = new Rectangle(pad, pad, num2, Theme.S(64f));
		int y2 = _headR.Bottom + Theme.S(16f);
		_cellsR = new Rectangle(pad, y2, num2, Theme.S(64f));
		y2 = _cellsR.Bottom + Theme.S(12f);
		if (Vpn)
		{
			_vpnR = new Rectangle(pad, y2, num2, Theme.S(54f));
			y2 = _vpnR.Bottom + Theme.S(12f);
		}
		else
		{
			_vpnR = Rectangle.Empty;
		}
		y2 = (_pingHead = y2 + Theme.S(10f)) + (Theme.S(28f) + Theme.S(10f));
		int num3 = Math.Max(1, _targets.Count);
		int num4 = (num2 - num * (num3 - 1)) / num3;
		for (int i = 0; i < _targets.Count; i++)
		{
			_targets[i].R = new Rectangle(pad + i * (num4 + num), y2, (i == num3 - 1) ? (num2 - (num4 + num) * (num3 - 1)) : num4, Theme.S(136f));
		}
		y2 = (_dnsHead = y2 + (Theme.S(136f) + Theme.S(22f))) + (Theme.S(28f) + Theme.S(10f));
		int num5 = Math.Max(2, Math.Min(5, (num2 + num) / (Theme.S(150f) + num)));
		int num6 = (num2 - num * (num5 - 1)) / num5;
		int num7 = Theme.S(100f);
		for (int j = 0; j < _dnsList.Count; j++)
		{
			int num8 = j % num5;
			int num9 = j / num5;
			_dnsList[j].R = new Rectangle(pad + num8 * (num6 + num), y2 + num9 * (num7 + num), num6, num7);
		}
		int num10 = (_dnsList.Count + num5 - 1) / num5;
		y2 += num10 * num7 + (num10 - 1) * num + Theme.S(12f);
		_noteR = new Rectangle(pad, y2, num2, Theme.S(18f));
		y2 = _noteR.Bottom;
		int num11 = y2 + pad;
		SetBounds(x, y, w, num11);
		RelayoutButtons();
		return num11;
	}

	private void Tile(Graphics g, Rectangle r, int alpha = 14, Color? border = null)
	{
		using GraphicsPath path = Theme.Round(r, Theme.S(12f));
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
		{
			g.FillPath(brush, path);
		}
		using Pen pen = new Pen(border ?? Color.FromArgb(34, 255, 255, 255), border.HasValue ? ((float)Theme.S(1.4f)) : 1f);
		g.DrawPath(pen, path);
	}

	private void SectionHead(Graphics g, int y, string text, string right, Color rc, int rightLimit)
	{
		int pad = base.Pad;
		TextRenderer.DrawText(g, text, Theme.Tiny, new Point(pad, y + Theme.S(7f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		int width = TextRenderer.MeasureText(g, text, Theme.Tiny, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
		int num = pad + width + Theme.S(12f);
		if (!string.IsNullOrEmpty(right))
		{
			int num2 = Theme.PillW(right, Theme.Tiny);
			Theme.Pill(g, new Rectangle(num, y + Theme.S(4f), num2, Theme.S(20f)), right, Theme.Tiny, rc, 26);
			num += num2 + Theme.S(12f);
		}
		int num3 = rightLimit - Theme.S(14f);
		if (num3 > num)
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(num, 0, num3 - num, 1), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(6, 255, 255, 255), 0f))
			{
				g.FillRectangle(brush, num, y + Theme.S(14f), num3 - num, 1);
			}
		}
	}

	private void ConnIcon(Graphics g, Rectangle r)
	{
		using (GraphicsPath path = Theme.Round(r, Theme.S(16f)))
		{
			Color c = (Online ? Color.FromArgb(70, 72, 84) : Color.FromArgb(60, 50, 52));
			Theme.FillGradient(g, path, r, c, Color.FromArgb(32, 32, 38));
			using Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255));
			g.DrawPath(pen, path);
		}
		if (_pinging || _dnsTesting)
		{
			float phase = _phase;
			int val = (int)(90f * (1f - phase));
			float num = (float)Theme.S(10f) * phase;
			using GraphicsPath path2 = Theme.Round(new RectangleF((float)r.X - num, (float)r.Y - num, (float)r.Width + num * 2f, (float)r.Height + num * 2f), (float)Theme.S(16f) + num);
			using Pen pen2 = new Pen(Color.FromArgb(Math.Max(0, val), 220, 220, 230), Theme.S(1.5f));
			g.DrawPath(pen2, path2);
		}
		float num2 = (float)r.X + (float)r.Width / 2f;
		float num3 = Theme.S(1f);
		Color color = (Online ? Theme.Silver1 : Theme.Red);
		Color color2 = Color.FromArgb(70, 255, 255, 255);
		if (Vpn)
		{
			float num4 = Theme.S(22f);
			RectangleF rect = new RectangleF((float)r.Right - num4 + (float)Theme.S(4f), (float)r.Bottom - num4 + (float)Theme.S(4f), num4, num4);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(125, 165, 235)))
			{
				g.FillEllipse(brush, rect);
			}
			using (Pen pen3 = new Pen(Theme.Card2, Theme.S(2f)))
			{
				g.DrawEllipse(pen3, rect);
			}
			using Pen pen4 = new Pen(Theme.OnSilver, Theme.S(1.5f));
			float num5 = rect.X + rect.Width / 2f;
			float num6 = rect.Y + rect.Height / 2f;
			g.DrawArc(pen4, num5 - 3.2f * num3, num6 - 6.5f * num3, 6.4f * num3, 7f * num3, 180f, 180f);
			using SolidBrush brush2 = new SolidBrush(Theme.OnSilver);
			g.FillRectangle(brush2, num5 - 4.6f * num3, num6 - 2.2f * num3, 9.2f * num3, 7f * num3);
		}
		if (!Online || Wifi)
		{
			int num7 = (Online ? Int(_info["Signal"], 100) : 0);
			if (num7 < 0)
			{
				num7 = 100;
			}
			float num8 = (float)r.Y + (float)r.Height * 0.7f;
			for (int i = 0; i < 3; i++)
			{
				float num9 = (float)(7 + i * 7) * num3;
				using Pen pen5 = new Pen((Online && num7 >= 15 + i * 30) ? color : color2, 2.6f * num3);
				pen5.StartCap = LineCap.Round;
				pen5.EndCap = LineCap.Round;
				g.DrawArc(pen5, num2 - num9, num8 - num9, num9 * 2f, num9 * 2f, 225f, 90f);
			}
			using SolidBrush brush3 = new SolidBrush(color);
			g.FillEllipse(brush3, num2 - 2.6f * num3, num8 - 2.6f * num3, 5.2f * num3, 5.2f * num3);
			return;
		}
		float num10 = 20f * num3;
		float num11 = 16f * num3;
		float num12 = num2 - num10 / 2f;
		float num13 = (float)r.Y + (float)r.Height / 2f - num11 / 2f - 2f * num3;
		using Pen pen6 = new Pen(color, 2.2f * num3);
		pen6.LineJoin = LineJoin.Round;
		g.DrawPolygon(pen6, new PointF[8]
		{
			new PointF(num12, num13),
			new PointF(num12 + num10, num13),
			new PointF(num12 + num10, num13 + num11 - 4f * num3),
			new PointF(num12 + num10 - 4f * num3, num13 + num11 - 4f * num3),
			new PointF(num12 + num10 - 4f * num3, num13 + num11),
			new PointF(num12 + 4f * num3, num13 + num11),
			new PointF(num12 + 4f * num3, num13 + num11 - 4f * num3),
			new PointF(num12, num13 + num11 - 4f * num3)
		});
		for (int j = 0; j < 3; j++)
		{
			float num14 = num12 + 6f * num3 + (float)j * 4f * num3;
			g.DrawLine(pen6, num14, num13 + 3.5f * num3, num14, num13 + 7f * num3);
		}
		g.DrawLine(pen6, num2, num13 + num11, num2, num13 + num11 + 6f * num3);
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
		int num = r.X + Theme.S(14f);
		TextRenderer.DrawText(g, label, Theme.Tiny, new Rectangle(num, r.Y + Theme.S(10f), r.Width - Theme.S(20f), Theme.S(16f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		using (Font font = Theme.F(11.5f, semi: true))
		{
			TextRenderer.DrawText(g, value, font, new Rectangle(num, r.Y + Theme.S(27f), r.Width - Theme.S(20f), Theme.S(22f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		if (!string.IsNullOrEmpty(hint))
		{
			int width = Theme.Measure(value, Theme.FC(11.5f, semi: true)).Width;
			int num2 = num + width + Theme.S(6f);
			hint = Theme.FitOrShort(hint, Theme.Tiny, r.Right - num2 - Theme.S(8f));
			if (hint != null && num2 < r.Right - Theme.S(30f))
			{
				TextRenderer.DrawText(g, hint, Theme.Tiny, new Rectangle(num2, r.Y + Theme.S(30f), r.Right - num2 - Theme.S(8f), Theme.S(18f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			}
		}
	}

	private void PingTile(Graphics g, Target t)
	{
		Rectangle r = t.R;
		Tile(g, r);
		Stats(t.Samples, out var _, out var min, out var avg, out var jit, out var loss);
		int num = r.X + Theme.S(14f);
		TextRenderer.DrawText(g, t.Label, Theme.Title, new Point(num, r.Y + Theme.S(11f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		int width = Theme.Measure(t.Label, Theme.Title).Width;
		string tsub = Theme.FitOrShort(t.Sub, Theme.Tiny, r.Right - num - width - Theme.S(90f));
		if (tsub != null)
		TextRenderer.DrawText(g, tsub, Theme.Tiny, new Rectangle(num + width + Theme.S(4f), r.Y + Theme.S(14f), r.Right - num - width - Theme.S(90f), Theme.S(16f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		string text = ((t.Samples.Count == 0) ? "—" : ((avg < 0) ? "нет" : (avg.ToString() ?? "")));
		Color fore = ((t.Samples.Count == 0) ? Theme.Dim : ((Vpn && t.Label != "Роутер" && avg >= 0 && avg <= 1) ? Theme.Yellow : MsColor(avg)));
		using (Font font = Theme.F(20f, semi: true))
		{
			Size size = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int num2 = r.Right - Theme.S(14f);
			string text2 = ((t.Samples.Count == 0 || avg < 0) ? "" : "мс");
			int num3 = ((text2.Length > 0) ? TextRenderer.MeasureText(g, text2, Theme.Small, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width : 0);
			if (num3 > 0)
			{
				TextRenderer.DrawText(g, text2, Theme.Small, new Point(num2 - num3, r.Y + Theme.S(18f)), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			TextRenderer.DrawText(g, text, font, new Point(num2 - num3 - Theme.S(3f) - size.Width, r.Y + Theme.S(6f)), fore, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		Rectangle r2 = new Rectangle(r.X + Theme.S(12f), r.Y + Theme.S(46f), r.Width - Theme.S(24f), Theme.S(52f));
		using (Pen pen = new Pen(Color.FromArgb(18, 255, 255, 255)))
		{
			pen.DashStyle = DashStyle.Dot;
			g.DrawLine(pen, r2.X, r2.Bottom, r2.Right, r2.Bottom);
			g.DrawLine(pen, r2.X, r2.Y + r2.Height / 2, r2.Right, r2.Y + r2.Height / 2);
		}
		int num4 = 30;
		foreach (int sample in t.Samples)
		{
			num4 = Math.Max(num4, sample);
		}
		num4 = (int)((float)num4 * 1.15f);
		if (t.Samples.Count > 0)
		{
			float num5 = (float)r2.Width / 19f;
			List<PointF> list = new List<PointF>();
			for (int i = 0; i < t.Samples.Count; i++)
			{
				int num6 = t.Samples[i];
				float num7 = (float)r2.X + (float)i * num5;
				if (num6 < 0)
				{
					using Pen pen2 = new Pen(Color.FromArgb(200, Theme.Red), Theme.S(2f));
					g.DrawLine(pen2, num7, r2.Bottom - Theme.S(8f), num7, r2.Bottom);
				}
				else
				{
					list.Add(new PointF(num7, (float)r2.Bottom - Math.Max(1f, (float)r2.Height * ((float)num6 / (float)num4))));
				}
			}
			Color color = ((Vpn && t.Label != "Роутер" && avg >= 0 && avg <= 1) ? Theme.Yellow : MsColor(avg));
			if (list.Count >= 2)
			{
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					graphicsPath.AddLine(list[0].X, r2.Bottom, list[0].X, list[0].Y);
					graphicsPath.AddLines(list.ToArray());
					graphicsPath.AddLine(list[list.Count - 1].X, list[list.Count - 1].Y, list[list.Count - 1].X, r2.Bottom);
					graphicsPath.CloseFigure();
					using LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(r2.X, r2.Y - 1, r2.Width, r2.Height + 2), Color.FromArgb(70, color), Color.FromArgb(0, color), 90f);
					g.FillPath(brush, graphicsPath);
				}
				using Pen pen3 = new Pen(color, Theme.S(1.8f));
				pen3.LineJoin = LineJoin.Round;
				g.DrawLines(pen3, list.ToArray());
			}
			if (list.Count > 0)
			{
				PointF pointF = list[list.Count - 1];
				float num8 = Theme.S(3.5f);
				if (_pinging)
				{
					float num9 = num8 + (float)Theme.S(6f) * _phase;
					using SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(110f * (1f - _phase)), color));
					g.FillEllipse(brush2, pointF.X - num9, pointF.Y - num9, num9 * 2f, num9 * 2f);
				}
				using SolidBrush brush3 = new SolidBrush(color);
				g.FillEllipse(brush3, pointF.X - num8, pointF.Y - num8, num8 * 2f, num8 * 2f);
			}
		}
		else
		{
			TextRenderer.DrawText(g, Online ? "Нажми «Проверить пинг»" : "Нет подключения", Theme.Small, r2, Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
		string text3 = ((t.Samples.Count == 0) ? "" : ("мин " + ((min < 0) ? "—" : min.ToString()) + "  •  разброс " + jit + "  •  потери " + loss + "%" + (_pinging ? ("  •  " + t.Samples.Count + "/" + 20) : "")));
		text3 = Theme.FitOrShort(text3, Theme.Tiny, r.Width - Theme.S(24f)) ?? "";
		TextRenderer.DrawText(g, text3, Theme.Tiny, new Rectangle(num, r.Bottom - Theme.S(28f), r.Width - Theme.S(24f), Theme.S(18f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
	}

	private void DnsTile(Graphics g, Dns d, int idx, int fastest)
	{
		Rectangle r = d.R;
		bool flag = d.Idx == _dns;
		bool flag2 = idx == _hotDns;
		Tile(g, r, flag ? 30 : (flag2 ? 22 : 12), flag ? new Color?(Color.FromArgb(200, Theme.Silver1)) : (flag2 ? new Color?(Color.FromArgb(70, 255, 255, 255)) : null));
		int num = r.X + Theme.S(14f);
		using (Font font = Theme.F(11.5f, semi: true))
		{
			TextRenderer.DrawText(g, d.Name, font, new Point(num, r.Y + Theme.S(12f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		if (flag)
		{
			float num2 = Theme.S(18f);
			RectangleF rect = new RectangleF((float)(r.Right - Theme.S(12f)) - num2, r.Y + Theme.S(12f), num2, num2);
			using (SolidBrush brush = new SolidBrush(Theme.Silver1))
			{
				g.FillEllipse(brush, rect);
			}
			float num3 = Theme.S(1f);
			using Pen pen = new Pen(Theme.OnSilver, Theme.S(1.8f));
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			g.DrawLines(pen, new PointF[3]
			{
				new PointF(rect.X + 5f * num3, rect.Y + 9.5f * num3),
				new PointF(rect.X + 8f * num3, rect.Y + 12.5f * num3),
				new PointF(rect.X + 13.5f * num3, rect.Y + 6.5f * num3)
			});
		}
		TextRenderer.DrawText(g, d.Ip, Theme.Small, new Rectangle(num, r.Y + Theme.S(34f), r.Width - Theme.S(24f), Theme.S(18f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, d.Tag, Theme.Tiny, new Rectangle(num, r.Y + Theme.S(53f), r.Width - Theme.S(24f), Theme.S(16f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		int num4 = r.Bottom - Theme.S(28f);
		if (_dnsTesting)
		{
			float num5 = (_phase + (float)idx * 0.15f) % 1f;
			int alpha = 20 + (int)(30f * (float)Math.Sin((double)num5 * Math.PI));
			using GraphicsPath path = Theme.Round(new RectangleF(num, num4 + Theme.S(3f), Theme.S(54f), Theme.S(14f)), Theme.S(7f));
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
			g.FillPath(brush2, path);
			return;
		}
		if (_dnsMs.TryGetValue(d.Idx, out var value))
		{
			string text = ((value < 0) ? "нет ответа" : (value + " мс"));
			Color c = ((value < 0) ? Theme.Red : ((d.Idx == fastest) ? Theme.Green : ((value <= 60) ? Theme.Silver2 : Theme.Yellow)));
			int num6 = Theme.PillW(text, Theme.Tiny);
			Theme.Pill(g, new Rectangle(num, num4, num6, Theme.S(20f)), text, Theme.Tiny, c, 26);
			if (d.Idx == fastest)
			{
				string text2 = "быстрее всех";
				int num7 = Theme.PillW(text2, Theme.Tiny);
				if (num + num6 + Theme.S(6f) + num7 < r.Right - Theme.S(8f))
				{
					Theme.Pill(g, new Rectangle(num + num6 + Theme.S(6f), num4, num7, Theme.S(20f)), text2, Theme.Tiny, Theme.Green, 18);
				}
			}
		}
		else if (d.Idx == 0 && _dnsMs.Count > 0)
		{
			Theme.Pill(g, new Rectangle(num, num4, Theme.PillW("не мерили", Theme.Tiny), Theme.S(20f)), "не мерили", Theme.Tiny, Theme.Dim, 18);
		}
		else if (flag)
		{
			Theme.Pill(g, new Rectangle(num, num4, Theme.PillW("сейчас", Theme.Tiny), Theme.S(20f)), "сейчас", Theme.Tiny, Theme.Silver1, 26);
		}
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		_ = base.Pad;
		Rectangle r = new Rectangle(_headR.X, _headR.Y, Theme.S(64f), Theme.S(64f));
		ConnIcon(g, r);
		int num = r.Right + Theme.S(16f);
		int num2 = _hideBtn.Left - Theme.S(14f);
		if (num2 < num + Theme.S(160f))
		{
			num2 = _headR.Right - Theme.S(220f);
		}
		string text;
		string text2;
		if (!_loaded)
		{
			text = "Проверяю подключение…";
			text2 = "";
		}
		else if (!Online)
		{
			text = "Нет подключения к интернету";
			text2 = "Проверь кабель или Wi-Fi. Если не помогает — «Инструменты → Сбросить сеть»";
		}
		else
		{
			string text3 = (_hide ? "" : Str(_info, "Ssid"));
			string text4 = ((!Phys) ? "" : (Wifi ? ("Wi-Fi" + ((text3.Length > 0) ? (" • " + text3) : "")) : "Ethernet • кабель"));
			if (Vpn)
			{
				string text5 = Str(_info, "VpnDesc");
				text = "VPN • " + ((text5.Length > 0) ? text5 : Str(_info, "VpnName"));
				text2 = (Phys ? ("Под туннелем: " + text4 + " — " + Str(_info, "Desc")) : "Физическое подключение не найдено");
			}
			else
			{
				text = text4;
				text2 = Str(_info, "Desc");
			}
		}
		using (Font font = Theme.F(15f, semi: true))
		{
			int height = Theme.Measure("Ag", font).Height;
			int num3 = _headR.Y + Theme.S(8f);
			int num4 = Math.Min(Theme.Measure(text, font).Width + 4, Math.Max(Theme.S(80f), num2 - num));
			TextRenderer.DrawText(g, text, font, new Rectangle(num, num3, num4, height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			if (_loaded)
			{
				string text6 = (Vpn ? "трафик через VPN" : (Online ? "подключено" : "нет сети"));
				int num5 = Theme.PillW(text6, Theme.Tiny);
				int num6 = num + num4 + Theme.S(10f);
				if (num6 + num5 < num2)
				{
					Rectangle r2 = new Rectangle(num6, num3 + (height - Theme.S(20f)) / 2 + Theme.S(1f), num5, Theme.S(20f));
					Theme.Pill(g, r2, text6, Theme.Tiny, Vpn ? Color.FromArgb(125, 165, 235) : (Online ? Theme.Green : Theme.Red), 26);
				}
			}
			TextRenderer.DrawText(g, text2, Theme.Small, new Rectangle(num, num3 + height + Theme.S(6f), num2 - num, Theme.S(20f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		int num7 = Theme.S(10f);
		int num8 = (_cellsR.Width - num7 * 3) / 4;
		string hint = ((Wifi && Int(_info["Signal"], -1) >= 0) ? ("сигнал " + Int(_info["Signal"], 0) + "%") : "");
		string text7 = (Phys ? Str(_info, "Dns") : "");
		string text8 = ((!Phys) ? "" : ((Str(_info, "Dhcp") == "Disabled") ? "вручную" : "по DHCP"));
		string text9 = (Phys ? Str(_info, "Gw") : "");
		Cell(g, new Rectangle(_cellsR.X, _cellsR.Y, num8, _cellsR.Height), "СКОРОСТЬ КАНАЛА", Phys ? SpeedText(Str(_info, "Speed")) : "—", hint);
		Cell(g, new Rectangle(_cellsR.X + (num8 + num7), _cellsR.Y, num8, _cellsR.Height), "IP-АДРЕС", Phys ? Mask(Str(_info, "Ip")) : "—", Vpn ? ("VPN " + Mask(Str(_info, "VpnIp"))) : text8);
		Cell(g, new Rectangle(_cellsR.X + (num8 + num7) * 2, _cellsR.Y, num8, _cellsR.Height), "РОУТЕР", (text9.Length > 0) ? Mask(text9) : "—", "");
		if (Vpn)
		{
			string text10 = Str(_info, "VpnDns");
			Cell(g, new Rectangle(_cellsR.X + (num8 + num7) * 3, _cellsR.Y, _cellsR.Width - (num8 + num7) * 3, _cellsR.Height), "DNS", "через VPN", (text10.Length > 0) ? Mask(text10.Split(',')[0].Trim()) : "");
			Rectangle vpnR = _vpnR;
			using (GraphicsPath path = Theme.Round(vpnR, Theme.S(12f)))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(22, 125, 165, 235)))
				{
					g.FillPath(brush, path);
				}
				using Pen pen = new Pen(Color.FromArgb(70, 125, 165, 235));
				g.DrawPath(pen, path);
			}
			TextRenderer.DrawText(g, "Включён VPN — весь трафик идёт через туннель", Theme.Body, new Rectangle(vpnR.X + Theme.S(16f), vpnR.Y + Theme.S(8f), vpnR.Width - Theme.S(32f), Theme.S(20f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(g, "Пинг и тест DNS идут через VPN («0 мс» — это отвечает сам VPN). Выбранный DNS может не действовать, пока VPN включён", Theme.Tiny, new Rectangle(vpnR.X + Theme.S(16f), vpnR.Y + Theme.S(30f), vpnR.Width - Theme.S(32f), Theme.S(18f)), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		else
		{
			Cell(g, new Rectangle(_cellsR.X + (num8 + num7) * 3, _cellsR.Y, _cellsR.Width - (num8 + num7) * 3, _cellsR.Height), "DNS", (text7.Length > 0) ? DnsName(text7) : "—", (text7.Length > 0) ? Mask(text7.Split(',')[0].Trim()) : "");
		}
		Verdict(out var text11, out var c);
		SectionHead(g, _pingHead, "ЗАДЕРЖКА", text11, c, _pingBtn.Left);
		foreach (Target target in _targets)
		{
			PingTile(g, target);
		}
		int fastest = Fastest();
		SectionHead(g, _dnsHead, "DNS-СЕРВЕР", (_dns >= 0 && _dns < _dnsList.Count) ? ("сейчас: " + _dnsList[_dns].Name) : null, Theme.Silver2, _dnsBtn.Left);
		for (int i = 0; i < _dnsList.Count; i++)
		{
			DnsTile(g, _dnsList[i], i, fastest);
		}
		TextRenderer.DrawText(g, Vpn ? "При включённом VPN DNS обычно задаёт сам VPN — выбор здесь сработает, когда VPN выключен. Смена DNS попадает в «Историю»" : "Нажми на сервер, чтобы переключиться. Смена DNS попадает в «Историю» и откатывается в один клик", Theme.Tiny, _noteR, Vpn ? Theme.Yellow : Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		for (int i = 0; i < _dnsList.Count; i++)
		{
			if (_dnsList[i].R.Contains(e.Location))
			{
				num = i;
			}
		}
		if (num != _hotDns)
		{
			_hotDns = num;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
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
		foreach (Dns dns in _dnsList)
		{
			if (dns.R.Contains(e.Location) && dns.Idx != _dns)
			{
				this.DnsPicked?.Invoke(dns.Idx);
				break;
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
