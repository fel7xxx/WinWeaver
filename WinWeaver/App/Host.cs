namespace WinWeaver;

internal abstract class Host
{
	public Worker Worker;

	private int _busy;

	private int _long;

	private int _seq;

	public int LastJob;

	public bool IsBusy => _busy > 0;

	public bool TryBegin(bool quick = false, string title = null)
	{
		bool queued = _busy > 0;
		int num = ++_seq;
		if (Worker != null)
		{
			Worker.PendingTag = num;
		}
		LastJob = num;
		_busy++;
		if (!quick)
		{
			_long++;
		}
		JobBegin(num, title, queued);
		BusyChanged(busy: true);
		return true;
	}

	public void End(bool quick = false)
	{
		if (_busy > 0)
		{
			_busy--;
		}
		if (!quick && _long > 0)
		{
			_long--;
		}
		JobEnd();
		BusyChanged(busy: false);
		AfterAction();
	}

	public void Cancel(int id)
	{
		if (Worker != null)
		{
			Worker.MarkCancelled(id);
			if (Worker.RunningTag == id)
			{
				Worker.Engine?.Stop();
			}
		}
	}

	public abstract void Status(string kind, string text);

	protected abstract void BusyChanged(bool busy);

	protected abstract void AfterAction();

	protected virtual void JobBegin(int id, string title, bool queued)
	{
	}

	protected virtual void JobEnd()
	{
	}
}
