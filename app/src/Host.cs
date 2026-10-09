// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

namespace WinWeaver
{
	internal abstract class Host
	{
		public Worker Worker;

		private int _busy;

		private int _long;

		public bool IsBusy => _busy > 0;

		// Every action goes to one queue: the engine thread (Worker) runs them one after another.
		// If something is already running, the new action is not refused any more — it waits in the queue
		// and the task toast shows "добавлено в очередь" with an expandable list.
		private int _seq;

		public bool TryBegin(bool quick = false, string title = null)
		{
			bool queued = _busy > 0;
			int id = ++_seq;
			if (Worker != null)
			{
				Worker.PendingTag = id;
			}
			LastJob = id;
			_busy++;
			if (!quick)
			{
				_long++;
			}
			JobBegin(id, title, queued);
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

		public int LastJob;

		// a queued job is skipped when its turn comes; a running one is stopped in the engine
		public void Cancel(int id)
		{
			if (Worker == null)
			{
				return;
			}
			Worker.MarkCancelled(id);
			if (Worker.RunningTag == id)
			{
				Worker.Engine?.Stop();
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
}
