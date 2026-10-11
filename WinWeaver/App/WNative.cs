using System;
using System.Runtime.InteropServices;

public static class WNative
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
	public static extern bool SpiString(int action, int param, string value, int flags);

	[DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
	public static extern bool SpiInts(int action, int param, int[] value, int flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
}
