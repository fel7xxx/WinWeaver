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
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class ToggleSwitch : Control
	{
		private bool _on;

		private bool _busy;

		private float _p;

		private readonly Timer _t;

		public bool Busy
		{
			get
			{
				return _busy;
			}
			set
			{
				_busy = value;
				base.Enabled = !value;
				Invalidate();
			}
		}

		public bool On
		{
			get
			{
				return _on;
			}
			set
			{
				if (_on != value)
				{
					_on = value;
					_t.Start();
				}
			}
		}

		public event EventHandler Toggled;

		public ToggleSwitch()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			base.Size = new Size(Theme.S(50f), Theme.S(28f));
			_t = new Timer
			{
				Interval = 15
			};
			_t.Tick += delegate
			{
				float num = (_on ? 1f : 0f);
				float num2 = 0.18f;
				if (Math.Abs(_p - num) <= num2)
				{
					_p = num;
					_t.Stop();
				}
				else
				{
					_p += ((_p < num) ? num2 : (0f - num2));
				}
				Invalidate();
			};
		}

		public void SimClick()
		{
			OnClick(EventArgs.Empty);
		}

		public void SetOnNoAnim(bool v)
		{
			_on = v;
			_p = (v ? 1f : 0f);
			Invalidate();
		}

		protected override void OnClick(EventArgs e)
		{
			if (!_busy)
			{
				base.OnClick(e);
				this.Toggled?.Invoke(this, EventArgs.Empty);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			RectangleF r = new RectangleF(1f, (float)(base.Height - Theme.S(26f)) / 2f, Theme.S(48f), Theme.S(26f));
			Color control = Theme.Control1;
			Color control2 = Theme.Control2;
			Color b = Theme.IceHi;
			Color b2 = Theme.Ice;
			using (GraphicsPath path = Theme.Round(r, r.Height / 2f))
			{
				Theme.FillGradient(graphics, path, r, Theme.Mix(control, b, _p), Theme.Mix(control2, b2, _p));
				using (Pen pen = new Pen(Theme.Mix(Theme.Border, Theme.Ice, _p)))
				{
					graphics.DrawPath(pen, path);
				}
			}
			float num = r.Height - (float)Theme.S(6f);
			float x = r.X + (float)Theme.S(3f) + _p * (r.Width - num - (float)Theme.S(6f));
			RectangleF r2 = new RectangleF(x, r.Y + (float)Theme.S(3f), num, num);
			using (GraphicsPath path2 = Theme.Round(r2, num / 2f))
			{
				Theme.FillGradient(graphics, path2, r2, _busy ? Theme.Dim : Theme.Mix(Color.FromArgb(225, 225, 230), Theme.OnSilver, (_p < 0.5f) ? _p : 0f), _busy ? Theme.Dim : Theme.Mix(Color.FromArgb(188, 188, 196), Color.FromArgb(44, 44, 52), _p));
			}
		}
	}
}
