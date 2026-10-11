using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace WinWeaver;

internal static class Security
{
	private static readonly SecurityIdentifier Admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

	private static readonly SecurityIdentifier SystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

	private static readonly SecurityIdentifier Users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

	private const string MarkerKey = "SOFTWARE\\WinWeaver";

	public static void HardenDataDir()
	{
		if (Environment.OSVersion.Platform != PlatformID.Win32NT)
		{
			return;
		}
		string dataDir = Brand.DataDir;
		try
		{
			bool flag = IsHardened();
			if (Directory.Exists(dataDir))
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(dataDir);
				if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
				{
					Program.Log("SECURITY data folder was a reparse point, removed");
					Directory.Delete(dataDir, recursive: false);
					flag = false;
				}
				else if (!flag || !OwnedByAdmins(directoryInfo.GetAccessControl(AccessControlSections.Owner)))
				{
					Program.Log("SECURITY data folder was not protected, its contents were removed");
					Wipe(dataDir);
					flag = false;
				}
			}
			Directory.CreateDirectory(dataDir);
			Apply(new DirectoryInfo(dataDir));
			Directory.CreateDirectory(Path.Combine(dataDir, "dl"));
			if (!flag)
			{
				MarkHardened();
			}
		}
		catch (Exception ex)
		{
			Program.Log("SECURITY harden failed: " + ex.Message);
			throw new InvalidOperationException("Не удалось защитить папку данных. Системные операции заблокированы.", ex);
		}
	}

	private static void Apply(DirectoryInfo di)
	{
		DirectorySecurity directorySecurity = new DirectorySecurity();
		directorySecurity.SetOwner(Admins);
		directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
		InheritanceFlags inheritanceFlags = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
		directorySecurity.AddAccessRule(new FileSystemAccessRule(SystemSid, FileSystemRights.FullControl, inheritanceFlags, PropagationFlags.None, AccessControlType.Allow));
		directorySecurity.AddAccessRule(new FileSystemAccessRule(Admins, FileSystemRights.FullControl, inheritanceFlags, PropagationFlags.None, AccessControlType.Allow));
		directorySecurity.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inheritanceFlags, PropagationFlags.None, AccessControlType.Allow));
		di.SetAccessControl(directorySecurity);
		FileSystemInfo[] fileSystemInfos = di.GetFileSystemInfos();
		foreach (FileSystemInfo fileSystemInfo in fileSystemInfos)
		{
			try
			{
				if ((fileSystemInfo.Attributes & FileAttributes.ReparsePoint) != 0)
				{
					if (fileSystemInfo is DirectoryInfo)
					{
						Directory.Delete(fileSystemInfo.FullName, recursive: false);
					}
					else
					{
						File.Delete(fileSystemInfo.FullName);
					}
				}
				else if (fileSystemInfo is DirectoryInfo directoryInfo)
				{
					DirectorySecurity directorySecurity2 = new DirectorySecurity();
					directorySecurity2.SetOwner(Admins);
					directorySecurity2.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
					directoryInfo.SetAccessControl(directorySecurity2);
					Apply(directoryInfo);
				}
				else
				{
					FileSecurity fileSecurity = new FileSecurity();
					fileSecurity.SetOwner(Admins);
					fileSecurity.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
					new FileInfo(fileSystemInfo.FullName).SetAccessControl(fileSecurity);
				}
			}
			catch (Exception ex)
			{
				throw new IOException("Не удалось защитить " + fileSystemInfo.FullName, ex);
			}
		}
	}

	private static bool OwnedByAdmins(ObjectSecurity s)
	{
		IdentityReference owner = s.GetOwner(typeof(SecurityIdentifier));
		if (owner != null)
		{
			if (!owner.Equals(Admins))
			{
				return owner.Equals(SystemSid);
			}
			return true;
		}
		return false;
	}

	private static void Wipe(string dir)
	{
		string[] files = Directory.GetFiles(dir);
		foreach (string path in files)
		{
			try
			{
				File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
			}
			catch
			{
			}
		}
		files = Directory.GetDirectories(dir);
		foreach (string path2 in files)
		{
			try
			{
				if ((File.GetAttributes(path2) & FileAttributes.ReparsePoint) == 0) Wipe(path2);
				Directory.Delete(path2, recursive: false);
			}
			catch
			{
			}
		}
	}

	private static bool IsHardened()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WinWeaver");
			return registryKey != null && (registryKey.GetValue("DataDirProtected") as int?).GetValueOrDefault() == 1;
		}
		catch
		{
			return false;
		}
	}

	private static void MarkHardened()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\\WinWeaver");
			registryKey.SetValue("DataDirProtected", 1, RegistryValueKind.DWord);
		}
		catch
		{
		}
	}
}
