using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinWeaver;

internal static class Anim
{
	private class Run
	{
		public Control Owner;

		public object Key;

		public double Start;

		public int Ms;

		public Action<float> Step;

		public Action Done;
	}

	public static bool Enabled = Environment.GetEnvironmentVariable("WW_NOANIM") != "1" && !Prefs.NoAnim;

	private static readonly List<Run> _runs = new List<Run>();

	private static readonly List<Tween> _tweens = new List<Tween>();

	private static Timer _timer;

	private static readonly Stopwatch _clock = Stopwatch.StartNew();

	private static readonly double _tickMs = 1000.0 / (double)Stopwatch.Frequency;

	private static readonly List<Func<bool>> _frames = new List<Func<bool>>();








	// Время кадра: внутри vsync-тика анимации считают от метки кадра (ровный шаг),
	// а не от момента вызова — так меньше рывков при прокрутке.
	private static double _frameNow = double.NaN;

	private static double _frameOffset = double.NaN;

	private static double RealNow => (double)_clock.ElapsedTicks * _tickMs;

	public static double Now => double.IsNaN(_frameNow) ? RealNow : _frameNow;

	private static bool Busy
	{
		get
		{
			if (_runs.Count <= 0 && _tweens.Count <= 0)
			{
				return _frames.Count > 0;
			}
			return true;
		}
	}

	public static void Frame(Func<bool> f)
	{
		if (!_frames.Contains(f))
		{
			_frames.Add(f);
		}
		Ensure();
	}

	// Avalonia: кадры анимации синхронизированы с рендером окна (TopLevel.RequestAnimationFrame —
	// аналог DwmFlush-vsync в оригинале). Резервный таймер нужен, когда окно свёрнуто/не рисуется.
	private static bool _rafPending;

	private static double _lastTick;

	private static Avalonia.Controls.TopLevel FrameSource()
	{
		foreach (Form f in Application.OpenForms)
		{
			if (f.IsShownInternal && !f.IsDisposed && f.WindowState != FormWindowState.Minimized)
			{
				return f.Window;
			}
		}
		return null;
	}

	private static bool StartVsync()
	{
		var top = FrameSource();
		if (top == null)
		{
			return false;
		}
		if (!_rafPending)
		{
			_rafPending = true;
			top.RequestAnimationFrame(delegate(TimeSpan ts)
			{
				_rafPending = false;
				double real = RealNow;
				double fm = ts.TotalMilliseconds;
				if (double.IsNaN(_frameOffset) || Math.Abs(fm + _frameOffset - real) > 50.0)
				{
					_frameOffset = real - fm;
				}
				_frameNow = Math.Min(real, fm + _frameOffset);
				_lastTick = real;
				try
				{
					Tick(null, EventArgs.Empty);
				}
				finally
				{
					_frameNow = double.NaN;
				}
				if (Busy)
				{
					Ensure();
				}
			});
		}
		EnsureFallback();
		return true;
	}

	private static void EnsureFallback()
	{
		if (_timer == null)
		{
			_timer = new Timer
			{
				Interval = 15
			};
			_timer.Tick += delegate
			{
				if (Now - _lastTick > 45.0)
				{
					_lastTick = Now;
					Tick(null, EventArgs.Empty);
				}
				if (!Busy)
				{
					_timer.Stop();
				}
				else if (!_rafPending)
				{
					Ensure();
				}
			};
		}
		if (!_timer.Enabled)
		{
			_timer.Start();
		}
	}

	private static void Stop()
	{
		_timer?.Stop();
	}

	private static void HiRes(bool on)
	{
	}

	private static void Ensure()
	{
		if (!StartVsync())
		{
			EnsureFallback();
		}
	}

	public static float EaseOut(float t)
	{
		t = Clamp(t);
		float num = 1f - t;
		return 1f - num * num * num;
	}

	public static float EaseBack(float t)
	{
		t = Clamp(t);
		float num = t - 1f;
		return 1f + 2.5314221f * num * num * num + 1.531422f * num * num;
	}

	public static float EaseInOut(float t)
	{
		t = Clamp(t);
		if (!(t < 0.5f))
		{
			return 1f - (float)Math.Pow(-2f * t + 2f, 3.0) / 2f;
		}
		return 4f * t * t * t;
	}

	public static float Clamp(float t)
	{
		if (!(t < 0f))
		{
			if (!(t > 1f))
			{
				return t;
			}
			return 1f;
		}
		return 0f;
	}

	public static void Start(Control owner, int ms, Action<float> step, Action done = null, object key = null)
	{
		if (key != null)
		{
			_runs.RemoveAll((Run r) => r.Key != null && r.Key.Equals(key));
		}
		if (!Enabled || ms <= 0)
		{
			step(1f);
			done?.Invoke();
			return;
		}
		_runs.Add(new Run
		{
			Owner = owner,
			Key = key,
			Start = Now,
			Ms = ms,
			Step = step,
			Done = done
		});
		step(0f);
		Ensure();
	}

	public static void Cancel(object key)
	{
		_runs.RemoveAll((Run r) => r.Key != null && r.Key.Equals(key));
	}

	internal static void Watch(Tween t)
	{
		if (!_tweens.Contains(t))
		{
			_tweens.Add(t);
		}
		Ensure();
	}

	private static void Tick(object sender, EventArgs e)
	{
		long t = Lag.T0();
		try
		{
			TickCore();
		}
		finally
		{
			Lag.Done("Анимации (кадр)", t);
		}
	}

	private static void TickCore()
	{
		double now = Now;
		Run[] array = _runs.ToArray();
		foreach (Run run in array)
		{
			if (run.Owner != null && run.Owner.IsDisposed)
			{
				_runs.Remove(run);
				continue;
			}
			float num = (float)((now - run.Start) / (double)run.Ms);
			if (num >= 1f)
			{
				_runs.Remove(run);
				try
				{
					run.Step(1f);
					run.Done?.Invoke();
				}
				catch
				{
				}
			}
			else
			{
				try
				{
					run.Step(num);
				}
				catch
				{
					_runs.Remove(run);
				}
			}
		}
		Tween[] array2 = _tweens.ToArray();
		foreach (Tween tween in array2)
		{
			if (!tween.Advance())
			{
				_tweens.Remove(tween);
			}
		}
		if (_frames.Count > 0)
		{
			Func<bool>[] array3 = _frames.ToArray();
			foreach (Func<bool> func in array3)
			{
				bool flag;
				try
				{
					flag = func();
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					_frames.Remove(func);
				}
			}
		}
		if (!Busy)
		{
			Stop();
		}
	}
}
