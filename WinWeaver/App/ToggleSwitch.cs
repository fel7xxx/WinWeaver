using System;

namespace WinWeaver;

internal class ToggleSwitch : Control
{
	private bool _on;

	private bool _busy;

	private readonly Tween _t;

	private readonly Tween _k;

	private float _p => Math.Max(0f, Math.Min(1f, _t.Value));

	public bool Busy
	{
		get
		{
			return _busy;
		}
		set
		{
			if (_busy != value)
			{
				_busy = value;
				base.Enabled = !value;
				Invalidate();
			}
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
				_t.Target = (value ? 1f : 0f);
				_k.Target = (value ? 1f : 0f);
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
		_t = new Tween(this).Timed(220);
		_k = new Tween(this).Timed(420).WithEase(Anim.EaseBack);
	}

	public void SimClick()
	{
		OnClick(EventArgs.Empty);
	}

	public void SetOnNoAnim(bool v)
	{
		_on = v;
		_t.Snap(v ? 1f : 0f);
		_k.Snap(v ? 1f : 0f);
		Invalidate();
	}

	protected override void OnClick(EventArgs e)
	{
		if (!_busy && base.Enabled)
		{
			base.OnClick(e);
			this.Toggled?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		FButton.PaintParent(this, e.Graphics);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Smooth(graphics);
		Theme.Toggle(graphics, new RectangleF(1.5f, (float)(base.Height - Theme.S(24f)) / 2f, Theme.S(42f), Theme.S(24f)), _k.Value, _p, _busy);
	}
}
