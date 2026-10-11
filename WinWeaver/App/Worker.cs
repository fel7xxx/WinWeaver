using System;
using System.Collections.Concurrent;
using System.Threading;

namespace WinWeaver;

internal class Worker : IDisposable
{
	private int _disposed;
	private readonly BlockingCollection<Action> _q = new BlockingCollection<Action>();

	private readonly Control _ui;

	public IEngine Engine;

	public string Name;

	public int PendingTag;

	public volatile int RunningTag;

	private readonly ConcurrentDictionary<int, bool> _cancelled = new ConcurrentDictionary<int, bool>();

	public event Action<Exception> Failed;

	public void MarkCancelled(int tag)
	{
		_cancelled[tag] = true;
	}

	public Worker(Control ui, IEngine engine)
	{
		_ui = ui;
		Engine = engine;
		Thread thread = new Thread(Loop);
		thread.IsBackground = true;
		thread.Name = "WinWeaver engine";
		if (Win.IsWindows) thread.SetApartmentState(ApartmentState.STA);
		thread.Priority = ThreadPriority.BelowNormal;
		thread.Start();
	}

	private void Loop()
	{
		try
		{
		foreach (Action item in _q.GetConsumingEnumerable())
		{
			if (Volatile.Read(ref _disposed) != 0) break;
			try
			{
				item();
			}
			catch (Exception ex)
			{
				Program.Log("WORKER ERROR " + ex);
			}
		}
		}
		finally
		{
			(Engine as IDisposable)?.Dispose();
			_cancelled.Clear();
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		_q.CompleteAdding();
		Engine?.Stop();
	}

	public void Post(Action a)
	{
		if (Volatile.Read(ref _disposed) != 0 || _ui.IsDisposed || !_ui.IsHandleCreated)
		{
			return;
		}
		try
		{
			_ui.BeginInvoke(a);
		}
		catch
		{
		}
	}

	public void Run(Func<IEngine, object> work, Action<object> done)
	{
		if (Volatile.Read(ref _disposed) != 0) return;
		int tag = PendingTag;
		PendingTag = 0;
		try { _q.Add(delegate
		{
			object r = null;
			Exception err = null;
			try
			{
				if (tag == 0 || !_cancelled.ContainsKey(tag))
				{
					RunningTag = tag;
					r = work(Engine);
				}
			}
			catch (Exception ex)
			{
				err = ex;
			}
			RunningTag = 0;
			if (err != null && tag != 0 && _cancelled.ContainsKey(tag))
			{
				err = null;
			}
			if (err != null)
			{
				Program.Log("JOB ERROR " + err);
				Action<Exception> f = this.Failed;
				if (f != null)
				{
					Post(delegate
					{
						f(err);
					});
				}
			}
			if (done != null)
			{
				Post(delegate
				{
					long t = Lag.T0();
					done(r);
					Lag.Done("Ответ движка" + ((Name != null) ? (" (" + Name + ")") : "") + ": " + (done.Method.DeclaringType?.Name ?? "?") + "." + done.Method.Name, t);
				});
			}
			if (tag != 0) _cancelled.TryRemove(tag, out _);
		}); }
		catch (InvalidOperationException) when (Volatile.Read(ref _disposed) != 0) { }
	}

	public T Sync<T>(Func<T> f)
	{
		if (!_ui.InvokeRequired)
		{
			return f();
		}
		return (T)_ui.Invoke(f);
	}
}
