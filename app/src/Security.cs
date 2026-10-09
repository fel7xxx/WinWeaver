// WinWeaver — Windows 11 tweaker
// Copyright (C) 2026 fel7xxx
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
// See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace WinWeaver
{
	// The data folder holds the change history (with rollback scripts), the Defender guard script that runs
	// as SYSTEM and downloaded installers. It must be writable only by administrators and SYSTEM, otherwise a
	// standard user could plant a file there and get it executed with full rights.
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
			string dir = Brand.DataDir;
			try
			{
				bool trusted = IsHardened();
				if (Directory.Exists(dir))
				{
					DirectoryInfo di = new DirectoryInfo(dir);
					// a junction / symlink could redirect our writes anywhere: remove the link itself
					if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
					{
						Program.Log("SECURITY data folder was a reparse point, removed");
						Directory.Delete(dir, false);
						trusted = false;
					}
					else if (!trusted || !OwnedByAdmins(di.GetAccessControl(AccessControlSections.Owner)))
					{
						// created before WinWeaver protected it (or by someone else): nothing inside can be trusted
						Program.Log("SECURITY data folder was not protected, its contents were removed");
						Wipe(dir);
						trusted = false;
					}
				}
				Directory.CreateDirectory(dir);
				Apply(new DirectoryInfo(dir));
				Directory.CreateDirectory(Path.Combine(dir, "dl"));
				if (!trusted)
				{
					MarkHardened();
				}
			}
			catch (Exception ex)
			{
				Program.Log("SECURITY harden failed: " + ex.Message);
			}
		}

		private static void Apply(DirectoryInfo di)
		{
			DirectorySecurity ds = new DirectorySecurity();
			ds.SetOwner(Admins);
			ds.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
			InheritanceFlags inh = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
			ds.AddAccessRule(new FileSystemAccessRule(SystemSid, FileSystemRights.FullControl, inh, PropagationFlags.None, AccessControlType.Allow));
			ds.AddAccessRule(new FileSystemAccessRule(Admins, FileSystemRights.FullControl, inh, PropagationFlags.None, AccessControlType.Allow));
			ds.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inh, PropagationFlags.None, AccessControlType.Allow));
			di.SetAccessControl(ds);
			// children inherit the new rules; reset explicit entries and owners that may have been planted
			foreach (FileSystemInfo f in di.GetFileSystemInfos("*", SearchOption.AllDirectories))
			{
				try
				{
					if ((f.Attributes & FileAttributes.ReparsePoint) != 0)
					{
						if (f is DirectoryInfo)
						{
							Directory.Delete(f.FullName, false);
						}
						else
						{
							File.Delete(f.FullName);
						}
						continue;
					}
					if (f is DirectoryInfo d)
					{
						DirectorySecurity s = new DirectorySecurity();
						s.SetOwner(Admins);
						s.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
						d.SetAccessControl(s);
					}
					else
					{
						FileSecurity s = new FileSecurity();
						s.SetOwner(Admins);
						s.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
						File.SetAccessControl(f.FullName, s);
					}
				}
				catch
				{
				}
			}
		}

		private static bool OwnedByAdmins(ObjectSecurity s)
		{
			IdentityReference o = s.GetOwner(typeof(SecurityIdentifier));
			return o != null && (o.Equals(Admins) || o.Equals(SystemSid));
		}

		private static void Wipe(string dir)
		{
			foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
			{
				try
				{
					File.SetAttributes(f, FileAttributes.Normal);
					File.Delete(f);
				}
				catch
				{
				}
			}
			foreach (string d in Directory.GetDirectories(dir))
			{
				try
				{
					Directory.Delete(d, true);
				}
				catch
				{
				}
			}
		}

		// the marker lives in HKLM, where standard users cannot write
		private static bool IsHardened()
		{
			try
			{
				using (RegistryKey k = Registry.LocalMachine.OpenSubKey(MarkerKey))
				{
					return k != null && (k.GetValue("DataDirProtected") as int? ?? 0) == 1;
				}
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
				using (RegistryKey k = Registry.LocalMachine.CreateSubKey(MarkerKey))
				{
					k.SetValue("DataDirProtected", 1, RegistryValueKind.DWord);
				}
			}
			catch
			{
			}
		}
	}
}
