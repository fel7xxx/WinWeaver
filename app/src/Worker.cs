// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;

namespace WinWeaver
{
	internal class Worker
	{
		private readonly BlockingCollection<Action> _q = new BlockingCollection<Action>();

		private readonly Control _ui;

		public IEngine Engine;

		public string Name;

		public event Action<Exception> Failed;

		// job id of the action that is about to enqueue its first step (set by Host.TryBegin)
		public int PendingTag;

		// the job whose step runs on the engine thread right now
		public volatile int RunningTag;

		private readonly ConcurrentDictionary<int, bool> _cancelled = new ConcurrentDictionary<int, bool>();

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
			thread.SetApartmentState(ApartmentState.STA);
			// reading/applying in PowerShell must never take CPU from the window
			thread.Priority = ThreadPriority.BelowNormal;
			thread.Start();
		}

		private void Loop()
		{
			foreach (Action item in _q.GetConsumingEnumerable())
			{
				try
				{
					item();
				}
				catch
				{
				}
			}
		}

		public void Post(Action a)
		{
			if (_ui.IsDisposed || !_ui.IsHandleCreated)
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
			int tag = PendingTag;
			PendingTag = 0;
			_q.Add(delegate
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
						long t0 = Lag.T0();
						done(r);
						Lag.Done("Ответ движка" + (Name != null ? (" (" + Name + ")") : "") + ": " + (done.Method.DeclaringType?.Name ?? "?") + "." + done.Method.Name, t0);
					});
				}
			});
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
}
