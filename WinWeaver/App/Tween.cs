using System;

namespace WinWeaver;

internal class Tween
{
	private readonly Control _c;

	private readonly float _speed;

	private readonly Action _repaint;

	private float _target;

	private float _from;

	private double _t0;

	private double _last;

	private bool _running;

	private int _ms;

	public float Value;

	public Func<float, float> Ease;

	public bool Moving => _running;

	public float Target
	{
		get
		{
			return _target;
		}
		set
		{
			if ((Math.Abs(value - _target) < 0.0001f && Math.Abs(Value - value) < 0.0001f) || (Math.Abs(value - _target) < 0.0001f && _running))
			{
				return;
			}
			_target = value;
			if (!Anim.Enabled)
			{
				Value = value;
				Repaint();
				return;
			}
			double now = Anim.Now;
			_from = Value;
			_t0 = now;
			if (!_running)
			{
				_last = now;
			}
			_running = true;
			Anim.Watch(this);
		}
	}

	public Tween(Control c, float start = 0f, float speed = 0.2f, Action repaint = null)
	{
		_c = c;
		Value = start;
		_target = start;
		_speed = speed;
		_repaint = repaint;
	}

	public Tween Timed(int ms)
	{
		_ms = ms;
		return this;
	}

	public Tween WithEase(Func<float, float> ease)
	{
		Ease = ease;
		return this;
	}

	private void Repaint()
	{
		if (_repaint != null)
		{
			_repaint();
		}
		else
		{
			_c?.Invalidate();
		}
	}

	public void Snap(float v)
	{
		_target = v;
		Value = v;
		_from = v;
		_running = false;
	}

	internal bool Advance()
	{
		if (_c != null && _c.IsDisposed)
		{
			_running = false;
			return false;
		}
		if (!_running)
		{
			return false;
		}
		double now = Anim.Now;
		if (_ms > 0)
		{
			float num = (float)((now - _t0) / (double)_ms);
			if (num >= 1f)
			{
				Value = _target;
				_running = false;
				Repaint();
				return false;
			}
			Value = _from + (_target - _from) * ((Ease != null) ? Ease(num) : Anim.EaseOut(num));
			Repaint();
			return true;
		}
		double num2 = Math.Min(100.0, Math.Max(0.5, now - _last));
		_last = now;
		float num3 = _target - Value;
		float num4 = Math.Max(0.002f, Math.Abs(_target) * 0.001f);
		if (Math.Abs(num3) <= num4)
		{
			Value = _target;
			_running = false;
			Repaint();
			return false;
		}
		float num5 = 1f - (float)Math.Pow(1.0 - (double)_speed, num2 / 16.667);
		Value += num3 * num5;
		Repaint();
		return true;
	}
}
