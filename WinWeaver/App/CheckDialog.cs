using System;

namespace WinWeaver;

internal class CheckDialog : Form, ISurface
{
	public class Row
	{
		public string Text;

		public string Hint;

		public bool Checked;

		public Rectangle R;

		public bool Header;

		public string Tag;
	}

	private readonly string _title;

	private readonly string _body;

	private readonly DlgIcon _icon;

	private readonly Row[] _rows;

	private readonly FButton _yes;

	private readonly FButton _no;

	private Rectangle _titleR;

	private Rectangle _bodyR;

	private Rectangle _listR;

	private int _scroll;

	private int _hot = -1;

	private int _footerTop;

	private bool _closeHot;

	private Rectangle _iconR;

	private DlgChrome.Head _head;

	private int RowH => Theme.S((_rows.Length != 0 && HasHints) ? 44f : 34f);

	private int HeadH => Theme.S(30f);

	private int TotalH
	{
		get
		{
			int num = 0;
			Row[] rows = _rows;
			foreach (Row r in rows)
			{
				num += H(r);
			}
			return num;
		}
	}

	private bool HasHints
	{
		get
		{
			Row[] rows = _rows;
			foreach (Row row in rows)
			{
				if (!string.IsNullOrEmpty(row.Hint) && row.Hint.StartsWith("\n"))
				{
					return true;
				}
			}
			return false;
		}
	}

	private int MaxScroll => Math.Max(0, TotalH + Theme.S(8f) - _listR.Height);

	private int H(Row r)
	{
		if (!r.Header)
		{
			return RowH;
		}
		return HeadH;
	}

	private CheckDialog(string title, string body, Row[] rows, string yes, string no, DlgIcon icon)
	{
		_title = title;
		_body = body;
		_rows = rows ?? new Row[0];
		_icon = icon;
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.Manual;
		base.ShowInTaskbar = false;
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Theme.Card2;
		base.KeyPreview = true;
		Text = title;
		if (WDialog.AppIcon != null)
		{
			base.Icon = WDialog.AppIcon;
		}
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		_yes = new FButton
		{
			Text = yes,
			Kind = DlgChrome.YesKind(yes, icon),
			Font = Theme.Body
		};
		_yes.AutoWidth(Theme.S(44f));
		_yes.Click += delegate
		{
			base.DialogResult = DialogResult.Yes;
			Close();
		};
		base.Controls.Add(_yes);
		_no = new FButton
		{
			Text = (no ?? "Отмена"),
			Kind = BtnKind.Ghost,
			Font = Theme.Body
		};
		_no.AutoWidth(Theme.S(44f));
		_no.Click += delegate
		{
			base.DialogResult = DialogResult.No;
			Close();
		};
		base.Controls.Add(_no);
		DoLayoutDlg();
	}

	private void DoLayoutDlg()
	{
		int num = Theme.S((_rows.Length > 12) ? 620f : ((_rows.Length > 3) ? 560f : 500f));
		int m = DlgChrome.M;
		_head = DlgChrome.Layout(num, _title, _body);
		_iconR = _head.Icon;
		int y = _head.Bottom + Theme.S(18f);
		int num2 = Math.Min(TotalH, RowH * 8);
		_listR = new Rectangle(m, y, num - m * 2, num2 + ((_rows.Length != 0) ? Theme.S(8f) : 0));
		_footerTop = _listR.Bottom + Theme.S(22f);
		base.Size = new Size(num, _footerTop + DlgChrome.FooterH);
		DlgChrome.PlaceButtons(num, _footerTop, _yes, _no);
	}

	public void PaintSurface(Graphics g)
	{
		DlgChrome.Bg(g, base.Size, _icon, _footerTop);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (_closeHot || _hot >= 0)
		{
			_closeHot = false;
			_hot = -1;
			Invalidate();
		}
		base.OnMouseLeave(e);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		DlgChrome.Round(this);
		DlgChrome.FadeIn(this);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			base.DialogResult = DialogResult.No;
			Close();
		}
		else if (e.KeyCode == Keys.Return)
		{
			base.DialogResult = DialogResult.Yes;
			Close();
		}
		base.OnKeyDown(e);
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		int scroll = _scroll;
		_scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - Math.Sign(e.Delta) * RowH));
		if (scroll != _scroll)
		{
			Invalidate();
		}
		base.OnMouseWheel(e);
	}

	private int HitRow(Point p)
	{
		if (!_listR.Contains(p))
		{
			return -1;
		}
		for (int i = 0; i < _rows.Length; i++)
		{
			if (!_rows[i].Header && _rows[i].R.Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = HitRow(e.Location);
		bool flag = DlgChrome.CloseR(base.Width).Contains(e.Location);
		if (num != _hot || flag != _closeHot)
		{
			_hot = num;
			_closeHot = flag;
			Cursor = ((num >= 0 || flag) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && DlgChrome.CloseR(base.Width).Contains(e.Location))
		{
			base.DialogResult = DialogResult.No;
			Close();
			return;
		}
		int num = HitRow(e.Location);
		if (num >= 0 && e.Button == MouseButtons.Left)
		{
			_rows[num].Checked = !_rows[num].Checked;
			Invalidate();
		}
		base.OnMouseClick(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		DlgChrome.Paint(graphics, base.Size, _icon, _footerTop, _closeHot);
		DlgChrome.DrawHead(graphics, _head, _icon, _title);
		Theme.Smooth(graphics);
		if (_rows.Length == 0)
		{
			return;
		}
		using (GraphicsPath path = Theme.Round(_listR, Theme.S(10f)))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(70, 12, 12, 15)))
			{
				graphics.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(34, 255, 255, 255));
			graphics.DrawPath(pen, path);
		}
		Region clip = graphics.Clip;
		graphics.SetClip(new Rectangle(_listR.X, _listR.Y + Theme.S(4f), _listR.Width, _listR.Height - Theme.S(8f)));
		int num = _listR.Y + Theme.S(4f) - _scroll;
		for (int i = 0; i < _rows.Length; i++)
		{
			Row row = _rows[i];
			row.R = new Rectangle(_listR.X + Theme.S(4f), num, _listR.Width - Theme.S(8f), H(row));
			if (row.Header)
			{
				int num2 = 0;
				int num3 = 0;
				for (int j = i + 1; j < _rows.Length && !_rows[j].Header; j++)
				{
					num2++;
					if (_rows[j].Checked)
					{
						num3++;
					}
				}
				TextRenderer.DrawText(graphics, row.Text.ToUpperInvariant(), Theme.Tiny, new Rectangle(row.R.X + Theme.S(10f), row.R.Y + Theme.S(8f), row.R.Width, row.R.Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
				TextRenderer.DrawText(graphics, num3 + " из " + num2, Theme.Tiny, new Rectangle(row.R.X, row.R.Y + Theme.S(8f), row.R.Width - Theme.S(10f), row.R.Height), Theme.Dim, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
				if (i > 0)
				{
					using Pen pen2 = new Pen(Color.FromArgb(24, 255, 255, 255));
					graphics.DrawLine(pen2, row.R.X + Theme.S(10f), row.R.Y + Theme.S(2f), row.R.Right - Theme.S(10f), row.R.Y + Theme.S(2f));
				}
				num += H(row);
				continue;
			}
			if (i == _hot)
			{
				using GraphicsPath path2 = Theme.Round(row.R, Theme.S(8f));
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb(18, 255, 255, 255));
				graphics.FillPath(brush2, path2);
			}
			Rectangle rectangle = new Rectangle(row.R.X + Theme.S(10f), row.R.Y + (RowH - Theme.S(18f)) / 2, Theme.S(18f), Theme.S(18f));
			using (GraphicsPath path3 = Theme.Round(rectangle, Theme.S(4f)))
			{
				using (SolidBrush brush3 = new SolidBrush(row.Checked ? Theme.Silver1 : Theme.Control2))
				{
					graphics.FillPath(brush3, path3);
				}
				using Pen pen3 = new Pen(row.Checked ? Theme.Silver1 : Theme.BorderHi);
				graphics.DrawPath(pen3, path3);
			}
			if (row.Checked)
			{
				using Pen pen4 = new Pen(Theme.OnSilver, Math.Max(1.6f, Theme.S(2f)))
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				};
				graphics.DrawLines(pen4, new PointF[3]
				{
					new PointF((float)rectangle.X + (float)rectangle.Width * 0.24f, (float)rectangle.Y + (float)rectangle.Height * 0.52f),
					new PointF((float)rectangle.X + (float)rectangle.Width * 0.43f, (float)rectangle.Y + (float)rectangle.Height * 0.7f),
					new PointF((float)rectangle.X + (float)rectangle.Width * 0.77f, (float)rectangle.Y + (float)rectangle.Height * 0.32f)
				});
			}
			int num4 = rectangle.Right + Theme.S(12f);
			string text = row.Hint ?? "";
			bool num5 = text.StartsWith("\n");
			string text2 = (num5 ? "" : text);
			int num6 = ((text2 != "") ? (Theme.Measure(text2, Theme.Small).Width + Theme.S(8f)) : 0);
			if (!string.IsNullOrEmpty(row.Tag))
			{
				int num7 = Theme.PillW(row.Tag, Theme.Tiny);
				Theme.Pill(graphics, new Rectangle(row.R.Right - Theme.S(10f) - num7, row.R.Y + (row.R.Height - Theme.S(20f)) / 2, num7, Theme.S(20f)), row.Tag, Theme.Tiny, row.Checked ? Theme.Silver1 : Theme.Dim);
				num6 = Math.Max(num6, num7 + Theme.S(10f));
			}
			int width = row.R.Right - Theme.S(10f) - num6 - num4;
			if (num5)
			{
				TextRenderer.DrawText(graphics, row.Text, Theme.Body, new Rectangle(num4, row.R.Y + Theme.S(5f), width, Theme.S(20f)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
				TextRenderer.DrawText(graphics, text.Substring(1), Theme.Tiny, new Rectangle(num4, row.R.Y + Theme.S(24f), width, Theme.S(16f)), Theme.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
			}
			else
			{
				TextRenderer.DrawText(graphics, row.Text, Theme.Body, new Rectangle(num4, row.R.Y, width, RowH), row.Checked ? Theme.Text : Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping);
			}
			if (text2 != "")
			{
				TextRenderer.DrawText(graphics, text2, Theme.Small, new Rectangle(row.R.Right - Theme.S(10f) - num6, row.R.Y, num6, RowH), Theme.Sub, TextFormatFlags.NoPrefix | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping);
			}
			num += H(row);
		}
		graphics.Clip = clip;
		clip.Dispose();
		if (MaxScroll <= 0)
		{
			return;
		}
		int num8 = _listR.Height - Theme.S(16f);
		int num9 = Math.Max(Theme.S(30f), num8 * _listR.Height / (TotalH + Theme.S(8f)));
		int y = _listR.Y + Theme.S(8f) + (num8 - num9) * _scroll / MaxScroll;
		using GraphicsPath path4 = Theme.Round(new Rectangle(_listR.Right - Theme.S(7f), y, Theme.S(4f), num9), Theme.S(2f));
		using SolidBrush brush4 = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
		graphics.FillPath(brush4, path4);
	}

	public static bool[] Ask(IWin32Window owner, string title, string body, Row[] rows, string yes, string no = "Отмена", DlgIcon icon = DlgIcon.Question)
	{
		using CheckDialog checkDialog = new CheckDialog(title, body, rows, yes, no, icon);
		Form form = owner as Form;
		Dimmer dimmer = null;
		if (form != null && form.Visible && form.WindowState != FormWindowState.Minimized)
		{
			checkDialog.Location = new Point(form.Left + (form.Width - checkDialog.Width) / 2, form.Top + (form.Height - checkDialog.Height) / 2);
			dimmer = new Dimmer(form);
			dimmer.Show(form);
		}
		else
		{
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			checkDialog.Location = new Point(workingArea.Left + (workingArea.Width - checkDialog.Width) / 2, workingArea.Top + (workingArea.Height - checkDialog.Height) / 2);
			checkDialog.TopMost = true;
		}
		try
		{
			IWin32Window win32Window = dimmer;
			if (checkDialog.ShowDialog(win32Window ?? owner) != DialogResult.Yes)
			{
				return null;
			}
			bool[] array = new bool[checkDialog._rows.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = checkDialog._rows[i].Checked;
			}
			return array;
		}
		finally
		{
			if (dimmer != null)
			{
				dimmer.Close();
				dimmer.Dispose();
			}
			form?.Activate();
		}
	}
}
