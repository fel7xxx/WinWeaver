// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class Grid : IBlock
	{
		public readonly List<IBlock> Items = new List<IBlock>();

		public int MinColW;

		public Grid(int minColW, params IBlock[] items)
		{
			MinColW = minColW;
			Items.AddRange(items);
		}

		public int DoLayout(int x, int y, int w)
		{
			int num = Theme.S(14f);
			int num2 = Math.Max(1, Math.Min(Items.Count, (w + num) / (Theme.S(MinColW) + num)));
			int num3 = (w - num * (num2 - 1)) / num2;
			int num4 = y;
			for (int i = 0; i < Items.Count; i += num2)
			{
				int num5 = 0;
				for (int j = 0; j < num2 && i + j < Items.Count; j++)
				{
					num5 = Math.Max(num5, Items[i + j].DoLayout(x + j * (num3 + num), num4, num3));
				}
				for (int k = 0; k < num2 && i + k < Items.Count; k++)
				{
					if (Items[i + k] is Control control && control.Height != num5)
					{
						control.Height = num5;
						if (Items[i + k] is ActionCard)
						{
							((ActionCard)Items[i + k]).PinButtonsBottom();
						}
					}
				}
				num4 += num5 + num;
			}
			return num4 - y - num;
		}

		public object Fetch(IEngine e)
		{
			object[] array = new object[Items.Count];
			for (int i = 0; i < Items.Count; i++)
			{
				array[i] = Items[i].Fetch(e);
			}
			return array;
		}

		public void Apply(object v)
		{
			if (v is object[] array)
			{
				for (int i = 0; i < Items.Count && i < array.Length; i++)
				{
					Items[i].Apply(array[i]);
				}
			}
		}
	}
}
