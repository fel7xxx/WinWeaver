// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;

namespace WinWeaver
{
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

		// JPEG bytes of a built-in WeavOS wallpaper (called by Save-FWallpaperFile in engine.ps1)
		public byte[] Wallpaper(int variant)
		{
			return Walls.Bytes(variant);
		}
	}
}
