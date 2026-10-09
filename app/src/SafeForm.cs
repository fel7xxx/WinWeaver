// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class SafeForm : Form
	{
		private readonly string _mode;

		private readonly IEngine _engine;

		private string _line = "Работа в безопасном режиме — не выключай компьютер…";

		private float _ang;

		private readonly System.Windows.Forms.Timer _spin;

		private bool _done;

		public SafeForm(string mode, IEngine engine)
		{
			_mode = mode;
			_engine = engine;
			Text = Brand.Name;
			base.FormBorderStyle = FormBorderStyle.None;
			base.StartPosition = FormStartPosition.CenterScreen;
			base.AutoScaleMode = AutoScaleMode.None;
			base.Size = new Size(Theme.S(460f), Theme.S(170f));
			BackColor = Theme.Card2;
			base.TopMost = true;
			DoubleBuffered = true;
			base.Icon = WDialog.AppIcon;
			_spin = new System.Windows.Forms.Timer
			{
				Interval = 30
			};
			_spin.Tick += delegate
			{
				_ang = (_ang + 12f) % 360f;
				Invalidate();
			};
			_spin.Start();
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			EngineUI ui = new EngineUI(delegate
			{
			}, (string a, string b, string c, string d) => true);
			Thread thread = new Thread((ThreadStart)delegate
			{
				string res = "ok";
				try
				{
					IEngine engine = _engine ?? Program.CreatePsEngine(ui, _mode);
					engine.Start();
					res = engine.Str(engine.Eval("Invoke-FSafePhase $p", new Hashtable { { "p", _mode } }));
				}
				catch (Exception ex)
				{
					res = "failed: " + ex.Message;
				}
				try
				{
					BeginInvoke((Action)delegate
					{
						Finish(res);
					});
				}
				catch
				{
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground = true;
			thread.Start();
		}

		private void Finish(string res)
		{
			_done = true;
			_spin.Stop();
			_line = ((res == "ok") ? "Готово. Перезагрузка в обычный режим через 5 секунд…" : ("Не всё получилось (" + res + "). Перезагрузка через 5 секунд…"));
			Invalidate();
			if (Environment.GetEnvironmentVariable("WW_FAKE") != "1")
			{
				try
				{
					Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5")
					{
						UseShellExecute = false,
						CreateNoWindow = true
					});
				}
				catch
				{
				}
			}
			System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
			timer.Interval = 6000;
			timer.Tick += delegate
			{
				Close();
			};
			timer.Start();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Smooth(graphics);
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, -1, base.Width, base.Height + 2), Color.FromArgb(40, 40, 47), Color.FromArgb(22, 22, 26), 90f))
			{
				graphics.FillRectangle(brush, base.ClientRectangle);
			}
			using (Pen pen = new Pen(Theme.BorderHi))
			{
				graphics.DrawRectangle(pen, 0, 0, base.Width - 1, base.Height - 1);
			}
			int num = Theme.S(28f);
			int num2 = Theme.S(34f);
			if (!_done)
			{
				using (Pen pen2 = new Pen(Theme.Control1, Theme.S(3f)))
				{
					graphics.DrawEllipse(pen2, num, num, num2, num2);
				}
				using (Pen pen3 = new Pen(Theme.Silver1, Theme.S(3f))
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round
				})
				{
					graphics.DrawArc(pen3, num, num, num2, num2, _ang, 110f);
				}
			}
			else
			{
				using (SolidBrush brush2 = new SolidBrush(Theme.Green))
				{
					graphics.FillEllipse(brush2, num + num2 / 4, num + num2 / 4, num2 / 2, num2 / 2);
				}
			}
			int num3 = num + num2 + Theme.S(18f);
			TextRenderer.DrawText(graphics, (_mode == "def-off-safe") ? "WinWeaver: отключение Defender" : "WinWeaver: возврат Defender", Theme.FC(12f, semi: true), new Point(num3, num), Theme.Text);
			TextRenderer.DrawText(graphics, _line, Theme.Body, new Rectangle(num3, num + Theme.S(32f), base.Width - num3 - num, Theme.S(80f)), Theme.Sub, TextFormatFlags.WordBreak);
		}
	}
}
