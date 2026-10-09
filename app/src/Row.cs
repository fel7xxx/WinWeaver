// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System.Drawing;

namespace WinWeaver
{
	internal class Row
	{
		public string Id;

		public string[] Cells;

		public Color? Accent;

		public bool Checkable;

		public bool Checked;

		public bool Toggleable;

		public bool On;

		public object Tag;
	}
}
