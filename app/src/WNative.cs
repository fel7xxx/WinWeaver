// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Runtime.InteropServices;

public static class WNative
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
	public static extern bool SpiString(int action, int param, string value, int flags);

	[DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
	public static extern bool SpiInts(int action, int param, int[] value, int flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
}
