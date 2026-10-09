// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System.Drawing;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class Dimmer : Form
	{
		protected override bool ShowWithoutActivation => true;

		public Dimmer(Form owner)
		{
			base.FormBorderStyle = FormBorderStyle.None;
			base.ShowInTaskbar = false;
			base.StartPosition = FormStartPosition.Manual;
			BackColor = Color.Black;
			base.Opacity = 0.45;
			base.Bounds = owner.RectangleToScreen(owner.ClientRectangle);
			base.AutoScaleMode = AutoScaleMode.None;
		}
	}
}
