using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class ListCard : Card
{
	public string Title;

	public string Info;

	public Color InfoColor = Theme.Sub;

	public string GlyphText;

	public FList List = new FList();

	public readonly List<FButton> Buttons = new List<FButton>();

	public FCheck Check;

	public int ListHeight = 260;

	private Func<IEngine, object> _fetch;

	private Action<ListCard, object> _apply;

	private Rectangle _titleR;

	private Rectangle _infoR;

	public ListCard(string glyph, string title, string info, Func<IEngine, object> fetch, Action<ListCard, object> apply)
	{
		GlyphText = glyph;
		Title = title;
		Info = info;
		_fetch = fetch;
		_apply = apply;
		base.Controls.Add(List);
		List.DataChanged += delegate
		{
			if (base.Parent is Canvas)
			{
				((Canvas)base.Parent).Page.RelayoutSoon();
			}
		};
	}

	public FButton AddButton(string text, BtnKind kind, EventHandler click, string glyph = null)
	{
		FButton fButton = new FButton
		{
			Text = text,
			Kind = kind,
			Font = Theme.Body,
			Glyph = glyph
		};
		fButton.AutoWidth(Theme.S(34f));
		fButton.Click += click;
		Buttons.Add(fButton);
		base.Controls.Add(fButton);
		return fButton;
	}

	public override object Fetch(IEngine e)
	{
		if (_fetch == null)
		{
			return null;
		}
		return _fetch(e);
	}

	public override void Apply(object v)
	{
		if (_apply != null)
		{
			_apply(this, v);
		}
		Invalidate();
	}

	public override int DoLayout(int x, int y, int w)
	{
		int pad = base.Pad;
		int width = w - pad * 2;
		int height = Theme.Measure(Title, Theme.Title, width, wrap: true).Height;
		int height2 = ((!string.IsNullOrEmpty(Info)) ? Theme.Measure(Info, Theme.Small, width, wrap: true).Height : 0);
		_titleR = new Rectangle(pad, pad - Theme.S(2f), width, height);
		_infoR = new Rectangle(pad, _titleR.Bottom + Theme.S(4f), width, height2);
		int y2 = _infoR.Bottom + Theme.S(14f);
		List.SetBounds(pad, y2, width, Math.Max(Theme.S(140f), Math.Min(Theme.S(ListHeight), List.ContentHeight)));
		int num = List.Bottom + Theme.S(14f);
		if (Check != null)
		{
			Check.Location = new Point(pad, num);
			num += Check.Height + Theme.S(10f);
		}
		int num2 = pad;
		int num3 = Theme.S(36f);
		int num4 = Theme.S(8f);
		foreach (FButton button in Buttons)
		{
			if (num2 + button.Width > w - pad && num2 > pad)
			{
				num2 = pad;
				num += num3 + num4;
			}
			button.SetBounds(num2, num, button.Width, num3);
			num2 += button.Width + num4;
		}
		int num5 = ((Buttons.Count > 0) ? (num + num3) : (num - Theme.S(14f))) + pad;
		SetBounds(x, y, w, num5);
		return num5;
	}

	public void SetInfo(string text, Color c)
	{
		int height = Theme.Measure(Info, Theme.Small, _infoR.Width, wrap: true).Height;
		Info = text;
		InfoColor = c;
		if (Theme.Measure(Info, Theme.Small, _infoR.Width, wrap: true).Height != height && base.Parent is Canvas)
		{
			((Canvas)base.Parent).Page.RelayoutSoon();
		}
		Invalidate();
	}

	protected override void PaintContent(Graphics g)
	{
		TextRenderer.DrawText(g, Title, Theme.Title, _titleR, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		if (!string.IsNullOrEmpty(Info))
		{
			TextRenderer.DrawText(g, Info, Theme.Small, new Rectangle(_infoR.X, _infoR.Y, _infoR.Width, _infoR.Height + Theme.S(20f)), InfoColor, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		}
	}
}
