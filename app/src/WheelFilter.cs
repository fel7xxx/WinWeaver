// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class WheelFilter : IMessageFilter
	{
		private const int WM_MOUSEWHEEL = 522;

		private readonly Form _form;

		[DllImport("user32.dll")]
		private static extern IntPtr WindowFromPoint(Point p);

		[DllImport("user32.dll")]
		private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

		public WheelFilter(Form form)
		{
			_form = form;
		}

		public bool PreFilterMessage(ref Message m)
		{
			if (m.Msg != WM_MOUSEWHEEL || !Win.IsWindows || Form.ActiveForm != _form)
			{
				return false;
			}
			long num = m.LParam.ToInt64();
			Point p = new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF));
			IntPtr intPtr = WindowFromPoint(p);
			if (intPtr == IntPtr.Zero || intPtr == m.HWnd)
			{
				return false;
			}
			Control control = Control.FromHandle(intPtr);
			if (control == null || control.FindForm() != _form)
			{
				return false;
			}
			PostMessage(intPtr, m.Msg, m.WParam, m.LParam);
			return true;
		}
	}
}
