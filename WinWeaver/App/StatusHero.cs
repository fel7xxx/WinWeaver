using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinWeaver;

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

	public Hashtable Data;

	private bool _tilesRight;

	private static readonly Regex _ofRx = new Regex("^\\s*(\\d+)\\s+(\\S+\\s+\\d+)\\s*$");

	private static Font TitleF => Theme.FC(13f, semi: true);

	private int IconSize
	{
		get
		{
			if (!_ring)
			{
				return Theme.S(64f);
			}
			return Theme.S(112f);
		}
	}

	public event Action<int> TileClicked;

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
			_tiles[i] = new TileData
			{
				L = tileLabels[i]
			};
		}
		foreach (ActionDef actionDef in actions)
		{
			FButton fButton = new FButton
			{
				Text = actionDef.Text,
				Kind = actionDef.Kind,
				Glyph = actionDef.Glyph,
				Font = Theme.Body
			};
			fButton.AutoWidth(Theme.S(32f));
			fButton.Tag = actionDef;
			fButton.Click += delegate(object s, EventArgs e)
			{
				Run((ActionDef)((FButton)s).Tag, (FButton)s);
			};
			Buttons.Add(fButton);
			base.Controls.Add(fButton);
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
		}
		else if (_host.TryBegin(quick: false, a.Text))
		{
			b.Enabled = false;
			_host.Worker.Run(delegate(IEngine e)
			{
				object obj = ((a.Work != null) ? a.Work(e) : null);
				return new object[2]
				{
					obj,
					Fetch(e)
				};
			}, delegate(object o)
			{
				b.Enabled = true;
				object[] array = (object[])o;
				Apply(array[1]);
				a.Done?.Invoke(array[0]);
				_host.End();
			});
		}
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
		if (o != null && !(o.ToString() == ""))
		{
			return o.ToString();
		}
		return d;
	}

	public override void Apply(object v)
	{
		if (!(v is Hashtable hashtable))
		{
			return;
		}
		Data = hashtable;
		_title = Str(hashtable["Title"], _loadingTitle);
		_sub = Str(hashtable["Sub"]);
		_tone = Str(hashtable["Tone"], "info");
		try
		{
			_score = ((hashtable["Score"] != null) ? Convert.ToSingle(hashtable["Score"]) : (-1f));
		}
		catch
		{
			_score = -1f;
		}
		if (_score >= 0f)
		{
			_ringT.Target = _score;
		}
		object[] array = hashtable["Tiles"] as object[];
		if (array == null && hashtable["Tiles"] is Hashtable)
		{
			array = new object[1] { hashtable["Tiles"] };
		}
		if (array != null)
		{
			for (int i = 0; i < _tiles.Length && i < array.Length; i++)
			{
				if (array[i] is Hashtable hashtable2)
				{
					_tiles[i].L = Str(hashtable2["L"], _tiles[i].L);
					_tiles[i].V = Str(hashtable2["V"], "—");
					_tiles[i].H = Str(hashtable2["H"]);
					_tiles[i].T = Str(hashtable2["T"], "info");
					try
					{
						_tiles[i].P = ((hashtable2["P"] != null) ? Convert.ToSingle(hashtable2["P"]) : (-1f));
					}
					catch
					{
						_tiles[i].P = -1f;
					}
				}
			}
		}
		_loaded = true;
		Skeleton.Track(this, loading: false);
		Invalidate();
	}

	private int Cols(int w)
	{
		int num = w - base.Pad * 2;
		int num2 = Theme.S(10f);
		int num3 = _tiles.Length;
		if (num3 == 0)
		{
			return 1;
		}
		int num4 = num3;
		while (num4 > 1 && (num - num2 * (num4 - 1)) / num4 < Theme.S(150f))
		{
			num4 = ((num4 > 3 && num3 % 2 == 0) ? (num4 / 2) : (num4 - 1));
		}
		return num4;
	}

	private string Kicker()
	{
		Control parent = base.Parent;
		while (parent != null && !(parent is Page))
		{
			parent = parent.Parent;
		}
		if (!(parent is Page page))
		{
			return "";
		}
		bool flag = page.Tabs != null && page.Blocks.IndexOf(this) > page.Blocks.IndexOf(page.Tabs) && page.CurrentTab >= 0 && page.CurrentTab < page.Tabs.Items.Length;
		int num = 0;
		foreach (IBlock block in page.Blocks)
		{
			if (block is SettingCard settingCard && (!settingCard.IsExpert || Prefs.Expert) && (!flag || settingCard.Visible))
			{
				num++;
			}
		}
		string text = Theme.Up(flag ? page.Tabs.Items[page.CurrentTab] : page.Title);
		if (num > 0)
		{
			int num2 = num % 10;
			int num3 = num % 100;
			string s = ((num2 == 1 && num3 != 11) ? "параметр" : ((num2 >= 2 && num2 <= 4 && (num3 < 12 || num3 > 14)) ? "параметра" : "параметров"));
			text = text + " · " + num + " " + Theme.Up(s);
		}
		return text;
	}

	public override int DoLayout(int x, int y, int w)
	{
		int num = Theme.S(26f);
		int iconSize = IconSize;
		int num2 = Theme.S(10f);
		int num3 = Theme.S(142f);
		int num4 = Theme.S(100f);
		int num5 = _tiles.Length;
		int num6 = ((num5 > 0) ? (num5 * num3 + (num5 - 1) * num2) : 0);
		_tilesRight = num5 > 0 && w - num * 2 - iconSize - Theme.S(24f) - num6 - Theme.S(28f) >= Theme.S(280f);
		_iconR = new Rectangle(num, num, iconSize, iconSize);
		int num7 = _iconR.Right + Theme.S(24f);
		int num8 = (_tilesRight ? (w - num - num6 - Theme.S(28f)) : (w - num));
		int width = Math.Max(Theme.S(120f), num8 - num7);
		int height = Theme.Measure("A", Theme.Tiny).Height;
		int num9 = Math.Min(Theme.Measure(Theme.Up(_title ?? ""), TitleF, width, wrap: true).Height, Theme.Measure("A\nA", TitleF).Height);
		int num10 = ((!string.IsNullOrEmpty(_sub)) ? Math.Min(Theme.Measure(_sub, Theme.Small, width, wrap: true).Height, Theme.Measure("A\nA\nA", Theme.Small).Height) : 0);
		int num11 = height + Theme.S(10f) + num9 + Theme.S(10f) + num10;
		int num12 = num + num11 + Theme.S(14f);
		if (Buttons.Count > 0)
		{
			int num13 = num7;
			int num14 = Theme.S(36f);
			foreach (FButton button in Buttons)
			{
				if (num13 + button.Width > num8 && num13 > num7)
				{
					num13 = num7;
					num12 += num14 + Theme.S(8f);
				}
				button.SetBounds(num13, num12, button.Width, num14);
				num13 += button.Width + Theme.S(8f);
			}
			_btnY = num12;
			num12 += num14 + Theme.S(14f);
		}
		int num15 = Math.Max(iconSize + num * 2, num12 - Theme.S(14f) + num);
		int num16 = num12 - Theme.S(14f) - num;
		int num17 = Math.Max(0, (iconSize - num16) / 2);
		if (num17 > 0)
		{
			foreach (FButton button2 in Buttons)
			{
				button2.Top += num17;
			}
		}
		_textR = new Rectangle(num7, num + num17, width, num11);
		int num18;
		if (_tilesRight)
		{
			num18 = Math.Max(num15, num4 + num * 2);
			int num19 = w - num - num6;
			int y2 = (num18 - num4) / 2;
			for (int i = 0; i < num5; i++)
			{
				_tiles[i].R = new Rectangle(num19 + i * (num3 + num2), y2, num3, num4);
			}
		}
		else
		{
			int num20 = num15 - num + Theme.S(18f);
			int num21 = Cols(w);
			int num22 = (num5 + num21 - 1) / Math.Max(1, num21);
			int num23 = (w - num * 2 - num2 * (num21 - 1)) / Math.Max(1, num21);
			int num24 = Theme.S(92f);
			for (int j = 0; j < num5; j++)
			{
				int num25 = j % num21;
				int num26 = j / num21;
				int width2 = ((num25 == num21 - 1) ? (w - num * 2 - (num23 + num2) * (num21 - 1)) : num23);
				_tiles[j].R = new Rectangle(num + num25 * (num23 + num2), num20 + num26 * (num24 + num2), width2, num24);
			}
			num18 = ((num5 > 0) ? (num20 + num22 * (num24 + num2) - num2 + num) : num15);
		}
		_iconR.Y = (_tilesRight ? ((num18 - iconSize) / 2) : num);
		SetBounds(x, y, w, num18);
		Invalidate();
		return num18;
	}

	public override void PaintSurface(Graphics g)
	{
		Theme.Card(g, base.ClientRectangle, 0f, 17f);
	}

	private static Color ToneC(string t)
	{
		return t switch
		{
			"ok" => Theme.Green, 
			"warn" => Theme.Yellow, 
			"err" => Theme.Red, 
			_ => Theme.Silver2, 
		};
	}

	private void PaintRing(Graphics g)
	{
		Rectangle iconR = _iconR;
		float num = (float)iconR.X + (float)iconR.Width / 2f;
		float num2 = (float)iconR.Y + (float)iconR.Height / 2f;
		float num3 = (float)iconR.Width / 2f - (float)Theme.S(6f);
		float num5 = Math.Max(0f, Math.Min(1f, _ringT.Value));
		Theme.Gauge(g, num, num2, num3, num5, Theme.S(9f));
		if (_score >= 0f)
		{
			string s = ((int)Math.Round(num5 * 100f)).ToString();
			float num13 = Theme.S(3.6f);
			float num14 = Theme.DotTextW(s, num13);
			Theme.DotText(g, s, num - num14 / 2f, num2 - num13 * 7f / 2f - (float)Theme.S(6f), num13, Color.White);
			string text = Theme.Up("Индекс");
			Size size = Theme.Measure(text, Theme.FC(6f));
			TextRenderer.DrawText(g, text, Theme.FC(6f), new Point((int)(num - (float)size.Width / 2f), (int)(num2 + num13 * 3.5f - (float)Theme.S(1f))), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
	}

	protected override void PaintContent(Graphics g)
	{
		Theme.Smooth(g);
		if (!_loaded)
		{
			_ = Theme.Silver2;
		}
		else
		{
			ToneC(_tone);
		}
		if (_ring && _score >= 0f)
		{
			PaintRing(g);
		}
		else
		{
			RectangleF rect = new RectangleF((float)_iconR.X + 0.75f, (float)_iconR.Y + 0.75f, (float)_iconR.Width - 1.5f, (float)_iconR.Height - 1.5f);
			using (Pen pen = new Pen(Theme.BorderHi, Theme.BW))
			{
				g.DrawEllipse(pen, rect);
			}
			Theme.DrawIcon(g, _glyph, Theme.IconBig, _iconR, Color.White);
			if (_loaded && (_tone == "warn" || _tone == "err"))
			{
				Theme.Dot(g, _iconR.Right - Theme.S(8f), _iconR.Y + Theme.S(8f), Theme.S(4f), Theme.Accent);
			}
		}
		int x = _textR.X;
		int y = _textR.Y;
		int width = _textR.Width;
		string text = Kicker();
		int height = Theme.Measure("A", Theme.Tiny).Height;
		TextRenderer.DrawText(g, text, Theme.Tiny, new Rectangle(x, y, width, height + 2), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		y += height + Theme.S(10f);
		string text2 = Theme.Up(_title ?? "");
		int num = Math.Min(Theme.Measure(text2, TitleF, width, wrap: true).Height, Theme.Measure("A\nA", TitleF).Height);
		TextRenderer.DrawText(g, text2, TitleF, new Rectangle(x, y, width, num + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		y += num + Theme.S(10f);
		if (!string.IsNullOrEmpty(_sub))
		{
			int num2 = Math.Min(Theme.Measure(_sub, Theme.Small, width, wrap: true).Height, Theme.Measure("A\nA\nA", Theme.Small).Height);
			TextRenderer.DrawText(g, _sub, Theme.Small, new Rectangle(x, y, width, num2 + 2), Theme.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		}
		int num3 = CurrentTab();
		for (int i = 0; i < _tiles.Length; i++)
		{
			PaintTile(g, _tiles[i], i == _hot && this.TileClicked != null, i == num3);
		}
	}

	private int CurrentTab()
	{
		if (this.TileClicked == null)
		{
			return -1;
		}
		Control parent = base.Parent;
		while (parent != null && !(parent is Page))
		{
			parent = parent.Parent;
		}
		if (!(parent is Page { Tabs: not null } page) || page.Tabs.Items.Length != _tiles.Length)
		{
			return -1;
		}
		return page.CurrentTab;
	}

	private void PaintTile(Graphics g, TileData t, bool hot, bool cur)
	{
		Rectangle r = t.R;
		using (GraphicsPath path = Theme.Round(new RectangleF((float)r.X + 0.75f, (float)r.Y + 0.75f, (float)r.Width - 1.5f, (float)r.Height - 1.5f), Theme.S(22f)))
		{
			if (hot)
			{
				using SolidBrush brush = new SolidBrush(Theme.Hover);
				g.FillPath(brush, path);
			}
			if (cur)
			{
				using SolidBrush cb = new SolidBrush(Color.FromArgb(29, 29, 35));
				g.FillPath(cb, path);
			}
			using Pen pen = new Pen(cur ? Theme.Accent : (hot ? Color.FromArgb(60, 60, 68) : Theme.Border), Theme.BW);
			g.DrawPath(pen, path);
		}
		int num = Theme.S(15f);
		int num2 = r.X + num;
		int num3 = r.Width - num * 2;
		TextRenderer.DrawText(g, Theme.Up(t.L), Theme.FC(6.5f), new Rectangle(num2, r.Y + Theme.S(13f), num3, Theme.S(14f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		int num4 = r.Y + Theme.S(36f);
		if (!_loaded)
		{
			Skeleton.Bar(g, new RectangleF(num2, num4 + Theme.S(4f), Math.Min(num3, Theme.S(80f)), Theme.S(14f)), this);
			return;
		}
		Match match = _ofRx.Match(t.V ?? "");
		if (match.Success)
		{
			float num5 = Theme.S(3.4f);
			float num6 = Theme.DotText(g, match.Groups[1].Value, num2, num4, num5, Color.White);
			TextRenderer.DrawText(g, Theme.Up(match.Groups[2].Value), Theme.FC(6.5f), new Point(num2 + (int)num6 + Theme.S(7f), num4 + (int)(num5 * 7f) - Theme.Measure("A", Theme.FC(6.5f)).Height), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		else
		{
			TextRenderer.DrawText(g, Theme.Up(t.V), Theme.FC(9f, semi: true), new Rectangle(num2, num4 + Theme.S(4f), num3, Theme.S(20f)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		float num7 = r.Bottom - Theme.S(17f);
		if (t.P >= 0f)
		{
			int num8 = 8;
			float num9 = Theme.S(5.5f);
			float val = Math.Max(Theme.S(3f), ((float)num3 - (float)num8 * num9) / (float)(num8 - 1));
			val = Math.Min(val, Theme.S(6f));
			int num10 = (int)Math.Round(Math.Min(1f, t.P) * (float)num8);
			bool flag = t.T == "warn" || t.T == "err";
			using SolidBrush solidBrush = new SolidBrush(Color.White);
			using SolidBrush solidBrush2 = new SolidBrush(Color.FromArgb(44, 44, 44));
			for (int i = 0; i < num8; i++)
			{
				float num11 = (float)num2 + (float)i * (num9 + val);
				if (flag && i == num10)
				{
					Theme.Dot(g, num11 + num9 / 2f, num7, num9 / 2f, Theme.Accent, 0.6f);
				}
				else
				{
					g.FillEllipse((i < num10) ? solidBrush : solidBrush2, num11, num7 - num9 / 2f, num9, num9);
				}
			}
		}
		else if (!string.IsNullOrEmpty(t.H))
		{
			TextRenderer.DrawText(g, Theme.Up(t.H), Theme.FC(6.5f), new Rectangle(num2, (int)num7 - Theme.S(7f), num3, Theme.S(14f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
		if (!hot)
		{
			return;
		}
		using Pen pen2 = new Pen(Theme.Sub, Math.Max(1.4f, Theme.S(1.5f)))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		float num12 = r.Right - Theme.S(18f);
		float num13 = r.Y + Theme.S(19f);
		float num14 = Theme.S(3.5f);
		g.DrawLines(pen2, new PointF[3]
		{
			new PointF(num12 - num14 / 2f, num13 - num14),
			new PointF(num12 + num14 / 2f, num13),
			new PointF(num12 - num14 / 2f, num13 + num14)
		});
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		if (this.TileClicked != null)
		{
			for (int i = 0; i < _tiles.Length; i++)
			{
				if (_tiles[i].R.Contains(e.Location))
				{
					num = i;
				}
			}
		}
		if (num != _hot)
		{
			_hot = num;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
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
			this.TileClicked?.Invoke(_hot);
		}
		base.OnMouseClick(e);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Skeleton.Track(this, !_loaded);
	}
}
