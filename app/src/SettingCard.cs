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
using System.Windows.Forms;

namespace WinWeaver
{
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

		// ---- expandable explanation ----
		public string What;

		public string Pro;

		public string Con;

		public string Why;

		// recommended value: null = up to the user, bool for toggles, int for choices
		public object Rec;

		public int MinBuild;

		public int MaxBuild;

		public string GetScript;

		public string SetScript;

		public string Cat;

		// shown only in expert mode (weakens protection or can break apps)
		public bool IsExpert;

		public SettingCard Expert()
		{
			IsExpert = true;
			return this;
		}

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

		public bool HasInfo => What != null;

		public bool Unsupported => (MinBuild > 0 && Win.Build < MinBuild) || (MaxBuild > 0 && Win.Build > MaxBuild);

		public object Current => _cur;

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
			return new SettingCard(host, title, desc, (IEngine e) => (options != null) ? ((object)e.As(e.Eval(getScript), -1)) : ((object)e.As(e.Eval(getScript), fallback: false)), delegate(IEngine e, object v)
			{
				e.Eval(setScript, new Hashtable { { "v", v } });
			}, options);
		}

		// the change goes through Invoke-FSetting: it is skipped when nothing changes and is written to the history with the previous value
		public static SettingCard Tracked(Host host, string title, string desc, string getScript, string setScript, string[] options, string cat)
		{
			SettingCard c = TrackedRaw(host, title, desc, getScript, setScript, options, cat);
			c.GetScript = getScript;
			c.SetScript = setScript;
			c.Cat = cat ?? "other";
			return c;
		}

		// history title for a value (same wording as when the user flips the control)
		public string HistTitle(object v)
		{
			string[] options = (Seg != null) ? Seg.Items : null;
			return (options != null) ? (Title + ": " + ((v is int && (int)v >= 0 && (int)v < options.Length) ? options[(int)v] : "?")) : ((v is bool && (bool)v) ? Title : (Title + " (отменено)"));
		}

		private static SettingCard TrackedRaw(Host host, string title, string desc, string getScript, string setScript, string[] options, string cat)
		{
			return new SettingCard(host, title, desc, (IEngine e) => (options != null) ? ((object)e.As(e.Eval(getScript), -1)) : ((object)e.As(e.Eval(getScript), fallback: false)), delegate(IEngine e, object v)
			{
				string ht = (options != null) ? (title + ": " + ((v is int && (int)v >= 0 && (int)v < options.Length) ? options[(int)v] : "?")) : ((v is bool && (bool)v) ? title : (title + " (отменено)"));
				e.Eval("Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5", new Hashtable
				{
					{ "fa1", ht },
					{ "fa2", getScript },
					{ "fa3", setScript },
					{ "fa4", v },
					{ "fa5", cat ?? "other" }
				});
			}, options);
		}

		// independent copy (same scripts and texts) for the "recommendations" page
		public SettingCard CloneFor(Host host)
		{
			SettingCard c = Tracked(host, Title, Desc, GetScript, SetScript, (Seg != null) ? Seg.Items : null, Cat);
			if (HasInfo)
			{
				c.Info(What, Pro, Con, Rec, Why);
			}
			c.OptDescs = OptDescs;
			c.FpsBest = FpsBest;
			c.IsExpert = IsExpert;
			c.FpsW = FpsW;
			c.Builds(MinBuild, MaxBuild);
			return c;
		}

		// one short description per segment option; replaces the generic pro/con blocks in the details
		public string[] OptDescs;

		public SettingCard Opts(params string[] descs)
		{
			OptDescs = descs;
			return this;
		}

		private bool HasOpts => OptDescs != null && Seg != null && OptDescs.Length > 0;

		private int OptsW(int iw) => iw - Theme.S(36f);

		private int OptRowH(int i, int iw)
		{
			string d = (i < OptDescs.Length) ? OptDescs[i] : "";
			int nh = Theme.Measure("Ag", OptNameF).Height;
			int dh = string.IsNullOrEmpty(d) ? 0 : (Theme.S(3f) + Theme.Measure(d, Theme.Small, OptsW(iw), wrap: true).Height);
			return nh + dh + Theme.S(16f);
		}

		private static readonly Font OptNameF = Theme.F(9.5f, semi: true);

		private int OptsH(int iw)
		{
			int h = Theme.Measure("ВАРИАНТЫ", Theme.Tiny).Height + Theme.S(8f);
			for (int i = 0; i < Seg.Items.Length; i++)
			{
				h += OptRowH(i, iw) + Theme.S(4f);
			}
			return h;
		}

		private int DrawOpts(Graphics g, int x, int y, int iw)
		{
			int y0 = y;
			TextRenderer.DrawText(g, "ВАРИАНТЫ", Theme.Tiny, new Point(x, y), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			y += Theme.Measure("ВАРИАНТЫ", Theme.Tiny).Height + Theme.S(8f);
			int cur = (_cur is int) ? (int)_cur : -1;
			int rec = (Rec is int) ? (int)Rec : -1;
			Theme.Smooth(g);
			for (int i = 0; i < Seg.Items.Length; i++)
			{
				int rh = OptRowH(i, iw);
				Rectangle r = new Rectangle(x, y, iw, rh);
				bool isCur = i == cur;
				bool isRec = i == rec;
				Color tone = isCur ? ((rec < 0 || isRec) ? Theme.Green : Theme.Yellow) : (isRec ? Theme.Green : Theme.Silver2);
				using (System.Drawing.Drawing2D.GraphicsPath rp = Theme.Round(r, Theme.S(9f)))
				{
					using (SolidBrush rb = new SolidBrush(isCur ? Color.FromArgb(22, tone) : Color.FromArgb(10, 255, 255, 255)))
					{
						g.FillPath(rb, rp);
					}
					using (Pen rpen = new Pen(isCur ? Color.FromArgb(80, tone) : Color.FromArgb(22, 255, 255, 255)))
					{
						g.DrawPath(rpen, rp);
					}
				}
				// radio mark
				int d = Theme.S(14f);
				int nh = Theme.Measure("Ag", OptNameF).Height;
				Rectangle rr = new Rectangle(x + Theme.S(12f), y + Theme.S(8f) + (nh - d) / 2, d, d);
				using (Pen op = new Pen(isCur ? tone : Theme.BorderHi, Theme.S(1.5f)))
				{
					g.DrawEllipse(op, rr);
				}
				if (isCur)
				{
					using (SolidBrush ob = new SolidBrush(tone))
					{
						g.FillEllipse(ob, Rectangle.Inflate(rr, -Theme.S(3.5f), -Theme.S(3.5f)));
					}
				}
				int tx = x + Theme.S(36f);
				string name = Seg.Items[i];
				TextRenderer.DrawText(g, name, OptNameF, new Point(tx, y + Theme.S(8f)), isCur ? Theme.Text : Theme.Mix(Theme.Sub, Theme.Text, 0.5f), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
				int px = tx + Theme.Measure(name, OptNameF).Width + Theme.S(8f);
				if (isCur)
				{
					int pw = Theme.PillW("сейчас", Theme.Tiny);
					Theme.Pill(g, new Rectangle(px, y + Theme.S(7f), pw, Theme.S(18f)), "сейчас", Theme.Tiny, tone);
					px += pw + Theme.S(6f);
				}
				if (isRec)
				{
					int pw = Theme.PillW("рекомендуем", Theme.Tiny);
					Theme.Pill(g, new Rectangle(px, y + Theme.S(7f), pw, Theme.S(18f)), "рекомендуем", Theme.Tiny, Theme.Green);
				}
				string ds = (i < OptDescs.Length) ? OptDescs[i] : "";
				if (!string.IsNullOrEmpty(ds))
				{
					TextRenderer.DrawText(g, ds, Theme.Small, new Rectangle(tx, y + Theme.S(8f) + nh + Theme.S(3f), OptsW(iw), rh), Theme.Mix(Theme.Sub, Theme.Text, 0.3f), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
				}
				y += rh + Theme.S(4f);
			}
			return y - y0;
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

		// true/false when the current value is known and there is a recommendation; null otherwise
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

		// value(s) that give the most FPS and how much this setting weighs in the home FPS score
		public object[] FpsBest;

		public int FpsW;

		public SettingCard Fps(int weight, params object[] best)
		{
			FpsW = weight;
			FpsBest = best;
			return this;
		}

		public bool? FpsOk
		{
			get
			{
				if (FpsBest == null || FpsW <= 0 || _cur == null || Unsupported || GetScript == null)
				{
					return null;
				}
				foreach (object b in FpsBest)
				{
					if (Same(_cur, b))
					{
						return true;
					}
				}
				return false;
			}
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
				return ((bool)Rec) ? "включить" : "выключить";
			}
			if (Rec is int && Seg != null && (int)Rec >= 0 && (int)Rec < Seg.Items.Length)
			{
				return "«" + Seg.Items[(int)Rec] + "»";
			}
			return lower ? "на твой выбор" : "На твой выбор";
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
			if (Rec == null)
			{
				c = Theme.Silver2;
				return "На твой выбор";
			}
			if (_cur != null && Same(_cur, Rec))
			{
				c = Theme.Green;
				return "Настроено";
			}
			c = (_cur == null) ? Theme.Silver2 : Theme.Yellow;
			return "Лучше " + RecValueText(lower: true);
		}

		private bool ShowPill => HasInfo || Unsupported;

		private bool PillCheck => !Unsupported && Rec != null && _cur != null && Same(_cur, Rec);

		private void OnToggle()
		{
			if (Unsupported)
			{
				return;
			}
			if (_host.TryBegin(quick: true, title: Title + ": " + (!Toggle.On ? "ВКЛ" : "ВЫКЛ")))
			{
				bool want = !Toggle.On;
				Toggle.On = want;
				Toggle.Busy = true;
				Exception fail = null;
				_host.Worker.Run(delegate(IEngine e)
				{
					return SetThenGet(e, want, out fail);
				}, delegate(object r)
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
			if (Unsupported || !_host.TryBegin(quick: true, title: Title + ((Seg != null && i >= 0 && i < Seg.Items.Length) ? (": " + Seg.Items[i]) : "")))
			{
				_host.Worker.Run((IEngine e) => _get(e), Apply);
				return;
			}
			Seg.Busy = true;
			Exception fail = null;
			_host.Worker.Run(delegate(IEngine e)
			{
				return SetThenGet(e, i, out fail);
			}, delegate(object r)
			{
				Seg.Busy = false;
				Apply(r);
				ReportFail(fail);
				_host.End(quick: true);
				Relayout();
			});
		}

		// a failed setter used to skip the read-back: the control then showed "off"/nothing instead of
		// the real state. Now the state is always read back and the error is shown separately
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

		// applies a value without touching the control (profile import); runs on the engine thread
		public void SetValue(IEngine e, object v)
		{
			_set(e, v);
		}

		public override void Apply(object v)
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
				int selected = ((v is int) ? ((int)v) : (-1));
				_cur = (selected >= 0) ? (object)selected : null;
				Seg.Selected = selected;
			}
			Invalidate();
		}

		private Page OwnerPage()
		{
			Control c = Parent;
			while (c != null && !(c is Page))
			{
				c = c.Parent;
			}
			return c as Page;
		}

		private void Relayout()
		{
			Page p = OwnerPage();
			if (p != null)
			{
				p.DoLayoutBlocks();
			}
		}

		public void SetExpanded(bool on, bool animate = true)
		{
			if (!HasInfo)
			{
				return;
			}
			_expanded = on;
			float from = _expT;
			float to = on ? 1f : 0f;
			Anim.Start(this, animate ? 260 : 0, delegate(float t)
			{
				_expT = from + (to - from) * Anim.EaseInOut(t);
				Relayout();
			}, null, this);
		}

		public bool Expanded => _expanded;

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (e.Button == MouseButtons.Left && HasInfo && e.Y < _baseH)
			{
				SetExpanded(!_expanded);
			}
		}

		private int InnerColsW(int w)
		{
			return w - base.Pad * 2;
		}

		private bool TwoCols(int w)
		{
			return !HasOpts && !string.IsNullOrEmpty(Con) && !string.IsNullOrEmpty(Pro) && InnerColsW(w) >= Theme.S(560f);
		}

		private int BlockH(string head, string text, int w)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0;
			}
			return Theme.Measure(head, Theme.Tiny).Height + Theme.S(5f) + Theme.Measure(text, Theme.Small, w, wrap: true).Height;
		}

		private const string HWhat = "ЧТО ДЕЛАЕТ";

		private const string HPro = "ПОЧЕМУ СТОИТ";

		private const string HCon = "ПОЧЕМУ МОЖЕТ НЕ ПОДОЙТИ";

		private int MeasureDetails(int w)
		{
			int iw = InnerColsW(w);
			int h = Theme.S(16f);
			h += BlockH(HWhat, What, iw) + Theme.S(14f);
			if (HasOpts)
			{
				h += OptsH(iw) + Theme.S(10f);
			}
			else if (TwoCols(w))
			{
				int cw = (iw - Theme.S(20f)) / 2;
				h += Math.Max(BlockH(HPro, Pro, cw - Theme.S(14f)), BlockH(HCon, Con, cw - Theme.S(14f))) + Theme.S(14f);
			}
			else
			{
				if (!string.IsNullOrEmpty(Pro))
				{
					h += BlockH(HPro, Pro, iw - Theme.S(14f)) + Theme.S(12f);
				}
				if (!string.IsNullOrEmpty(Con))
				{
					h += BlockH(HCon, Con, iw - Theme.S(14f)) + Theme.S(14f);
				}
			}
			h += RecBoxH(iw) + base.Pad - Theme.S(2f);
			return h;
		}

		private int RecBoxH(int iw)
		{
			int tw = iw - Theme.S(28f) - Theme.S(30f);
			int th = Theme.Measure("Рекомендация", Theme.FC(9f, semi: true)).Height;
			int bh = string.IsNullOrEmpty(Why) ? 0 : (Theme.S(3f) + Theme.Measure(Why, Theme.Small, tw, wrap: true).Height);
			return th + bh + Theme.S(24f);
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
			int chevW = HasInfo ? Theme.S(30f) : 0;
			_segBelow = Seg != null && w - num2 - chevW - pad * 3 < Theme.S(300f);
			int width = (_segBelow ? (w - pad * 2 - chevW) : (w - pad * 3 - num2 - chevW));
			_titleH = Theme.Measure(Title, Theme.Title, width, wrap: true).Height;
			_descH = ((!string.IsNullOrEmpty(Desc)) ? (Theme.Measure(Desc, Theme.Small, width, wrap: true).Height + Theme.S(4f)) : 0);
			int num5 = _titleH + _descH;
			int pillH = Theme.S(22f);
			if (ShowPill)
			{
				Color pc;
				int pw = Theme.PillW(PillText(out pc), Theme.Tiny) + (PillCheck ? Theme.S(18f) : 0);
				int tw1 = Theme.Measure(Title, Theme.Title).Width;
				_pillInline = tw1 + Theme.S(10f) + pw <= width;
				if (_pillInline)
				{
					_pillR = new Rectangle(tw1 + Theme.S(10f), (_titleH - pillH) / 2, pw, pillH);
				}
				else
				{
					_pillR = new Rectangle(0, num5 + Theme.S(7f), pw, pillH);
					num5 += pillH + Theme.S(7f);
				}
			}
			int num6 = Math.Max(Theme.S(72f), num5 + pad * 2 - Theme.S(4f));
			if (_segBelow)
			{
				num6 = num5 + pad * 2 + Seg.Height + Theme.S(10f);
			}
			_textR = new Rectangle(pad, _segBelow ? (pad - Theme.S(2f)) : ((num6 - num5) / 2), width, num5);
			_pillR.Offset(_textR.X, _textR.Y);
			_baseH = num6;
			int total = num6;
			if (HasInfo)
			{
				_detH = MeasureDetails(w);
				total += (int)Math.Round((float)_detH * _expT);
			}
			SetBounds(x, y, w, total);
			if (Toggle != null)
			{
				Toggle.Location = new Point(w - pad - Toggle.Width, (num6 - Toggle.Height) / 2);
				_chevR = new Rectangle(Toggle.Left - chevW - Theme.S(4f), (num6 - Theme.S(28f)) / 2, chevW, Theme.S(28f));
			}
			else if (_segBelow)
			{
				Seg.SetBounds(pad, num6 - pad - Seg.Height, w - pad * 2, Seg.Height);
				_chevR = new Rectangle(w - pad - chevW + Theme.S(6f), _textR.Y + (_titleH - Theme.S(28f)) / 2, chevW, Theme.S(28f));
			}
			else
			{
				Seg.SetBounds(w - pad - num, (num6 - Seg.Height) / 2, num, Seg.Height);
				_chevR = new Rectangle(Seg.Left - chevW - Theme.S(4f), (num6 - Theme.S(28f)) / 2, chevW, Theme.S(28f));
			}
			return total;
		}

		private void DrawChevron(Graphics g)
		{
			if (!HasInfo)
			{
				return;
			}
			Theme.Smooth(g);
			float cx = _chevR.X + _chevR.Width / 2f;
			float cy = _chevR.Y + _chevR.Height / 2f;
			float r = Theme.S(4.5f);
			float a = _expT * (float)Math.PI;
			// chevron "v" rotated by 180° as the card expands
			PointF[] pts = new PointF[3]
			{
				new PointF(-r, -r / 2f),
				new PointF(0f, r / 2f),
				new PointF(r, -r / 2f)
			};
			for (int i = 0; i < pts.Length; i++)
			{
				float px = pts[i].X, py = pts[i].Y;
				pts[i] = new PointF(cx + px * (float)Math.Cos(a) - py * (float)Math.Sin(a), cy + px * (float)Math.Sin(a) + py * (float)Math.Cos(a));
			}
			if (HotT.Value > 0.01f || _expT > 0.01f)
			{
				using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(18f * Math.Max(HotT.Value, _expT)), 255, 255, 255)))
				{
					g.FillEllipse(hb, cx - Theme.S(13f), cy - Theme.S(13f), Theme.S(26f), Theme.S(26f));
				}
			}
			using (Pen pen = new Pen(Theme.Mix(Theme.Sub, Theme.Text, Math.Max(HotT.Value, _expT)), Theme.S(1.6f)))
			{
				pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
				pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
				pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
				g.DrawLines(pen, pts);
			}
		}

		private int DrawBlock(Graphics g, string head, string text, int x, int y, int w, Color dot)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0;
			}
			int hh = Theme.Measure(head, Theme.Tiny).Height;
			int tx = x;
			if (dot != Color.Empty)
			{
				Theme.Smooth(g);
				using (SolidBrush b = new SolidBrush(dot))
				{
					g.FillEllipse(b, x, y + (hh - Theme.S(7f)) / 2f, Theme.S(7f), Theme.S(7f));
				}
				tx = x + Theme.S(14f);
				w -= Theme.S(14f);
			}
			TextRenderer.DrawText(g, head, Theme.Tiny, new Point(tx, y), (dot != Color.Empty) ? dot : Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			int th = Theme.Measure(text, Theme.Small, w, wrap: true).Height;
			TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(tx, y + hh + Theme.S(5f), w, th + 2), Theme.Mix(Theme.Sub, Theme.Text, 0.35f), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			return hh + Theme.S(5f) + th;
		}

		private void DrawDetails(Graphics g)
		{
			int w = base.Width;
			int pad = base.Pad;
			int iw = InnerColsW(w);
			int y = _baseH;
			using (System.Drawing.Drawing2D.LinearGradientBrush lb = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(pad, y, iw, 1), Color.FromArgb(70, 70, 80), Color.FromArgb(10, 70, 70, 80), 0f))
			{
				g.FillRectangle(lb, pad, y, iw, 1);
			}
			y += Theme.S(16f);
			y += DrawBlock(g, HWhat, What, pad, y, iw, Color.Empty) + Theme.S(14f);
			if (HasOpts)
			{
				y += DrawOpts(g, pad, y, iw) + Theme.S(10f);
			}
			else if (TwoCols(w))
			{
				int cw = (iw - Theme.S(20f)) / 2;
				int h1 = DrawBlock(g, HPro, Pro, pad, y, cw, Theme.Green);
				int h2 = DrawBlock(g, HCon, Con, pad + cw + Theme.S(20f), y, cw, Theme.Yellow);
				y += Math.Max(h1, h2) + Theme.S(14f);
			}
			else
			{
				if (!string.IsNullOrEmpty(Pro))
				{
					y += DrawBlock(g, HPro, Pro, pad, y, iw, Theme.Green) + Theme.S(12f);
				}
				if (!string.IsNullOrEmpty(Con))
				{
					y += DrawBlock(g, HCon, Con, pad, y, iw, Theme.Yellow) + Theme.S(14f);
				}
			}
			// recommendation box
			Rectangle box = new Rectangle(pad, y, iw, RecBoxH(iw));
			Color rc = (Rec == null) ? Theme.Silver2 : ((_cur != null && Same(_cur, Rec)) ? Theme.Green : Theme.Yellow);
			Theme.Smooth(g);
			using (System.Drawing.Drawing2D.GraphicsPath bp = Theme.Round(box, Theme.S(10f)))
			{
				using (SolidBrush bb = new SolidBrush(Color.FromArgb(20, rc)))
				{
					g.FillPath(bb, bp);
				}
				using (Pen bpen = new Pen(Color.FromArgb(60, rc)))
				{
					g.DrawPath(bpen, bp);
				}
			}
			// small "lightbulb" mark: circle with a dot
			int ix = box.X + Theme.S(14f);
			int iy = box.Y + Theme.S(13f);
			using (Pen ipen = new Pen(rc, Theme.S(1.5f)))
			{
				g.DrawEllipse(ipen, ix, iy, Theme.S(14f), Theme.S(14f));
			}
			using (SolidBrush ib = new SolidBrush(rc))
			{
				g.FillEllipse(ib, ix + Theme.S(4.5f), iy + Theme.S(4.5f), Theme.S(5f), Theme.S(5f));
			}
			Font hf = Theme.FC(9f, semi: true);
			int tx = box.X + Theme.S(42f);
			int tw = box.Width - Theme.S(28f) - Theme.S(30f);
			string head = (Rec == null) ? "Рекомендация: на твой выбор" : ("Рекомендация: " + RecValueText(lower: true));
			if (Rec != null && _cur != null && Same(_cur, Rec))
			{
				head += "  •  уже сделано";
			}
			int th = Theme.Measure(head, hf).Height;
			TextRenderer.DrawText(g, head, hf, new Point(tx, box.Y + Theme.S(12f)), rc, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
			if (!string.IsNullOrEmpty(Why))
			{
				TextRenderer.DrawText(g, Why, Theme.Small, new Rectangle(tx, box.Y + Theme.S(12f) + th + Theme.S(3f), tw, box.Height), Theme.Mix(Theme.Sub, Theme.Text, 0.35f), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			}
		}

		protected override void PaintContent(Graphics g)
		{
			TextRenderer.DrawText(g, Title, Theme.Title, new Rectangle(_textR.X, _textR.Y, _textR.Width, _titleH + 2), Unsupported ? Theme.Sub : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			if (!string.IsNullOrEmpty(Desc))
			{
				TextRenderer.DrawText(g, Desc, Theme.Small, new Rectangle(_textR.X, _textR.Y + _titleH + Theme.S(4f), _textR.Width, _descH), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			}
			if (ShowPill)
			{
				Color pc;
				string pt = PillText(out pc);
				if (PillCheck)
				{
					Theme.Pill(g, _pillR, "", Theme.Tiny, pc, 26);
					float u = Theme.S(1f);
					float cx0 = _pillR.X + Theme.S(11f);
					float cy0 = _pillR.Y + _pillR.Height / 2f;
					using (Pen cp = new Pen(pc, Theme.S(1.6f)))
					{
						cp.StartCap = System.Drawing.Drawing2D.LineCap.Round;
						cp.EndCap = System.Drawing.Drawing2D.LineCap.Round;
						cp.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
						g.DrawLines(cp, new PointF[] { new PointF(cx0, cy0), new PointF(cx0 + 3 * u, cy0 + 3 * u), new PointF(cx0 + 9 * u, cy0 - 3.5f * u) });
					}
					Rectangle trc = new Rectangle(_pillR.X + Theme.S(20f), _pillR.Y, _pillR.Width - Theme.S(22f), _pillR.Height);
					TextRenderer.DrawText(g, pt, Theme.Tiny, trc, pc, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
				else
				{
					Theme.Pill(g, _pillR, pt, Theme.Tiny, pc, 26);
				}
			}
			DrawChevron(g);
			if (_expT > 0.001f && HasInfo)
			{
				DrawDetails(g);
			}
		}
	}
}
