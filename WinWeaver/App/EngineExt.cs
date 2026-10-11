namespace WinWeaver;

internal static class EngineExt
{
	public static string Str(this IEngine e, object o)
	{
		if (o != null)
		{
			return o.ToString();
		}
		return "";
	}
}
