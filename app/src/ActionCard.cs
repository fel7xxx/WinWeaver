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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class ActionCard : Card
	{
		public string Title;

		public string GlyphText;

		public string Status = "…";

		public Color StatusColor = Theme.Sub;

		private readonly Func<IEngine, object> _status;

		private readonly Host _host;

		public readonly List<FButton> Buttons = new List<FButton>();

		private Rectangle _titleR;

		private Rectangle _statusR;

		private int _iconBox;

		public ActionCard(Host host, string glyph, string title, Func<IEngine, object> status, params ActionDef[] actions)
		{
			_host = host;
			Title = title;
			GlyphText = glyph;
			_status = status;
			foreach (ActionDef actionDef in actions)
			{
				FButton fButton = new FButton
				{
					Text = actionDef.Text,
					Kind = actionDef.Kind,
					Glyph = actionDef.Glyph,
					Font = Theme.Body
				};
				fButton.AutoWidth(Theme.S(34f));
				fButton.Tag = actionDef;
				fButton.Click += delegate(object s, EventArgs e)
				{
					Run((ActionDef)((FButton)s).Tag, (FButton)s);
				};
				Buttons.Add(fButton);
				base.Controls.Add(fButton);
			}
		}

		public static Func<IEngine, object> PsStatus(string script)
		{
			return (IEngine e) => e.Str(e.Eval(script));
		}

		private void Run(ActionDef a, FButton b)
		{
			if (a.Confirm != null && !WDialog.Ask(FindForm(), a.ConfirmTitle ?? Title, a.Confirm, a.ConfirmYes ?? "Продолжить", "Отмена", DlgIcon.Warn))
			{
				return;
			}
			if (a.Ui != null)
			{
				a.Ui();
			}
			else
			{
				if (!_host.TryBegin(title: string.IsNullOrEmpty(Title) ? a.Text : (Title + " — " + a.Text)))
				{
					return;
				}
				b.Enabled = false;
				_host.Worker.Run(delegate(IEngine e)
				{
					object obj = ((a.Work != null) ? a.Work(e) : null);
					object obj2 = ((_status != null) ? _status(e) : null);
					return new object[2] { obj, obj2 };
				}, delegate(object o)
				{
					b.Enabled = true;
					object[] array = (object[])o;
					if (_status != null)
					{
						Apply(array[1]);
					}
					if (a.Done != null)
					{
						a.Done(array[0]);
					}
					_host.End();
				});
			}
		}

		public void RefreshStatus()
		{
			if (_status != null)
			{
				_host.Worker.Run((IEngine e) => _status(e), Apply);
			}
		}

		public override object Fetch(IEngine e)
		{
			if (_status == null)
			{
				return null;
			}
			return _status(e);
		}

		public override void Apply(object v)
		{
			if (_status != null)
			{
				Status = ((v == null) ? "" : v.ToString());
				StatusColor = (Regex.IsMatch(Status, "вернулись|не смог|зашифрованы:|ждёт|Внимание|ВЫКЛ полностью|установлен$") ? Theme.Yellow : Theme.Sub);
				int height = base.Height;
				DoLayout(base.Left, base.Top, base.Width);
				if (base.Height != height && base.Parent is Canvas)
				{
					((Canvas)base.Parent).Page.RelayoutSoon();
				}
				else
				{
					Invalidate();
				}
			}
		}

		public override int DoLayout(int x, int y, int w)
		{
			int pad = base.Pad;
			_iconBox = Theme.S(40f);
			int num = pad + _iconBox + Theme.S(14f);
			int width = w - num - pad;
			int height = Theme.Measure(Title, Theme.Title, width, wrap: true).Height;
			int height2 = ((!string.IsNullOrEmpty(Status)) ? Theme.Measure(Status, Theme.Small, width, wrap: true).Height : 0);
			_titleR = new Rectangle(num, pad - Theme.S(2f), width, height);
			_statusR = new Rectangle(num, _titleR.Bottom + Theme.S(4f), width, height2);
			int num2 = Math.Max(_statusR.Bottom, pad + _iconBox) + Theme.S(14f);
			int num3 = num;
			int num4 = Theme.S(36f);
			int num5 = Theme.S(8f);
			if (Buttons.Count == 0)
			{
				num2 -= Theme.S(14f) + num4;
			}
			foreach (FButton button in Buttons)
			{
				if (num3 + button.Width > w - pad && num3 > num)
				{
					num3 = num;
					num2 += num4 + num5;
				}
				button.SetBounds(num3, num2, Math.Min(button.Width, w - pad - num3), num4);
				num3 += button.Width + num5;
			}
			int num6 = num2 + num4 + pad;
			SetBounds(x, y, w, num6);
			Invalidate();
			return num6;
		}

		public void PinButtonsBottom()
		{
			if (Buttons.Count == 0)
			{
				return;
			}
			int num = 0;
			foreach (FButton button in Buttons)
			{
				num = Math.Max(num, button.Bottom);
			}
			int num2 = base.Height - base.Pad - num;
			if (num2 <= 0)
			{
				return;
			}
			foreach (FButton button2 in Buttons)
			{
				button2.Top += num2;
			}
		}

		protected override void PaintContent(Graphics g)
		{
			int pad = base.Pad;
			Rectangle rectangle = new Rectangle(pad, pad, _iconBox, _iconBox);
			using (GraphicsPath path = Theme.Round(rectangle, Theme.S(10f)))
			{
				Theme.FillGradient(g, path, rectangle, Color.FromArgb(58, 58, 67), Color.FromArgb(40, 40, 47));
				using (Pen pen = new Pen(Theme.BorderHi))
				{
					g.DrawPath(pen, path);
				}
			}
			Theme.DrawIcon(g, GlyphText, Theme.Icon, rectangle, Theme.Silver1);
			TextRenderer.DrawText(g, Title, Theme.Title, _titleR, Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			if (!string.IsNullOrEmpty(Status))
			{
				TextRenderer.DrawText(g, Status, Theme.Small, _statusR, StatusColor, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
			}
		}
	}
}
