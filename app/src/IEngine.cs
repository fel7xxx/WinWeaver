// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System.Collections;

namespace WinWeaver
{
	internal interface IEngine
	{
		bool Ready { get; }

		string LastError { get; }

		void Start();

		// stops the script that is running right now (called from the UI thread)
		void Stop();

		object Eval(string script, Hashtable vars = null);

		string Get(string funcName);

		void Call(string funcName, params object[] args);

		T As<T>(object o, T fallback);
	}
}
