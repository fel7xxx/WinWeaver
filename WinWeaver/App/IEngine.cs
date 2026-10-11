using System.Collections;

namespace WinWeaver;

internal interface IEngine
{
	bool Ready { get; }

	string LastError { get; }

	void Start();

	void Stop();

	object Eval(string script, Hashtable vars = null);

	string Get(string funcName);

	void Call(string funcName, params object[] args);

	T As<T>(object o, T fallback);
}
