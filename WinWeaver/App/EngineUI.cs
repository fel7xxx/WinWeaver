using System;

namespace WinWeaver;

public class EngineUI
{
	private readonly Action<string, string> _msg;

	private readonly Func<string, string, string, string, bool> _confirm;

	public EngineUI(Action<string, string> msg, Func<string, string, string, string, bool> confirm)
	{
		_msg = msg;
		_confirm = confirm;
	}

	public void Msg(string kind, string text)
	{
		if (_msg != null)
		{
			_msg(kind, text);
		}
	}

	public bool Confirm(string title, string body, string yes, string no)
	{
		if (_confirm != null)
		{
			return _confirm(title, body, yes, no);
		}
		return false;
	}

	public byte[] Wallpaper(int variant)
	{
		return Walls.Bytes(variant);
	}
}
