using System;

namespace WinWeaver;

internal class ActionDef
{
	public string Text;

	public BtnKind Kind;

	public string Glyph;

	public Func<IEngine, object> Work;

	public Action<object> Done;

	public Action Ui;

	public string Confirm;

	public string ConfirmTitle;

	public string ConfirmYes;

	public ActionDef(string text, BtnKind kind = BtnKind.Ghost)
	{
		Text = text;
		Kind = kind;
	}
}
