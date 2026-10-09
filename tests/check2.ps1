# WinWeaver — Windows 11 tweaker
# Copyright (C) 2026 fel7xxx
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify it under the terms of the
# GNU General Public License as published by the Free Software Foundation, either version 3 of the
# License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
# See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

param($f)
$t=[IO.File]::ReadAllText($f); $tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseInput($t,[ref]$tok,[ref]$err)
"PARSE ERRORS: $($err.Count)"; $err | % { "  line $($_.Extent.StartLineNumber): $($_.Message)" }
$aliases='ac asnp cat cd chdir clc clear clhy cli clp cls clv cnsn compare copy cp cpi cpp curl cvpa dbp del diff dir dnsn ebp echo epal epcsv epsn erase etsn exsn fc fhx fl foreach ft fw gal gbp gc gcb gci gcm gcs gdr ghy gi gin gjb gl gm gmo gp gps gpv group gsn gsnp gsv gtz gu gv gwmi h history icm iex ihy ii ipal ipcsv ipmo ipsn irm ise iwmi iwr kill lp ls man md measure mi mount move mp mv nal ndr ni nmo npssc nsn nv ogv oh popd ps pushd pwd r rbp rcjb rcsn rd rdr ren ri rjb rm rmdir rmo rni rnp rp rsn rsnp rujb rv rvpa rwmi sajb sal saps sasv sbp sc scb select set shcm si sl sleep sls sort sp spjb spps spsv start stz sujb sv swmi tee trcm type wget where wjb write' -split ' '
$funcs=$ast.FindAll({$args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true)|%{$_.Name}
"FUNCTIONS: $($funcs.Count) ALIAS CLASHES: $(($funcs|?{$aliases -contains $_}) -join ',')"
"REDEFINED (expected GUI overrides): $(($funcs|Group-Object|?{$_.Count -gt 1}|%{$_.Name}) -join ', ')"
"PS7-ONLY: $(($tok|?{$_.Kind -in 'QuestionQuestion','QuestionQuestionEquals','AndAnd','OrOr','QuestionDot','QuestionLBracket'}).Count)"
# called functions that are not defined and not cmdlets/exes
$called=$ast.FindAll({$args[0] -is [System.Management.Automation.Language.CommandAst]},$true)|%{$_.GetCommandName()}|?{$_}|Sort-Object -Unique
$known='Add-AppxPackage Clear-RecycleBin Confirm-SecureBootUEFI ConvertTo-SecureString Delete-DeliveryOptimizationCache Disable-MMAgent Enable-MMAgent Export-ScheduledTask Get-FileHash Get-HotFix Get-LocalUser Get-MMAgent Get-MpPreference Get-MpThreatDetection Get-NetFirewallProfile Get-SmbServerConfiguration Get-StartApps Get-Tpm Get-WindowsReservedStorageState Measure-Object Move-Item Rename-Computer Rename-Item Restart-Service Set-LocalUser Set-SmbServerConfiguration Set-WindowsReservedStorageState Start-MpScan Update-MpSignature Add-Content Add-Member Add-Type Checkpoint-Computer Clear-Host Copy-Item Disable-BitLocker Disable-ScheduledTask Disable-WindowsOptionalFeature Enable-ComputerRestore Enable-ScheduledTask ForEach-Object Get-AppxPackage Get-AppxProvisionedPackage Get-BitLockerVolume Get-ChildItem Get-CimInstance Get-ComputerRestorePoint Get-Content Get-Date Get-ItemProperty Get-MpComputerStatus Get-PnpDevice Get-Process Get-PSDrive Get-ScheduledTask Get-Volume Get-WindowsOptionalFeature Join-Path New-Item New-ItemProperty New-Object New-ScheduledTaskAction New-ScheduledTaskPrincipal New-ScheduledTaskSettingsSet New-ScheduledTaskTrigger Out-Null Read-Host Register-ScheduledTask Remove-AppxPackage Remove-AppxProvisionedPackage Remove-Item Remove-ItemProperty Restore-Computer Set-Content Set-ItemProperty Set-MpPreference Set-Service Sort-Object Start-Process Start-Service Start-Sleep Stop-Process Stop-Service Test-Path Unregister-ScheduledTask Where-Object Write-Host Clear-DnsClientCache ConvertFrom-Json ConvertTo-Json Get-AuthenticodeSignature Get-Command Get-Disk Get-DnsClientServerAddress Get-Item Get-NetAdapter Get-Partition Get-PhysicalDisk Select-Object Set-DnsClientServerAddress Split-Path Add-DnsClientDohServerAddress Get-NetAdapterPowerManagement Get-NetIPAddress Get-NetIPInterface Get-NetOffloadGlobalSetting Get-NetRoute Get-NetTCPSetting Get-NetTeredoConfiguration Resolve-DnsName Set-NetAdapterPowerManagement' -split ' '
"UNKNOWN COMMANDS: $(($called|?{($funcs -notcontains $_) -and ($known -notcontains $_) -and ($_ -notmatch '\.(exe|msc)$')}) -join ', ')"
$ln=0;foreach($l in ($t -split "`n")){$ln++; if($l -match "[\u2018\u2019\u201C\u201D]"){"CURLY QUOTE line $ln"}}
# CI: parse errors or PowerShell 7-only syntax break Windows PowerShell 5.1
if ($err.Count -gt 0 -or ($tok | ? { $_.Kind -in 'QuestionQuestion','QuestionQuestionEquals','AndAnd','OrOr','QuestionDot','QuestionLBracket' }).Count -gt 0) { exit 1 }
