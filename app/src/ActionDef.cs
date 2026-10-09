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
}
