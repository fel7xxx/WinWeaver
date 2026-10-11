using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinWeaver;

internal class SettingCard : Card
{
	public string Title;

	public string Desc;

	public string GlyphText;

	public ToggleSwitch Toggle;

	public Segment Seg;

	private readonly Func<IEngine, object> _get;

	private readonly Action<IEngine, object> _set;

	private readonly Host _host;

	private Rectangle _textR;

	private bool _segBelow;

	public string What;

	public string Pro;

	public string Con;

	public string Why;

	public object Rec;

	public int MinBuild;

	public int MaxBuild;

	public string GetScript;

	public string SetScript;

	public string Cat;

	/// <summary>Из какой вкладки настройка (для «Рекомендованных»).</summary>
	public string Origin;

	public Color OriginColor;

	public bool IsExpert;

	private object _cur;

	private bool _expanded;

	private float _expT;

	private int _baseH;

	private int _detH;

	private Rectangle _pillR;

	private Rectangle _chevR;

	private bool _pillInline;

	private int _titleH;

	private int _descH;

	public string[] OptDescs;

	private static readonly Font OptNameF = Theme.FC(8.5f, semi: true);

	public object[] FpsBest;

	public int FpsW;

	private Rectangle _statR;

	private int _statW;

	private static readonly HashSet<SettingCard> _blinking = new HashSet<SettingCard>();

	private static Timer _blinkTimer;

	private Rectangle _statDot;

	private const string HWhat = "ЧТО ДЕЛАЕТ";

	private const string HPro = "ПОЧЕМУ СТОИТ";

	private const string HCon = "ПОЧЕМУ МОЖЕТ НЕ ПОДОЙТИ";

	private SettingMeta _meta;

	private bool _metaRead;

	private string _tweakId;

	private List<string[]> _plan;

	private string _planFlag = "";

	private int _planState;

	private bool _tech;

	private int _typeT0;

	private Timer _typeTimer;

	private Rectangle _techBtnR;

	private bool _techHot;

	private const string HWhy = "ПОЧЕМУ СТОИТ";

	private const string HNot = "МОЖЕТ НЕ ПОДОЙТИ";

	private const string HChange = "ЧТО ИЗМЕНИТСЯ";

	private const string HRec = "РЕКОМЕНДАЦИЯ";

	private const string HUndo = "ОТКАТ";

	private const string UndoText = "Прежнее значение сохраняется в «Историю». Вернуть можно здесь или там — по одному параметру или всё сразу.";

	internal Control InspectorHost;

	internal bool InList;

	private int _planT0;

	internal PointF CursorAt = new PointF(float.NaN, float.NaN);

	public bool HasInfo => What != null;

	public bool Unsupported
	{
		get
		{
			if (MinBuild <= 0 || Win.Build >= MinBuild)
			{
				if (MaxBuild > 0)
				{
					return Win.Build > MaxBuild;
				}
				return false;
			}
			return true;
		}
	}

	public object Current => _cur;

	private bool HasOpts
	{
		get
		{
			if (OptDescs != null && Seg != null)
			{
				return OptDescs.Length != 0;
			}
			return false;
		}
	}

	public bool? Matches
	{
		get
		{
			if (Rec == null || _cur == null || Unsupported || GetScript == null)
			{
				return null;
			}
			return Same(_cur, Rec);
		}
	}

	public bool? FpsOk
	{
		get
		{
			if (FpsBest == null || FpsW <= 0 || _cur == null || Unsupported || GetScript == null)
			{
				return null;
			}
			object[] fpsBest = FpsBest;
			foreach (object b in fpsBest)
			{
				if (Same(_cur, b))
				{
					return true;
				}
			}
			return false;
		}
	}

	private static Font TagF => Theme.FC(6.5f, semi: true);

	private static Font StatF => Theme.FC(7.5f);

	private bool ShowPill
	{
		get
		{
			if (!HasInfo)
			{
				return Unsupported;
			}
			return true;
		}
	}

	private bool PillCheck => false;

	public bool Expanded
	{
		get
		{
			if (!_expanded)
			{
				if (InList)
				{
					return OwnerPage()?.Selected == this;
				}
				return false;
			}
			return true;
		}
	}

	private SettingMeta Meta
	{
		get
		{
			if (!_metaRead)
			{
				_metaRead = true;
				_meta = SettingMeta.For(Title);
				Match match = Regex.Match(SetScript ?? "", "^Set-FTweak '(\\w+)'");
				_tweakId = (match.Success ? match.Groups[1].Value : null);
			}
			return _meta;
		}
	}

	private bool InspOpen
	{
		get
		{
			if (!_expanded)
			{
				return InspectorHost != null;
			}
			return true;
		}
	}

	private bool HasTech
	{
		get
		{
			if (Meta != null)
			{
				return !string.IsNullOrEmpty(Meta.Tech);
			}
			return false;
		}
	}

	private bool CanPlan
	{
		get
		{
			SettingMeta meta = Meta;
			if (_tweakId == null)
			{
				if (meta != null && meta.Writes != null)
				{
					return Toggle != null;
				}
				return false;
			}
			return true;
		}
	}

	private string DescText
	{
		get
		{
			if (!_tech || !HasTech)
			{
				return What;
			}
			return Meta.Tech;
		}
	}

	private static Font FInsp => Theme.FC(8f);

	private static Font FMono => Theme.FC(7.5f);

	private int Reboot
	{
		get
		{
			if (_planFlag == "rb")
			{
				return 2;
			}
			if (_planFlag == "ex")
			{
				return 1;
			}
			if (Meta == null)
			{
				return -1;
			}
			return Meta.Reboot;
		}
	}

	private int Risk
	{
		get
		{
			if (Meta != null && Meta.Risk >= 0)
			{
				return Meta.Risk;
			}
			if (!IsExpert)
			{
				return -1;
			}
			return 3;
		}
	}

	private bool HasMetrics
	{
		get
		{
			if ((Meta == null || Meta.Impact < 0) && Risk < 0)
			{
				return Reboot >= 0;
			}
			return true;
		}
	}

	private int TileH => Theme.S(62f);

	private int RowH => Theme.Measure("Ag", FMono).Height + Theme.S(5f);

	private static Font PanelTitleF => Theme.FC(12.5f, semi: true);

	internal bool PanelHasTech => HasTech;

	internal bool PanelTech => _tech;

	public SettingCard Expert()
	{
		IsExpert = true;
		return this;
	}

	public SettingCard(Host host, string title, string desc, Func<IEngine, object> get, Action<IEngine, object> set, string[] options = null, string glyph = null)
	{
		_host = host;
		Title = title;
		Desc = desc;
		_get = get;
		_set = set;
		GlyphText = glyph;
		Cursor = Cursors.Default;
		if (options == null)
		{
			Toggle = new ToggleSwitch();
			base.Controls.Add(Toggle);
			Toggle.Toggled += delegate
			{
				OnToggle();
			};
			return;
		}
		Seg = new Segment
		{
			Items = options
		};
		base.Controls.Add(Seg);
		Seg.Changed += delegate(object s, int i)
		{
			OnSeg(i);
		};
	}

	public static SettingCard Ps(Host host, string title, string desc, string getScript, string setScript, string[] options = null)
	{
		return new SettingCard(host, title, desc, (IEngine e) => (options == null) ? ((object)e.As(e.Eval(getScript), fallback: false)) : ((object)e.As(e.Eval(getScript), -1)), delegate(IEngine e, object v)
		{
			e.Eval(setScript, new Hashtable { { "v", v } });
		}, options);
	}

	public static SettingCard Tracked(Host host, string title, string desc, string getScript, string setScript, string[] options, string cat)
	{
		SettingCard settingCard = TrackedRaw(host, title, desc, getScript, setScript, options, cat);
		settingCard.GetScript = getScript;
		settingCard.SetScript = setScript;
		settingCard.Cat = cat ?? "other";
		return settingCard;
	}

	public string HistTitle(object v)
	{
		string[] array = ((Seg != null) ? Seg.Items : null);
		if (array == null)
		{
			if (!(v is bool) || !(bool)v)
			{
				return Title + " (отменено)";
			}
			return Title;
		}
		return Title + ": " + ((v is int && (int)v >= 0 && (int)v < array.Length) ? array[(int)v] : "?");
	}

	private static SettingCard TrackedRaw(Host host, string title, string desc, string getScript, string setScript, string[] options, string cat)
	{
		return new SettingCard(host, title, desc, (IEngine e) => (options == null) ? ((object)e.As(e.Eval(getScript), fallback: false)) : ((object)e.As(e.Eval(getScript), -1)), delegate(IEngine e, object v)
		{
			string value = ((options != null) ? (title + ": " + ((v is int && (int)v >= 0 && (int)v < options.Length) ? options[(int)v] : "?")) : ((v is bool && (bool)v) ? title : (title + " (отменено)")));
			e.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
			{
				{ "fa1", value },
				{ "fa2", getScript },
				{ "fa3", setScript },
				{ "fa4", v },
				{
					"fa5",
					cat ?? "other"
				}
			});
		}, options);
	}

	public SettingCard CloneFor(Host host)
	{
		SettingCard settingCard = Tracked(host, Title, Desc, GetScript, SetScript, (Seg != null) ? Seg.Items : null, Cat);
		if (HasInfo)
		{
			settingCard.Info(What, Pro, Con, Rec, Why);
		}
		settingCard.OptDescs = OptDescs;
		settingCard.FpsBest = FpsBest;
		settingCard.IsExpert = IsExpert;
		settingCard.FpsW = FpsW;
		settingCard.Builds(MinBuild, MaxBuild);
		return settingCard;
	}

	public SettingCard Opts(params string[] descs)
	{
		OptDescs = descs;
		return this;
	}

	private int OptsW(int iw)
	{
		return iw - Theme.S(36f) - Theme.S(14f);
	}

	private int OptRowH(int i, int iw)
	{
		string text = ((i < OptDescs.Length) ? OptDescs[i] : "");
		int height = Theme.Measure("Ag", OptNameF).Height;
		int num = ((!string.IsNullOrEmpty(text)) ? (Theme.S(3f) + Theme.Measure(text, Theme.Small, OptsW(iw), wrap: true).Height) : 0);
		return height + num + Theme.S(16f);
	}

	private int OptsH(int iw)
	{
		int num = Theme.Measure("ВАРИАНТЫ", Theme.Tiny).Height + Theme.S(8f);
		for (int i = 0; i < Seg.Items.Length; i++)
		{
			num += OptRowH(i, iw) + Theme.S(4f);
		}
		return num;
	}

	private int DrawOpts(Graphics g, int x, int y, int iw)
	{
		int num = y;
		TextRenderer.DrawText(g, "ВАРИАНТЫ", Theme.Tiny, new Point(x, y), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		y += Theme.Measure("ВАРИАНТЫ", Theme.Tiny).Height + Theme.S(8f);
		int num2 = ((_cur is int) ? ((int)_cur) : (-1));
		int num3 = ((Rec is int) ? ((int)Rec) : (-1));
		Theme.Smooth(g);
		for (int i = 0; i < Seg.Items.Length; i++)
		{
			int num4 = OptRowH(i, iw);
			Rectangle rectangle = new Rectangle(x, y, iw, num4);
			bool flag = i == num2;
			bool flag2 = i == num3;
			Color white = Theme.Sub;
			using (GraphicsPath path = Theme.Round(new RectangleF((float)rectangle.X + 0.75f, (float)rectangle.Y + 0.75f, (float)rectangle.Width - 1.5f, (float)rectangle.Height - 1.5f), Theme.S(16f)))
			{
				using (SolidBrush brush = new SolidBrush(flag ? Color.FromArgb(34, 24, 27) : Color.FromArgb(24, 24, 28)))
				{
					g.FillPath(brush, path);
				}
				using Pen pen = new Pen(flag ? Theme.Accent : Theme.Border, Theme.BW);
				g.DrawPath(pen, path);
			}
			int num5 = Theme.S(14f);
			int height = Theme.Measure("Ag", OptNameF).Height;
			Rectangle rect = new Rectangle(x + Theme.S(12f), y + Theme.S(8f) + (height - num5) / 2, num5, num5);
			using (Pen pen2 = new Pen(flag ? Theme.Accent : Color.FromArgb(85, 85, 94), flag ? Theme.S(4f) : Theme.S(1.5f)))
			{
				if (flag)
				{
					g.DrawEllipse(pen2, (float)rect.X + Theme.S(2f), (float)rect.Y + Theme.S(2f), num5 - Theme.S(4f), num5 - Theme.S(4f));
				}
				else
				{
					g.DrawEllipse(pen2, rect);
				}
			}
			int num6 = x + Theme.S(36f);
			string s = Seg.Items[i];
			TextRenderer.DrawText(g, Theme.Up(s), OptNameF, new Point(num6, y + Theme.S(8f)), flag ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, 0.5f), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int num7 = num6 + Theme.Measure(Theme.Up(s), OptNameF).Width + Theme.S(8f);
			if (flag)
			{
				int num8 = Theme.PillW("сейчас", Theme.Tiny);
				Theme.Pill(g, new Rectangle(num7, y + Theme.S(7f), num8, Theme.S(18f)), "сейчас", Theme.Tiny, white);
				num7 += num8 + Theme.S(6f);
			}
			if (flag2)
			{
				int width = Theme.PillW("совет", Theme.Tiny);
				Theme.Pill(g, new Rectangle(num7, y + Theme.S(7f), width, Theme.S(18f)), "совет", Theme.Tiny, Theme.Green);
			}
			string text = ((i < OptDescs.Length) ? OptDescs[i] : "");
			if (!string.IsNullOrEmpty(text))
			{
				TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(num6, y + Theme.S(8f) + height + Theme.S(3f), OptsW(iw), num4), Theme.Mix(Theme.Sub, Theme.Text, 0.3f), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			}
			y += num4 + Theme.S(4f);
		}
		return y - num;
	}

	public SettingCard Info(string what, string pro, string con, object rec, string why)
	{
		What = what;
		Pro = pro;
		Con = con;
		Rec = rec;
		Why = why;
		HoverFx = true;
		Cursor = Cursors.Hand;
		return this;
	}

	public SettingCard Builds(int min, int max = 0)
	{
		MinBuild = min;
		MaxBuild = max;
		if (Unsupported)
		{
			if (Toggle != null)
			{
				Toggle.Busy = true;
				Toggle.Cursor = Cursors.Default;
			}
			if (Seg != null)
			{
				Seg.Busy = true;
			}
		}
		return this;
	}

	public SettingCard Fps(int weight, params object[] best)
	{
		FpsW = weight;
		FpsBest = best;
		return this;
	}

	private static bool Same(object a, object b)
	{
		if (a is bool && b is bool)
		{
			return (bool)a == (bool)b;
		}
		if (a is int && b is int)
		{
			return (int)a == (int)b;
		}
		return false;
	}

	internal string RecValueText(bool lower)
	{
		if (Rec is bool)
		{
			if (!(bool)Rec)
			{
				return "выключить";
			}
			return "включить";
		}
		if (Rec is int && Seg != null && (int)Rec >= 0 && (int)Rec < Seg.Items.Length)
		{
			return "«" + Seg.Items[(int)Rec] + "»";
		}
		if (!lower)
		{
			return "На твой выбор";
		}
		return "на твой выбор";
	}

	private string PillText(out Color c)
	{
		if (Unsupported)
		{
			c = Theme.Dim;
			if (MaxBuild > 0 && Win.Build > MaxBuild)
			{
				return "Только до Windows 11 " + Win.BuildName(MaxBuild);
			}
			return "Нужна Windows 11 " + Win.BuildName(MinBuild) + "+";
		}
		if (Origin != null)
		{
			c = OriginColor;
			return Origin;
		}
		if (Rec == null)
		{
			c = Theme.Sub;
			return "На твой выбор";
		}
		c = Theme.Green;
		return "Рекомендуем";
	}

	private static int TagW(string t)
	{
		return Theme.Measure(Theme.Up(t), TagF).Width + Theme.S(18f);
	}

	private static void DrawTag(Graphics g, Rectangle r, string text, Color c)
	{
		Theme.Smooth(g);
		RectangleF r2 = new RectangleF((float)r.X + 0.75f, (float)r.Y + 0.75f, (float)r.Width - 1.5f, (float)r.Height - 1.5f);
		bool flag = c.ToArgb() != Theme.Sub.ToArgb() && c.ToArgb() != Theme.Dim.ToArgb();
		using (GraphicsPath path = Theme.Round(r2, r2.Height / 2f))
		{
			using (SolidBrush fb = new SolidBrush(flag ? Color.FromArgb(26, c) : Color.FromArgb(10, 255, 255, 255)))
			{
				g.FillPath(fb, path);
			}
			using Pen pen = new Pen(flag ? Color.FromArgb(90, c) : Theme.Border, Theme.BW);
			g.DrawPath(pen, path);
		}
		TextRenderer.DrawText(g, Theme.Up(text), TagF, r, flag ? c : Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	private string StatusLine(out int kind)
	{
		kind = 0;
		if (Unsupported)
		{
			return Theme.Up("Недоступно");
		}
		if (_cur == null)
		{
			kind = 3;
			return Theme.Up("Проверяю");
		}
		if (Toggle != null)
		{
			bool flag = _cur is bool && (bool)_cur;
			if (Rec is bool)
			{
				if ((bool)Rec == flag)
				{
					kind = 1;
					return Theme.Up("Как советуем");
				}
				kind = 2;
				return Theme.Up("Можно улучшить");
			}
			return Theme.Up(flag ? "Включено" : "Выключено");
		}
		if (Rec == null)
		{
			return "";
		}
		if (Same(_cur, Rec))
		{
			kind = 1;
			return Theme.Up("Как советуем");
		}
		kind = 2;
		int num = ((Rec is int) ? ((int)Rec) : (-1));
		return Theme.Up("Советуем: ") + Theme.Up((Seg != null && num >= 0 && num < Seg.Items.Length) ? Seg.Items[num] : RecValueText(lower: false));
	}

	private int StatusMaxW()
	{
		int num = 0;
		string[] array = new string[5] { "Как советуем", "Можно улучшить", "Включено", "Выключено", "Недоступно" };
		foreach (string s in array)
		{
			num = Math.Max(num, Theme.Measure(Theme.Up(s), StatF).Width);
		}

		return num + Theme.S(15f);
	}

	private static void Blink(SettingCard c, bool on)
	{
		if ((on ? (!_blinking.Add(c)) : (!_blinking.Remove(c))) || !on || _blinkTimer != null || !Anim.Enabled)
		{
			return;
		}
		_blinkTimer = new Timer
		{
			Interval = 500
		};
		_blinkTimer.Tick += delegate
		{
			foreach (SettingCard item in new List<SettingCard>(_blinking))
			{
				if (item.IsDisposed || !item.Visible)
				{
					_blinking.Remove(item);
				}
				else
				{
					item.Invalidate(item._statDot);
				}
			}
			if (_blinking.Count == 0)
			{
				_blinkTimer.Stop();
				_blinkTimer.Dispose();
				_blinkTimer = null;
			}
		};
		_blinkTimer.Start();
	}

	private void DrawStatus(Graphics g, int x, int right, int cy)
	{
		int kind;
		string text = StatusLine(out kind);
		if (!string.IsNullOrEmpty(text))
		{
			Size size = Theme.Measure(text, StatF);
			int num = ((kind != 0) ? Theme.S(16f) : 0);
			int num2 = ((right > 0) ? (right - size.Width) : (x + num));
			float num3 = num2 - Theme.S(11f);
			Color fore = ((kind == 2) ? Theme.Text : Theme.Sub);
			switch (kind)
			{
			case 1:
				Theme.Dot(g, num3, cy, Theme.S(3f), Theme.Blue, 0f);
				break;
			case 2:
			{
				_statDot = new Rectangle((int)num3 - Theme.S(9f), cy - Theme.S(9f), Theme.S(18f), Theme.S(18f));
				Blink(this, on: true);
				bool flag = Anim.Enabled && Environment.TickCount / 500 % 2 == 1;
				Theme.Dot(g, num3, cy, Theme.S(3f), flag ? Color.FromArgb(64, Theme.Accent) : Theme.Accent, flag ? 0.2f : 0.8f);
				break;
			}
			case 3:
				Theme.Dot(g, num3, cy, Theme.S(2f), Color.FromArgb(160, 255, 255, 255), 0f);
				break;
			}
			if (kind != 2)
			{
				Blink(this, on: false);
			}
			TextRenderer.DrawText(g, text, StatF, new Point(num2, cy - size.Height / 2), fore, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
	}

	private void OnToggle()
	{
		if (InList)
		{
			OwnerPage()?.SelectCard(this);
		}
		if (!Unsupported && _host.TryBegin(quick: true, Title + ": " + ((!Toggle.On) ? "ВКЛ" : "ВЫКЛ")))
		{
			bool want = !Toggle.On;
			Toggle.On = want;
			Toggle.Busy = true;
			Exception fail = null;
			_host.Worker.Run((IEngine e) => SetThenGet(e, want, out fail), delegate(object r)
			{
				Toggle.Busy = false;
				Apply(r);
				ReportFail(fail);
				_host.End(quick: true);
				Relayout();
			});
		}
	}

	private void OnSeg(int i)
	{
		if (InList)
		{
			OwnerPage()?.SelectCard(this);
		}
		if (Unsupported || !_host.TryBegin(quick: true, Title + ((Seg != null && i >= 0 && i < Seg.Items.Length) ? (": " + Seg.Items[i]) : "")))
		{
			_host.Worker.Run((IEngine e) => _get(e), Apply);
			return;
		}
		Seg.Busy = true;
		Exception fail = null;
		_host.Worker.Run((IEngine e) => SetThenGet(e, i, out fail), delegate(object r)
		{
			Seg.Busy = false;
			Apply(r);
			ReportFail(fail);
			_host.End(quick: true);
			Relayout();
		});
	}

	private object SetThenGet(IEngine e, object v, out Exception fail)
	{
		fail = null;
		try
		{
			_set(e, v);
		}
		catch (Exception ex)
		{
			fail = ex;
		}
		try
		{
			return _get(e);
		}
		catch (Exception ex2)
		{
			if (fail == null)
			{
				fail = ex2;
			}
			return null;
		}
	}

	private void ReportFail(Exception fail)
	{
		if (fail != null)
		{
			Program.Main0?.Status("err", "Ошибка: " + fail.Message);
		}
	}

	public override object Fetch(IEngine e)
	{
		return _get(e);
	}

	public void SetValue(IEngine e, object v)
	{
		_set(e, v);
	}

	public override void Apply(object v)
	{
		object cur = _cur;
		try
		{
			ApplyValue(v);
		}
		finally
		{
			bool num;
			if (cur != null)
			{
				if (_cur == null)
				{
					goto IL_0037;
				}
				num = Same(cur, _cur);
			}
			else
			{
				num = _cur == null;
			}
			if (!num)
			{
				goto IL_0037;
			}
			goto end_IL_0010;
			IL_0037:
			InspectorValueChanged();
			end_IL_0010:;
		}
	}

	private void ApplyValue(object v)
	{
		if (Toggle != null)
		{
			bool flag = v is bool && (bool)v;
			_cur = flag;
			if (Toggle.On != flag)
			{
				Toggle.On = flag;
			}
		}
		else
		{
			int num = ((v is int) ? ((int)v) : (-1));
			_cur = ((num >= 0) ? ((object)num) : null);
			Seg.Selected = num;
		}
		Invalidate();
	}

	private Page OwnerPage()
	{
		Control parent = base.Parent;
		while (parent != null && !(parent is Page))
		{
			parent = parent.Parent;
		}
		return parent as Page;
	}

	private void Relayout()
	{
		OwnerPage()?.DoLayoutBlocks();
	}

	public void SetExpanded(bool on, bool animate = true)
	{
		if (!HasInfo)
		{
			return;
		}
		if (InList)
		{
			if (on)
			{
				OwnerPage()?.SelectCard(this);
			}
			return;
		}
		_expanded = on;
		if (on)
		{
			OnInspectorOpened();
		}
		float from = _expT;
		float to = (on ? 1f : 0f);
		Anim.Start(this, animate ? 260 : 0, delegate(float t)
		{
			_expT = from + (to - from) * Anim.EaseInOut(t);
			Relayout();
		}, null, this);
	}

	internal void SetInList(bool v)
	{
		if (v && (_expanded || _expT > 0f))
		{
			Anim.Cancel(this);
			_expanded = false;
			_expT = 0f;
		}
		if (InList != v)
		{
			InList = v;
			Invalidate();
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button != MouseButtons.Left || !InspectorMouseUp(e))
		{
			if (e.Button == MouseButtons.Left && InList)
			{
				OwnerPage()?.SelectCard(this);
			}
			else if (e.Button == MouseButtons.Left && HasInfo && e.Y < _baseH)
			{
				SetExpanded(!_expanded);
			}
		}
	}

	private int InnerColsW(int w)
	{
		return w - base.Pad * 2;
	}

	private bool TwoCols(int w)
	{
		if (!HasOpts && !string.IsNullOrEmpty(Con) && !string.IsNullOrEmpty(Pro))
		{
			return InnerColsW(w) >= Theme.S(560f);
		}
		return false;
	}

	private int BlockH(string head, string text, int w)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}
		return Theme.Measure(head, Theme.Tiny).Height + Theme.S(5f) + Theme.Measure(text, Theme.Small, w, wrap: true).Height;
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		int num = 0;
		int num2;
		if (Toggle != null)
		{
			num2 = Toggle.Width;
		}
		else
		{
			int num3 = Theme.S(96f);
			string[] items = Seg.Items;
			foreach (string s in items)
			{
				num3 = Math.Max(num3, Theme.Measure(s, Theme.Small).Width + Theme.S(36f));
			}
			num = Math.Min(Seg.Items.Length * num3, (int)((double)w * 0.6));
			num2 = num;
		}
		int num4 = ((HasInfo && !InList) ? Theme.S(30f) : 0);
		int num5 = (InList ? Theme.S(26f) : 0);
		_segBelow = Seg != null && w - num2 - num4 - pad * 3 < Theme.S(300f);
		int num6 = (_segBelow ? (w - pad * 2 - num4) : (w - pad * 3 - num2 - num4)) - num5;
		_statW = 0;
		if (Toggle != null && HasInfo)
		{
			int num7 = StatusMaxW();
			if (num6 - num7 - Theme.S(16f) >= Theme.S(220f))
			{
				_statW = num7;
				num6 -= num7 + Theme.S(16f);
			}
		}
		_titleH = Theme.Measure(Theme.Up(Title), Theme.Title, num6, wrap: true).Height;
		_descH = ((!string.IsNullOrEmpty(Desc)) ? (Theme.Measure(Desc, Theme.Small, num6, wrap: true).Height + Theme.S(4f)) : 0);
		int num8 = _titleH + _descH;
		int num9 = Theme.S(20f);
		if (ShowPill)
		{
			Color c;
			int num10 = TagW(PillText(out c));
			int width = Theme.Measure(Theme.Up(Title), Theme.Title).Width;
			_pillInline = width + Theme.S(10f) + num10 <= num6;
			if (_pillInline)
			{
				_pillR = new Rectangle(width + Theme.S(10f), (_titleH - num9) / 2, num10, num9);
			}
			else
			{
				_pillR = new Rectangle(0, num8 + Theme.S(7f), num10, num9);
				num8 += num9 + Theme.S(7f);
			}
		}
		int num11 = Math.Max(Theme.S(72f), num8 + pad * 2 - Theme.S(4f));
		if (_segBelow)
		{
			num11 = num8 + pad * 2 + Seg.Height + Theme.S(10f);
		}
		_textR = new Rectangle(pad + num5, _segBelow ? (pad - Theme.S(2f)) : ((num11 - num8) / 2), num6, num8);
		_pillR.Offset(_textR.X, _textR.Y);
		_baseH = num11;
		int num12 = num11;
		if (HasInfo && !InList)
		{
			_detH = MeasureDetails(w);
			num12 += (int)Math.Round((float)_detH * _expT);
		}
		SetBounds(x, y, w, num12);
		if (Toggle != null)
		{
			Toggle.Location = new Point(w - pad - Toggle.Width, (num11 - Toggle.Height) / 2);
			_chevR = new Rectangle(Toggle.Left - num4 - Theme.S(4f), (num11 - Theme.S(28f)) / 2, num4, Theme.S(28f));
			int num13 = ((num4 > 0) ? _chevR.Left : Toggle.Left) - Theme.S(14f);
			_statR = new Rectangle(num13 - _statW, 0, _statW, num11);
		}
		else if (_segBelow)
		{
			Seg.SetBounds(pad + num5, num11 - pad - Seg.Height, w - pad * 2 - num5, Seg.Height);
			_chevR = new Rectangle(w - pad - num4 + Theme.S(6f), _textR.Y + (_titleH - Theme.S(28f)) / 2, num4, Theme.S(28f));
		}
		else
		{
			Seg.SetBounds(w - pad - num, (num11 - Seg.Height) / 2, num, Seg.Height);
			_chevR = new Rectangle(Seg.Left - num4 - Theme.S(4f), (num11 - Theme.S(28f)) / 2, num4, Theme.S(28f));
		}
		return num12;
	}

	private void DrawChevron(Graphics g)
	{
		if (!HasInfo || InList)
		{
			return;
		}
		Theme.Smooth(g);
		float num = (float)_chevR.X + (float)_chevR.Width / 2f;
		float num2 = (float)_chevR.Y + (float)_chevR.Height / 2f;
		float num3 = Theme.S(4.5f);
		float num4 = _expT * (float)Math.PI;
		PointF[] array = new PointF[3]
		{
			new PointF(0f - num3, (0f - num3) / 2f),
			new PointF(0f, num3 / 2f),
			new PointF(num3, (0f - num3) / 2f)
		};
		for (int i = 0; i < array.Length; i++)
		{
			float x = array[i].X;
			float y = array[i].Y;
			array[i] = new PointF(num + x * (float)Math.Cos(num4) - y * (float)Math.Sin(num4), num2 + x * (float)Math.Sin(num4) + y * (float)Math.Cos(num4));
		}
		if (HotT.Value > 0.01f || _expT > 0.01f)
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)(18f * Math.Max(HotT.Value, _expT)), 255, 255, 255));
			g.FillEllipse(brush, num - (float)Theme.S(13f), num2 - (float)Theme.S(13f), Theme.S(26f), Theme.S(26f));
		}
		using Pen pen = new Pen(Theme.Mix(Theme.Sub, Theme.Text, Math.Max(HotT.Value, _expT)), Theme.S(1.6f));
		pen.StartCap = LineCap.Round;
		pen.EndCap = LineCap.Round;
		pen.LineJoin = LineJoin.Round;
		g.DrawLines(pen, array);
	}

	private int DrawBlock(Graphics g, string head, string text, int x, int y, int w, Color dot)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}
		int height = Theme.Measure(head, Theme.Tiny).Height;
		int x2 = x;
		if (dot != Color.Empty)
		{
			Theme.Smooth(g);
			using (SolidBrush brush = new SolidBrush(dot))
			{
				g.FillEllipse(brush, x, (float)y + (float)(height - Theme.S(7f)) / 2f, Theme.S(7f), Theme.S(7f));
			}
			x2 = x + Theme.S(14f);
			w -= Theme.S(14f);
		}
		TextRenderer.DrawText(g, head, Theme.Tiny, new Point(x2, y), (dot != Color.Empty) ? dot : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		int height2 = Theme.Measure(text, Theme.Small, w, wrap: true).Height;
		TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(x2, y + height + Theme.S(5f), w, height2 + 2), Theme.Mix(Theme.Sub, Theme.Text, 0.35f), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		return height + Theme.S(5f) + height2;
	}

	protected override void PaintContent(Graphics g)
	{
		TextRenderer.DrawText(g, Theme.Up(Title), Theme.Title, new Rectangle(_textR.X, _textR.Y, _textR.Width, _titleH + 2), Unsupported ? Theme.Sub : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		if (!string.IsNullOrEmpty(Desc))
		{
			TextRenderer.DrawText(g, Desc, Theme.Small, new Rectangle(_textR.X, _textR.Y + _titleH + Theme.S(4f), _textR.Width, _descH), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		}
		if (ShowPill)
		{
			Color c;
			string text = PillText(out c);
			DrawTag(g, _pillR, text, c);
		}
		if (_statW > 0)
		{
			DrawStatus(g, 0, _statR.Right, _statR.Y + _statR.Height / 2);
		}
		else if (Seg != null && HasInfo && ShowPill)
		{
			int kind;
			string text2 = StatusLine(out kind);
			int num = _pillR.Right + Theme.S(16f);
			if (!string.IsNullOrEmpty(text2) && num + Theme.S(13f) + Theme.Measure(text2, StatF).Width <= _textR.Right)
			{
				DrawStatus(g, num, 0, _pillR.Y + _pillR.Height / 2);
			}
		}
		DrawChevron(g);
		if (InList)
		{
			DrawRowDots(g);
		}
		if (_expT > 0.001f && HasInfo && !InList)
		{
			DrawDetails(g);
		}
	}

	private void InvInsp()
	{
		if (InspectorHost is InspectorPanel inspectorPanel)
		{
			inspectorPanel.Dirty();
		}
		else if (InspectorHost != null)
		{
			InspectorHost.Invalidate();
		}
		else
		{
			Invalidate();
		}
	}

	private void StartTyping()
	{
		_typeT0 = Environment.TickCount;
		if (_typeTimer == null)
		{
			_typeTimer = new Timer
			{
				Interval = 30
			};
			_typeTimer.Tick += delegate
			{
				bool flag = TypedCount() < L.T(DescText ?? "").Length || Environment.TickCount - _planT0 < 900;
				InvInsp();
				if (!InspOpen || (!flag && (InspectorHost != null || Environment.TickCount - _typeT0 > 4000)))
				{
					_typeTimer.Stop();
				}
			};
		}
		_typeTimer.Start();
	}

	private int TypedCount()
	{
		string text = L.T(DescText ?? "");
		int num = Environment.TickCount - _typeT0;
		float num2 = Math.Max(110f, (float)text.Length / 1.2f);
		return Math.Min(text.Length, (int)((float)num * num2 / 1000f));
	}

	protected override void Dispose(bool disposing)
	{
		Blink(this, on: false);
		if (disposing && _typeTimer != null)
		{
			_typeTimer.Dispose();
			_typeTimer = null;
		}
		base.Dispose(disposing);
	}

	private void OnInspectorOpened()
	{
		StartTyping();
		if (_planState == 0)
		{
			LoadPlan();
		}
	}

	private void LoadPlan()
	{
		if (!CanPlan || _host == null)
		{
			return;
		}
		_planState = 1;
		string id = _tweakId;
		string[] list = null;
		if (id == null)
		{
			bool flag = _cur is bool && (bool)_cur;
			list = new string[Meta.Writes.Length];
			for (int i = 0; i < list.Length; i++)
			{
				string[] array = Meta.Writes[i].Split('|');
				list[i] = array[0] + "|" + array[1] + "|" + (flag ? "-" : array[2]);
			}
		}
		_host.Worker.Run((IEngine e) => (id == null) ? e.Eval("Get-FPlan $list", new Hashtable { { "list", list } }) : e.Eval("Get-FTweakPlan '" + id + "'"), delegate(object o)
		{
			ParsePlan((o == null) ? "" : o.ToString());
			_planState = 2;
			_planT0 = Environment.TickCount;
			_typeTimer?.Start();
			if (InspectorHost == null)
			{
				Relayout();
			}
			InvInsp();
		});
	}

	private void ParsePlan(string s)
	{
		_plan = new List<string[]>();
		_planFlag = "";
		string[] array = s.Replace("\r", "").Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length == 0)
			{
				continue;
			}
			if (text[0] == '#')
			{
				_planFlag = text.Substring(1);
				continue;
			}
			string[] array2 = text.Split('|');
			if (array2.Length >= 4)
			{
				_plan.Add(array2);
			}
		}
	}

	private void InspectorValueChanged()
	{
		if (_planState == 2)
		{
			_planState = 0;
			if (InspOpen)
			{
				LoadPlan();
			}
		}
		if (InspectorHost != null)
		{
			InvInsp();
		}
	}

	private static Font DescF(bool panel)
	{
		if (!panel)
		{
			return Theme.Small;
		}
		return Theme.FC(8.5f);
	}

	private int TextH(string s, Font f, int w)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return Theme.Measure(s, f, w, wrap: true).Height;
		}
		return 0;
	}

	private bool TwoPair(int iw, bool panel)
	{
		if (!string.IsNullOrEmpty(Pro) && !string.IsNullOrEmpty(Con))
		{
			return iw >= Theme.S(panel ? 300f : 480f);
		}
		return false;
	}

	private int PairH(int iw, bool panel)
	{
		if (HasOpts)
		{
			return OptsH(iw);
		}
		int num = Theme.Measure("A", Theme.Tiny).Height + Theme.S(6f);
		if (TwoPair(iw, panel))
		{
			int w = (iw - Theme.S(24f)) / 2;
			return num + Math.Max(TextH(Pro, FInsp, w), TextH(Con, FInsp, w));
		}
		int num2 = 0;
		if (!string.IsNullOrEmpty(Pro))
		{
			num2 += num + TextH(Pro, FInsp, iw) + Theme.S(12f);
		}
		if (!string.IsNullOrEmpty(Con))
		{
			num2 += num + TextH(Con, FInsp, iw) + Theme.S(12f);
		}
		return Math.Max(0, num2 - Theme.S(12f));
	}

	private int PlanH()
	{
		if (!CanPlan)
		{
			return 0;
		}
		int num = Theme.Measure("A", Theme.Tiny).Height + Theme.S(8f);
		int num2 = ((_planState != 2) ? 1 : Math.Max(1, _plan.Count));
		return num + num2 * RowH + Theme.S(28f);
	}

	private string RecLine()
	{
		string result = ((Rec == null) ? "На твой выбор — WinWeaver ничего не навязывает" : ((Rec is bool) ? (((bool)Rec) ? "Советуем включить этот переключатель" : "Советуем оставить переключатель выключенным") : (L.T("Советуем ") + L.T(RecValueText(lower: true)))));
		if (Rec != null && _cur != null && Same(_cur, Rec))
		{
			result = "Уже как советуем";
		}
		return result;
	}

	private int RecH(int iw)
	{
		int num = Theme.Measure("A", Theme.Tiny).Height + Theme.S(4f) + TextH(RecLine(), FInsp, iw);
		if (!string.IsNullOrEmpty(Why))
		{
			num += Theme.S(3f) + TextH(Why, FInsp, iw);
		}
		return num;
	}

	internal int BodyH(int iw, bool panel)
	{
		int num = Math.Max(Theme.S(panel ? 44f : 36f), TextH(L.T(DescText ?? ""), DescF(panel), iw)) + Theme.S(16f);
		if (HasMetrics)
		{
			num += TileH + Theme.S(16f);
		}
		int num2 = PairH(iw, panel);
		if (num2 > 0)
		{
			num += num2 + Theme.S(16f);
		}
		num += PlanH();
		num += RecH(iw) + Theme.S(12f);
		return num + (Theme.Measure("A", Theme.Tiny).Height + Theme.S(4f) + TextH("Прежнее значение сохраняется в «Историю». Вернуть можно здесь или там — по одному параметру или всё сразу.", FInsp, iw));
	}

	private int MeasureDetails(int w)
	{
		int iw = InnerColsW(w);
		int num = Theme.S(18f);
		num += Theme.S(22f) + Theme.S(10f);
		num += BodyH(iw, panel: false) + Theme.S(14f);
		if (HasTech)
		{
			num += Theme.S(36f) + Theme.S(4f);
		}
		return num + base.Pad - Theme.S(6f);
	}

	private void SiblingIndex(out int idx, out int total)
	{
		idx = 1;
		total = 1;
		if (base.Parent == null)
		{
			return;
		}
		List<SettingCard> list = new List<SettingCard>();
		foreach (Control control in base.Parent.Controls)
		{
			if (control is SettingCard { Visible: not false, HasInfo: not false } settingCard)
			{
				list.Add(settingCard);
			}
		}
		if (!list.Contains(this))
		{
			list.Add(this);
		}
		list.Sort((SettingCard a, SettingCard b) => a.Top.CompareTo(b.Top));
		total = list.Count;
		idx = list.IndexOf(this) + 1;
	}

	private string CatCaption()
	{
		Page page = OwnerPage();
		if (page != null && page.Tabs != null && page.CurrentTab >= 0 && page.CurrentTab < page.Tabs.Items.Length)
		{
			return page.Tabs.Items[page.CurrentTab];
		}
		if (page == null)
		{
			return "";
		}
		return page.Title;
	}

	private string StateText(out bool hi)
	{
		hi = false;
		if (Unsupported)
		{
			return "Недоступно";
		}
		if (_cur == null)
		{
			return "Проверяю…";
		}
		if (Rec != null && Same(_cur, Rec))
		{
			hi = true;
			return "Как советуем";
		}
		if (Toggle != null)
		{
			if (!(bool)_cur)
			{
				return "Выключено";
			}
			return "Включено";
		}
		int num = (int)_cur;
		if (Seg == null || num < 0 || num >= Seg.Items.Length)
		{
			return "";
		}
		return Seg.Items[num];
	}

	private static void Outline(Graphics g, RectangleF r, float radius, Color c)
	{
		using GraphicsPath path = Theme.Round(new RectangleF(r.X + 0.75f, r.Y + 0.75f, r.Width - 1.5f, r.Height - 1.5f), radius);
		using Pen pen = new Pen(c, Theme.BW);
		g.DrawPath(pen, path);
	}

	private void Tile(Graphics g, Rectangle r, string label, string value, int dots, Color edge, Color valColor, bool red)
	{
		Outline(g, r, Theme.S(18f), edge);
		int num = Theme.S(12f);
		TextRenderer.DrawText(g, Theme.Up(label), Theme.FC(6.5f), new Point(r.X + num, r.Y + Theme.S(10f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, Theme.Up(value), FInsp, new Rectangle(r.X + num, r.Y + Theme.S(24f), r.Width - num * 2, Theme.S(16f)), valColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		float num2 = Theme.S(10f);
		float num3 = Theme.S(3f);
		float num4 = r.Bottom - Theme.S(14f);
		Theme.Smooth(g);
		for (int i = 0; i < 5; i++)
		{
			float num5 = (float)(r.X + num) + (float)i * (num2 + num3);
			Color sc = (i < dots) ? (red ? Theme.Accent : Color.FromArgb(201, 201, 207)) : Color.FromArgb(38, 38, 45);
			using GraphicsPath sp = Theme.Round(new RectangleF(num5, num4 - Theme.S(1.5f), num2, Theme.S(3f)), Theme.S(1.5f));
			using SolidBrush sb = new SolidBrush(sc);
			g.FillPath(sb, sp);
		}
	}

	private static string ShortKey(string key)
	{
		if (key == "svc")
		{
			return L.T("Службы");
		}
		string text = key.Replace(":", "");
		int num = text.IndexOf('\\');
		int num2 = text.LastIndexOf('\\');
		if (num < 0 || num2 <= num)
		{
			return text;
		}
		string text2 = text.Substring(0, num);
		string text3 = text.Substring(num2 + 1);
		if (num2 != num)
		{
			return text2 + "\\…\\" + text3;
		}
		return text;
	}

	private static string ShowVal(string key, string v)
	{
		if (v == "-" || v == "")
		{
			return "—";
		}
		if (key == "svc")
		{
			switch (v)
			{
			case "2":
				return L.T("Авто");
			case "3":
				return L.T("Вручную");
			case "4":
				return L.T("Откл.");
			}
		}
		return v;
	}

	private static string Plural(int n)
	{
		int num = n % 10;
		int num2 = n % 100;
		if (num == 1 && num2 != 11)
		{
			return n + " " + L.T("значение");
		}
		if (num >= 2 && num <= 4 && (num2 < 12 || num2 > 14))
		{
			return n + " " + L.T("значения");
		}
		return n + " " + L.T("значений");
	}

	private int Caption(Graphics g, string text, int x, int y, Color dot, Color tc)
	{
		int height = Theme.Measure("A", Theme.Tiny).Height;
		int num = x;
		if (dot != Color.Empty)
		{
			Theme.Dot(g, x + Theme.S(3f), (float)y + (float)height / 2f, Theme.S(3f), dot, 0.7f);
			num += Theme.S(13f);
		}
		TextRenderer.DrawText(g, Theme.Up(text), Theme.Tiny, new Point(num, y), tc, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		return height;
	}

	private string Scramble(string v, int row)
	{
		int num = Environment.TickCount - _planT0;
		if (!Anim.Enabled || num >= 300 + row * 45 || string.IsNullOrEmpty(v))
		{
			return v;
		}
		Random random = new Random(num / 38 * 977 + row * 31);
		char[] array = v.ToCharArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (!char.IsWhiteSpace(array[i]) && array[i] != '.' && array[i] != '\\')
			{
				array[i] = "01ABCDEF#"[random.Next(9)];
			}
		}
		return new string(array);
	}

	private void HeadLine(Graphics g, int x, int y, int iw, bool panel)
	{
		SiblingIndex(out var idx, out var total);
		string text = CatCaption();
		string text2 = L.T("Настройка") + " " + idx + " " + L.T("из") + " " + total + (string.IsNullOrEmpty(text) ? "" : (" · " + L.T(text)));
		string s = StateText(out var hi);
		if (panel && !hi && Rec != null && _cur != null && !Unsupported)
		{
			int kind;
			string text3 = StatusLine(out kind);
			s = ((kind == 2) ? text3 : L.T("Можно улучшить"));
		}
		s = Theme.Up(s);
		int num = Theme.Measure(s, Theme.FC(6.5f)).Width + Theme.S(20f);
		Rectangle rectangle = new Rectangle(x + iw - num, y, num, Theme.S(22f));
		bool flag = hi || (panel && Rec != null && _cur != null && !Unsupported);
		Color hc = (hi ? Theme.Green : Theme.Yellow);
		if (flag)
		{
			Theme.Smooth(g);
			using GraphicsPath hp = Theme.Round(new RectangleF((float)rectangle.X + 0.5f, (float)rectangle.Y + 0.5f, (float)rectangle.Width - 1f, (float)rectangle.Height - 1f), (float)rectangle.Height / 2f);
			using (SolidBrush hb = new SolidBrush(Color.FromArgb(24, hc))) g.FillPath(hb, hp);
		}
		Outline(g, rectangle, (float)rectangle.Height / 2f, flag ? Color.FromArgb(110, hc) : Theme.Border);
		TextRenderer.DrawText(g, s, Theme.FC(6.5f), rectangle, flag ? hc : Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		int height = Theme.Measure("A", Theme.Tiny).Height;
		TextRenderer.DrawText(g, text2, Theme.Tiny, new Rectangle(x, y + (Theme.S(22f) - height) / 2, Math.Max(10, iw - num - Theme.S(24f)), height + 2), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
	}

	internal int PanelHeadH(int iw)
	{
		return Theme.S(22f) + Theme.S(12f) + Theme.Measure("AG", PanelTitleF).Height;
	}

	internal int PanelHead(Graphics g, int x, int y, int iw, out int titleMid)
	{
		Theme.Smooth(g);
		HeadLine(g, x, y, iw, panel: true);
		int num = y + Theme.S(22f) + Theme.S(12f);
		int height = Theme.Measure("AG", PanelTitleF).Height;
		TextRenderer.DrawText(g, Theme.Up(Title), PanelTitleF, new Rectangle(x - Theme.S(1f), num, iw + Theme.S(2f), height + 2), Unsupported ? Theme.Sub : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		titleMid = num + height / 2;
		return PanelHeadH(iw);
	}

	private void DrawDetails(Graphics g)
	{
		Theme.Smooth(g);
		int width = base.Width;
		int pad = base.Pad;
		int num = InnerColsW(width);
		int x = pad;
		int baseH = _baseH;
		using (SolidBrush brush = new SolidBrush(Theme.Line))
		{
			g.FillRectangle(brush, pad, baseH, num, 1);
		}
		baseH += Theme.S(18f);
		HeadLine(g, x, baseH, num, panel: false);
		baseH += Theme.S(22f) + Theme.S(10f);
		baseH = PaintBody(g, x, baseH, num, panel: false) + Theme.S(14f);
		_techBtnR = Rectangle.Empty;
		if (HasTech)
		{
			string text = Theme.Up(_tech ? "Проще" : "Подробнее");
			int width2 = Math.Max(Theme.S(150f), Theme.Measure(text, Theme.Tiny).Width + Theme.S(40f));
			_techBtnR = new Rectangle(x, baseH, width2, Theme.S(36f));
			PillButton(g, _techBtnR, text, _tech, _techHot, enabled: true);
		}
	}

	internal static void PillButton(Graphics g, Rectangle r, string text, bool on, bool hot, bool enabled)
	{
		PillButton(g, r, text, on, hot, enabled, primary: false);
	}

	internal static void PillButton(Graphics g, Rectangle r, string text, bool on, bool hot, bool enabled, bool primary)
	{
		Theme.Smooth(g);
		RectangleF r2 = new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, (float)r.Width - 1f, (float)r.Height - 1f);
		using (GraphicsPath path = Theme.Round(r2, Theme.S(10f)))
		{
			if (primary && enabled)
			{
				using SolidBrush brush0 = new SolidBrush(hot ? Color.FromArgb(255, 92, 104) : Theme.Accent);
				g.FillPath(brush0, path);
			}
			else if (on)
			{
				using SolidBrush brush = new SolidBrush(Color.FromArgb(38, 38, 45));
				g.FillPath(brush, path);
			}
			else if (hot && enabled)
			{
				using SolidBrush brush2 = new SolidBrush(Theme.Hover);
				g.FillPath(brush2, path);
			}
			if (!(primary && enabled))
			{
				using Pen pen = new Pen(on ? Color.FromArgb(70, 70, 80) : ((!enabled) ? Theme.Line : (hot ? Color.FromArgb(72, 72, 82) : Color.FromArgb(46, 46, 53))), Theme.BW);
				g.DrawPath(pen, path);
			}
		}
		TextRenderer.DrawText(g, text, Theme.Small, r, (primary && enabled) ? Color.White : (enabled ? Theme.Text : Theme.Dim), TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	internal int PaintBody(Graphics g, int x, int y, int iw, bool panel)
	{
		Theme.Smooth(g);
		Font font = DescF(panel);
		string text = L.T(DescText ?? "");
		int num = Math.Max(Theme.S(panel ? 44f : 36f), TextH(text, font, iw));
		int num2 = TypedCount();
		bool flag = num2 < text.Length;
		string text2 = (flag ? text.Substring(0, Math.Min(num2, text.Length)) : text);
		Rectangle r = new Rectangle(x, y, iw, num + 2);
		TextRenderer.DrawText(g, text2, font, r, Color.FromArgb(214, 214, 214), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		CursorAt = new PointF(float.NaN, float.NaN);
		bool flag2 = flag || !panel;
		if ((flag2 && (flag || (Environment.TickCount - _typeT0) % 900 < 520)) || !flag2)
		{
			Size size = Theme.Measure((text2.Length == 0) ? " " : text2, font, iw, wrap: true);
			string[] array = SplitLines(text2, font, iw);
			string text3 = ((array.Length != 0) ? array[array.Length - 1] : "");
			int num3 = ((text3.Length != 0) ? Theme.Measure(text3, font).Width : 0);
			int height = Theme.Measure("Ag", font).Height;
			float num4 = (float)(y + Math.Max(0, size.Height - height)) + (float)height / 2f;
			if (flag2)
			{
				Theme.Dot(g, x + num3 + Theme.S(7f), num4, Theme.S(3f), Theme.Accent, 0.7f);
			}
			else
			{
				CursorAt = new PointF(x + num3 + Theme.S(7f), num4);
			}
		}
		y += num + Theme.S(16f);
		if (HasMetrics)
		{
			int num5 = Theme.S(8f);
			int num6 = (iw - num5 * 2) / 3;
			int num7 = ((Meta != null) ? Meta.Impact : (-1));
			int risk = Risk;
			int reboot = Reboot;
			Rectangle r2 = new Rectangle(x, y, num6, TileH);
			Rectangle r3 = new Rectangle(x + num6 + num5, y, num6, TileH);
			Rectangle r4 = new Rectangle(x + (num6 + num5) * 2, y, iw - (num6 + num5) * 2, TileH);
			Tile(g, r2, SettingMeta.ImpactLabel(Cat), (num7 >= 0) ? SettingMeta.ImpactNames[Math.Min(5, num7)] : "—", Math.Max(0, num7), (num7 >= 4) ? Color.FromArgb(120, Theme.Green) : Theme.Border, Theme.Text, red: false);
			Tile(g, r3, "Риск", (risk >= 0) ? SettingMeta.RiskNames[Math.Min(3, risk)] : "—", (risk >= 0) ? SettingMeta.RiskDots[Math.Min(3, risk)] : 0, (risk >= 2) ? Color.FromArgb(166, Theme.Accent) : Theme.Border, (risk >= 2) ? Theme.IceHi : Theme.Text, red: true);
			string value = ((reboot >= 0) ? SettingMeta.RebootNames[Math.Min(2, reboot)] : "—");
			if (reboot == 1 && _planFlag == "ex")
			{
				value = "проводник";
			}
			Tile(g, r4, "Перезагрузка", value, (reboot >= 0) ? SettingMeta.RebootDots[Math.Min(2, reboot)] : 0, Theme.Border, Theme.Text, red: false);
			y += TileH + Theme.S(16f);
		}
		int num8 = PairH(iw, panel);
		if (num8 > 0)
		{
			if (HasOpts)
			{
				DrawOpts(g, x, y, iw);
			}
			else
			{
				int num9 = Theme.Measure("A", Theme.Tiny).Height + Theme.S(6f);
				Color fore = Theme.Mix(Theme.Sub, Theme.Text, 0.2f);
				if (TwoPair(iw, panel))
				{
					int num10 = (iw - Theme.S(24f)) / 2;
					Caption(g, "ПОЧЕМУ СТОИТ", x, y, Theme.Accent, Theme.IceHi);
					TextRenderer.DrawText(g, Pro, FInsp, new Rectangle(x, y + num9, num10, num8), fore, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
					int x2 = x + num10 + Theme.S(24f);
					Caption(g, "МОЖЕТ НЕ ПОДОЙТИ", x2, y, Theme.Accent, Theme.IceHi);
					TextRenderer.DrawText(g, Con, FInsp, new Rectangle(x2, y + num9, num10, num8), fore, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
				}
				else
				{
					int num11 = y;
					if (!string.IsNullOrEmpty(Pro))
					{
						Caption(g, "ПОЧЕМУ СТОИТ", x, num11, Theme.Accent, Theme.IceHi);
						int num12 = TextH(Pro, FInsp, iw);
						TextRenderer.DrawText(g, Pro, FInsp, new Rectangle(x, num11 + num9, iw, num12 + 2), fore, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
						num11 += num9 + num12 + Theme.S(12f);
					}
					if (!string.IsNullOrEmpty(Con))
					{
						Caption(g, "МОЖЕТ НЕ ПОДОЙТИ", x, num11, Theme.Accent, Theme.IceHi);
						TextRenderer.DrawText(g, Con, FInsp, new Rectangle(x, num11 + num9, iw, TextH(Con, FInsp, iw) + 2), fore, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
					}
				}
			}
			y += num8 + Theme.S(16f);
		}
		if (CanPlan)
		{
			int num13 = Caption(g, "ЧТО ИЗМЕНИТСЯ", x, y, Color.Empty, Theme.Dim);
			if (_planState == 2 && _plan.Count > 0)
			{
				string text4 = Plural(_plan.Count).ToUpperInvariant();
				int width = Theme.Measure(text4, Theme.Tiny).Width;
				TextRenderer.DrawText(g, text4, Theme.Tiny, new Point(x + iw - width, y), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			y += num13 + Theme.S(8f);
			int num14 = ((_planState != 2) ? 1 : Math.Max(1, _plan.Count));
			Rectangle rectangle = new Rectangle(x, y, iw, num14 * RowH + Theme.S(14f));
			Outline(g, rectangle, Theme.S(16f), Theme.Border);
			int num15 = y + Theme.S(7f);
			int num16 = x + Theme.S(14f);
			int num17 = iw - Theme.S(28f);
			if (_planState != 2)
			{
				TextRenderer.DrawText(g, L.T("читаю текущие значения…").ToUpperInvariant(), FMono, new Point(num16, num15 + Theme.S(2f)), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			}
			else if (_plan.Count == 0)
			{
				TextRenderer.DrawText(g, L.T("значения меняет сценарий — список не готов заранее").ToUpperInvariant(), FMono, new Rectangle(num16, num15 + Theme.S(2f), num17, RowH), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			}
			else
			{
				// Колонки по содержимому: значения занимают сколько нужно, остальное делят ключ и имя.
				int valW = 0, keyW = 1, nameW = 1;
				using (Font fv = Theme.FC(7.5f, semi: true))
				{
					foreach (string[] it in _plan)
					{
						string a0 = ShowVal(it[0], it[2]);
						string b0 = ShowVal(it[0], it[3]);
						valW = Math.Max(valW, Theme.Measure(a0, FMono).Width + Theme.Measure("→", FMono).Width + Theme.Measure(b0, fv).Width + Theme.S(16f));
						keyW = Math.Max(keyW, Theme.Measure(ShortKey(it[0]), FMono).Width + Theme.S(10f));
						nameW = Math.Max(nameW, Theme.Measure(it[1], FMono).Width + Theme.S(10f));
					}
				}
				valW = Math.Min(valW, (int)(num17 * 0.4f));
				int restW = num17 - valW;
				int num18;
				int num19;
				if (keyW + nameW <= restW)
				{
					num18 = keyW + (restW - keyW - nameW) / 2;
					num19 = restW - num18;
				}
				else
				{
					num18 = Math.Max((int)(restW * 0.3f), Math.Min(keyW, restW - Math.Min(nameW, (int)(restW * 0.55f))));
					num19 = restW - num18;
				}
				int num20 = 0;
				foreach (string[] item in _plan)
				{
					TextRenderer.DrawText(g, ShortKey(item[0]), FMono, new Rectangle(num16, num15, num18 - Theme.S(8f), RowH), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					TextRenderer.DrawText(g, item[1], FMono, new Rectangle(num16 + num18, num15, num19 - Theme.S(8f), RowH), Color.FromArgb(207, 207, 207), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					string text5 = ShowVal(item[0], item[2]);
					string text6 = ShowVal(item[0], item[3]);
					bool flag3 = text5 == text6;
					int num21 = num16 + num18 + num19;
					int width2 = Theme.Measure(text5, FMono).Width;
					TextRenderer.DrawText(g, text5, FMono, new Rectangle(num21, num15, width2 + 2, RowH), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					int num22 = num21 + width2 + Theme.S(6f);
					string text7 = (flag3 ? "=" : "→");
					int width3 = Theme.Measure(text7, FMono).Width;
					if (!flag3)
					{
						Theme.Dot(g, (float)num22 + (float)width3 / 2f, (float)num15 + (float)RowH / 2f, Theme.S(1.2f), Color.FromArgb(120, Theme.Accent), 0.9f);
					}
					TextRenderer.DrawText(g, text7, FMono, new Rectangle(num22, num15, width3 + 2, RowH), flag3 ? Theme.Sub : Theme.Accent, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					int num23 = num22 + width3 + Theme.S(6f);
					TextRenderer.DrawText(g, Scramble(text6, num20), Theme.FC(7.5f, semi: true), new Rectangle(num23, num15, Math.Max(10, num16 + num17 - num23), RowH), flag3 ? Theme.Sub : Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					num15 += RowH;
					num20++;
				}
			}
			y = rectangle.Bottom + Theme.S(14f);
		}
		int num24 = Caption(g, "РЕКОМЕНДАЦИЯ", x, y, Color.Empty, Theme.Dim);
		y += num24 + Theme.S(4f);
		string text8 = RecLine();
		bool flag4 = Rec != null && _cur != null && Same(_cur, Rec);
		int num25 = TextH(text8, FInsp, iw);
		TextRenderer.DrawText(g, text8, FInsp, new Rectangle(x, y, iw, num25 + 2), flag4 ? Theme.Sub : Color.White, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		y += num25;
		if (!string.IsNullOrEmpty(Why))
		{
			y += Theme.S(3f);
			int num26 = TextH(Why, FInsp, iw);
			TextRenderer.DrawText(g, Why, FInsp, new Rectangle(x, y, iw, num26 + 2), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			y += num26;
		}
		y += Theme.S(12f);
		num24 = Caption(g, "ОТКАТ", x, y, Color.Empty, Theme.Dim);
		y += num24 + Theme.S(4f);
		int num27 = TextH("Прежнее значение сохраняется в «Историю». Вернуть можно здесь или там — по одному параметру или всё сразу.", FInsp, iw);
		TextRenderer.DrawText(g, "Прежнее значение сохраняется в «Историю». Вернуть можно здесь или там — по одному параметру или всё сразу.", FInsp, new Rectangle(x, y, iw, num27 + 2), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		y += num27;
		return y;
	}

	internal void PanelOpened()
	{
		OnInspectorOpened();
		_planT0 = Environment.TickCount;
	}

	internal void PanelToggleTech()
	{
		if (HasTech)
		{
			_tech = !_tech;
			StartTyping();
			InvInsp();
		}
	}

	internal string PanelRecButton(out bool enabled)
	{
		enabled = false;
		if (Unsupported)
		{
			return "Недоступно";
		}
		if (Rec == null)
		{
			return "На твой выбор";
		}
		if (_cur != null && Same(_cur, Rec))
		{
			return "Уже как советуем";
		}
		enabled = _cur != null;
		return "Сделать как советуем";
	}

	internal void PanelApplyRec()
	{
		PanelRecButton(out var enabled);
		if (!enabled)
		{
			return;
		}
		if (Toggle != null && Rec is bool)
		{
			if (Toggle.On != (bool)Rec)
			{
				OnToggle();
			}
		}
		else if (Seg != null && Rec is int)
		{
			OnSeg((int)Rec);
		}
	}

	private void DrawRowDots(Graphics g)
	{
		bool? matches = Matches;
		float r = Theme.S(8f);
		float cx = (float)(base.Pad + Theme.S(2f)) + r;
		float cy = (float)_baseH / 2f;
		Theme.StatusCircle(g, cx, cy, r, Unsupported ? 0 : (matches == true ? 1 : (matches == false ? 2 : 3)));
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		if (InList)
		{
			e.Graphics.Clear(Theme.Card1);
		}
		else
		{
			base.OnPaintBackground(e);
		}
	}

	public override void PaintSurface(Graphics g)
	{
		if (!InList)
		{
			base.PaintSurface(g);
			return;
		}
		Page page = OwnerPage();
		bool flag = page != null && page.Selected == this;
		float value = HotT.Value;
		Color bg = Theme.Card1;
		if (value > 0.01f)
		{
			bg = Theme.Mix(bg, Color.FromArgb(25, 25, 30), value);
		}
		if (flag)
		{
			bg = Color.FromArgb(28, 28, 34);
		}
		using (SolidBrush brush = new SolidBrush(bg))
		{
			g.FillRectangle(brush, 0, 0, base.Width, base.Height);
		}
		using (SolidBrush line = new SolidBrush(Theme.Line))
		{
			g.FillRectangle(line, 0, 0, base.Width, 1);
		}
		if (flag)
		{
			using SolidBrush acc = new SolidBrush(Theme.Accent);
			g.FillRectangle(acc, 0, 0, Theme.S(2f), base.Height);
		}
	}

	private static string[] SplitLines(string s, Font f, int w)
	{
		List<string> list = new List<string>();
		string[] array = s.Split('\n');
		foreach (string obj in array)
		{
			string text = "";
			string[] array2 = obj.Split(' ');
			foreach (string text2 in array2)
			{
				string text3 = ((text.Length == 0) ? text2 : (text + " " + text2));
				if (text.Length > 0 && Theme.Measure(text3, f).Width > w)
				{
					list.Add(text);
					text = text2;
				}
				else
				{
					text = text3;
				}
			}
			list.Add(text);
		}
		return list.ToArray();
	}

	private bool InspectorMouseUp(MouseEventArgs e)
	{
		if (_expanded && !_techBtnR.IsEmpty && _techBtnR.Contains(e.Location))
		{
			_tech = !_tech;
			StartTyping();
			Relayout();
			Invalidate();
			return true;
		}
		return false;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		bool flag = _expanded && !_techBtnR.IsEmpty && _techBtnR.Contains(e.Location);
		if (flag != _techHot)
		{
			_techHot = flag;
			Invalidate();
		}
		Cursor cursor = ((flag || (HasInfo && e.Y < _baseH)) ? Cursors.Hand : Cursors.Default);
		if (Cursor != cursor)
		{
			Cursor = cursor;
		}
	}
}
