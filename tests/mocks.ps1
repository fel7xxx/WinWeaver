# WinWeaver — Windows 11 tweaker
# Copyright (C) 2026 fel7xxx
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify it under the terms of the
# GNU General Public License as published by the Free Software Foundation, either version 3 of the
# License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
# See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

$global:REG=@{}; $global:FILES=@{}; $global:CALLS=New-Object System.Collections.ArrayList; $global:FEED=New-Object System.Collections.Queue
function global:K($p){ ($p -replace '^HKCU:','HKCU' -replace '^HKLM:','HKLM').TrimEnd('\').ToLower() }
function global:IsReg($p){ "$p" -match '^(HKCU|HKLM)' }
function global:Test-Path { [CmdletBinding()] param([Parameter(Position=0)]$Path,$PathType)
  foreach($p in @($Path)){ if(IsReg $p){ if(-not $REG.ContainsKey((K $p))){return $false} } else { if(-not $FILES.ContainsKey("$p".ToLower())){return $false} } }; return $true }
function global:New-Item { [CmdletBinding()] param([Parameter(Position=0)]$Path,$ItemType,[switch]$Force,$Value)
  foreach($p in @($Path)){ if(IsReg $p){ if(-not $REG.ContainsKey((K $p))){ $REG[(K $p)]=@{} } } else { $FILES["$p".ToLower()]='DIR' } } }
function global:New-ItemProperty { [CmdletBinding()] param($Path,$Name,$Value,$PropertyType,[switch]$Force) if(-not $REG.ContainsKey((K $Path))){ throw "key missing $Path" }; $REG[(K $Path)][$Name]=$Value }
function global:Set-ItemProperty { [CmdletBinding()] param($Path,$Name,$Value,$Type) if(-not $REG.ContainsKey((K $Path))){ throw "key missing $Path" }; $REG[(K $Path)][$Name]=$Value }
function global:Get-ItemProperty { [CmdletBinding()] param([Parameter(Position=0)]$Path,$Name)
  $k=K $Path; if(-not $REG.ContainsKey($k)){ return $null }; if($Name -and -not $REG[$k].ContainsKey($Name)){ return $null }
  $o=[pscustomobject]@{}; foreach($n in $REG[$k].Keys){ $o | Add-Member -NotePropertyName $n -NotePropertyValue $REG[$k][$n] }; return $o }
function global:Remove-ItemProperty { [CmdletBinding()] param($Path,$Name) $k=K $Path; if($REG.ContainsKey($k)){ $REG[$k].Remove($Name) } }
function global:Remove-Item { [CmdletBinding()] param([Parameter(Position=0)]$Path,[switch]$Recurse,[switch]$Force)
  foreach($p in @($Path)){ if(IsReg $p){ $kk=K $p; @($REG.Keys) | ? { $_ -eq $kk -or $_.StartsWith("$kk\") } | % { $REG.Remove($_) } } else { $FILES.Remove("$p".ToLower()) } } }
function global:Set-Content { [CmdletBinding()] param($Path,$Value,$Encoding) $FILES["$Path".ToLower()]="$Value" }
function global:Add-Content { [CmdletBinding()] param($Path,$Value) $FILES["$Path".ToLower()]+="$Value`n" }
function global:Get-Content { [CmdletBinding()] param([Parameter(Position=0)]$Path,$TotalCount) $v=$FILES["$Path".ToLower()]; if($v){ ($v -split "`n")[0] } }
function global:Copy-Item { [CmdletBinding()] param($Path,$Destination,[switch]$Force) $FILES["$Destination".ToLower()]='COPY' }
function global:Clear-Host {}; function global:Start-Sleep {}
function global:Read-Host { param($Prompt) if("$Prompt" -match 'y/n'){ [void]$CALLS.Add("CONFIRM: $Prompt"); return 'n' }; if($FEED.Count){ return $FEED.Dequeue() } else { return '0' } }
function global:Write-Host { param([Parameter(Position=0)]$Object,$ForegroundColor,[switch]$NoNewline) }
function global:Rec { [void]$CALLS.Add(($args -join ' ')); $global:LASTEXITCODE=0 }
foreach($c in 'Get-Service','Set-Service','Stop-Service','Start-Service','Disable-ScheduledTask','Enable-ScheduledTask','Register-ScheduledTask','Unregister-ScheduledTask','Remove-AppxPackage','Remove-AppxProvisionedPackage','Disable-WindowsOptionalFeature','Set-MpPreference','Disable-BitLocker','Checkpoint-Computer','Enable-ComputerRestore','Stop-Process','Start-Process','Get-Process','Get-AppxPackage','Get-AppxProvisionedPackage','Get-ChildItem'){ Set-Item "function:global:$c" ([scriptblock]::Create("Rec '$c' `$args; `$null")) }
function global:Get-ScheduledTask { [CmdletBinding()] param($TaskPath,$TaskName) [pscustomobject]@{TaskPath=$TaskPath;TaskName=$TaskName;State='Ready'} }
function global:New-ScheduledTaskAction {'A'}; function global:New-ScheduledTaskTrigger {'T'}; function global:New-ScheduledTaskPrincipal {'P'}
function global:Get-WindowsOptionalFeature { [pscustomobject]@{State='Enabled'} }
function global:Get-MpComputerStatus { [pscustomobject]@{IsTamperProtected=$global:TAMPER;RealTimeProtectionEnabled=$true} }
function global:Get-BitLockerVolume { @([pscustomobject]@{MountPoint='C:';VolumeStatus='FullyEncrypted';EncryptionPercentage=100}) }
function global:Get-Volume { @() }; function global:Get-CimInstance { [pscustomobject]@{SecurityServicesRunning=@(2)} }; function global:Get-PSDrive { [pscustomobject]@{Free=1000MB} }
$global:ACTIVE='381b4222-f694-41f0-9685-ff5bb260df2e'
function global:powercfg.exe { Rec 'powercfg' @args
  if($args[0] -eq '/getactivescheme'){ "GUID: $global:ACTIVE  (x)" } elseif($args[0] -eq '/list'){ 'GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (x) *' } elseif($args[0] -eq '-duplicatescheme'){ 'GUID: 11111111-2222-3333-4444-555555555555  (y)' } elseif($args[0] -eq '/setactive'){ $global:ACTIVE=$args[1] } }
function global:reg.exe { Rec 'reg.exe' @args
  if($args[0] -eq 'add' -and $args -contains '/ve'){ $k=($args[1] -replace '^HKCU\\','HKCU\').ToLower(); $REG[$k]=@{'(default)'=''} ; $p=$k; while($p -match '\\'){ $p=$p.Substring(0,$p.LastIndexOf('\')); if(-not $REG.ContainsKey($p)){$REG[$p]=@{}} } }
  elseif($args[0] -eq 'delete'){ $k=$args[1].ToLower(); @($REG.Keys) | ? { $_ -eq $k -or $_.StartsWith("$k\") } | % { $REG.Remove($_) } } }
foreach($e in 'bcdedit.exe','takeown.exe','icacls.exe','manage-bde.exe','ipconfig.exe','dism.exe','sfc.exe','shutdown.exe'){ Set-Item "function:global:$e" ([scriptblock]::Create("Rec '$e' @args")) }
Add-Type -TypeDefinition 'using System; public static class WNative { public static bool SpiString(int a,int b,string c,int d){return true;} public static bool SpiInts(int a,int b,int[] c,int d){ return c.Length==3; } public static IntPtr SendMessageTimeout(IntPtr h,uint m,UIntPtr w,string l,uint f,uint t,out UIntPtr r){ r=UIntPtr.Zero; return IntPtr.Zero; } }'
$env:ProgramData='C:\ProgramData'; $env:WINDIR='C:\Windows'; $env:SystemDrive='C:'; $env:TEMP='C:\T'; $env:LOCALAPPDATA='C:\L'; $env:APPDATA='C:\A'; $env:PUBLIC='C:\Users\Public'; $env:ProgramFiles='C:\Program Files'; ${env:ProgramFiles(x86)}='C:\Program Files (x86)'

$global:REG['hklm\software\microsoft\windows nt\currentversion']=@{DisplayVersion='25H2';CurrentBuild='26200'}
