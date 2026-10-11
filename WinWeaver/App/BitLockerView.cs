using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace WinWeaver;

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

		public double Rate;

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

	private bool _wasBusy;

	private int _w;

	private int RowH => Theme.S(62f);

	public event Action DecryptClicked;

	public event Action BlockClicked;

	public event Action<string, string, float, bool> Progress;

	public BitLockerView(Host host)
	{
		_host = host;
		_decrypt = new FButton
		{
			Text = "Расшифровать все диски",
			Kind = BtnKind.Silver,
			Font = Theme.Body,
			Glyph = "\ue72e"
		};
		_decrypt.AutoWidth(Theme.S(30f));
		_decrypt.Click += delegate
		{
			this.DecryptClicked?.Invoke();
		};
		_block = new FButton
		{
			Text = "Запретить автошифрование",
			Kind = BtnKind.Ghost,
			Font = Theme.Body
		};
		_block.AutoWidth(Theme.S(30f));
		_block.Click += delegate
		{
			this.BlockClicked?.Invoke();
		};
		base.Controls.Add(_decrypt);
		base.Controls.Add(_block);
		_decrypt.Enabled = false;
		_block.Enabled = false;
		_poll = new Timer
		{
			Interval = 4000
		};
		_poll.Tick += delegate
		{
			Poll();
		};
		_shine = new Timer
		{
			Interval = 33
		};
		_shine.Tick += delegate
		{
			_phase = (float)(Anim.Now % 2500.0 / 2500.0);
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
		if (!(s == "DecryptionInProgress"))
		{
			return s == "EncryptionInProgress";
		}
		return true;
	}

	private bool AnyBusy()
	{
		foreach (Vol vol in _vols)
		{
			if (Busy(vol.Status))
			{
				return true;
			}
		}
		return false;
	}

	private void Poll()
	{
		if (!_polling && !_host.IsBusy && _host.Worker != null)
		{
			_polling = true;
			_host.Worker.Run((IEngine e) => Fetch(e), delegate(object v)
			{
				_polling = false;
				Apply(v);
			});
		}
	}

	public override object Fetch(IEngine e)
	{
		return e.Eval("Get-FBLInfo");
	}

	public override void Apply(object v)
	{
		if (!(v is Hashtable hashtable))
		{
			return;
		}
		_auto = hashtable["Auto"] is bool && (bool)hashtable["Auto"];
		object[] array = hashtable["Vols"] as object[];
		if (array == null && hashtable["Vols"] is Hashtable)
		{
			array = new object[1] { hashtable["Vols"] };
		}
		DateTime now = DateTime.Now;
		double num = ((_prevAt == DateTime.MinValue) ? 0.0 : (now - _prevAt).TotalMinutes);
		Dictionary<string, Vol> dictionary = new Dictionary<string, Vol>();
		foreach (Vol vol2 in _vols)
		{
			dictionary[vol2.Drive] = vol2;
		}
		int count = _vols.Count;
		_vols.Clear();
		if (array != null)
		{
			object[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (!(array2[i] is Hashtable hashtable2))
				{
					continue;
				}
				Vol vol = new Vol
				{
					Drive = (hashtable2["Drive"] ?? "?").ToString(),
					Status = (hashtable2["Status"] ?? "").ToString(),
					Pct = Num(hashtable2["Pct"]),
					Size = Num(hashtable2["Size"]),
					Method = (hashtable2["Method"] ?? "").ToString()
				};
				if (dictionary.TryGetValue(vol.Drive, out var value))
				{
					vol.Shown = value.Shown;
					vol.Rate = value.Rate;
					if (num > 0.02 && Busy(vol.Status))
					{
						double num2 = Math.Abs(vol.Pct - value.Pct) / num;
						vol.Rate = ((value.Rate <= 0.0) ? num2 : (value.Rate * 0.6 + num2 * 0.4));
					}
				}
				else
				{
					vol.Shown = new Tween(this, (float)(vol.Pct / 100.0), 0.12f).Timed(1200);
				}
				vol.Shown.Target = (float)(vol.Pct / 100.0);
				_vols.Add(vol);
			}
		}
		_prevAt = now;
		_loaded = true;
		LayoutRows();
		bool enabled = false;
		foreach (Vol vol3 in _vols)
		{
			if ((vol3.Status != "FullyDecrypted" && !Busy(vol3.Status)) || vol3.Status == "EncryptionInProgress")
			{
				enabled = true;
			}
		}
		_decrypt.Enabled = enabled;
		_block.Enabled = !_auto;
		_block.Text = (_auto ? "Автошифрование запрещено" : "Запретить автошифрование");
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
		if (count != _vols.Count && base.Parent is Canvas)
		{
			((Canvas)base.Parent).Page.RelayoutSoon();
		}
		ReportProgress();
		Invalidate();
	}

	private void LayoutRows()
	{
		int num = ((_w > 0) ? _w : base.Width);
		for (int i = 0; i < _vols.Count; i++)
		{
			_vols[i].R = new Rectangle(base.Pad, _listTop + i * (RowH + Theme.S(8f)), num - base.Pad * 2, RowH);
		}
	}

	private void ReportProgress()
	{
		Action<string, string, float, bool> progress = this.Progress;
		if (progress == null)
		{
			return;
		}
		foreach (Vol vol in _vols)
		{
			if (Busy(vol.Status))
			{
				bool flag = vol.Status == "DecryptionInProgress";
				double num = (flag ? (100.0 - vol.Pct) : vol.Pct);
				string text = (flag ? "расшифровано " : "зашифровано ") + (int)Math.Round(num) + "%";
				if (vol.Rate > 0.01)
				{
					text = text + "  •  осталось ≈ " + Eta((flag ? vol.Pct : (100.0 - vol.Pct)) / vol.Rate);
				}
				text += "  •  можно пользоваться ПК";
				progress((flag ? "Расшифровка BitLocker — диск " : "Шифрование BitLocker — диск ") + vol.Drive, text, (float)(num / 100.0), arg4: true);
				_wasBusy = true;
				return;
			}
		}
		if (_wasBusy)
		{
			_wasBusy = false;
			progress("BitLocker", null, 1f, arg4: false);
		}
	}

	private static double Num(object o)
	{
		try
		{
			return (o == null) ? 0.0 : Convert.ToDouble(o, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0.0;
		}
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		_headR = new Rectangle(pad, pad, w - pad * 2, Theme.S(48f));
		int num = _headR.Bottom + Theme.S(14f);
		_decrypt.Location = new Point(pad, num);
		_block.Location = new Point(_decrypt.Right + Theme.S(8f), num);
		num = (_listTop = num + (_decrypt.Height + Theme.S(16f)));
		_w = w;
		int num2 = ((!_loaded) ? 1 : Math.Max(1, _vols.Count));
		LayoutRows();
		num += num2 * (RowH + Theme.S(8f)) - Theme.S(8f);
		int num3 = num + pad;
		SetBounds(x, y, w, num3);
		return num3;
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
			if (!(v.Status == ""))
			{
				return v.Status;
			}
			return "Нет данных";
		}
	}

	private string Summary()
	{
		if (!_loaded)
		{
			return "Проверяю диски…";
		}
		foreach (Vol vol in _vols)
		{
			if (vol.Status == "DecryptionInProgress")
			{
				return "Идёт расшифровка " + vol.Drive + " — " + (int)Math.Round(100.0 - vol.Pct) + "% готово. Компьютером можно пользоваться";
			}
			if (vol.Status == "EncryptionInProgress")
			{
				return "Идёт шифрование " + vol.Drive + " — " + (int)Math.Round(vol.Pct) + "%";
			}
		}
		int num = 0;
		foreach (Vol vol2 in _vols)
		{
			if (vol2.Status != "FullyDecrypted")
			{
				num++;
			}
		}
		string text = (_auto ? "автошифрование запрещено" : "Windows может зашифровать диск сама");
		if (_vols.Count == 0)
		{
			return "BitLocker недоступен в этой редакции Windows";
		}
		return ((num == 0) ? "Диски не зашифрованы" : ("Зашифровано дисков: " + num)) + " • " + text;
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		int num = Theme.S(44f);
		bool flag = AnyBusy();
		Theme.IconBox(g, new Rectangle(_headR.X, _headR.Y + (_headR.Height - num) / 2, num, num), "\ue72e", flag);
		int num2 = _headR.X + num + Theme.S(14f);
		int height = Theme.Measure("Ag", Theme.Title).Height;
		int num3 = _headR.Y + (_headR.Height - height - Theme.S(22f)) / 2;
		TextRenderer.DrawText(g, "BitLocker", Theme.Title, new Point(num2, num3), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, Summary(), Theme.Small, new Rectangle(num2, num3 + height + Theme.S(4f), _headR.Right - num2, Theme.S(20f)), flag ? Theme.Yellow : Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		if (!_loaded)
		{
			Rectangle r = new Rectangle(base.Pad, _listTop, base.Width - base.Pad * 2, RowH);
			Skeleton.Row(g, r, this);
			return;
		}
		foreach (Vol vol in _vols)
		{
			Rectangle r2 = vol.R;
			using (GraphicsPath path = Theme.Round(r2, Theme.S(12f)))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
				{
					g.FillPath(brush, path);
				}
				using Pen pen = new Pen(Color.FromArgb(30, 255, 255, 255));
				g.DrawPath(pen, path);
			}
			Rectangle rectangle = new Rectangle(r2.X + Theme.S(12f), r2.Y + (r2.Height - Theme.S(38f)) / 2, Theme.S(38f), Theme.S(38f));
			using (GraphicsPath path2 = Theme.Round(rectangle, Theme.S(9f)))
			{
				Theme.FillGradient(g, path2, rectangle, Color.FromArgb(62, 62, 72), Color.FromArgb(40, 40, 47));
			}
			using (Font font = Theme.F(11f, semi: true))
			{
				TextRenderer.DrawText(g, vol.Drive.TrimEnd(':', '\\'), font, rectangle, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			}
			int num4 = rectangle.Right + Theme.S(14f);
			int num5 = r2.Right - Theme.S(16f);
			bool flag2 = Busy(vol.Status);
			bool flag3 = vol.Status != "FullyDecrypted";
			Color c = (flag2 ? Theme.Yellow : (flag3 ? Theme.Silver1 : Theme.Green));
			string text = "Диск " + vol.Drive + ((vol.Size > 0.0) ? ("  •  " + CleanView.Fmt(vol.Size)) : "");
			TextRenderer.DrawText(g, text, Theme.Body, new Point(num4, r2.Y + Theme.S(11f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			string text2 = StatusText(vol);
			int num6 = Theme.PillW(text2, Theme.Tiny);
			Theme.Pill(g, new Rectangle(num5 - num6, r2.Y + Theme.S(9f), num6, Theme.S(20f)), text2, Theme.Tiny, c, 22);
			float num7 = ((vol.Shown != null) ? vol.Shown.Value : ((float)(vol.Pct / 100.0)));
			float num8;
			string text3;
			if (vol.Status == "DecryptionInProgress")
			{
				num8 = 1f - num7;
				text3 = "расшифровано " + (int)Math.Round(num8 * 100f) + "%";
				if (vol.Rate > 0.01)
				{
					text3 = text3 + "  •  осталось ≈ " + Eta(vol.Pct / vol.Rate);
				}
			}
			else if (vol.Status == "EncryptionInProgress")
			{
				num8 = num7;
				text3 = "зашифровано " + (int)Math.Round(num8 * 100f) + "%";
				if (vol.Rate > 0.01)
				{
					text3 = text3 + "  •  осталось ≈ " + Eta((100.0 - vol.Pct) / vol.Rate);
				}
			}
			else
			{
				num8 = (flag3 ? num7 : 0f);
				text3 = (flag3 ? ("зашифровано " + (int)Math.Round(vol.Pct) + "%" + ((vol.Method != "" && vol.Method != "None") ? ("  •  " + vol.Method) : "")) : "данные хранятся без шифрования");
			}
			RectangleF r3 = new RectangleF(num4, r2.Bottom - Theme.S(20f), num5 - num4 - Theme.S(220f), Theme.S(6f));
			if (flag2)
			{
				Theme.Progress(g, r3, num8, _phase);
			}
			else
			{
				using (GraphicsPath path3 = Theme.Round(r3, r3.Height / 2f))
				{
					using SolidBrush brush2 = new SolidBrush(Color.FromArgb(34, 255, 255, 255));
					g.FillPath(brush2, path3);
				}
				if (num8 > 0.005f)
				{
					using GraphicsPath path4 = Theme.Round(new RectangleF(r3.X, r3.Y, Math.Max(r3.Height, r3.Width * num8), r3.Height), r3.Height / 2f);
					using SolidBrush brush3 = new SolidBrush(flag3 ? Theme.Silver2 : Theme.Green);
					g.FillPath(brush3, path4);
				}
			}
			TextRenderer.DrawText(g, text3, Theme.Small, new Rectangle((int)r3.Right + Theme.S(12f), (int)r3.Y - Theme.S(8f), num5 - (int)r3.Right - Theme.S(12f), Theme.S(22f)), flag2 ? Theme.Text : Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	private static string Eta(double min)
	{
		if (min < 1.0)
		{
			return "меньше минуты";
		}
		if (min < 90.0)
		{
			return (int)Math.Ceiling(min) + " мин";
		}
		return (min / 60.0).ToString("0.#", L.Culture) + (L.IsRu ? " ч" : " h");
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Skeleton.Track(this, !_loaded);
	}
}
