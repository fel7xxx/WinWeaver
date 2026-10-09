// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PowerShell;

namespace WinWeaver
{
	internal class PsEngine : IEngine
	{
		private Runspace _rs;

		private PowerShell _ps;

		private readonly object _lock = new object();

		private readonly EngineUI _ui;

		private readonly string _exePath;

		private readonly string _mode;

		public bool Ready { get; private set; }

		public string LastError { get; private set; }

		public PsEngine(EngineUI ui, string exePath, string mode)
		{
			_ui = ui;
			_exePath = exePath;
			_mode = mode;
		}

		public void Start()
		{
			InitialSessionState initialSessionState = InitialSessionState.CreateDefault();
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				initialSessionState.ExecutionPolicy = ExecutionPolicy.Bypass;
			}
			_rs = RunspaceFactory.CreateRunspace(initialSessionState);
			_rs.ApartmentState = ApartmentState.STA;
			_rs.ThreadOptions = PSThreadOptions.ReuseThread;
			_rs.Open();
			_ps = PowerShell.Create();
			_ps.Runspace = _rs;
			_rs.SessionStateProxy.SetVariable("EngineUI", _ui);
			_rs.SessionStateProxy.SetVariable("WwExe", _exePath);
			_rs.SessionStateProxy.SetVariable("WwMode", _mode ?? "");
			_rs.SessionStateProxy.SetVariable("ErrorActionPreference", "SilentlyContinue");
			string script = ReadResource("engine.ps1");
			_ps.AddScript(script, useLocalScope: false).Invoke();
			_ps.Commands.Clear();
			Ready = true;
		}

		public static string ReadResource(string name)
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
			foreach (string text in manifestResourceNames)
			{
				if (!(text == name) && !text.EndsWith("." + name))
				{
					continue;
				}
				using (Stream stream = executingAssembly.GetManifestResourceStream(text))
				{
					using (StreamReader streamReader = new StreamReader(stream))
					{
						return streamReader.ReadToEnd();
					}
				}
			}
			throw new Exception("resource not found: " + name);
		}

		public void Stop()
		{
			try
			{
				PowerShell ps = _ps;
				if (ps != null && ps.InvocationStateInfo.State == PSInvocationState.Running)
				{
					ps.BeginStop(null, null);
				}
			}
			catch
			{
			}
		}

		public object Eval(string script, Hashtable vars = null)
		{
			lock (_lock)
			{
				try
				{
					_ps.Commands.Clear();
					_ps.Streams.ClearStreams();
					if (vars != null)
					{
						foreach (DictionaryEntry var in vars)
						{
							_rs.SessionStateProxy.SetVariable((string)var.Key, var.Value);
						}
					}
					_ps.AddScript(script, useLocalScope: false);
					Collection<PSObject> collection = _ps.Invoke();
					if (_ps.HadErrors && _ps.Streams.Error.Count > 0)
					{
						LastError = _ps.Streams.Error[0].ToString();
					}
					if (collection == null || collection.Count == 0)
					{
						return null;
					}
					return Unwrap(collection[collection.Count - 1]);
				}
				catch (Exception ex)
				{
					LastError = ex.Message;
					return null;
				}
			}
		}

		private static object Unwrap(object o)
		{
			if (o is PSObject pSObject)
			{
				o = pSObject.BaseObject;
			}
			if (o is object[] array)
			{
				object[] array2 = new object[array.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array2[i] = Unwrap(array[i]);
				}
				return array2;
			}
			if (o is Hashtable hashtable)
			{
				// PowerShell hashtables are case-insensitive; keep that, so a key read as "Name" finds "name"
				Hashtable hashtable2 = new Hashtable(StringComparer.OrdinalIgnoreCase);
				{
					foreach (DictionaryEntry item in hashtable)
					{
						hashtable2[item.Key] = Unwrap(item.Value);
					}
					return hashtable2;
				}
			}
			return o;
		}

		public string Get(string f)
		{
			object obj = Eval(f);
			if (obj != null)
			{
				return obj.ToString();
			}
			return "";
		}

		public void Call(string f, params object[] args)
		{
			StringBuilder stringBuilder = new StringBuilder(f);
			Hashtable hashtable = new Hashtable();
			for (int i = 0; i < args.Length; i++)
			{
				string text = "felarg" + i;
				hashtable[text] = args[i];
				stringBuilder.Append(" $").Append(text);
			}
			Eval(stringBuilder.ToString(), hashtable);
		}

		public T As<T>(object o, T fallback)
		{
			try
			{
				if (o == null)
				{
					return fallback;
				}
				if (o is PSObject)
				{
					o = ((PSObject)o).BaseObject;
				}
				return (T)Convert.ChangeType(o, typeof(T));
			}
			catch
			{
				return fallback;
			}
		}
	}
}
