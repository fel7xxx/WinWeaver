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
using System.Windows.Forms;

namespace WinWeaver
{
	internal class Card : Control, IBlock, ISurface
	{
		public bool Hot;

		public bool HoverFx;

		protected readonly Tween HotT;

		protected int Pad => Theme.S(20f);

		public Card()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			ForeColor = Theme.Text;
			HotT = new Tween(this, 0f, 0.22f, delegate
			{
				Invalidate(invalidateChildren: true);
			});
		}

		protected void SetHot(bool hot)
		{
			Hot = hot;
			HotT.Target = hot ? 1f : 0f;
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			if (HoverFx)
			{
				SetHot(hot: true);
			}
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (HoverFx && !base.ClientRectangle.Contains(PointToClient(Cursor.Position)))
			{
				SetHot(hot: false);
			}
			base.OnMouseLeave(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			PaintSurface(e.Graphics);
			PaintContent(e.Graphics);
		}

		public virtual void PaintSurface(Graphics g)
		{
			Theme.Card(g, base.ClientRectangle, HotT.Value);
		}

		protected virtual void PaintContent(Graphics g)
		{
		}

		public virtual int DoLayout(int x, int y, int w)
		{
			SetBounds(x, y, w, base.Height);
			return base.Height;
		}

		public virtual object Fetch(IEngine e)
		{
			return null;
		}

		public virtual void Apply(object v)
		{
		}
	}
}
