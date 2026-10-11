using System;
using System.Collections.Generic;

namespace WinWeaver;

internal class WelcomeDialog : PanelDialog
{
	private readonly TileChoice _lang;

	private readonly TileChoice _inst;

	private readonly FCheck _auto;

	private readonly FCheck _upd;

	private readonly int _labLang;

	private readonly int _labInst;

	private readonly int _labMore;

	private readonly List<string> _codes = new List<string>();

	private readonly Image _banner;

	public string Lang => _codes[_lang.Selected];

	public bool Install => _inst.Selected == 0;

	public bool Autostart
	{
		get
		{
			if (_auto.Checked)
			{
				return Install;
			}
			return false;
		}
	}

	public bool Updates => _upd.Checked;

	public WelcomeDialog()
		: base("Добро пожаловать в WinWeaver", "Настройка Windows 11 в пару кликов: приватность, оформление, скорость и обслуживание. Перед изменениями создаётся точка восстановления, а каждый шаг можно отменить в «Истории».", "Начать", null, DlgIcon.Info, 720f)
	{
		int innerW = base.InnerW;
		_banner = Walls.Banner(0, 1280) ?? Walls.Load(0, 1280);
		int y = (_labLang = Theme.S(150f)) + Theme.S(22f);
		_lang = new TileChoice();
		foreach (string item in L.Available())
		{
			_codes.Add(item);
			_lang.Add(L.NameOf(item), (item == "ru") ? "интерфейс на русском" : ((item == "en") ? "English interface" : item));
		}
		_lang.Selected = Math.Max(0, _codes.IndexOf(L.Lang));
		_lang.Changed += delegate(int i)
		{
			L.Set(_codes[i]);
			Invalidate(invalidateChildren: true);
		};
		_lang.SetBounds(base.M, y, innerW, _lang.Measure(innerW));
		Content.Controls.Add(_lang);
		y = (_labInst = _lang.Bottom + Theme.S(20f)) + Theme.S(22f);
		_inst = new TileChoice
		{
			hintLines = 2
		};
		_inst.Add("Установить", "в Program Files: ярлык в Пуске, удаление через «Установленные приложения»");
		_inst.Add("Без установки", "запускать этот файл как есть, ничего не копируя");
		_inst.Selected = 0;
		_inst.Changed += delegate
		{
			_auto.Enabled = _inst.Selected == 0;
			_auto.Invalidate();
			Content.Invalidate();
		};
		_inst.SetBounds(base.M, y, innerW, _inst.Measure(innerW));
		Content.Controls.Add(_inst);
		y = (_labMore = _inst.Bottom + Theme.S(20f)) + Theme.S(22f);
		_auto = new FCheck("Открывать WinWeaver при входе в Windows")
		{
			Checked = false
		};
		_auto.Location = new Point(base.M, y);
		Content.Controls.Add(_auto);
		y = _auto.Bottom + Theme.S(4f);
		_upd = new FCheck("Сообщать о новых версиях на GitHub (только уведомление, ничего не скачивается)")
		{
			Checked = true
		};
		_upd.Location = new Point(base.M, y);
		Content.Controls.Add(_upd);
		y = _upd.Bottom + Theme.S(8f);
		Finish(y);
	}

	protected override void PaintContent(Graphics g)
	{
		Rectangle rectangle = new Rectangle(base.M, 0, base.InnerW, Theme.S(130f));
		Theme.Smooth(g);
		using (GraphicsPath path = Theme.Round(rectangle, Theme.S(14f)))
		{
			if (_banner != null)
			{
				Region clip = g.Clip;
				g.SetClip(path, CombineMode.Intersect);
				float num = Math.Max((float)rectangle.Width / (float)_banner.Width, (float)rectangle.Height / (float)_banner.Height);
				float num2 = (float)_banner.Width * num;
				float num3 = (float)_banner.Height * num;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(_banner, (float)rectangle.X + ((float)rectangle.Width - num2) / 2f, (float)rectangle.Y - num3 * 0.62f + (float)rectangle.Height / 2f, num2, num3);
				using (LinearGradientBrush brush = new LinearGradientBrush(rectangle, Color.FromArgb(150, 12, 13, 16), Color.FromArgb(20, 12, 13, 16), 0f))
				{
					g.FillRectangle(brush, rectangle);
				}
				g.Clip = clip;
				clip.Dispose();
			}
			using Pen pen = new Pen(Color.FromArgb(60, Theme.Ice));
			g.DrawPath(pen, path);
		}
		using (Font font = Theme.F(22f, semi: true, FontStyle.Bold))
		{
			Size size = Theme.Measure("WinWeaver", font);
			Theme.GradientText(g, "WinWeaver", font, new PointF(rectangle.X + Theme.S(26f), (float)rectangle.Y + (float)(rectangle.Height - size.Height) / 2f - (float)Theme.S(10f)));
			TextRenderer.DrawText(g, L.T("настройка Windows 11") + "  •  v1.0.2  •  fel7xxx", Theme.Small, new Point(rectangle.X + Theme.S(28f), rectangle.Y + (rectangle.Height + size.Height) / 2 - Theme.S(6f)), Theme.Mix(Theme.Sub, Theme.IceHi, 0.3f), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
		}
		Label(g, "Язык", _labLang, "можно сменить в «Настройках»");
		Label(g, "Установка", _labInst);
		Label(g, "Запуск", _labMore, (_inst.Selected == 0) ? null : "автозапуск — только для установленной версии");
	}
}
