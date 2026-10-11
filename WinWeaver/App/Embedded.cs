#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	internal sealed class ModuleInitializerAttribute : Attribute
	{
	}
}

namespace WinWeaver
{
	/// <summary>
	/// Однофайловая сборка: библиотеки Avalonia/Skia лежат внутри WinWeaver.exe (ресурсы ww.lib.* и ww.native.*, сжаты GZip).
	/// Управляемые сборки загружаются из памяти через AssemblyResolve, нативные распаковываются
	/// в %ProgramData%\WinWeaver\runtime\&lt;версия&gt; (доступ на запись только у администраторов и SYSTEM),
	/// проверяются по SHA-256 и загружаются до первого обращения к Skia.
	/// </summary>
	internal static class Embedded
	{
		private const string LibPrefix = "ww.lib.";
		private const string NativePrefix = "ww.native.";
		private static readonly Dictionary<string, Assembly> _loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
		private static readonly object _lock = new object();
		private static readonly List<IntPtr> _handles = new List<IntPtr>();
		public static Exception NativeError;

		[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

		[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern bool SetDllDirectory(string path);

		[System.Runtime.CompilerServices.ModuleInitializer]
		internal static void Init()
		{
			AppDomain.CurrentDomain.AssemblyResolve += Resolve;
			try
			{
				ExtractNatives();
			}
			catch (Exception ex)
			{
				NativeError = ex;
				try
				{
					string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinWeaver");
					Directory.CreateDirectory(dir);
					File.AppendAllText(Path.Combine(dir, "tweaker.log"), DateTime.Now.ToString("s") + " NATIVE " + ex + Environment.NewLine);
				}
				catch
				{
				}
			}
		}

		private static byte[] Read(string resource)
		{
			using Stream s = typeof(Embedded).Assembly.GetManifestResourceStream(resource);
			if (s == null) return null;
			using var gz = new GZipStream(s, CompressionMode.Decompress);
			using var ms = new MemoryStream();
			gz.CopyTo(ms);
			return ms.ToArray();
		}

		private static Assembly Resolve(object sender, ResolveEventArgs e)
		{
			AssemblyName name;
			try
			{
				name = new AssemblyName(e.Name);
			}
			catch
			{
				return null;
			}
			if (!string.IsNullOrEmpty(name.CultureName) && !name.CultureName.Equals("neutral", StringComparison.OrdinalIgnoreCase)) return null;
			lock (_lock)
			{
				if (_loaded.TryGetValue(name.Name, out Assembly a)) return a;
				byte[] bytes = Read(LibPrefix + name.Name + ".dll.gz");
				if (bytes == null) return null;
				a = Assembly.Load(bytes);
				_loaded[name.Name] = a;
				return a;
			}
		}

		private static bool IsAdmin()
		{
			try
			{
				using var id = WindowsIdentity.GetCurrent();
				return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
			}
			catch
			{
				return false;
			}
		}

		private static void ExtractNatives()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT || !Environment.Is64BitProcess) return;
			var natives = new List<KeyValuePair<string, byte[]>>();
			foreach (string res in typeof(Embedded).Assembly.GetManifestResourceNames())
			{
				if (!res.StartsWith(NativePrefix, StringComparison.Ordinal) || !res.EndsWith(".gz", StringComparison.Ordinal)) continue;
				natives.Add(new KeyValuePair<string, byte[]>(res.Substring(NativePrefix.Length, res.Length - NativePrefix.Length - 3), Read(res)));
			}
			if (natives.Count == 0) return;
			bool admin = IsAdmin();
			string root = Environment.GetFolderPath(admin ? Environment.SpecialFolder.CommonApplicationData : Environment.SpecialFolder.LocalApplicationData);
			string ver = typeof(Embedded).Assembly.GetName().Version.ToString();
			string dir = Path.Combine(root, "WinWeaver", "runtime", ver);
			Directory.CreateDirectory(dir);
			if (admin)
			{
				// Нативные DLL загружаются в процесс с правами администратора: менять их могут только администраторы и SYSTEM.
				var sec = new DirectorySecurity();
				sec.SetAccessRuleProtection(true, false);
				var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
				sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
				sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
				sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
				Directory.SetAccessControl(dir, sec);
			}
			using var sha = SHA256.Create();
			foreach (var n in natives)
			{
				string path = Path.Combine(dir, n.Key);
				byte[] want = sha.ComputeHash(n.Value);
				if (!File.Exists(path) || !Same(sha.ComputeHash(File.ReadAllBytes(path)), want))
				{
					string tmp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
					File.WriteAllBytes(tmp, n.Value);
					try
					{
						if (File.Exists(path)) File.Delete(path);
						File.Move(tmp, path);
					}
					finally
					{
						if (File.Exists(tmp)) File.Delete(tmp);
					}
					if (!Same(sha.ComputeHash(File.ReadAllBytes(path)), want)) throw new IOException("Checksum mismatch: " + path);
				}
			}
			SetDllDirectory(dir);
			foreach (var n in natives)
			{
				// LOAD_WITH_ALTERED_SEARCH_PATH: зависимости ищутся рядом с самой DLL.
				IntPtr h = LoadLibraryEx(Path.Combine(dir, n.Key), IntPtr.Zero, 0x00000008);
				if (h == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "LoadLibrary " + n.Key);
				_handles.Add(h);
			}
		}

		private static bool Same(byte[] a, byte[] b)
		{
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++)
				if (a[i] != b[i]) return false;
			return true;
		}
	}
}
#endif
