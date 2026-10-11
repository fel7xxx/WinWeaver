using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace WinWeaver;

internal class Page : Control
{
	public string Key;

	public string Title;

	public string Subtitle;

	public string GlyphText;

	public readonly Canvas Body;

	public readonly List<IBlock> Blocks = new List<IBlock>();

	/// <summary>Блоки, которые не показываются, но обновляются вместе со страницей.</summary>
	public readonly List<IBlock> Reserve = new List<IBlock>();

	/// <summary>Не рисовать заголовок страницы (у «Главной» свой).</summary>
	public bool NoHeader;

	public Action OnShown;

	private int _scroll;

	private int _contentH;

	private bool _dragThumb;

	private int _dragY0;

	private int _dragS0;

	private readonly Timer _relayout;

	private readonly Dictionary<IBlock, int> _section = new Dictionary<IBlock, int>();

	private int _addSection = -1;

	public TabStrip Tabs;

	public int CurrentTab;

	public ReadGate Gate;

	private Action _gateRead;

	public InspectorPanel Insp;

	public SettingCard Selected;

	public bool SplitOn;

	public Rectangle ListRect;

	public int ListLineY = -1;

	private int _splitX;

	private int _splitW;

	private int _splitIdx = -1;

	private float _smoothTarget = -1f;

	private float _smoothPos;

	private bool _smoothOn;

	private double _smoothLast;

	private Func<bool> _smoothFrame;

	private static double _scrollingUntil;

	public Action AfterTab;

	public Action AfterRefresh;

	public int FreshStamp = -1;

	public DateTime FreshAt = DateTime.MinValue;

	private readonly Dictionary<IBlock, string> _sigs = new Dictionary<IBlock, string>();

	public new int Margin
	{
		get
		{
			if (base.Width <= Theme.S(1000f))
			{
				return Theme.S(28f);
			}
			return Theme.S(40f);
		}
	}

	private int BarW => Theme.S(12f);

	private int MaxScroll => Math.Max(0, _contentH - base.Height);

	public bool Gated
	{
		get
		{
			if (Gate != null)
			{
				return Gate.Visible;
			}
			return false;
		}
	}

	public static bool Scrolling => Anim.Now < _scrollingUntil;

	public Page(string key, string glyph, string title, string subtitle)
	{
		Key = key;
		GlyphText = glyph;
		Title = title;
		Subtitle = subtitle;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
		Body = new Canvas(this);
		base.Controls.Add(Body);
		_relayout = new Timer
		{
			Interval = 10
		};
		_relayout.Tick += delegate
		{
			_relayout.Stop();
			DoLayoutBlocks();
		};
	}

	public int SectionOf(IBlock b)
	{
		return _section.TryGetValue(b, out var v) ? v : -1;
	}

	public T Add<T>(T b) where T : IBlock
	{
		Blocks.Add(b);
		_section[b] = _addSection;
		AddControls(b);
		return b;
	}

	public TabStrip AddTabs(params string[] names)
	{
		_addSection = -1;
		Tabs = Add(new TabStrip(names));
		Tabs.Changed += delegate(int i)
		{
			SelectTab(i);
		};
		_addSection = 0;
		return Tabs;
	}

	public void Section(int i)
	{
		_addSection = i;
	}

	public void SelectTab(int i, bool animate = true)
	{
		if (Tabs != null && i >= 0 && i < Tabs.Items.Length)
		{
			int currentTab = CurrentTab;
			int val = Math.Max(0, Tabs.Top - Theme.S(12f));
			int newScroll = Math.Min(_scroll, val);
			bool flag = newScroll == _scroll;
			Action action = delegate
			{
				StopSmooth();
				CurrentTab = i;
				Tabs.Select(i);
				_scroll = newScroll;
				DoLayoutBlocks();
				PinRows();
			};
			if (animate && currentTab != i && base.Visible)
			{
				int num = (flag ? Math.Max(0, Body.Top + Tabs.Bottom + Theme.S(6f)) : 0);
				Transition.Play(this, action, new Rectangle(0, num, base.Width - BarW, base.Height - num), (i > currentTab) ? 1 : (-1), 220);
			}
			else
			{
				action();
			}
			if (currentTab != i && AfterTab != null)
			{
				AfterTab();
			}
		}
	}

	private bool InTab(IBlock b)
	{
		if (b is SettingCard { IsExpert: not false } && !Prefs.Expert)
		{
			return false;
		}
		if (_section.TryGetValue(b, out var value) && value >= 0)
		{
			return value == CurrentTab;
		}
		return true;
	}

	private static void SetShown(IBlock b, bool v)
	{
		if (b is Control control)
		{
			if (control.Visible != v)
			{
				control.Visible = v;
			}
		}
		else
		{
			if (!(b is Grid grid))
			{
				return;
			}
			foreach (IBlock item in grid.Items)
			{
				SetShown(item, v);
			}
		}
	}

	private void AddControls(IBlock b)
	{
		if (b is Control value)
		{
			Body.Controls.Add(value);
		}
		else
		{
			if (!(b is Grid grid))
			{
				return;
			}
			foreach (IBlock item in grid.Items)
			{
				AddControls(item);
			}
		}
	}

	public void RelayoutSoon()
	{
		_relayout.Stop();
		_relayout.Start();
	}

	public void ShowGate(Action read)
	{
		if (Gate == null)
		{
			Gate = new ReadGate
			{
				PageTitle = Title
			};
			Gate.ReadClicked += delegate
			{
				_gateRead?.Invoke();
			};
			base.Controls.Add(Gate);
		}
		_gateRead = read;
		Gate.Busy = false;
		_scroll = 0;
		ClampScroll();
		Gate.Visible = true;
		PlaceGate();
		PlacePanel();
		Gate.BringToFront();
	}

	public void HideGate()
	{
		if (Gate != null && Gate.Visible)
		{
			Gate.Busy = false;
			Gate.Visible = false;
			PlacePanel();
			Invalidate();
		}
	}

	public void GateRead()
	{
		if (Gated)
		{
			_gateRead?.Invoke();
		}
	}

	private void PlaceGate()
	{
		if (Gated)
		{
			int num = Math.Max(0, Body.Top + Body.HeaderH);
			Gate.Bounds = new Rectangle(0, num, Math.Max(1, base.Width - BarW), Math.Max(1, base.Height - num));
		}
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		DoLayoutBlocks();
	}

	public void DoLayoutBlocks()
	{
		long t = Lag.T0();
		try
		{
			DoLayoutBlocksCore();
			PlaceGate();
		}
		finally
		{
			Lag.Done("DoLayoutBlocks " + Key + (base.Visible ? "" : " (скрыт)"), t);
		}
	}

	public void SelectCard(SettingCard c)
	{
		if (c != null && SplitOn && Selected != c)
		{
			SettingCard selected = Selected;
			Selected = c;
			selected?.Invalidate();
			c.Invalidate();
			if (Insp != null)
			{
				Insp.Show(c);
			}
			RelayoutSoon();
		}
	}

	private bool SplitWanted(int contentW)
	{
		if (contentW >= Theme.S(860f))
		{
			return Environment.GetEnvironmentVariable("WW_NOSPLIT") != "1";
		}
		return false;
	}

	private void DoLayoutBlocksCore()
	{
		if (base.Width <= 0 || base.Height <= 0)
		{
			return;
		}
		int num = base.Width - BarW;
		int margin = Margin;
		int num2 = Math.Min(num - margin * 2, Theme.S(1200f));
		margin = Math.Max(margin, (num - num2) / 2);
		int num3 = -1;
		if (SplitWanted(num2))
		{
			for (int i = 0; i < Blocks.Count; i++)
			{
				if (Blocks[i] is SettingCard { HasInfo: not false } settingCard && InTab(settingCard))
				{
					num3 = i;
					break;
				}
			}
			if (num3 >= 0 && Tabs != null)
			{
				int num4 = Blocks.IndexOf(Tabs);
				if (num4 >= 0 && num4 < num3)
				{
					num3 = num4;
				}
			}
		}
		SplitOn = num3 >= 0;
		_splitIdx = num3;
		int num5 = (SplitOn ? Math.Max(Theme.S(340f), Math.Min(Theme.S(440f), (int)((float)num2 * 0.36f))) : 0);
		int strip = InspectorPanel.Strip;
		int num6 = num2 - num5 - strip;
		int num7 = Theme.S(12f);
		Body.SuspendLayout();
		int num8 = Body.HeaderH;
		int num9 = Theme.S(12f);
		int num10 = 0;
		int num11 = num9;
		ListLineY = -1;
		int[] array = ((Tabs != null) ? new int[Tabs.Items.Length] : null);
		bool[] array2 = ((Tabs != null) ? new bool[Tabs.Items.Length] : null);
		int num12 = 0;
		while (array != null && num12 < Blocks.Count)
		{
			if (Blocks[num12] is SettingCard settingCard2 && (!settingCard2.IsExpert || Prefs.Expert) && _section.TryGetValue(Blocks[num12], out var value) && value >= 0 && value < array.Length)
			{
				array[value]++;
				if (settingCard2.Matches == false && !settingCard2.Unsupported)
				{
					array2[value] = true;
				}
			}
			num12++;
		}
		if (array != null)
		{
			Tabs.SetCounts(array, array2);
		}
		for (int j = 0; j < Blocks.Count; j++)
		{
			IBlock block = Blocks[j];
			bool flag = InTab(block);
			SetShown(block, flag);
			if (flag)
			{
				bool flag2 = SplitOn && j >= num3;
				if (SplitOn && j == num3)
				{
					num10 = num8;
					num8 += num7;
				}
				if (block is SettingCard settingCard3)
				{
					settingCard3.SetInList(flag2);
				}
				int num13 = (flag2 ? ((block is SettingCard) ? block.DoLayout(margin + 1, num8, num6 - 3) : block.DoLayout(margin + num7, num8, num6 - num7 * 2)) : block.DoLayout(margin, num8, num2));
				if (flag2 && block == Tabs)
				{
					ListLineY = num8 + num13 + Theme.S(4f);
					num8 = ListLineY;
					num11 = 0;
				}
				else
				{
					num11 = ((flag2 && block is SettingCard) ? 0 : num9);
					num8 += num13 + num11;
				}
			}
		}
		Body.ResumeLayout(performLayout: false);
		if (SplitOn)
		{
			EnsureSelection();
			if (Insp == null)
			{
				Insp = new InspectorPanel(this);
				base.Controls.Add(Insp);
				Insp.BringToFront();
			}
			Insp.Show(Selected);
			int val = num8 - num11 + num7;
			int num14 = Math.Min(base.Height - Theme.S(32f), Insp.DesiredH(num5 + strip));
			val = Math.Max(val, num10 + num14);
			ListRect = new Rectangle(margin, num10, num6, val - num10);
			_splitX = margin + num6;
			_splitW = num5 + strip;
			num8 = val + num9;
		}
		else
		{
			ListRect = Rectangle.Empty;
			if (Selected != null)
			{
				SettingCard selected = Selected;
				Selected = null;
				selected.Invalidate();
			}
			if (Insp != null)
			{
				Insp.Show(null);
			}
		}
		_contentH = num8 + Theme.S(24f);
		Body.SetBounds(0, -_scroll, num, Math.Max(base.Height, _contentH));
		ClampScroll();
		PlacePanel();
		Invalidate();
		Body.Invalidate(invalidateChildren: true);
	}

	private void EnsureSelection()
	{
		if (Selected != null && Selected.HasInfo && InTab(Selected) && Blocks.IndexOf(Selected) >= _splitIdx)
		{
			return;
		}
		SettingCard selected = Selected;
		Selected = null;
		for (int i = Math.Max(0, _splitIdx); i < Blocks.Count; i++)
		{
			if (Blocks[i] is SettingCard { HasInfo: not false } settingCard && InTab(settingCard))
			{
				Selected = settingCard;
				break;
			}
		}
		selected?.Invalidate();
		Selected?.Invalidate();
	}

	private void PlacePanel()
	{
		if (Insp == null)
		{
			return;
		}
		if (!SplitOn || Gated || Selected == null)
		{
			if (Insp.Visible)
			{
				Insp.Visible = false;
			}
			return;
		}
		int num = Theme.S(16f);
		int num2 = ListRect.Top - _scroll;
		int val = ListRect.Bottom - _scroll;
		int num3 = Math.Max(num, num2);
		int num4 = Math.Min(base.Height - num, val);
		int num5 = Math.Min(Theme.S(320f), ListRect.Height);
		if (num4 - num3 < num5)
		{
			if (num2 >= num)
			{
				num4 = num3 + num5;
			}
			else
			{
				num3 = num4 - num5;
			}
		}
		Rectangle rectangle = new Rectangle(_splitX, num3, _splitW, Math.Max(1, num4 - num3));
		if (Insp.Bounds != rectangle)
		{
			Insp.Bounds = rectangle;
		}
		Insp.Invalidate(new Rectangle(0, 0, InspectorPanel.Strip + Theme.S(2f), Insp.Height));
		if (!Insp.Visible)
		{
			Insp.Visible = true;
		}
	}

	public void PinRows()
	{
		if (!SplitOn || !Anim.Enabled || !base.Visible)
		{
			return;
		}
		List<Control> rows = new List<Control>();
		for (int i = Math.Max(0, _splitIdx); i < Blocks.Count; i++)
		{
			if (Blocks[i] is Control { Visible: not false } control && Blocks[i] != Tabs && control.Top - _scroll < base.Height && control.Bottom - _scroll > 0)
			{
				rows.Add(control);
			}
			if (rows.Count >= 14)
			{
				break;
			}
		}
		if (rows.Count == 0)
		{
			return;
		}
		int[] baseX = new int[rows.Count];
		int dx = Theme.S(12f);
		for (int j = 0; j < rows.Count; j++)
		{
			baseX[j] = rows[j].Left;
			rows[j].Left = baseX[j] - dx;
		}
		int each = 460;
		int stagger = 40;
		int total = each + stagger * (rows.Count - 1);
		Anim.Start(this, total, delegate(float t)
		{
			float num = t * (float)total;
			for (int l = 0; l < rows.Count; l++)
			{
				if (!rows[l].IsDisposed)
				{
					float t2 = Anim.Clamp((num - (float)(l * stagger)) / (float)each);
					int num2 = (int)Math.Round((float)dx * (1f - Anim.EaseOut(t2)));
					if (rows[l].Left != baseX[l] - num2)
					{
						rows[l].Left = baseX[l] - num2;
					}
				}
			}
		}, delegate
		{
			for (int k = 0; k < rows.Count; k++)
			{
				if (!rows[k].IsDisposed && rows[k].Left != baseX[k])
				{
					rows[k].Left = baseX[k];
				}
			}
		}, "pin-" + GetHashCode());
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		base.OnVisibleChanged(e);
		if (base.Visible)
		{
			PinRows();
		}
	}

	public void SmoothBy(int d)
	{
		if (Gated)
		{
			return;
		}
		if (!Anim.Enabled)
		{
			ScrollBy(d);
			return;
		}
		if (_smoothTarget < 0f)
		{
			_smoothTarget = _scroll;
			_smoothPos = _scroll;
			_smoothLast = Anim.Now;
		}
		else if (Math.Abs(_smoothPos - (float)_scroll) > 1.5f)
		{
			_smoothPos = _scroll;
		}
		float num = _smoothTarget - _smoothPos;
		if (d != 0 && num != 0f && Math.Sign(d) != Math.Sign(num))
		{
			_smoothTarget = _smoothPos;
		}
		_smoothTarget = Math.Max(0f, Math.Min(MaxScroll, _smoothTarget + (float)d));
		if (_smoothFrame == null)
		{
			_smoothFrame = SmoothStep;
		}
		if (!_smoothOn)
		{
			_smoothOn = true;
			_smoothLast = Anim.Now - 16.0;
			if (SmoothStep())
			{
				Anim.Frame(_smoothFrame);
			}
		}
	}

	private void StopSmooth()
	{
		_smoothTarget = -1f;
		_smoothOn = false;
	}

	private bool SmoothStep()
	{
		if (_smoothTarget < 0f || base.IsDisposed)
		{
			_smoothOn = false;
			return false;
		}
		if (Math.Abs(_smoothPos - (float)_scroll) > 1.5f)
		{
			_smoothPos = _scroll;
		}
		double now = Anim.Now;
		float num = Math.Min(50f, (float)(now - _smoothLast));
		_smoothLast = now;
		_smoothTarget = Math.Max(0f, Math.Min(MaxScroll, _smoothTarget));
		float num2 = 1f - (float)Math.Exp((0f - num) / 45f);
		_smoothPos += (_smoothTarget - _smoothPos) * num2;
		bool num3 = Math.Abs(_smoothTarget - _smoothPos) < 0.6f;
		if (num3)
		{
			_smoothPos = _smoothTarget;
		}
		int num4 = (int)Math.Round(_smoothPos);
		if (num4 != _scroll)
		{
			_scroll = num4;
			ClampScroll();
			Invalidate(new Rectangle(base.Width - BarW, 0, BarW, base.Height));
			PaintNow();
		}
		if (num3)
		{
			StopSmooth();
			return false;
		}
		return true;
	}


	private void PaintNow()
	{
		// Avalonia: сдвиг Body перерисовывается композитором без повторного OnPaint.
		_scrollingUntil = Anim.Now + 120.0;
	}

	private void ClampScroll()
	{
		int num = Math.Max(0, Math.Min(MaxScroll, _scroll));
		if (num != _scroll || Body.Top != -num)
		{
			_scroll = num;
			Body.Top = -num;
		}
		if (SplitOn)
		{
			PlacePanel();
		}
	}

	public void ScrollBy(int d)
	{
		if (!Gated)
		{
			StopSmooth();
			int scroll = _scroll;
			_scroll += d;
			ClampScroll();
			if (_scroll != scroll)
			{
				Invalidate(new Rectangle(base.Width - BarW, 0, BarW, base.Height));
				PaintNow();
			}
		}
	}

	public void ScrollTop()
	{
		StopSmooth();
		_scroll = 0;
		ClampScroll();
		Invalidate();
	}

	internal static int WheelPx(int delta)
	{
		int num = SystemInformation.MouseWheelScrollLines;
		if (num <= 0 || num > 20)
		{
			num = 3;
		}
		return (int)Math.Round((double)delta / 120.0 * (double)num * (double)Theme.S(40f));
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		SmoothBy(-WheelPx(e.Delta));
		if (e is HandledMouseEventArgs)
		{
			((HandledMouseEventArgs)e).Handled = true;
		}
	}

	private Rectangle Thumb()
	{
		if (MaxScroll <= 0)
		{
			return Rectangle.Empty;
		}
		int num = base.Height - Theme.S(16f);
		int num2 = Math.Max(Theme.S(40f), num * base.Height / Math.Max(1, _contentH));
		int y = Theme.S(8f) + (num - num2) * _scroll / MaxScroll;
		return new Rectangle(base.Width - BarW + Theme.S(3f), y, Theme.S(6f), num2);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		Rectangle rectangle = Thumb();
		if (rectangle != Rectangle.Empty && e.X >= base.Width - BarW)
		{
			if (rectangle.Contains(e.Location))
			{
				StopSmooth();
				_dragThumb = true;
				_dragY0 = e.Y;
				_dragS0 = _scroll;
				base.Capture = true;
			}
			else
			{
				SmoothBy((e.Y < rectangle.Y) ? (-base.Height + Theme.S(60f)) : (base.Height - Theme.S(60f)));
			}
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (_dragThumb)
		{
			Rectangle rectangle = Thumb();
			int num = base.Height - Theme.S(16f) - rectangle.Height;
			if (num > 0)
			{
				_scroll = _dragS0 + (e.Y - _dragY0) * MaxScroll / num;
				ClampScroll();
				Invalidate(new Rectangle(base.Width - BarW, 0, BarW, base.Height));
				PaintNow();
			}
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		_dragThumb = false;
		base.Capture = false;
		base.OnMouseUp(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		using (SolidBrush brush = new SolidBrush(Theme.Bg))
		{
			graphics.FillRectangle(brush, base.Width - BarW, 0, BarW, base.Height);
		}
		int num = Theme.S(280f);
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(base.Width - BarW, -_scroll - 1, BarW, num + 2), Color.FromArgb(30, 30, 35), Theme.Bg, 90f))
		{
			graphics.FillRectangle(brush2, base.Width - BarW, -_scroll, BarW, num);
		}
		Rectangle rectangle = Thumb();
		if (rectangle == Rectangle.Empty)
		{
			return;
		}
		using GraphicsPath path = Theme.Round(rectangle, (float)rectangle.Width / 2f);
		using LinearGradientBrush brush3 = new LinearGradientBrush(new Rectangle(rectangle.X, rectangle.Y - 1, rectangle.Width, rectangle.Height + 2), Color.FromArgb(110, 110, 122), Color.FromArgb(70, 70, 80), 90f);
		graphics.FillPath(brush3, path);
	}

	public void Refresh(Worker w)
	{
		Refresh(w, all: false, null);
	}

	public void Refresh(Worker w, bool all, Action done)
	{
		List<IBlock> list = (all ? new List<IBlock>(Blocks) : Blocks.FindAll(InTab));
		foreach (IBlock r in Reserve)
		{
			if (!list.Contains(r))
			{
				list.Add(r);
			}
		}
		IBlock[] blocks = list.ToArray();
		w.Run(delegate(IEngine e)
		{
			object[] array = new object[blocks.Length];
			for (int i = 0; i < blocks.Length; i++)
			{
				try
				{
					array[i] = blocks[i].Fetch(e);
				}
				catch
				{
				}
			}
			return array;
		}, delegate(object o)
		{
			ApplyWhenCalm(o, blocks, done, all);
		});
	}

	public void ForgetData()
	{
		_sigs.Clear();
	}

	private static string Sig(object v, int depth)
	{
		if (depth > 8)
		{
			return null;
		}
		if (v == null)
		{
			return "~";
		}
		if (v is string text)
		{
			return "s" + text.Length + ":" + text;
		}
		if (v is bool || v is int || v is long || v is double || v is float || v is decimal || v is short || v is byte || v is uint || v is ulong || v is DateTime || v.GetType().IsEnum)
		{
			return v.GetType().Name[0] + Convert.ToString(v, CultureInfo.InvariantCulture);
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (v is IDictionary dictionary)
		{
			List<string> list = new List<string>();
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
			foreach (DictionaryEntry item in dictionary)
			{
				string text2 = Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? "";
				list.Add(text2);
				dictionary2[text2] = item.Value;
			}
			list.Sort(StringComparer.Ordinal);
			stringBuilder.Append('{');
			foreach (string item2 in list)
			{
				string text3 = Sig(dictionary2[item2], depth + 1);
				if (text3 == null)
				{
					return null;
				}
				stringBuilder.Append(item2.Length).Append(':').Append(item2)
					.Append('=')
					.Append(text3)
					.Append(';');
			}
			stringBuilder.Append('}');
			return stringBuilder.ToString();
		}
		if (v is IEnumerable enumerable)
		{
			stringBuilder.Append('[');
			foreach (object item3 in enumerable)
			{
				string text4 = Sig(item3, depth + 1);
				if (text4 == null)
				{
					return null;
				}
				stringBuilder.Append(text4).Append(',');
			}
			stringBuilder.Append(']');
			return stringBuilder.ToString();
		}
		return null;
	}

	private void ApplyWhenCalm(object o, IBlock[] blocks, Action done, bool skipSame = false)
	{
		int num = (int)(Transition.BusyUntil - DateTime.Now).TotalMilliseconds;
		if (num > 0 && base.Visible)
		{
			Timer t = new Timer
			{
				Interval = Math.Max(15, num)
			};
			t.Tick += delegate
			{
				t.Stop();
				t.Dispose();
				ApplyWhenCalm(o, blocks, done, skipSame);
			};
			t.Start();
			return;
		}
		if (o is object[] array)
		{
			bool flag = false;
			SuspendLayout();
			for (int i = 0; i < blocks.Length && i < array.Length; i++)
			{
				string text = Sig(array[i], 0);
				if (skipSame && text != null && _sigs.TryGetValue(blocks[i], out var value) && value == text)
				{
					continue;
				}
				flag = true;
				try
				{
					long t2 = Lag.T0();
					blocks[i].Apply(array[i]);
					Lag.Done("Применение " + Key + "/" + blocks[i].GetType().Name, t2);
					if (text != null)
					{
						_sigs[blocks[i]] = text;
					}
					else
					{
						_sigs.Remove(blocks[i]);
					}
				}
				catch
				{
					_sigs.Remove(blocks[i]);
				}
			}
			long t3 = Lag.T0();
			ResumeLayout(flag);
			Lag.Done("ResumeLayout " + Key, t3);
			if (flag)
			{
				t3 = Lag.T0();
				AfterRefresh?.Invoke();
				Lag.Done("AfterRefresh " + Key, t3);
				t3 = Lag.T0();
				DoLayoutBlocks();
				Lag.Done("Раскладка " + Key, t3);
			}
		}
		done?.Invoke();
	}
}
