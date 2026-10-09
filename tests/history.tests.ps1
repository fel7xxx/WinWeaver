# WinWeaver — Windows 11 tweaker
# Copyright (C) 2026 fel7xxx
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify it under the terms of the
# GNU General Public License as published by the Free Software Foundation, either version 3 of the
# License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
# See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

$ErrorActionPreference = 'Stop'
$env:ProgramData = '/tmp/pd'; $env:WINDIR = '/tmp/win'; $env:APPDATA='/tmp/ad'; $env:TEMP='/tmp'
Remove-Item /tmp/pd -Recurse -Force -ErrorAction SilentlyContinue; foreach ($f in [IO.Directory]::GetFiles('/tmp', 'pd*')) { [IO.File]::Delete($f) }
New-Item -ItemType Directory /tmp/pd -Force | Out-Null
$global:FAKEREG = @{}
function global:K($p) { ($p -replace '^HKCU:','HKCU' -replace '^HKLM:','HKLM').TrimEnd('\').ToLower() }
function global:IsReg($p) { "$p" -match '^(HKCU|HKLM)' }
$src = Get-Content (Join-Path $PSScriptRoot '..' 'app' 'engine.ps1') -Raw
# engine sets ErrorActionPreference; load it
. ([scriptblock]::Create($src))
# override registry primitives AFTER loading (engine functions call these cmdlets)
function Test-Path { param([Parameter(Position=0)]$Path,$PathType) if (IsReg $Path) { return $global:FAKEREG.ContainsKey((K $Path)) } ; return [System.IO.File]::Exists($Path) -or [System.IO.Directory]::Exists($Path) }
function New-Item { param([Parameter(Position=0)]$Path,$ItemType,[switch]$Force) if (IsReg $Path) { if (-not $global:FAKEREG.ContainsKey((K $Path))) { $global:FAKEREG[(K $Path)] = @{} } } else { [void][System.IO.Directory]::CreateDirectory($Path) } }
function New-ItemProperty { param($Path,$Name,$Value,$PropertyType,[switch]$Force) $global:FAKEREG[(K $Path)][$Name] = @{ V = $Value; T = $PropertyType } }
function Get-ItemProperty { param([Parameter(Position=0)]$Path,$Name) $k = K $Path; if (-not $global:FAKEREG.ContainsKey($k) -or -not $global:FAKEREG[$k].ContainsKey($Name)) { return $null }; $o = [pscustomobject]@{}; $o | Add-Member -NotePropertyName $Name -NotePropertyValue $global:FAKEREG[$k][$Name].V; return $o }
function Remove-ItemProperty { param($Path,$Name) $k = K $Path; if ($global:FAKEREG.ContainsKey($k)) { $global:FAKEREG[$k].Remove($Name) } }
function Get-Item { param($LiteralPath) $k = K $LiteralPath; if (-not $global:FAKEREG.ContainsKey($k)) { throw 'nokey' }
  $o = [pscustomobject]@{ K = $k }
  $o | Add-Member ScriptMethod GetValueNames { @($global:FAKEREG[$this.K].Keys) }
  $o | Add-Member ScriptMethod GetValueKind { param($n) $global:FAKEREG[$this.K][$n].T }
  $o | Add-Member ScriptMethod GetValue { param($n,$d,$opt) $global:FAKEREG[$this.K][$n].V }
  return $o }
function Send-FSettingChange {}
function Restart-FExplorer { $global:FExplorerTouched = $true; if ($global:FDeferExplorer) { $global:FExplorerPending = $true; return }; $global:ExplorerRestarts++ }
$global:ExplorerRestarts = 0
# fake setting
$global:Dark = $false
function Get-Theme { if ($global:Dark) { 'Тёмная' } else { 'Светлая' } }
function Set-Theme([bool]$d) { $global:Dark = $d }
$fail = 0
function Check($c, $m) { if ($c) { "ok   $m" } else { "FAIL $m"; $script:fail++ } }

Invoke-FSetting 'Тёмная тема' "(Get-Theme) -eq 'Тёмная'" 'Set-Theme $v' $true 'ui'
Check ($global:Dark) 'setting applied'
Invoke-FSetting 'Тёмная тема' "(Get-Theme) -eq 'Тёмная'" 'Set-Theme $v' $true 'ui'
$h = Get-FHistory
Check ($h.Count -eq 1) "no duplicate entry when value unchanged (count=$($h.Count))"
Set-FReg 'HKCU:\Test' 'A' 5
Invoke-FTracked 'reg change' "Set-FReg 'HKCU:\Test' 'A' 7; Set-FReg 'HKCU:\Test' 'B' 'x' 'String'; Restart-FExplorer" -Cat 'ui'
Invoke-FTracked 'reg nochange' "Set-FReg 'HKCU:\Test' 'A' 7" -Cat 'ui'
Invoke-FTracked 'info' "1" -NoUndo
$h = Get-FHistory
$h | % { "  - " + $_.Title + " can=" + $_.Can }
Check ($h.Count -eq 3) "entries=3 (got $($h.Count))"
Check ($h[2].Can -and $h[1].Can -and -not $h[0].Can) 'can flags'
# reload from disk
$script:FHist = $null
$h = Get-FHistory
Check ($h.Count -eq 3) "reload from json ($($h.Count))"
$id = ($h | Where-Object { $_.Title -eq 'reg change' }).Id
$r0 = $global:ExplorerRestarts
Check (Undo-FHistory $id) 'undo reg'
Check ((Get-FReg 'HKCU:\Test' 'A') -eq 5) 'A restored to 5'
Check ($null -eq (Get-FReg 'HKCU:\Test' 'B')) 'B removed'
Check ($global:ExplorerRestarts -eq $r0 + 1) 'explorer restarted once'
$id = ($h | Where-Object { $_.Title -eq 'Тёмная тема' }).Id
Check (Undo-FHistory $id) 'undo setting'
Check (-not $global:Dark) 'theme back to light'
Check (-not (Undo-FHistory $id)) 'second undo refused'
# group
Start-FGroup 'G'
Invoke-FSetting 'Тёмная тема' "(Get-Theme) -eq 'Тёмная'" 'Set-Theme $v' $true 'ui'
Invoke-FTracked 'g reg' "Set-FReg 'HKCU:\Test' 'C' 1; Restart-FExplorer"
Stop-FGroup
$h = Get-FHistory
$g = $h[0].Group
Check ($g -and $h[1].Group -eq $g -and $h[0].GroupTitle -eq 'G') 'group ids'
$r0 = $global:ExplorerRestarts
Check ((Undo-FHistGroup $g) -eq 2) 'group undo count'
Check (-not $global:Dark -and $null -eq (Get-FReg 'HKCU:\Test' 'C')) 'group undone'
Check ($global:ExplorerRestarts -eq $r0 + 1) 'single explorer restart for group'
Check ((Get-FHistCount) -eq 1) "active count (=$(Get-FHistCount))"
# binary + multi
Set-FReg 'HKCU:\Bin' 'X' ([byte[]](1,2,3)) 'Binary'
Invoke-FTracked 'bin' "Set-FReg 'HKCU:\Bin' 'X' ([byte[]](9,9)) 'Binary'"
$script:FHist = $null
$id = (Get-FHistory)[0].Id
[void](Undo-FHistory $id)
Check ((@(Get-FReg 'HKCU:\Bin' 'X') -join ',') -eq '1,2,3') 'binary restored'
# ---- exact command strings sent by the C# UI ----
Clear-FHistory
$global:Dark = $false
$fa1 = 'Тёмная тема'; $fa2 = "(Get-Theme) -eq 'Тёмная'"; $fa3 = 'Set-Theme $v'; $fa4 = $true; $fa5 = 'ui'
Invoke-Expression 'Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5'
$h = Get-FHistory
if ($global:Dark -and $h.Count -eq 1 -and $h[0].Can) { "ok   ui setting string" } else { "FAIL ui setting string"; $fail++ }
$fa1 = 'Запрещено автошифрование'; $fa2 = "Set-FReg 'HKLM:\SYSTEM\X' 'PreventDeviceEncryption' 1"; $fa3 = ''; $fa4 = 'protect'; $fa5 = $false; $fa6 = $false; $fa7 = ''
Invoke-Expression 'Invoke-FTracked $fa1 $fa2 -Undo $fa3 -Cat $fa4 -NoUndo:$fa5 -KeepReg:$fa6 -Note $fa7'
$h = Get-FHistory
if ($h.Count -eq 2 -and $h[0].Can) { "ok   ui tracked string" } else { "FAIL ui tracked string ($($h.Count))"; $fail++ }
$fa1 = $h[0].Id; Invoke-Expression '[void](Undo-FHistory $fa1)'
if ($null -eq (Get-FReg 'HKLM:\SYSTEM\X' 'PreventDeviceEncryption')) { "ok   ui undo string" } else { "FAIL ui undo string"; $fail++ }
# ---- data-driven tweaks ----
if (-not (Get-FTweak 'seconds')) { "ok   tweak off by default" } else { "FAIL tweak default"; $fail++ }
$fa1 = 'Секунды'; $fa2 = "Get-FTweak 'seconds'"; $fa3 = "Set-FTweak 'seconds' `$v"; $fa4 = $true; $fa5 = 'ui'
Invoke-Expression 'Invoke-FSetting $fa1 $fa2 $fa3 $fa4 $fa5'
if (Get-FTweak 'seconds') { "ok   tweak on" } else { "FAIL tweak on"; $fail++ }
$h = Get-FHistory; $fa1 = $h[0].Id; Invoke-Expression '[void](Undo-FHistory $fa1)'
if (-not (Get-FTweak 'seconds')) { "ok   tweak undone" } else { "FAIL tweak undo"; $fail++ }
Set-FTweakIdx 'combine' 2
if ((Get-FTweakIdx 'combine') -eq 2) { "ok   choice tweak" } else { "FAIL choice tweak"; $fail++ }
Set-FTweak 'feedback' $true; Set-FTweak 'feedback' $false
if ($null -eq (Get-FReg 'HKCU:\Software\Microsoft\Siuf\Rules' 'NumberOfSIUFInPeriod')) { "ok   tweak delete value" } else { "FAIL tweak delete"; $fail++ }
$sc = Get-FScore @(@("Get-FTweak 'feedback'", $false, 'a'), @("Get-FTweak 'seconds'", $true, 'b'))
if ($sc.Ok -eq 1 -and $sc.Total -eq 2 -and $sc.Bad[0] -eq 'b') { "ok   score" } else { "FAIL score"; $fail++ }
# ---- grouped tweaks (auto-tune / privacy all / "fix" from the home screen) ----
Start-FGroup 'Рекомендованные настройки'
Step-FTweaks @('tailored', 'lockads', 'nosuchtweak') 'privacy'
Stop-FGroup
$h = Get-FHistory
if ((Get-FTweak 'tailored') -and (Get-FTweak 'lockads') -and $h[0].GroupTitle -eq 'Рекомендованные настройки') { "ok   step tweaks grouped" } else { "FAIL step tweaks"; $fail++ }
Step-FTweaks @('tailored') 'privacy'
$h2 = Get-FHistory
if ($h2.Count -eq $h.Count) { "ok   step tweaks skip unchanged" } else { "FAIL step tweaks dup"; $fail++ }
$fa1 = $h[0].GroupId; if (-not $fa1) { $fa1 = $h[0].Group }
Invoke-Expression '[void](Undo-FHistGroup $fa1)'
if (-not (Get-FTweak 'tailored') -and -not (Get-FTweak 'lockads')) { "ok   group undo tweaks" } else { "FAIL group undo tweaks"; $fail++ }
$plan = Get-FPlan 'optimal' 'keep'
$names = @($plan | ForEach-Object { $_.N })
if ($names -contains 'Сбор данных и советы' -and $names -contains 'Виджеты и Copilot' -and $names -contains 'Обновления') { "ok   plan has new steps" } else { "FAIL plan steps: $($names -join ',')"; $fail++ }
$plan = Get-FPlan 'gamer' 'keep'
if (@($plan | ForEach-Object { $_.N }) -contains 'Отклик системы') { "ok   gamer plan" } else { "FAIL gamer plan"; $fail++ }

# ---- WeavOS options and step selection ----
$names = Get-FPlanNames 'optimal' 'keep' '7zip'
if ($names -contains 'Обои WeavOS' -and $names -contains 'Название WeavOS') { "ok   weavos steps on by default" } else { "FAIL weavos default: $($names -join ',')"; $fail++ }
$names = Get-FPlanNames 'optimal' 'keep' '7zip,nowall,noname'
if ($names -notcontains 'Обои WeavOS' -and $names -notcontains 'Название WeavOS') { "ok   weavos steps can be turned off" } else { "FAIL weavos off"; $fail++ }
if ((Test-FDeviceName 'GAMING-PC') -and -not (Test-FDeviceName '12345') -and -not (Test-FDeviceName 'имя') -and -not (Test-FDeviceName 'A234567890123456') -and -not (Test-FDeviceName '-pc')) { "ok   device name validation" } else { "FAIL device name validation"; $fail++ }

# ---- drift: a setting that Windows changed back is reported and repaired ----
$global:Dark = $false
Invoke-FSetting 'Тёмная тема' "(Get-Theme) -eq 'Тёмная'" 'Set-Theme $v' $true 'ui'
if (@(Get-FDrift | Where-Object { $_.Title -eq 'Тёмная тема' }).Count -eq 0) { "ok   no drift right after applying" } else { "FAIL drift too early"; $fail++ }
$global:Dark = $false   # "Windows update" switched it back
$d = @(Get-FDrift | Where-Object { $_.Title -eq 'Тёмная тема' })
if ($d.Count -eq 1 -and $d[0].Title -eq 'Тёмная тема') { "ok   drift detected" } else { "FAIL drift: $($d.Count)"; $fail++ }
$fa1 = @($d[0].Id)
Invoke-Expression '[void](Repair-FDrift $fa1)'
if ($global:Dark -and @(Get-FDrift | Where-Object { $_.Title -eq 'Тёмная тема' }).Count -eq 0) { "ok   drift repaired" } else { "FAIL drift repair"; $fail++ }

# ---- downloaded installers: hash pin is enforced ----
$tf = Join-Path ([IO.Path]::GetTempPath()) 'ww_inst.bin'; [IO.File]::WriteAllText($tf, 'hello')
$good = (Get-FileHash -LiteralPath $tf -Algorithm SHA256).Hash
if ((Test-FInstaller $tf $good) -and -not (Test-FInstaller $tf ('0' * 64))) { "ok   installer hash check" } else { "FAIL installer hash check"; $fail++ }
Remove-Item $tf -Force
"FAILS: $fail"
if ($fail -gt 0) { exit 1 }
