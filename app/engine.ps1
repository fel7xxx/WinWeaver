# WinWeaver — Windows 11 tweaker
# Copyright (C) 2026 fel7xxx
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify it under the terms of the
# GNU General Public License as published by the Free Software Foundation, either version 3 of the
# License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY.
# See the LICENSE file for details. "WinWeaver" and "WeavOS" names: see TRADEMARKS.md.

# ============================================================
#  WinWeaver 1.0.0  -  Windows 11 tweaker engine
#  Copyright (C) 2026 fel7xxx  -  https://github.com/fel7xxx/WinWeaver
#  Licensed under the GNU General Public License v3.0 (see LICENSE)
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference    = 'SilentlyContinue'
$Dir  = "$env:ProgramData\WinWeaver"
$Log  = "$Dir\tweaker.log"
# downloads go to a folder only administrators can write to (the C# side hardens $Dir before the engine starts)
$DlDir = "$Dir\dl"
if (-not (Test-Path $Dir)) { New-Item -ItemType Directory -Path $Dir -Force | Out-Null }
$global:NeedReboot = $false

# ---------------- helpers ----------------
function Write-FLog([string]$t) { Add-Content -Path $Log -Value ("{0}  {1}" -f (Get-Date -Format 's'), $t) }
function Set-FReg([string]$Path, [string]$Name, $Value, [string]$Type = 'DWord') {
  Save-FRegPrev $Path $Name
  if (-not (Test-Path $Path)) { New-Item -Path $Path -Force | Out-Null }
  New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType $Type -Force | Out-Null
}
function Get-FReg([string]$Path, [string]$Name) {
  $i = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
  if ($null -ne $i) { return $i.$Name } else { return $null }
}
function Remove-FReg([string]$Path, [string]$Name) { Save-FRegPrev $Path $Name; Remove-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue }
function Write-Host { [CmdletBinding()] param([Parameter(Position=0, ValueFromRemainingArguments=$true)]$Object, $ForegroundColor, $BackgroundColor, [switch]$NoNewline, $Separator) if ($Object) { Write-FLog ("    " + ($Object -join ' ')) } }
function Show-Ok([string]$t)   { Write-FLog "OK  $t";  if ($EngineUI) { $EngineUI.Msg('ok', $t) } }
function Show-Warn([string]$t) { Write-FLog "WARN $t"; if ($EngineUI) { $EngineUI.Msg('warn', $t) } }
function Show-Err([string]$t)  { Write-FLog "ERR  $t"; if ($EngineUI) { $EngineUI.Msg('err', $t) } }
function Wait-Enter {}
function Confirm-F([string]$q, [string]$title = 'WinWeaver', [string]$yes = 'Да', [string]$no = 'Нет') { if ($EngineUI) { return [bool]$EngineUI.Confirm($title, $q, $yes, $no) }; return $false }

if (-not ('WNative' -as [type])) {
  Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class WNative {
  [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
  public static extern bool SpiString(int action, int param, string value, int flags);
  [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
  public static extern bool SpiInts(int action, int param, int[] value, int flags);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
}
"@
}
function Send-FSettingChange([string]$area) {
  $r = [UIntPtr]::Zero
  [void][WNative]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, $area, 2, 3000, [ref]$r)
}
function Restart-FExplorer {
  $global:FExplorerTouched = $true
  if ($global:FDeferExplorer) { $global:FExplorerPending = $true; return }
  Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 4
  if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process -FilePath "$env:WINDIR\explorer.exe" }
}
function Get-FRebootInfo {
  $wu = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
  $cbs = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
  return @{ App = [bool]$global:NeedReboot; Sys = [bool]($wu -or $cbs) }
}
function Get-FOnOff($v) { if ($v) { 'ВКЛ' } else { 'ВЫКЛ' } }

# ---------------- change history ----------------
# Every change made by the program is one entry in $Dir\history.json, so it can be rolled back on its own.
#  * Invoke-FSetting  - a setting with a getter/setter pair: undo = the setter with the previous value
#  * Invoke-FTracked  - any other action: registry values touched through Set-FReg/Remove-FReg/Set-FSvcStart
#                       are captured before the change and written back on undo (plus an optional undo script)
#  * entries without undo data are kept as information ("can't be undone, use a restore point")
$HistFile = "$Dir\history.json"
$global:FCap = $null
$global:FCapSeen = @{}
$global:FGroup = ''
$global:FGroupTitle = ''
$global:FDeferExplorer = $false
$global:FExplorerPending = $false
$global:FExplorerTouched = $false
function Save-FRegPrev([string]$Path, [string]$Name) {
  if ($null -eq $global:FCap) { return }
  $k = ("$Path|$Name").ToLower()
  if ($global:FCapSeen.ContainsKey($k)) { return }
  $global:FCapSeen[$k] = 1
  $e = @{ P = $Path; N = $Name; E = $false; K = ''; V = $null }
  try {
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if (@($item.GetValueNames()) -contains $Name) {
      $kind = [string]$item.GetValueKind($Name)
      $val = $item.GetValue($Name, $null, 'DoNotExpandEnvironmentNames')
      if ($kind -eq 'Binary') { $val = [Convert]::ToBase64String([byte[]]$val) } elseif ($kind -eq 'MultiString') { $val = @($val) }
      $e.E = $true; $e.K = $kind; $e.V = $val
    }
  } catch {}
  [void]$global:FCap.Add($e)
}
function Start-FCapture { $global:FCap = New-Object System.Collections.ArrayList; $global:FCapSeen = @{}; $global:FExplorerTouched = $false }
function Stop-FCapture { $c = $global:FCap; $global:FCap = $null; if ($null -eq $c) { return ,@() }; return ,($c.ToArray()) }
function Get-FRegNow([string]$Path, [string]$Name) {
  try {
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if (@($item.GetValueNames()) -contains $Name) {
      $kind = [string]$item.GetValueKind($Name); $val = $item.GetValue($Name, $null, 'DoNotExpandEnvironmentNames')
      if ($kind -eq 'Binary') { $val = [Convert]::ToBase64String([byte[]]$val) } elseif ($kind -eq 'MultiString') { $val = (@($val) -join "`n") }
      return @{ E = $true; V = [string]$val }
    }
  } catch {}
  return @{ E = $false; V = '' }
}
# $true when at least one captured value differs from what is in the registry now
function Test-FRegChanged($cap) {
  foreach ($e in @($cap)) {
    if (-not $e) { continue }
    $now = Get-FRegNow $e.P $e.N
    $was = if ($e.K -eq 'MultiString') { (@($e.V) -join "`n") } else { [string]$e.V }
    if ([bool]$e.E -ne $now.E) { return $true }
    if ($now.E -and $was -ne $now.V) { return $true }
  }
  return $false
}
function Restore-FRegList($list) {
  foreach ($e in @($list)) {
    if (-not $e -or -not $e.P) { continue }
    if ([bool]$e.E) {
      $v = $e.V
      switch ([string]$e.K) {
        'Binary'      { $v = [Convert]::FromBase64String([string]$v) }
        'MultiString' { $v = [string[]]@($v) }
        'DWord'       { $v = [int]$v }
        'QWord'       { $v = [long]$v }
        default       { $v = [string]$v }
      }
      $t = if ($e.K) { [string]$e.K } else { 'String' }
      if ($e.N -eq 'Start' -and $e.P -like '*\CurrentControlSet\Services\*') { [void](Set-FSvcStart (Split-Path $e.P -Leaf) ([int]$v)) } else { Set-FReg $e.P $e.N $v $t }
    } else { Remove-FReg $e.P $e.N }
  }
}
function Get-FHistData {
  if ($null -ne $script:FHist) { return ,$script:FHist }
  $list = New-Object System.Collections.ArrayList
  if (Test-Path $HistFile) {
    try {
      $o = [IO.File]::ReadAllText($HistFile, [Text.Encoding]::UTF8) | ConvertFrom-Json
      foreach ($x in @($o.items)) { if ($x -and $x.Id) { [void]$list.Add($x) } }
    } catch { Write-FLog "HISTORY read error: $($_.Exception.Message)" }
  }
  $script:FHist = $list
  return ,$list
}
function Save-FHist {
  $list = Get-FHistData
  while ($list.Count -gt 400) { $list.RemoveAt(0) }
  try {
    $json = ConvertTo-Json -InputObject @{ v = 1; items = @($list.ToArray()) } -Depth 8 -Compress
    [IO.File]::WriteAllText($HistFile, $json, (New-Object System.Text.UTF8Encoding($false)))
  } catch { Write-FLog "HISTORY write error: $($_.Exception.Message)" }
}
function Add-FHist([string]$Title, [string]$Cat = 'other', [string]$Undo = '', $Reg = $null, [string]$Note = '', [bool]$Explorer = $false, [string]$Get = '', [string]$Set = '', [string]$Val = '') {
  $reg = @(@($Reg) | Where-Object { $_ })
  $e = [pscustomobject]@{
    Id = [guid]::NewGuid().ToString('N').Substring(0, 12); Time = (Get-Date -Format 's'); Title = $Title; Cat = $Cat
    Group = [string]$global:FGroup; GroupTitle = [string]$global:FGroupTitle
    Undo = $Undo; Reg = $reg; Ex = $Explorer; Can = [bool]($Undo -or $reg.Count -gt 0); Done = $false; DoneTime = ''; Note = $Note
    Get = $Get; Set = $Set; Val = $Val
  }
  [void](Get-FHistData).Add($e)
  Save-FHist
  Write-FLog "HIST + $Title"
}
function ConvertTo-FLiteral($x) {
  if ($x -is [bool]) { if ($x) { return '$true' } else { return '$false' } }
  if ($x -is [int] -or $x -is [long] -or $x -is [double]) { return [string]$x }
  return "'" + ([string]$x).Replace("'", "''") + "'"
}
# a setting: $Get returns the current value (bool or index), $Set applies $v
function Invoke-FSetting([string]$Title, [string]$Get, [string]$Set, $Value, [string]$Cat = 'other') {
  $prev = $null
  try { $prev = & ([scriptblock]::Create($Get)) } catch {}
  if ($prev -is [array]) { $prev = $prev[-1] }
  $v = $Value
  Start-FCapture
  try { & ([scriptblock]::Create($Set)) | Out-Null } catch { Write-FLog "SET FAIL $Title : $($_.Exception.Message)" }
  [void](Stop-FCapture)
  if ($null -ne $prev -and "$prev" -eq "$Value") { return }
  $undo = ''
  if ($null -ne $prev -and -not ($prev -is [int] -and $prev -lt 0)) { $undo = '$v = ' + (ConvertTo-FLiteral $prev) + '; ' + $Set }
  Add-FHist $Title $Cat $undo $null '' $false $Get $Set (ConvertTo-FLiteral $Value)
}
# any other change. -NoUndo: information only. -KeepReg: write the captured values back AND run $Undo
function Invoke-FTracked([string]$Title, [string]$Script, [string]$Undo = '', [string]$Cat = 'other', [switch]$NoUndo, [switch]$KeepReg, [string]$Note = '') {
  Start-FCapture
  try { & ([scriptblock]::Create($Script)) | Out-Null } catch { Write-FLog "TRACK FAIL $Title : $($_.Exception.Message)" }
  $reg = Stop-FCapture
  $ex = [bool]$global:FExplorerTouched
  if ($NoUndo) { Add-FHist $Title $Cat '' $null $Note $false; return }
  if ($Undo -and -not $KeepReg) { Add-FHist $Title $Cat $Undo $null $Note $ex; return }
  if (-not (Test-FRegChanged $reg)) { return }
  Add-FHist $Title $Cat $Undo $reg $Note $ex
}
function Start-FGroup([string]$title) { $global:FGroup = 'g' + [guid]::NewGuid().ToString('N').Substring(0, 10); $global:FGroupTitle = $title; $global:FDeferExplorer = $true; $global:FExplorerPending = $false }
function Stop-FGroup {
  $global:FGroup = ''; $global:FGroupTitle = ''; $global:FDeferExplorer = $false
  if ($global:FExplorerPending) { $global:FExplorerPending = $false; Restart-FExplorer }
}
function Undo-FHistItem($e) {
  if (-not $e -or [bool]$e.Done -or -not [bool]$e.Can) { return $false }
  try {
    if (@($e.Reg).Count -gt 0) { Restore-FRegList $e.Reg; Send-FSettingChange 'ImmersiveColorSet'; Send-FSettingChange 'Policy' }
    if ($e.Undo) { & ([scriptblock]::Create([string]$e.Undo)) | Out-Null }
    if ([bool]$e.Ex) { Restart-FExplorer }
  } catch { Write-FLog "UNDO FAIL $($e.Title): $($_.Exception.Message)"; return $false }
  $e.Done = $true; $e.DoneTime = (Get-Date -Format 's')
  Write-FLog "HIST undo $($e.Title)"
  return $true
}
function Undo-FHistory([string]$id) {
  $e = $null; foreach ($x in (Get-FHistData)) { if ($x.Id -eq $id) { $e = $x; break } }
  if (-not $e) { Show-Err 'Изменение не найдено в истории'; return $false }
  if (-not [bool]$e.Can) { Show-Warn 'Это изменение нельзя откатить отдельно — используй точку восстановления'; return $false }
  $global:FDeferExplorer = $true; $global:FExplorerPending = $false
  try { $ok = Undo-FHistItem $e } finally { $global:FDeferExplorer = $false }
  if ($global:FExplorerPending) { $global:FExplorerPending = $false; Restart-FExplorer }
  Save-FHist
  if ($ok) { Show-Ok ('Откачено: ' + $e.Title) } else { Show-Err ('Не удалось откатить: ' + $e.Title) }
  return $ok
}
function Undo-FHistGroup([string]$group) {
  $list = Get-FHistData
  $items = @(); for ($i = $list.Count - 1; $i -ge 0; $i--) { $x = $list[$i]; if ($x.Group -eq $group -and -not [bool]$x.Done -and [bool]$x.Can) { $items += $x } }
  if ($items.Count -eq 0) { Show-Warn 'В этой группе нечего откатывать'; return 0 }
  $global:FDeferExplorer = $true; $global:FExplorerPending = $false
  $n = 0; $k = 0
  try {
    foreach ($x in $items) { $k++; Show-Ok ('Откат [{0}/{1}]: {2}…' -f $k, $items.Count, $x.Title); if (Undo-FHistItem $x) { $n++ } }
  } finally { $global:FDeferExplorer = $false }
  if ($global:FExplorerPending) { $global:FExplorerPending = $false; Restart-FExplorer }
  Save-FHist
  Show-Ok ('Откачено изменений: {0} из {1}' -f $n, $items.Count)
  return $n
}
function Get-FHistory {
  $out = @()
  $list = Get-FHistData
  for ($i = $list.Count - 1; $i -ge 0; $i--) {
    $e = $list[$i]
    $out += @{ Id = [string]$e.Id; Time = [string]$e.Time; Title = [string]$e.Title; Cat = [string]$e.Cat; Group = [string]$e.Group; GroupTitle = [string]$e.GroupTitle; Can = [bool]$e.Can; Done = [bool]$e.Done; Note = [string]$e.Note }
  }
  return ,$out
}
# ---- settings that Windows changed back (usually after a feature update) ----
# every toggle applied through Invoke-FSetting remembers its get/set scripts and the value; the latest
# not-undone entry per setting is compared with the current state
function Get-FDriftItems {
  $seen = @{}; $out = @()
  $list = Get-FHistData
  for ($i = $list.Count - 1; $i -ge 0; $i--) {
    $e = $list[$i]
    if (-not $e.Get -or -not $e.Set) { continue }
    $key = [string]$e.Get
    if ($seen.ContainsKey($key)) { continue }
    $seen[$key] = $true
    if ([bool]$e.Done) { continue }
    $want = $null; try { $want = & ([scriptblock]::Create('return ' + [string]$e.Val)) } catch { continue }
    $now = $null; try { $now = & ([scriptblock]::Create([string]$e.Get)) } catch { continue }
    if ($now -is [array]) { $now = $now[-1] }
    if ($null -eq $now -or ($now -is [int] -and $now -lt 0)) { continue }
    if ("$now" -ne "$want") { $out += $e }
  }
  return ,$out
}
function Get-FDrift { return ,@((Get-FDriftItems) | ForEach-Object { @{ Id = [string]$_.Id; Title = [string]$_.Title; Cat = [string]$_.Cat } }) }
function Repair-FDrift([string[]]$ids) {
  $items = @((Get-FDriftItems) | Where-Object { $ids -contains [string]$_.Id })
  if ($items.Count -eq 0) { Show-Ok 'Все настройки на месте'; return 0 }
  Start-FGroup 'Возврат настроек после обновления Windows'
  $n = 0
  try {
    foreach ($e in $items) {
      $want = & ([scriptblock]::Create('return ' + [string]$e.Val))
      Invoke-FSetting ([string]$e.Title) ([string]$e.Get) ([string]$e.Set) $want ([string]$e.Cat); $n++
    }
  } finally { Stop-FGroup }
  Show-Ok ('Возвращено настроек: {0}' -f $n)
  return $n
}
# remembers the Windows build; returns $true once after it changed (the UI then checks for drift)
function Test-FBuildChanged {
  $cv = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
  $b = '{0}.{1}' -f (Get-FReg $cv 'CurrentBuild'), (Get-FReg $cv 'UBR')
  $old = [string](Get-FReg 'HKLM:\SOFTWARE\WinWeaver' 'LastBuild')
  if ($old -eq $b) { return $false }
  if (-not (Test-Path 'HKLM:\SOFTWARE\WinWeaver')) { New-Item -Path 'HKLM:\SOFTWARE\WinWeaver' -Force | Out-Null }
  New-ItemProperty -Path 'HKLM:\SOFTWARE\WinWeaver' -Name 'LastBuild' -Value $b -PropertyType String -Force | Out-Null
  return [bool]$old
}
function Get-FHistCount { $n = 0; foreach ($e in (Get-FHistData)) { if (-not [bool]$e.Done) { $n++ } }; return $n }
function Clear-FHistory { (Get-FHistData).Clear(); Save-FHist; Show-Ok 'История изменений очищена' }

# ---------------- constants ----------------
$HKP  = 'HKLM:\SOFTWARE\Policies\Microsoft'
$CU   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion'
$ADV  = "$CU\Explorer\Advanced"
$CDM  = "$CU\ContentDeliveryManager"
$CLS  = 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}'
$DefPol = "$HKP\Windows Defender"
$DefRtp = "$HKP\Windows Defender\Real-Time Protection"
$DefNtf = "$HKP\Windows Defender Security Center\Notifications"
$DefTray = "$HKP\Windows Defender Security Center\Systray"
$StartApp = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$DefSvc = [ordered]@{ WinDefend = 2; WdNisSvc = 3; WdNisDrv = 3; WdFilter = 0; WdBoot = 0; MDCoreSvc = 2; Sense = 3 }
$DefBk  = 'HKLM:\SOFTWARE\WinWeaver\DefenderBackup'
$RunOnce = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce'
$TelTasks = @(
  '\Microsoft\Windows\Application Experience\|Microsoft Compatibility Appraiser',
  '\Microsoft\Windows\Application Experience\|ProgramDataUpdater',
  '\Microsoft\Windows\Application Experience\|MareBackup',
  '\Microsoft\Windows\Application Experience\|PcaPatchDbTask',
  '\Microsoft\Windows\Customer Experience Improvement Program\|Consolidator',
  '\Microsoft\Windows\Customer Experience Improvement Program\|UsbCeip',
  '\Microsoft\Windows\DiskDiagnostic\|Microsoft-Windows-DiskDiagnosticDataCollector',
  '\Microsoft\Windows\Feedback\Siuf\|DmClient',
  '\Microsoft\Windows\Feedback\Siuf\|DmClientOnScenarioDownload',
  '\Microsoft\Windows\Windows Error Reporting\|QueueReporting'
)
$CdmValues = @('ContentDeliveryAllowed','FeatureManagementEnabled','OemPreInstalledAppsEnabled','PreInstalledAppsEnabled','PreInstalledAppsEverEnabled','SilentInstalledAppsEnabled','SoftLandingEnabled','SubscribedContentEnabled','SystemPaneSuggestionsEnabled','SubscribedContent-310093Enabled','SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SubscribedContent-338393Enabled','SubscribedContent-353694Enabled','SubscribedContent-353696Enabled')
$JunkApps = @('Clipchamp.Clipchamp','Microsoft.BingNews','Microsoft.BingSearch','Microsoft.BingWeather','Microsoft.GetHelp','Microsoft.MicrosoftOfficeHub','Microsoft.MicrosoftSolitaireCollection','Microsoft.OutlookForWindows','Microsoft.PowerAutomateDesktop','Microsoft.Todos','Microsoft.Windows.DevHome','Microsoft.WindowsFeedbackHub','MicrosoftCorporationII.QuickAssist','MSTeams','MicrosoftWindows.Client.WebExperience','Microsoft.YourPhone','MicrosoftWindows.CrossDevice','Microsoft.Copilot','Microsoft.Windows.Ai.Copilot.Provider','Microsoft.549981C3F5F10')
$GuidBalanced = '381b4222-f694-41f0-9685-ff5bb260df2e'
$GuidHigh     = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'
$GuidUltimate = 'e9a42b02-d5df-448d-aa00-03f14749eb61'
$GuidWW       = '57a1e7e0-0f7c-4e5a-9b1d-3e2a5c0de1a0'   # own "WinWeaver" power plan (created on demand)
$JunkInfo = [ordered]@{
  'Microsoft.Copilot'                      = @('Copilot', 'ИИ-помощник Microsoft в отдельном окне', 1)
  'Microsoft.BingSearch'                   = @('Поиск Bing', 'Веб-поиск Bing, встроенный в систему', 1)
  'Microsoft.BingNews'                     = @('Новости', 'Лента новостей Microsoft Start', 1)
  'Microsoft.BingWeather'                  = @('Погода', 'Прогноз погоды от MSN', 0)
  'MicrosoftWindows.Client.WebExperience'  = @('Виджеты', 'Доска виджетов с новостями и рекламой', 1)
  'Clipchamp.Clipchamp'                    = @('Clipchamp', 'Онлайн-видеоредактор с подпиской', 1)
  'MSTeams'                                = @('Microsoft Teams', 'Мессенджер для работы', 1)
  'Microsoft.OutlookForWindows'            = @('Outlook (новый)', 'Почта — веб-приложение Outlook', 0)
  'Microsoft.MicrosoftOfficeHub'           = @('Microsoft 365', 'Реклама подписки Office', 1)
  'Microsoft.MicrosoftSolitaireCollection' = @('Solitaire', 'Пасьянсы с рекламой', 1)
  'Microsoft.Todos'                        = @('To Do', 'Список задач Microsoft', 0)
  'Microsoft.PowerAutomateDesktop'         = @('Power Automate', 'Автоматизация действий для офисов', 1)
  'Microsoft.Windows.DevHome'              = @('Dev Home', 'Панель для разработчиков', 1)
  'Microsoft.GetHelp'                      = @('Техподдержка', 'Приложение «Получить помощь»', 1)
  'Microsoft.WindowsFeedbackHub'           = @('Центр отзывов', 'Отправка отзывов в Microsoft', 1)
  'MicrosoftCorporationII.QuickAssist'     = @('Быстрая помощь', 'Удалённое управление ПК — частый инструмент мошенников', 1)
  'Microsoft.YourPhone'                    = @('Связь с телефоном', 'Звонки, SMS и фото с Android/iPhone', 0)
  'MicrosoftWindows.CrossDevice'           = @('Подключённые устройства', 'Служба для «Связи с телефоном»', 0)
  'Microsoft.Windows.Ai.Copilot.Provider'  = @('Copilot Provider', 'Компонент Copilot в системе', 1)
  'Microsoft.549981C3F5F10'                = @('Cortana', 'Старый голосовой помощник', 1)
}
$GuidRx = '([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'

# ======================================================================
#  DEFENDER: safe-mode phase (started automatically by RunOnce)
# ======================================================================
function Get-FSvcStart([string]$n) { Get-FReg "HKLM:\SYSTEM\CurrentControlSet\Services\$n" 'Start' }
function Set-FSvcStart([string]$n, [int]$v) {
  $k = "HKLM:\SYSTEM\CurrentControlSet\Services\$n"
  if (-not (Test-Path $k)) { return $true }
  Save-FRegPrev $k 'Start'
  try { Set-ItemProperty -Path $k -Name Start -Value $v -Type DWord -ErrorAction Stop } catch {
    & reg.exe add "HKLM\SYSTEM\CurrentControlSet\Services\$n" /v Start /t REG_DWORD /d $v /f 2>&1 | Out-Null
  }
  return ((Get-FSvcStart $n) -eq $v)
}

function Invoke-FSafePhase([string]$phase) {
  & bcdedit.exe /deletevalue '{current}' safeboot 2>&1 | Out-Null   # next boot is always normal
  Remove-Item "$Dir\defender_safe_failed.flag", "$Dir\defender_returned.flag" -Force -ErrorAction SilentlyContinue
  if ($phase -eq 'def-off-safe') {
    if (-not (Test-Path $DefBk)) { New-Item -Path $DefBk -Force | Out-Null }
    foreach ($n in $DefSvc.Keys) {
      $cur = Get-FSvcStart $n
      if ($null -ne $cur -and $cur -ne 4 -and $null -eq (Get-FReg $DefBk $n)) { Set-FReg $DefBk $n $cur }
    }
    foreach ($n in $DefSvc.Keys) { if (Set-FSvcStart $n 4) { Write-FLog "SAFE OFF $n -> 4" } else { Write-FLog "SAFE OFF $n FAILED" } }
    if ((Get-FSvcStart 'WinDefend') -ne 4) { Set-Content -Path "$Dir\defender_safe_failed.flag" -Value (Get-Date) }
  } else {
    foreach ($n in $DefSvc.Keys) {
      $v = Get-FReg $DefBk $n
      if ($null -eq $v) { $v = $DefSvc[$n] }
      if (Set-FSvcStart $n $v) { Write-FLog "SAFE ON $n -> $v" } else { Write-FLog "SAFE ON $n FAILED" }
    }
    Remove-Item -Path $DefBk -Recurse -Force -ErrorAction SilentlyContinue
    Set-FReg $RunOnce 'WinWeaverDefenderDone' 'explorer.exe windowsdefender://threatsettings' 'String'
  }
  if (Test-Path "$Dir\defender_safe_failed.flag") { return 'failed' } else { return 'ok' }
}

# ======================================================================
#  FEATURES
# ======================================================================
# ---- Telemetry ----
function Get-Telemetry { $st = Get-FSvcStart 'DiagTrack'; if (($null -eq $st -or $st -eq 4) -and (Get-FReg "$HKP\Windows\DataCollection" 'AllowTelemetry') -eq 0) { 'ВЫКЛ' } else { 'ВКЛ' } }
function Set-Telemetry([bool]$off) {
  $pairs = @(
    @("$HKP\Windows\DataCollection",'AllowTelemetry',0), @("$HKP\Windows\DataCollection",'DoNotShowFeedbackNotifications',1),
    @("$HKP\Windows\DataCollection",'AllowDeviceNameInTelemetry',0), @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection','AllowTelemetry',0),
    @("$HKP\SQMClient\Windows",'CEIPEnable',0), @("$HKP\Windows\AppCompat",'AITEnable',0), @("$HKP\Windows\AppCompat",'DisableInventory',1),
    @("$HKP\Windows\AppCompat",'DisableUAR',1), @("$HKP\Windows\Windows Error Reporting",'Disabled',1),
    @("$HKP\Windows\System",'EnableActivityFeed',0), @("$HKP\Windows\System",'PublishUserActivities',0), @("$HKP\Windows\System",'UploadUserActivities',0),
    @("$HKP\Windows\AdvertisingInfo",'DisabledByGroupPolicy',1)
  )
  foreach ($p in $pairs) { if ($off) { Set-FReg $p[0] $p[1] $p[2] } else { Remove-FReg $p[0] $p[1] } }
  if ($off) {
    Set-FReg "$CU\AdvertisingInfo" 'Enabled' 0
    Set-FReg "$CU\Privacy" 'TailoredExperiencesWithDiagnosticDataEnabled' 0
    Set-FReg 'HKCU:\Software\Microsoft\Siuf\Rules' 'NumberOfSIUFInPeriod' 0
    foreach ($s in 'DiagTrack','dmwappushservice') { Stop-Service -Name $s -Force -ErrorAction SilentlyContinue; Set-Service -Name $s -StartupType Disabled -ErrorAction SilentlyContinue; [void](Set-FSvcStart $s 4) }
  } else {
    Set-FReg "$CU\AdvertisingInfo" 'Enabled' 1
    Set-FReg "$CU\Privacy" 'TailoredExperiencesWithDiagnosticDataEnabled' 1
    Remove-FReg 'HKCU:\Software\Microsoft\Siuf\Rules' 'NumberOfSIUFInPeriod'
    Set-Service -Name 'DiagTrack' -StartupType Automatic -ErrorAction SilentlyContinue; [void](Set-FSvcStart 'DiagTrack' 2); Start-Service -Name 'DiagTrack' -ErrorAction SilentlyContinue
    Set-Service -Name 'dmwappushservice' -StartupType Manual -ErrorAction SilentlyContinue; [void](Set-FSvcStart 'dmwappushservice' 3)
  }
  foreach ($t in $TelTasks) {
    $parts = $t.Split('|'); $task = Get-ScheduledTask -TaskPath $parts[0] -TaskName $parts[1] -ErrorAction SilentlyContinue
    if ($task) { if ($off) { $task | Disable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null } else { $task | Enable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null } }
  }
  if ($off) { Show-Ok 'Телеметрия выключена' } else { Show-Ok 'Телеметрия возвращена к стандартной' }
}

# ---- Copilot / Recall ----
function Get-AI { if ((Get-FReg "$HKP\Windows\WindowsCopilot" 'TurnOffWindowsCopilot') -eq 1) { 'ВЫКЛ' } else { 'ВКЛ' } }
function Set-AI([bool]$off) {
  $list = @(
    @("$HKP\Windows\WindowsCopilot",'TurnOffWindowsCopilot',1), @('HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot','TurnOffWindowsCopilot',1),
    @("$HKP\Windows\WindowsAI",'DisableAIDataAnalysis',1), @("$HKP\Windows\WindowsAI",'AllowRecallEnablement',0),
    @('HKCU:\Software\Policies\Microsoft\Windows\WindowsAI','DisableAIDataAnalysis',1)
  )
  foreach ($p in $list) { if ($off) { Set-FReg $p[0] $p[1] $p[2] } else { Remove-FReg $p[0] $p[1] } }
  if ($off) {
    Set-FReg $ADV 'ShowCopilotButton' 0
    foreach ($a in 'Microsoft.Copilot','Microsoft.Windows.Ai.Copilot.Provider') { Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue }
    $rc = Get-WindowsOptionalFeature -Online -FeatureName Recall -ErrorAction SilentlyContinue
    if ($rc -and $rc.State -eq 'Enabled') { Disable-WindowsOptionalFeature -Online -FeatureName Recall -NoRestart -ErrorAction SilentlyContinue | Out-Null; $global:NeedReboot = $true }
    Show-Ok 'Copilot и Recall выключены'
  } else { Show-Ok 'Политики Copilot и Recall сняты (сам Copilot можно поставить из Store)' }
}

# ---- Ads ----
function Get-Ads { if ((Get-FReg $CDM 'SilentInstalledAppsEnabled') -eq 0 -and (Get-FReg $ADV 'Start_IrisRecommendations') -eq 0) { 'ВЫКЛ' } else { 'ВКЛ' } }
function Set-Ads([bool]$off) {
  $v = if ($off) { 0 } else { 1 }
  foreach ($n in $CdmValues) { Set-FReg $CDM $n $v }
  Set-FReg $ADV 'Start_IrisRecommendations' $v
  Set-FReg $ADV 'ShowSyncProviderNotifications' $v
  Set-FReg "$CU\UserProfileEngagement" 'ScoobeSystemSettingEnabled' $v
  foreach ($n in 'DisableWindowsConsumerFeatures','DisableConsumerAccountStateContent','DisableCloudOptimizedContent') { if ($off) { Set-FReg "$HKP\Windows\CloudContent" $n 1 } else { Remove-FReg "$HKP\Windows\CloudContent" $n } }
  if ($off) { Set-FReg 'HKCU:\Software\Policies\Microsoft\Windows\CloudContent' 'DisableTailoredExperiencesWithDiagnosticData' 1 } else { Remove-FReg 'HKCU:\Software\Policies\Microsoft\Windows\CloudContent' 'DisableTailoredExperiencesWithDiagnosticData' }
  if ($off) { Show-Ok 'Реклама, советы и автоустановка приложений выключены' } else { Show-Ok 'Реклама и рекомендации возвращены' }
}

# ---- Bing ----
function Get-Bing { if ((Get-FReg 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions') -eq 1) { 'ВЫКЛ' } else { 'ВКЛ' } }
function Set-Bing([bool]$off) {
  if ($off) {
    Set-FReg 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions' 1
    Set-FReg "$CU\Search" 'BingSearchEnabled' 0
    Set-FReg "$HKP\Windows\Windows Search" 'DisableWebSearch' 1
    Set-FReg "$HKP\Windows\Windows Search" 'ConnectedSearchUseWeb' 0
    Show-Ok 'Bing и веб-результаты в поиске выключены'
  } else {
    Remove-FReg 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions'
    Remove-FReg "$CU\Search" 'BingSearchEnabled'
    Remove-FReg "$HKP\Windows\Windows Search" 'DisableWebSearch'
    Remove-FReg "$HKP\Windows\Windows Search" 'ConnectedSearchUseWeb'
    Show-Ok 'Bing в поиске включён'
  }
}

# ---- Interface ----
function Get-Theme { if ((Get-FReg "$CU\Themes\Personalize" 'AppsUseLightTheme') -eq 0) { 'Тёмная' } else { 'Светлая' } }
function Set-Theme([bool]$dark) {
  $v = if ($dark) { 0 } else { 1 }
  Set-FReg "$CU\Themes\Personalize" 'AppsUseLightTheme' $v
  Set-FReg "$CU\Themes\Personalize" 'SystemUsesLightTheme' $v
  Send-FSettingChange 'ImmersiveColorSet'
  if ($dark) { Show-Ok 'Тёмная тема' } else { Show-Ok 'Светлая тема' }
}
function Get-Transp { Get-FOnOff ((Get-FReg "$CU\Themes\Personalize" 'EnableTransparency') -ne 0) }
function Set-Transp([bool]$on) { Set-FReg "$CU\Themes\Personalize" 'EnableTransparency' $(if ($on) { 1 } else { 0 }); Send-FSettingChange 'ImmersiveColorSet'; Show-Ok ('Прозрачность: ' + (Get-FOnOff $on)) }
function Get-Menu { if (Test-Path "$CLS\InprocServer32") { 'Классическое (Win10)' } else { 'Windows 11' } }
function Set-Menu([bool]$classic) {
  if ($classic) { & reg.exe add 'HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' /ve /f 2>&1 | Out-Null; Show-Ok 'Классическое контекстное меню' }
  else { & reg.exe delete 'HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}' /f 2>&1 | Out-Null; Show-Ok 'Контекстное меню Windows 11' }
  Restart-FExplorer
}
function Get-Search { switch (Get-FReg "$CU\Search" 'SearchboxTaskbarMode') { 0 { 'Скрыт' } 1 { 'Значок' } 3 { 'Кнопка' } default { 'Поле поиска' } } }
function Set-Search([int]$m) { Set-FReg "$CU\Search" 'SearchboxTaskbarMode' $m; Set-FReg "$CU\Search" 'SearchboxTaskbarModeCache' $m; Restart-FExplorer; Show-Ok 'Поиск на панели задач изменён' }
function Get-TaskView { Get-FOnOff ((Get-FReg $ADV 'ShowTaskViewButton') -ne 0) }
function Set-TaskView([bool]$on) { Set-FReg $ADV 'ShowTaskViewButton' $(if ($on) { 1 } else { 0 }); Restart-FExplorer; Show-Ok ('Представление задач: ' + (Get-FOnOff $on)) }
function Get-EndTask { Get-FOnOff ((Get-FReg "$ADV\TaskbarDeveloperSettings" 'TaskbarEndTask') -eq 1) }
function Set-EndTask([bool]$on) { Set-FReg "$ADV\TaskbarDeveloperSettings" 'TaskbarEndTask' $(if ($on) { 1 } else { 0 }); Restart-FExplorer; Show-Ok ('Завершить задачу: ' + (Get-FOnOff $on)) }
function Get-Align { if ((Get-FReg $ADV 'TaskbarAl') -eq 0) { 'Слева' } else { 'По центру' } }
function Set-Align([bool]$left) { Set-FReg $ADV 'TaskbarAl' $(if ($left) { 0 } else { 1 }); Restart-FExplorer; Show-Ok 'Выравнивание панели задач изменено' }
function Get-Ext { if ((Get-FReg $ADV 'HideFileExt') -eq 0) { 'Показывать' } else { 'Скрывать' } }
function Set-Ext([bool]$show) { Set-FReg $ADV 'HideFileExt' $(if ($show) { 0 } else { 1 }); Restart-FExplorer; Show-Ok 'Расширения файлов изменены' }
function Get-Hidden { if ((Get-FReg $ADV 'Hidden') -eq 1) { 'Показывать' } else { 'Скрывать' } }
function Set-Hidden([bool]$show) { Set-FReg $ADV 'Hidden' $(if ($show) { 1 } else { 2 }); Restart-FExplorer; Show-Ok 'Скрытые файлы изменены' }

# ---- Performance / gaming ----
function Get-ActiveScheme { $o = (& powercfg.exe /getactivescheme) -join ' '; if ($o -match $GuidRx) { return $Matches[1].ToLower() } ; return '' }
function Test-SchemeExists([string]$g) { $l = (& powercfg.exe /list) -join ' '; return ($l.ToLower().Contains($g.ToLower())) }
function Get-Power {
  $a = Get-ActiveScheme; $u = Get-FReg 'HKLM:\SOFTWARE\WinWeaver' 'UltimateGuid'; $h = Get-FReg 'HKLM:\SOFTWARE\WinWeaver' 'HighGuid'
  if ($a -eq $GuidWW) { 'WinWeaver' } elseif ($a -eq $GuidBalanced) { 'Сбалансированная' } elseif ($a -eq $GuidHigh -or ($h -and $a -eq $h)) { 'Высокая производительность' } elseif ($a -eq $GuidUltimate -or ($u -and $a -eq $u)) { 'Максимальная производительность' } else { 'Другая' }
}
# WinWeaver plan: full CPU speed on demand without heating at idle, no core parking, no PCIe/USB power saving,
# the PC never sleeps on mains power; on battery it still saves energy
function Install-FPowerPlan {
  if (-not (Test-SchemeExists $GuidWW)) {
    $base = if (Test-SchemeExists $GuidHigh) { $GuidHigh } else { $GuidBalanced }
    [void](Invoke-FNative 'powercfg.exe' "-duplicatescheme $base $GuidWW")
    if (-not (Test-SchemeExists $GuidWW)) { return $false }
  }
  [void](Invoke-FNative 'powercfg.exe' "-changename $GuidWW WinWeaver `"Схема питания WinWeaver: быстрый отклик без лишнего нагрева`"")
  $ac = @(
    'SUB_PROCESSOR PROCTHROTTLEMIN 10', 'SUB_PROCESSOR PROCTHROTTLEMAX 100', 'SUB_PROCESSOR PERFBOOSTMODE 2', 'SUB_PROCESSOR PERFEPP 0', 'SUB_PROCESSOR CPMINCORES 100',
    'SUB_PCIEXPRESS ASPM 0', '2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0',
    '19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0',
    'SUB_DISK DISKIDLE 0', 'SUB_SLEEP STANDBYIDLE 0', 'SUB_SLEEP HIBERNATEIDLE 0', 'SUB_VIDEO VIDEOIDLE 900')
  $dc = @(
    'SUB_PROCESSOR PROCTHROTTLEMIN 5', 'SUB_PROCESSOR PROCTHROTTLEMAX 100', 'SUB_PROCESSOR PERFBOOSTMODE 3', 'SUB_PROCESSOR PERFEPP 50',
    'SUB_SLEEP STANDBYIDLE 900', 'SUB_VIDEO VIDEOIDLE 300')
  foreach ($x in $ac) { [void](Invoke-FNative 'powercfg.exe' "/setacvalueindex $GuidWW $x") }
  foreach ($x in $dc) { [void](Invoke-FNative 'powercfg.exe' "/setdcvalueindex $GuidWW $x") }
  return $true
}
function Set-Power([string]$which) {
  $target = ''
  if ($which -eq 'winweaver') { if (Install-FPowerPlan) { $target = $GuidWW } }
  elseif ($which -eq 'balanced') { $target = $GuidBalanced }
  else {
    $base = if ($which -eq 'high') { $GuidHigh } else { $GuidUltimate }
    $store = if ($which -eq 'high') { 'HighGuid' } else { 'UltimateGuid' }
    if (Test-SchemeExists $base) { $target = $base }
    else {
      $saved = Get-FReg 'HKLM:\SOFTWARE\WinWeaver' $store
      if ($saved -and (Test-SchemeExists $saved)) { $target = $saved }
      else {
        $o = (& powercfg.exe -duplicatescheme $base) -join ' '
        if ($o -match $GuidRx) { $target = $Matches[1].ToLower(); Set-FReg 'HKLM:\SOFTWARE\WinWeaver' $store $target 'String' }
      }
    }
  }
  if (-not $target) { Show-Err 'Эта схема недоступна на этом ПК (часто так на ноутбуках с Modern Standby)'; return }
  & powercfg.exe /setactive $target 2>&1 | Out-Null
  if ((Get-ActiveScheme) -eq $target) { Show-Ok ('Схема питания: ' + (Get-Power)) } else { Show-Err 'Windows не дала включить эту схему (Modern Standby). Оставлена текущая.' }
}
function Get-GameMode { Get-FOnOff ((Get-FReg 'HKCU:\Software\Microsoft\GameBar' 'AutoGameModeEnabled') -ne 0) }
function Set-GameMode([bool]$on) { $v = if ($on) { 1 } else { 0 }; Set-FReg 'HKCU:\Software\Microsoft\GameBar' 'AutoGameModeEnabled' $v; Set-FReg 'HKCU:\Software\Microsoft\GameBar' 'AllowAutoGameMode' $v; Show-Ok ('Игровой режим: ' + (Get-FOnOff $on)) }
function Get-Dvr { Get-FOnOff ((Get-FReg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled') -ne 0) }
function Set-Dvr([bool]$on) { $v = if ($on) { 1 } else { 0 }; Set-FReg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled' $v; Set-FReg "$CU\GameDVR" 'AppCaptureEnabled' $v; Show-Ok ('Фоновая запись игр: ' + (Get-FOnOff $on)) }
function Get-MouseAcc { Get-FOnOff ((Get-FReg 'HKCU:\Control Panel\Mouse' 'MouseSpeed') -ne '0') }
function Set-MouseAcc([bool]$on) {
  $m = 'HKCU:\Control Panel\Mouse'
  if ($on) { Set-FReg $m 'MouseSpeed' '1' 'String'; Set-FReg $m 'MouseThreshold1' '6' 'String'; Set-FReg $m 'MouseThreshold2' '10' 'String'; [void][WNative]::SpiInts(4, 0, [int[]](6, 10, 1), 3) }
  else     { Set-FReg $m 'MouseSpeed' '0' 'String'; Set-FReg $m 'MouseThreshold1' '0' 'String'; Set-FReg $m 'MouseThreshold2' '0' 'String'; [void][WNative]::SpiInts(4, 0, [int[]](0, 0, 0), 3) }
  Show-Ok ('Ускорение мыши: ' + (Get-FOnOff $on))
}
function Get-Hags { switch (Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode') { 2 { 'ВКЛ' } 1 { 'ВЫКЛ' } default { 'По умолчанию' } } }
function Set-Hags([bool]$on) { Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' $(if ($on) { 2 } else { 1 }); $global:NeedReboot = $true; Show-Ok ('GPU scheduling: ' + (Get-FOnOff $on) + ' (после перезагрузки, если видеокарта поддерживает)') }
function Get-Hvci {
  $dg = Get-CimInstance -ClassName Win32_DeviceGuard -Namespace 'root\Microsoft\Windows\DeviceGuard' -ErrorAction SilentlyContinue
  $run = ($dg -and ($dg.SecurityServicesRunning -contains 2))
  $cfg = Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' 'Enabled'
  $s = Get-FOnOff $run
  if ($null -ne $cfg -and ([bool]$cfg) -ne $run) { $s += ' (после перезагрузки: ' + (Get-FOnOff ([bool]$cfg)) + ')' }
  return $s
}
function Get-HvciWanted { $cfg = Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' 'Enabled'; if ($null -ne $cfg) { return ([int]$cfg -eq 1) }; return ((Get-Hvci) -like 'ВКЛ*') }
function Set-Hvci([bool]$on) { Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' 'Enabled' $(if ($on) { 1 } else { 0 }); $global:NeedReboot = $true; Show-Ok ('Целостность памяти: ' + (Get-FOnOff $on) + ' после перезагрузки') }
function Get-FastBoot { Get-FOnOff ((Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled') -eq 1) }
function Set-FastBoot([bool]$on) {
  if ($on) { & powercfg.exe /hibernate on 2>&1 | Out-Null; Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 1 }
  else     { Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 0; & powercfg.exe /hibernate off 2>&1 | Out-Null }
  Show-Ok ('Быстрый запуск и гибернация: ' + (Get-FOnOff $on))
}

# ---- Defender ----
function Get-Tamper { try { return [bool](Get-MpComputerStatus -ErrorAction Stop).IsTamperProtected } catch { return $false } }
function Get-Rtp { try { return [bool](Get-MpComputerStatus -ErrorAction Stop).RealTimeProtectionEnabled } catch { return $false } }
function Get-Defender {
  $mode = Get-Content "$Dir\defender.mode" -ErrorAction SilentlyContinue
  if ((Get-FSvcStart 'WinDefend') -eq 4) { $s = 'ВЫКЛ полностью' }
  elseif (-not (Get-Rtp)) { $s = 'ВЫКЛ (защита в реальном времени)' }
  else { $s = 'ВКЛ' }
  if ($null -ne (Get-FReg $RunOnce '*WinWeaverDefender')) { $s += '  [ждёт перезагрузки через безопасный режим]' }
  elseif ($mode -eq 'full' -and (Test-Path "$Dir\defender_returned.flag") -and (Get-FSvcStart 'WinDefend') -ne 4) { $s += '  [службы вернулись - повтори выключение]' }
  if (Test-Path "$Dir\defender_safe_failed.flag") { $s += '  [безопасный режим не смог отключить службы]' }
  return $s
}
# data for the Defender card: Mode 0 = on, 1 = no real-time, 2 = fully off
function Get-FDefInfo {
  $st = $null; $pref = $null
  try { $st = Get-MpComputerStatus -ErrorAction Stop } catch {}
  try { $pref = Get-MpPreference -ErrorAction Stop } catch {}
  $full = (Get-FSvcStart 'WinDefend') -eq 4
  $mode = if ($full -or -not $st) { 2 } elseif (-not $st.RealTimeProtectionEnabled) { 1 } else { 0 }
  $note = ''
  if ($null -ne (Get-FReg $RunOnce '*WinWeaverDefender')) { $note = 'Ждёт перезагрузки через безопасный режим — WinWeaver закончит сам' }
  elseif ((Get-Content "$Dir\defender.mode" -ErrorAction SilentlyContinue) -eq 'full' -and (Test-Path "$Dir\defender_returned.flag") -and -not $full) { $note = 'Windows вернула службы Defender — выбери «Выключен полностью» ещё раз' }
  if (Test-Path "$Dir\defender_safe_failed.flag") { $note = 'Безопасный режим не смог отключить службы — попробуй ещё раз' }
  $other = ''
  try { $other = (@(Get-CimInstance -Namespace 'root/SecurityCenter2' -ClassName AntiVirusProduct -ErrorAction Stop | Where-Object { $_.displayName -notmatch 'Defender' } | ForEach-Object { $_.displayName }) -join ', ') } catch {}
  $age = { param($v) if ($null -eq $v -or [uint32]$v -ge 65535) { -1 } else { [int]$v } }
  return @{
    Mode = $mode; Note = $note; Other = $other
    Rtp = [bool]($st -and $st.RealTimeProtectionEnabled)
    Tamper = [bool]($st -and $st.IsTamperProtected)
    Cloud = [bool]($pref -and $pref.MAPSReporting -gt 0)
    Pua = [bool]($pref -and $pref.PUAProtection -eq 1)
    SigAge = $(if ($st) { & $age $st.AntivirusSignatureAge } else { -1 })
    ScanAge = $(if ($st) { & $age $st.QuickScanAge } else { -1 })
  }
}
function Get-FDefSigVer { try { return [string](Get-MpComputerStatus -ErrorAction Stop).AntivirusSignatureVersion } catch { return '' } }
function Update-FDefSig {
  $before = Get-FDefSigVer
  $ok = $false
  try { Update-MpSignature -ErrorAction Stop; $ok = $true } catch {
    $mp = "$env:ProgramFiles\Windows Defender\MpCmdRun.exe"
    if (Test-Path $mp) { $ok = ((Invoke-FNative $mp '-SignatureUpdate') -eq 0) }
  }
  $after = Get-FDefSigVer
  if ($ok -and $after -and $after -ne $before) { Show-Ok ('Базы Defender обновлены: {0}' -f $after) }
  elseif ($ok) { Show-Ok 'Базы Defender уже свежие' }
  else { Show-Err 'Не удалось обновить базы Defender (нет интернета или Defender выключен)' }
}
function Start-FDefScan {
  Show-Ok 'Быстрая проверка Defender… обычно 2–10 минут'
  try { Start-MpScan -ScanType QuickScan -ErrorAction Stop; $t = @(Get-MpThreatDetection -ErrorAction SilentlyContinue | Where-Object { $_.InitialDetectionTime -gt (Get-Date).AddMinutes(-30) }).Count
    if ($t -gt 0) { Show-Warn ('Найдено угроз: {0} — открой «Защита от вирусов и угроз»' -f $t) } else { Show-Ok 'Проверка завершена: угроз не найдено' } }
  catch { Show-Err 'Проверка не запустилась: Defender выключен или занят' }
}
function Set-DefTray([bool]$show) {
  if ($show) {
    Remove-FReg $DefTray 'HideSystray'
    if (Test-Path $StartApp) { Set-FReg $StartApp 'SecurityHealth' ([byte[]](2,0,0,0,0,0,0,0,0,0,0,0)) 'Binary' }
    if (-not (Get-Process -Name SecurityHealthSystray -ErrorAction SilentlyContinue)) { Start-Process -FilePath "$env:WINDIR\explorer.exe" -ArgumentList "$env:WINDIR\System32\SecurityHealthSystray.exe" }
  } else {
    Set-FReg $DefTray 'HideSystray' 1
    Set-FReg $StartApp 'SecurityHealth' ([byte[]](3,0,0,0,0,0,0,0,0,0,0,0)) 'Binary'
    Get-Process -Name SecurityHealthSystray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  }
}
function Install-DefGuard([bool]$quiet = $false) {
  $g = @'
$d = "$env:ProgramData\WinWeaver"
$m = Get-Content "$d\defender.mode" -ErrorAction SilentlyContinue
if ($m -eq 'off' -or $m -eq 'full') {
  $p = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection'
  if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
  foreach ($v in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableOnAccessProtection','DisableScanOnRealtimeEnable','DisableIOAVProtection') { New-ItemProperty -Path $p -Name $v -Value 1 -PropertyType DWord -Force | Out-Null }
  try { Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction Stop } catch {}
  $t = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Systray'
  if (-not (Test-Path $t)) { New-Item -Path $t -Force | Out-Null }
  New-ItemProperty -Path $t -Name HideSystray -Value 1 -PropertyType DWord -Force | Out-Null
  if ($m -eq 'full') {
    $s = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\WinDefend' -ErrorAction SilentlyContinue).Start
    if ($s -ne 4) { Set-Content -Path "$d\defender_returned.flag" -Value (Get-Date) }
  }
}
'@
  Set-Content -Path "$Dir\defender_guard.ps1" -Value $g -Encoding ASCII
  $act = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$Dir\defender_guard.ps1`""
  $trg = New-ScheduledTaskTrigger -AtStartup
  $prn = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
  Register-ScheduledTask -TaskName 'Defender Guard' -TaskPath '\WinWeaver\' -Action $act -Trigger $trg -Principal $prn -Force -ErrorAction SilentlyContinue | Out-Null
  if ($quiet) { return }
  if (Get-ScheduledTask -TaskPath '\WinWeaver\' -TaskName 'Defender Guard' -ErrorAction SilentlyContinue) { Show-Ok 'Страж установлен: держит Defender выключенным после перезагрузок и обновлений' } else { Show-Warn 'Не удалось создать задачу-стража' }
}
function Invoke-SafeModeCycle([string]$arg) {
  $exe = $WwExe
  if (-not $exe -or -not (Test-Path $exe)) { Show-Err "Не найден WinWeaver.exe ($exe)"; return }
  Set-FReg $RunOnce '*WinWeaverDefender' ('"' + $exe + '" ' + $arg) 'String'
  & bcdedit.exe /set '{current}' safeboot minimal 2>&1 | Out-Null
  if ($LASTEXITCODE -ne 0) { Remove-FReg $RunOnce '*WinWeaverDefender'; Show-Err 'bcdedit не сработал. Ничего не изменено.'; return }
  $msg = "ПК перезагрузится в безопасный режим — чёрный экран и низкое разрешение это нормально.`n`nВойди в свою учётную запись (если PIN не принимается — введи пароль). WinWeaver сам закончит работу и вернёт обычный режим."
  if (Confirm-F $msg 'Перезагрузка в безопасный режим' 'Перезагрузить сейчас' 'Позже') { & shutdown.exe /r /t 3 } else { Show-Warn 'Сработает при следующей перезагрузке' }
}
function Set-DefenderOff([bool]$full) {
  if (Get-Tamper) {
    Show-Warn 'Сначала выключи «Защиту от подделки» (Tamper Protection).'
    Show-Warn 'Сейчас откроются настройки: пролистай вниз, выключи её и выбери этот пункт ещё раз.'
    Start-Process 'windowsdefender://threatsettings'; return
  }
  foreach ($v in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableOnAccessProtection','DisableScanOnRealtimeEnable','DisableIOAVProtection') { Set-FReg $DefRtp $v 1 }
  Set-FReg "$DefPol\Spynet" 'SpynetReporting' 0
  Set-FReg "$DefPol\Spynet" 'SubmitSamplesConsent' 2
  Set-FReg $DefNtf 'DisableNotifications' 1
  Set-DefTray $false
  try { Set-MpPreference -DisableRealtimeMonitoring $true -DisableBehaviorMonitoring $true -DisableIOAVProtection $true -DisableScriptScanning $true -MAPSReporting 0 -SubmitSamplesConsent 2 -ErrorAction Stop } catch {}
  Get-ScheduledTask -TaskPath '\Microsoft\Windows\Windows Defender\' -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null
  Set-Content -Path "$Dir\defender.mode" -Value $(if ($full) { 'full' } else { 'off' })
  Remove-Item "$Dir\defender_returned.flag", "$Dir\defender_safe_failed.flag" -Force -ErrorAction SilentlyContinue
  Install-DefGuard
  Start-Sleep -Seconds 2
  if (-not (Get-Rtp)) { Show-Ok 'Защита в реальном времени выключена' } else { Show-Warn 'Windows пока держит защиту включённой - проверь после перезагрузки' }
  if ($full) { Invoke-SafeModeCycle 'def-off-safe' }
}
function Set-DefenderOn {
  foreach ($v in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableOnAccessProtection','DisableScanOnRealtimeEnable','DisableIOAVProtection') { Remove-FReg $DefRtp $v }
  Remove-FReg $DefPol 'DisableAntiSpyware'
  Remove-FReg "$DefPol\Spynet" 'SpynetReporting'
  Remove-FReg $DefNtf 'DisableNotifications'
  Set-DefTray $true
  Remove-Item "$Dir\defender.mode", "$Dir\defender_returned.flag", "$Dir\defender_safe_failed.flag" -Force -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskPath '\WinWeaver\' -TaskName 'Defender Guard' -Confirm:$false -ErrorAction SilentlyContinue
  Get-ScheduledTask -TaskPath '\Microsoft\Windows\Windows Defender\' -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null
  if ((Get-FSvcStart 'WinDefend') -eq 4) { Show-Ok 'Политики сняты. Нужна перезагрузка через безопасный режим, чтобы вернуть службы.'; Invoke-SafeModeCycle 'def-on-safe'; return }
  try { Set-MpPreference -DisableRealtimeMonitoring $false -DisableBehaviorMonitoring $false -DisableIOAVProtection $false -DisableScriptScanning $false -MAPSReporting 2 -ErrorAction Stop } catch {}
  Start-Service -Name WinDefend -ErrorAction SilentlyContinue
  Show-Ok 'Defender включён. Включи обратно «Защиту от подделки» в открывшемся окне.'
  Start-Process 'windowsdefender://threatsettings'
}

# ---- BitLocker ----
function Get-BL {
  $v = Get-BitLockerVolume -ErrorAction SilentlyContinue
  if (-not $v) { return 'нет данных' }
  $enc = @($v | Where-Object { $_.VolumeStatus -ne 'FullyDecrypted' } | ForEach-Object { "$($_.MountPoint) $([int]$_.EncryptionPercentage)%" })
  $auto = if ((Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption') -eq 1) { 'автошифрование запрещено' } else { 'автошифрование разрешено' }
  if ($enc.Count -eq 0) { return "диски не зашифрованы, $auto" } else { return ('зашифрованы: ' + ($enc -join ', ') + ", $auto") }
}
function Get-FBLInfo {
  $auto = (Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption') -eq 1
  $vols = @()
  $v = $null; try { $v = Get-BitLockerVolume -ErrorAction Stop } catch {}
  if ($v) {
    foreach ($x in $v) {
      $size = 0.0; try { $size = [double]$x.CapacityGB * 1073741824 } catch {}
      $vols += @{ Drive = [string]$x.MountPoint; Status = [string]$x.VolumeStatus; Pct = [double]$x.EncryptionPercentage; Prot = [string]$x.ProtectionStatus; Method = [string]$x.EncryptionMethod; Type = [string]$x.VolumeType; Size = $size }
    }
  } else {
    foreach ($d in @(Get-Volume -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter -and $_.DriveType -eq 'Fixed' })) {
      $l = "$($d.DriveLetter):"; $st = 'FullyDecrypted'; $pct = 0.0
      $o = (& manage-bde.exe -status $l 2>$null) -join "`n"
      if ($o -match '(?:Percentage Encrypted|Зашифровано \(%\)|Процент шифрования)\s*:\s*([\d.,]+)') { $pct = [double]($Matches[1] -replace ',', '.') }
      if ($o -match 'Decryption in Progress|Выполняется расшифровка') { $st = 'DecryptionInProgress' } elseif ($o -match 'Encryption in Progress|Выполняется шифрование') { $st = 'EncryptionInProgress' } elseif ($pct -ge 100) { $st = 'FullyEncrypted' }
      $vols += @{ Drive = $l; Status = $st; Pct = $pct; Prot = ''; Method = ''; Type = ''; Size = [double]$d.Size }
    }
  }
  return @{ Auto = [bool]$auto; Vols = $vols }
}
function Set-BLDecrypt {
  Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption' 1
  $v = Get-BitLockerVolume -ErrorAction SilentlyContinue
  if ($v) {
    foreach ($x in $v) { if ($x.VolumeStatus -ne 'FullyDecrypted' -and $x.VolumeStatus -ne 'DecryptionInProgress') { Disable-BitLocker -MountPoint $x.MountPoint -ErrorAction SilentlyContinue | Out-Null; Show-Ok "Расшифровка запущена: $($x.MountPoint)" } }
  } else {
    foreach ($d in (Get-Volume -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter -and $_.DriveType -eq 'Fixed' })) { & manage-bde.exe -off "$($d.DriveLetter):" 2>&1 | Out-Null }
  }
  Show-Ok 'Автошифрование запрещено. Расшифровка идёт в фоне, ПК можно использовать.'
}

# ---- Windows Update ----
function Get-WU {
  $p = "$HKP\Windows\WindowsUpdate"
  $s = if ((Get-FReg $p 'TargetReleaseVersion') -eq 1) { 'остаётся на ' + (Get-FReg $p 'TargetReleaseVersionInfo') } else { 'обновляется до новых версий' }
  return $s
}
function Set-WUStay([bool]$stay) {
  $p = "$HKP\Windows\WindowsUpdate"
  if ($stay) {
    $ver = Get-FReg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' 'DisplayVersion'
    if (-not $ver) { $ver = Get-FReg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' 'ReleaseId' }
    if (-not $ver) { Show-Err 'Не удалось определить версию Windows'; return }
    Set-FReg $p 'TargetReleaseVersion' 1; Set-FReg $p 'ProductVersion' 'Windows 11' 'String'; Set-FReg $p 'TargetReleaseVersionInfo' $ver 'String'
    Show-Ok "Windows останется на $ver и будет получать обновления безопасности"
  } else { Remove-FReg $p 'TargetReleaseVersion'; Remove-FReg $p 'ProductVersion'; Remove-FReg $p 'TargetReleaseVersionInfo'; Show-Ok 'Обновление до новых версий разрешено' }
}

# ---- Edge / OneDrive / junk ----
function Remove-Edge {
  # 1) the official uninstaller (allowed everywhere once AllowUninstall is set; WebView2 is a separate product and stays)
  Get-Process -Name msedge, MicrosoftEdgeUpdate -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 1
  Set-FReg 'HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev' 'AllowUninstall' 1
  $setups = @()
  foreach ($root in "${env:ProgramFiles(x86)}\Microsoft\Edge\Application", "$env:ProgramFiles\Microsoft\Edge\Application") {
    if (Test-Path $root) { $setups += @(Get-ChildItem -Path $root -Filter 'setup.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq 'Installer' } | ForEach-Object { $_.FullName }) }
  }
  foreach ($st in $setups) {
    $rc = Invoke-FNative $st '--uninstall --system-level --verbose-logging --force-uninstall --delete-profile'
    Write-FLog "EDGE setup.exe rc=$rc ($st)"
    if ((Get-Edge) -eq 'удалён') { break }
  }
  # 2) the Store stub of Edge (it re-creates shortcuts on new user profiles)
  Get-AppxPackage -AllUsers -Name 'Microsoft.MicrosoftEdge.Stable' -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq 'Microsoft.MicrosoftEdge.Stable' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
  # 3) last resort: files that the uninstaller left behind (only the browser itself, never EdgeWebView)
  if ((Get-Edge) -ne 'удалён') {
    Write-FLog 'EDGE uninstaller did not finish, removing the remaining browser files'
    foreach ($p in "${env:ProgramFiles(x86)}\Microsoft\Edge", "$env:ProgramFiles\Microsoft\Edge") { if (Test-Path $p) { Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue } }
  }
  $desk = [Environment]::GetFolderPath('Desktop')
  foreach ($l in "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk", "$env:PUBLIC\Desktop\Microsoft Edge.lnk", "$desk\Microsoft Edge.lnk",
                 "$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk", "$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk") { Remove-Item -Path $l -Force -ErrorAction SilentlyContinue }
  if ((Get-Edge) -eq 'удалён') {
    & reg.exe delete 'HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge' /f 2>&1 | Out-Null
    & reg.exe delete 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe' /f 2>&1 | Out-Null
  }
  # 4) do not let Windows Update / EdgeUpdate bring the browser back; WebView2 keeps updating
  Set-FReg 'HKLM:\SOFTWARE\Microsoft\EdgeUpdate' 'DoNotUpdateToEdgeWithChromium' 1
  Set-FReg "$HKP\EdgeUpdate" 'InstallDefault' 0
  Set-FReg "$HKP\EdgeUpdate" 'Install{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}' 0
  Set-FReg "$HKP\EdgeUpdate" 'Install{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' 1
  Set-FReg "$HKP\EdgeUpdate" 'Update{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' 1
  Restart-FExplorer
  if ((Get-Edge) -eq 'удалён') { Show-Ok 'Edge удалён (WebView2 оставлен - он нужен приложениям)' } else { Show-Warn 'Часть файлов Edge занята. Перезагрузи ПК и повтори.' }
}
function Get-Edge {
  foreach ($e in "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") { if (Test-Path $e) { return 'установлен' } }
  return 'удалён'
}
function Get-OneDrive { if ((Test-Path "$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe") -or (Test-Path "$env:ProgramFiles\Microsoft OneDrive\OneDrive.exe")) { 'установлен' } else { 'удалён' } }
function Remove-OneDrive {
  Get-Process -Name OneDrive -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  $setups = @("$env:WINDIR\System32\OneDriveSetup.exe", "$env:WINDIR\SysWOW64\OneDriveSetup.exe") + @(Get-ChildItem "$env:LOCALAPPDATA\Microsoft\OneDrive\*\OneDriveSetup.exe" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }) + @(Get-ChildItem "$env:ProgramFiles\Microsoft OneDrive\*\OneDriveSetup.exe" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
  foreach ($s in $setups) { if ($s -and (Test-Path $s)) { Start-Process -FilePath $s -ArgumentList '/uninstall', '/allusers' -Wait -ErrorAction SilentlyContinue; Start-Process -FilePath $s -ArgumentList '/uninstall' -Wait -ErrorAction SilentlyContinue; break } }
  Set-FReg "$HKP\Windows\OneDrive" 'DisableFileSyncNGSC' 1
  Remove-FReg "$CU\Run" 'OneDrive'; Remove-FReg "$CU\Run" 'OneDriveSetup'
  Set-FReg 'HKCU:\Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}' 'System.IsPinnedToNameSpaceTree' 0
  foreach ($f in "$env:WINDIR\System32\OneDriveSetup.exe", "$env:WINDIR\SysWOW64\OneDriveSetup.exe") {
    if (Test-Path $f) { & takeown.exe /f $f 2>&1 | Out-Null; & icacls.exe $f /grant '*S-1-5-32-544:F' 2>&1 | Out-Null; Remove-Item -Path $f -Force -ErrorAction SilentlyContinue }
  }
  if ((Get-OneDrive) -eq 'удалён') { Show-Ok 'OneDrive удалён и заблокирован' } else { Show-Warn 'OneDrive ещё на месте - перезагрузи ПК и повтори' }
}
function Get-FApps {
  $all = @(Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
  $out = @()
  foreach ($k in $JunkInfo.Keys) {
    $i = $JunkInfo[$k]
    $out += @{ Id = $k; Name = $i[0]; Desc = $i[1]; Rec = [int]$i[2]; Installed = ($all -contains $k) }
  }
  return ,$out
}
function Remove-FApp([string]$id) {
  if (-not $JunkInfo.Contains($id)) { Show-Err "Неизвестное приложение: $id"; return }
  Get-AppxPackage -AllUsers -Name $id -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq $id } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
  if (Get-AppxPackage -AllUsers -Name $id -ErrorAction SilentlyContinue) { Show-Warn ($JunkInfo[$id][0] + ': Windows не дала удалить приложение') } else { Show-Ok ($JunkInfo[$id][0] + ' удалено') }
}
function Remove-Junk {
  $n = 0
  foreach ($j in $JunkApps) {
    $p = Get-AppxPackage -AllUsers -Name $j -ErrorAction SilentlyContinue
    if ($p) { $p | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; $n++ ; $global:FJunkRemoved++; Write-FLog "    junk removed: $j" }
    Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq $j } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
  }
  Show-Ok "Мусорные приложения проверены (удалено: $n)"
}
function Invoke-Repair {
  Write-FLog 'REPAIR after Windows update'
  Remove-Junk; Set-Telemetry $true; Set-AI $true; Set-Ads $true; Set-Bing $true
  if ((Get-Edge) -ne 'удалён') { Remove-Edge }
  if ((Get-OneDrive) -ne 'удалён') { Remove-OneDrive }
  Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption' 1
  Restart-FExplorer
  Show-Ok 'Готово. Defender этот пункт не трогает - для него отдельный пункт меню.'
}

# ---- Maintenance ----
function New-FRestorePoint {
  Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
  Set-FReg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' 'SystemRestorePointCreationFrequency' 0
  try { Checkpoint-Computer -Description "WinWeaver $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Show-Ok 'Точка восстановления создана' }
  catch { Show-Err "Не удалось создать точку восстановления: $($_.Exception.Message)" }
}
function Clear-FTemp {
  $before = (Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':'))).Free
  foreach ($p in "$env:TEMP\*", "$env:WINDIR\Temp\*") { Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue }
  Stop-Service -Name wuauserv, bits -Force -ErrorAction SilentlyContinue
  Remove-Item -Path "$env:WINDIR\SoftwareDistribution\Download\*" -Recurse -Force -ErrorAction SilentlyContinue
  Start-Service -Name bits, wuauserv -ErrorAction SilentlyContinue
  & ipconfig.exe /flushdns 2>&1 | Out-Null
  $after = (Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':'))).Free
  Show-Ok ('Очищено примерно {0:N0} МБ' -f ([math]::Max(0, ($after - $before) / 1MB)))
}


# ======================================================================
#  RESTORE POINTS
# ======================================================================
function Get-FRestorePoints {
  try { return @(Get-ComputerRestorePoint -ErrorAction Stop | Sort-Object SequenceNumber -Descending) } catch { return @() }
}
function Restore-FFromPoint([int]$seq) {
  try { Restore-Computer -RestorePoint $seq -Confirm:$false -ErrorAction Stop; return $true } catch { Write-FLog "RESTORE FAILED $seq : $_"; return $false }
}

# ======================================================================
#  APP-ONLY FUNCTIONS (WinWeaver.exe)
# ======================================================================
function Get-FSysInfo {
  if ($script:FSysCache -and ((Get-Date) - $script:FSysTime).TotalSeconds -lt 20) { return $script:FSysCache }
  $r = @{}
  $cv = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
  $name = Get-FReg $cv 'ProductName'; $disp = Get-FReg $cv 'DisplayVersion'; $build = Get-FReg $cv 'CurrentBuild'; $ubr = Get-FReg $cv 'UBR'
  $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction SilentlyContinue
  if ($os -and $os.Caption) { $name = ([string]$os.Caption) -replace '^Microsoft\s+', '' }
  $r.OS = (@($name, $disp) | Where-Object { $_ }) -join ' '
  $r.Build = if ($ubr) { "$build.$ubr" } else { "$build" }
  $cpu = Get-CimInstance -ClassName Win32_Processor -ErrorAction SilentlyContinue | Select-Object -First 1
  $r.CPU = if ($cpu) { (([string]$cpu.Name) -replace '\s+', ' ').Trim() } else { '' }
  $r.Cores = if ($cpu) { "$($cpu.NumberOfCores) ядер / $($cpu.NumberOfLogicalProcessors) потоков" } else { '' }
  $gpus = @(Get-CimInstance -ClassName Win32_VideoController -ErrorAction SilentlyContinue | Where-Object { $_.Name -and $_.Name -notmatch 'Basic Display|Remote|Virtual Display|Parsec|Meta Virtual' } | ForEach-Object { [string]$_.Name })
  $r.GPU = if ($gpus.Count) { $gpus -join ', ' } else { 'Базовый видеоадаптер' }
  $cs = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue
  $r.RAM = if ($cs) { '{0:N0} ГБ' -f [math]::Round($cs.TotalPhysicalMemory / 1GB) } else { '' }
  if ($os) {
    $free = [double]$os.FreePhysicalMemory * 1KB; $tot = [double]$os.TotalVisibleMemorySize * 1KB
    if ($tot -gt 0) { $r.RAMUsed = [int](100 - $free / $tot * 100) }
    try { $up = (Get-Date) - $os.LastBootUpTime; $r.Uptime = '{0} д {1} ч {2} мин' -f $up.Days, $up.Hours, $up.Minutes; if ($up.Days -eq 0) { $r.Uptime = '{0} ч {1} мин' -f $up.Hours, $up.Minutes } } catch {}
  }
  $d = Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='$env:SystemDrive'" -ErrorAction SilentlyContinue
  if ($d -and $d.Size) { $r.Disk = '{0:N0} ГБ свободно из {1:N0} ГБ' -f ($d.FreeSpace / 1GB), ($d.Size / 1GB); $r.DiskFree = '{0:N0} ГБ свободно' -f ($d.FreeSpace / 1GB); $r.DiskSize = '{0:N0} ГБ' -f ($d.Size / 1GB); $r.Drive = $env:SystemDrive; $r.DiskUsed = [int](100 - $d.FreeSpace / $d.Size * 100) }
  $r.PC = $env:COMPUTERNAME; $r.User = $env:USERNAME
  $script:FSysCache = $r; $script:FSysTime = Get-Date
  return $r
}

# ---- privacy summary for the home page ----
function Get-FPrivacyScore {
  $n = 0
  if ((Get-Telemetry) -eq 'ВЫКЛ') { $n++ }; if ((Get-AI) -eq 'ВЫКЛ') { $n++ }; if ((Get-Ads) -eq 'ВЫКЛ') { $n++ }; if ((Get-Bing) -eq 'ВЫКЛ') { $n++ }
  return $n
}

# ---- DNS ----
$DnsSets = @{
  1 = @('1.1.1.1', '1.0.0.1', '2606:4700:4700::1111', '2606:4700:4700::1001')
  2 = @('8.8.8.8', '8.8.4.4', '2001:4860:4860::8888', '2001:4860:4860::8844')
  3 = @('9.9.9.9', '149.112.112.112', '2620:fe::fe', '2620:fe::9')
  4 = @('94.140.14.14', '94.140.15.15', '2a10:50c0::ad1:ff', '2a10:50c0::ad2:ff')
}
function Get-FNetAdapters { @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' }) }
function Get-FDns {
  $a = Get-FNetAdapters | Select-Object -First 1
  if (-not $a) { return -1 }
  $s = @((Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses)
  if ($s -contains '1.1.1.1') { return 1 }; if ($s -contains '8.8.8.8') { return 2 }; if ($s -contains '9.9.9.9') { return 3 }; if ($s -contains '94.140.14.14') { return 4 }
  return 0
}
function Get-FDnsText {
  $a = Get-FNetAdapters
  if ($a.Count -eq 0) { return 'Нет активного подключения' }
  $s = @((Get-DnsClientServerAddress -InterfaceIndex $a[0].ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses)
  return ($a[0].Name + ': ' + $(if ($s.Count) { $s -join ', ' } else { 'автоматически' }))
}
function Set-FDns([int]$i) {
  $ad = Get-FNetAdapters
  if ($ad.Count -eq 0) { Show-Err 'Нет активного сетевого подключения'; return }
  foreach ($a in $ad) {
    if ($i -le 0) { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses -ErrorAction SilentlyContinue }
    else { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses $DnsSets[$i] -ErrorAction SilentlyContinue }
  }
  if ($i -eq 4) {
    # Windows 11 knows DoH templates for Cloudflare/Google/Quad9 only
    foreach ($ip in '94.140.14.14', '94.140.15.15') { try { Add-DnsClientDohServerAddress -ServerAddress $ip -DohTemplate 'https://dns.adguard-dns.com/dns-query' -AllowFallbackToUdp $true -AutoUpgrade $true -ErrorAction Stop | Out-Null } catch {} }
  }
  Clear-DnsClientCache -ErrorAction SilentlyContinue
  $n = @('автоматически (от провайдера)', 'Cloudflare 1.1.1.1', 'Google 8.8.8.8', 'Quad9 9.9.9.9', 'AdGuard 94.140.14.14')[[math]::Max(0, [math]::Min(4, $i))]
  Show-Ok "DNS: $n"
}

# ======================================================================
#  EXTRA TWEAKS (data-driven). Entry: 'Path|Name|Value|Type'; Value '-' = delete the value; 'svc:Name|Start' = service start type.
#  Works the same on 22H2 (22621) .. 26H2: keys that a build does not know are simply ignored by Windows; the UI shows MinBuild hints.
# ======================================================================
$FTweaks = @{}
function Add-FTweak([string]$id, [string]$title, [string[]]$on, [string[]]$off, [switch]$Ex, [switch]$Rb, [string]$area = '') {
  $FTweaks[$id] = @{ T = $title; On = $on; Off = $off; Ex = [bool]$Ex; Rb = [bool]$Rb; A = $area }
}
# tweak with its own get/set code (per-adapter keys, netsh, cmdlets) — same Get-FTweak / Set-FTweak API
function Add-FTweakFn([string]$id, [string]$title, [scriptblock]$get, [scriptblock]$set, [switch]$Rb) {
  $FTweaks[$id] = @{ T = $title; G = $get; S = $set; Ex = $false; Rb = [bool]$Rb; A = '' }
}
function Add-FTweakFnChoice([string]$id, [string]$title, [scriptblock]$get, [scriptblock]$set, [string[]]$names, [switch]$Rb) {
  $FTweaks[$id] = @{ T = $title; GI = $get; SI = $set; C = $names; Names = $names; Ex = $false; Rb = [bool]$Rb; A = '' }
}
function Add-FTweakChoice([string]$id, [string]$title, [object[]]$choices, [string[]]$names, [switch]$Ex) {
  $FTweaks[$id] = @{ T = $title; C = $choices; Names = $names; Ex = [bool]$Ex; Rb = $false; A = '' }
}
function Test-FTweakEntries([string[]]$list) {
  foreach ($e in $list) {
    $p = $e.Split('|')
    if ($p[0] -like 'svc:*') { if ([string](Get-FSvcStart $p[0].Substring(4)) -ne $p[1]) { return $false }; continue }
    $cur = Get-FReg $p[0] $p[1]
    if ($p[2] -eq '-') { if ($null -ne $cur) { return $false } }
    elseif ([string]$cur -ne $p[2]) { return $false }
  }
  return $true
}
function Set-FTweakEntries([string[]]$list) {
  foreach ($e in $list) {
    $p = $e.Split('|')
    if ($p[0] -like 'svc:*') { [void](Set-FSvcStart $p[0].Substring(4) ([int]$p[1])); continue }
    if ($p[2] -eq '-') { Remove-FReg $p[0] $p[1]; continue }
    $type = if ($p.Count -gt 3 -and $p[3]) { $p[3] } else { 'DWord' }
    $val = if ($type -eq 'DWord') { [int]$p[2] } else { $p[2] }
    Set-FReg $p[0] $p[1] $val $type
  }
}
function Complete-FTweak($t) {
  if ($t.A) { Send-FSettingChange $t.A }
  if ($t.Ex) { Restart-FExplorer }
  if ($t.Rb) { $global:NeedReboot = $true }
}
function Get-FTweak([string]$id) {
  $t = $FTweaks[$id]; if (-not $t) { return $false }
  if ($t.G) { try { return [bool](& $t.G) } catch { return $false } }
  return (Test-FTweakEntries $t.On)
}
function Set-FTweak([string]$id, [bool]$on) {
  $t = $FTweaks[$id]; if (-not $t) { Show-Err "Неизвестная настройка: $id"; return }
  if ($t.S) { & $t.S $on } elseif ($on) { Set-FTweakEntries $t.On } else { Set-FTweakEntries $t.Off }
  Complete-FTweak $t
  Show-Ok ($t.T + ': ' + (Get-FOnOff $on) + $(if ($t.Rb) { ' (после перезагрузки)' } else { '' }))
}
function Get-FTweakIdx([string]$id) {
  $t = $FTweaks[$id]; if (-not $t -or -not $t.C) { return -1 }
  if ($t.GI) { try { return [int](& $t.GI) } catch { return -1 } }
  for ($i = 0; $i -lt $t.C.Count; $i++) { if (Test-FTweakEntries $t.C[$i]) { return $i } }
  return -1
}
function Set-FTweakIdx([string]$id, [int]$i) {
  $t = $FTweaks[$id]; if (-not $t -or -not $t.C -or $i -lt 0 -or $i -ge $t.C.Count) { return }
  if ($t.SI) { & $t.SI $i } else { Set-FTweakEntries $t.C[$i] }
  Complete-FTweak $t
  Show-Ok ($t.T + ': ' + $t.Names[$i])
}

$CAM = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore'
$EXP = "$CU\Explorer"
# privacy
Add-FTweak 'location'  'Определение местоположения выкл.' @("$CAM\location|Value|Deny|String") @("$CAM\location|Value|Allow|String")
Add-FTweak 'tailored'  'Персональные советы по диагностике выкл.' @("$CU\Privacy|TailoredExperiencesWithDiagnosticDataEnabled|0") @("$CU\Privacy|TailoredExperiencesWithDiagnosticDataEnabled|1")
Add-FTweak 'speech'    'Онлайн-распознавание речи выкл.' @('HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy|HasAccepted|0') @('HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy|HasAccepted|1')
Add-FTweak 'inking'    'Сбор рукописного и набранного текста выкл.' @('HKCU:\Software\Microsoft\InputPersonalization|RestrictImplicitTextCollection|1', 'HKCU:\Software\Microsoft\InputPersonalization|RestrictImplicitInkCollection|1', 'HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore|HarvestContacts|0') @('HKCU:\Software\Microsoft\InputPersonalization|RestrictImplicitTextCollection|0', 'HKCU:\Software\Microsoft\InputPersonalization|RestrictImplicitInkCollection|0', 'HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore|HarvestContacts|1')
Add-FTweak 'feedback'  'Запросы отзывов выкл.' @('HKCU:\Software\Microsoft\Siuf\Rules|NumberOfSIUFInPeriod|0', "$HKP\Windows\DataCollection|DoNotShowFeedbackNotifications|1") @('HKCU:\Software\Microsoft\Siuf\Rules|NumberOfSIUFInPeriod|-', "$HKP\Windows\DataCollection|DoNotShowFeedbackNotifications|-")
Add-FTweak 'activity'  'Журнал действий выкл.' @("$HKP\Windows\System|PublishUserActivities|0", "$HKP\Windows\System|UploadUserActivities|0") @("$HKP\Windows\System|PublishUserActivities|-", "$HKP\Windows\System|UploadUserActivities|-")
Add-FTweak 'searchhl'  '«Главное в поиске» выкл.' @("$CU\SearchSettings|IsDynamicSearchBoxEnabled|0") @("$CU\SearchSettings|IsDynamicSearchBoxEnabled|1") -Ex
Add-FTweak 'lockads'   'Советы на экране блокировки выкл.' @("$CU\ContentDeliveryManager|RotatingLockScreenOverlayEnabled|0", "$CU\ContentDeliveryManager|SubscribedContent-338387Enabled|0") @("$CU\ContentDeliveryManager|RotatingLockScreenOverlayEnabled|1", "$CU\ContentDeliveryManager|SubscribedContent-338387Enabled|1")
Add-FTweak 'scoobe'    '«Завершите настройку устройства» выкл.' @("$CU\UserProfileEngagement|ScoobeSystemSettingEnabled|0") @("$CU\UserProfileEngagement|ScoobeSystemSettingEnabled|1")
Add-FTweak 'syncads'   'Реклама в Проводнике выкл.' @("$ADV|ShowSyncProviderNotifications|0") @("$ADV|ShowSyncProviderNotifications|1") -Ex
# look & taskbar & explorer
Add-FTweak 'widgets'   'Виджеты выкл.' @("$HKP\Dsh|AllowNewsAndInterests|0") @("$HKP\Dsh|AllowNewsAndInterests|-") -Ex
Add-FTweak 'seconds'   'Секунды в часах' @("$ADV|ShowSecondsInSystemClock|1") @("$ADV|ShowSecondsInSystemClock|0") -Ex
Add-FTweak 'teamschat' 'Кнопка Chat/Teams скрыта' @("$ADV|TaskbarMn|0") @("$ADV|TaskbarMn|1") -Ex
Add-FTweak 'deskcorner' '«Свернуть все окна» в углу' @("$ADV|TaskbarSd|1") @("$ADV|TaskbarSd|0") -Ex
Add-FTweakChoice 'combine' 'Группировка кнопок' @(@("$ADV|TaskbarGlomLevel|0"), @("$ADV|TaskbarGlomLevel|1"), @("$ADV|TaskbarGlomLevel|2")) @('Всегда', 'При переполнении', 'Никогда') -Ex
Add-FTweak 'snapflyout' 'Макеты при наведении на «Развернуть»' @("$ADV|EnableSnapAssistFlyout|1") @("$ADV|EnableSnapAssistFlyout|0")
Add-FTweak 'startdocs' 'Недавние файлы в Пуске скрыты' @("$ADV|Start_TrackDocs|0") @("$ADV|Start_TrackDocs|1") -Ex
Add-FTweak 'startlayout' 'Больше закреплённых в Пуске' @("$ADV|Start_Layout|1") @("$ADV|Start_Layout|0") -Ex
Add-FTweak 'startiris' 'Рекомендации в Пуске выкл.' @("$ADV|Start_IrisRecommendations|0") @("$ADV|Start_IrisRecommendations|1") -Ex
Add-FTweak 'launchpc'  'Проводник открывает «Этот компьютер»' @("$ADV|LaunchTo|1") @("$ADV|LaunchTo|2")
Add-FTweak 'compact'   'Компактный вид Проводника' @("$ADV|UseCompactMode|1") @("$ADV|UseCompactMode|0") -Ex
Add-FTweak 'checkbox'  'Флажки выбора файлов' @("$ADV|AutoCheckSelect|1") @("$ADV|AutoCheckSelect|0") -Ex
Add-FTweak 'qarecent'  'Недавнее в быстром доступе скрыто' @("$EXP|ShowRecent|0", "$EXP|ShowFrequent|0") @("$EXP|ShowRecent|1", "$EXP|ShowFrequent|1") -Ex
Add-FTweak 'thispc'    '«Этот компьютер» на рабочем столе' @("$EXP\HideDesktopIcons\NewStartPanel|{20D04FE0-3AEA-1069-A2D8-08002B30309D}|0") @("$EXP\HideDesktopIcons\NewStartPanel|{20D04FE0-3AEA-1069-A2D8-08002B30309D}|1") -Ex
# performance
Add-FTweak 'startdelay' 'Без задержки автозапуска' @("$EXP\Serialize|StartupDelayInMSec|0") @("$EXP\Serialize|StartupDelayInMSec|-")
Add-FTweak 'menudelay' 'Быстрые меню' @('HKCU:\Control Panel\Desktop|MenuShowDelay|100|String') @('HKCU:\Control Panel\Desktop|MenuShowDelay|400|String') -Rb
Add-FTweak 'bgapps'    'Фоновые приложения выкл.' @("$CU\BackgroundAccessApplications|GlobalUserDisabled|1", "$HKP\Windows\AppPrivacy|LetAppsRunInBackground|2") @("$CU\BackgroundAccessApplications|GlobalUserDisabled|0", "$HKP\Windows\AppPrivacy|LetAppsRunInBackground|-")
Add-FTweak 'pthrottle' 'Power Throttling выкл.' @('HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling|PowerThrottlingOff|1') @('HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling|PowerThrottlingOff|-') -Rb
Add-FTweak 'sysmain'   'SysMain выкл.' @('svc:SysMain|4') @('svc:SysMain|2') -Rb
Add-FTweak 'wsearch'   'Индексирование поиска выкл.' @('svc:WSearch|4') @('svc:WSearch|2') -Rb
Add-FTweak 'windowed'  'Оптимизация оконных игр' @('HKCU:\Software\Microsoft\DirectX\UserGpuPreferences|DirectXUserGlobalSettings|SwapEffectUpgradeEnable=1;|String') @('HKCU:\Software\Microsoft\DirectX\UserGpuPreferences|DirectXUserGlobalSettings|-')
Add-FTweak 'fso'       'Оптимизация во весь экран выкл.' @('HKCU:\System\GameConfigStore|GameDVR_FSEBehaviorMode|2', 'HKCU:\System\GameConfigStore|GameDVR_HonorUserFSEBehaviorMode|1', 'HKCU:\System\GameConfigStore|GameDVR_DXGIHonorFSEWindowsCompatible|1') @('HKCU:\System\GameConfigStore|GameDVR_FSEBehaviorMode|0', 'HKCU:\System\GameConfigStore|GameDVR_HonorUserFSEBehaviorMode|0', 'HKCU:\System\GameConfigStore|GameDVR_DXGIHonorFSEWindowsCompatible|0')
Add-FTweak 'sticky'    'Запрос залипания клавиш выкл.' @('HKCU:\Control Panel\Accessibility\StickyKeys|Flags|506|String', 'HKCU:\Control Panel\Accessibility\Keyboard Response|Flags|122|String', 'HKCU:\Control Panel\Accessibility\ToggleKeys|Flags|58|String') @('HKCU:\Control Panel\Accessibility\StickyKeys|Flags|510|String', 'HKCU:\Control Panel\Accessibility\Keyboard Response|Flags|126|String', 'HKCU:\Control Panel\Accessibility\ToggleKeys|Flags|62|String')
Add-FTweak 'nexus'     'Game Bar с геймпада выкл.' @('HKCU:\Software\Microsoft\GameBar|UseNexusForGameBarEnabled|0') @('HKCU:\Software\Microsoft\GameBar|UseNexusForGameBarEnabled|1')
# updates
Add-FTweak 'dop2p'     'Раздача обновлений другим ПК выкл.' @("$HKP\Windows\DeliveryOptimization|DODownloadMode|0") @("$HKP\Windows\DeliveryOptimization|DODownloadMode|-")
Add-FTweak 'noreboot'  'Без авто-перезагрузки при входе' @("$HKP\Windows\WindowsUpdate\AU|NoAutoRebootWithLoggedOnUsers|1") @("$HKP\Windows\WindowsUpdate\AU|NoAutoRebootWithLoggedOnUsers|-")
Add-FTweak 'storeupd'  'Обновления Store только вручную' @("$HKP\WindowsStore|AutoDownload|2") @("$HKP\WindowsStore|AutoDownload|-")

# one-call evaluation of many getters for the home "how well is the system tuned" ring: $list = @(@(getScript, wanted), ...)
function Get-FScore($list) {
  $ok = 0; $n = 0; $bad = @()
  foreach ($x in $list) {
    $n++
    $v = $null; try { $v = & ([scriptblock]::Create([string]$x[0])) } catch {}
    if ($v -is [array]) { $v = $v[-1] }
    if ("$v" -eq "$($x[1])") { $ok++ } else { $bad += [string]$x[2] }
  }
  return @{ Ok = $ok; Total = $n; Bad = $bad }
}

# ---- Startup manager ----
$SAp = 'Explorer\StartupApproved'
function Get-FStartupSources {
  @(
    @{ Loc = 'HKCU'; Run = "$CU\Run"; Appr = "$CU\$SAp\Run"; Label = 'Пользователь' },
    @{ Loc = 'HKLM'; Run = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'; Appr = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\$SAp\Run"; Label = 'Все пользователи' },
    @{ Loc = 'HKLM32'; Run = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run'; Appr = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\$SAp\Run32"; Label = 'Все пользователи (32-бит)' },
    @{ Loc = 'UFOLD'; Folder = [Environment]::GetFolderPath('Startup'); Appr = "$CU\$SAp\StartupFolder"; Label = 'Папка автозагрузки' },
    @{ Loc = 'CFOLD'; Folder = [Environment]::GetFolderPath('CommonStartup'); Appr = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\$SAp\StartupFolder"; Label = 'Общая папка автозагрузки' }
  )
}
function Test-FApproved($appr, [string]$name) {
  # REG_BINARY comes back unrolled (object[] of bytes); first byte: even = enabled, odd = disabled
  $v = @(Get-FReg $appr $name)
  if ($v.Count -lt 1 -or $null -eq $v[0]) { return $true }
  try { return ((([int]$v[0]) % 2) -eq 0) } catch { return $true }
}
# exe path from a Run command line: "C:\x\a.exe" -arg  |  C:\x\a.exe /arg  |  rundll32 ...  |  %windir%\...
function Get-FCmdExe([string]$cmd) {
  $c = [Environment]::ExpandEnvironmentVariables(([string]$cmd).Trim())
  if ($c -match '^"([^"]+)"') { return $Matches[1] }
  if ($c -match '^(.+?\.(exe|cmd|bat|lnk))(\s|$)') { return $Matches[1] }
  return ($c -split '\s')[0]
}
function Add-FStartupInfo($h, [string]$exe) {
  $h.Path = $exe; $h.Company = ''; $h.Desc = ''; $h.Exists = $false
  try {
    if ($exe -and (Test-Path -LiteralPath $exe)) {
      $h.Exists = $true
      $vi = (Get-Item -LiteralPath $exe -ErrorAction Stop).VersionInfo
      if ($vi) { $h.Company = [string]$vi.CompanyName; $h.Desc = [string]$vi.FileDescription }
    }
  } catch {}
  return $h
}
function Get-FStartup([switch]$NoTasks) {
  $out = @()
  foreach ($s in (Get-FStartupSources)) {
    if ($s.Run) {
      $k = Get-Item -Path $s.Run -ErrorAction SilentlyContinue
      if (-not $k) { continue }
      foreach ($n in $k.GetValueNames()) {
        if (-not $n) { continue }
        $cmd = [string]$k.GetValue($n)
        $out += (Add-FStartupInfo @{ Id = "$($s.Loc)|$n"; Name = $n; Command = $cmd; Source = $s.Label; Enabled = (Test-FApproved $s.Appr $n) } (Get-FCmdExe $cmd))
      }
    } elseif ($s.Folder -and (Test-Path $s.Folder)) {
      foreach ($f in (Get-ChildItem -Path $s.Folder -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'desktop.ini' })) {
        $exe = $f.FullName
        if ($f.Extension -eq '.lnk') { try { $tgt = (New-Object -ComObject WScript.Shell).CreateShortcut($f.FullName).TargetPath; if ($tgt) { $exe = $tgt } } catch {} }
        $out += (Add-FStartupInfo @{ Id = "$($s.Loc)|$($f.Name)"; Name = [IO.Path]::GetFileNameWithoutExtension($f.Name); Command = $f.FullName; Source = $s.Label; Enabled = (Test-FApproved $s.Appr $f.Name) } $exe)
      }
    }
  }
  if (-not $NoTasks) { $out += @(Get-FStartupMore) }
  $dl = Get-FStartupDelays
  foreach ($h in $out) { $h.Delay = $(if ($dl.ContainsKey([string]$h.Id)) { [int]$dl[[string]$h.Id] } else { 0 }) }
  return ,$out
}
function Set-FStartup([string]$id, [bool]$on) {
  if ($id -like 'TASK|*') { Set-FStartupTask $id $on; Show-Ok ('Автозагрузка ' + $(if ($on) { 'включена' } else { 'выключена' }) + ': ' + ($id -split '\\')[-1]); return }
  $p = $id.Split('|', 2); $src = Get-FStartupSources | Where-Object { $_.Loc -eq $p[0] } | Select-Object -First 1
  if (-not $src) { Show-Err "Неизвестный источник: $id"; return }
  $b = New-Object byte[] 12
  if ($on) { $b[0] = 2 } else { $b[0] = 3; [BitConverter]::GetBytes([DateTime]::UtcNow.ToFileTimeUtc()).CopyTo($b, 4) }
  Set-FReg $src.Appr $p[1] $b 'Binary'
  if ($on) { Show-Ok "Автозагрузка включена: $($p[1])" } else { Show-Ok "Убрано из автозагрузки: $($p[1])" }
}

# ======================================================================
#  NETWORK: connection info, ping, DNS speed test, network tweaks
# ======================================================================
$TcpIf = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'
$NbtIf = 'HKLM:\SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces'
$DnsPol = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient'
$MMProfile = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile'
# VPN / tunnel adapters: Happ, sing-box, v2ray/xray, WireGuard, AmneziaWG, OpenVPN (TAP), Wintun, Outline, Tailscale, ZeroTier…
$VpnRx = 'Happ|Wintun|WireGuard|Amnezia|sing-?box|v2ray|xray|Hiddify|Nekoray|Clash|Mihomo|Outline|OpenVPN|TAP-Windows|\bTAP\b|\bTUN\b|Tunnel|VPN|Tailscale|ZeroTier|Radmin|Hamachi|NordLynx|Proton|Mullvad|Cloudflare WARP|Windscribe|Fortinet|Cisco AnyConnect|Kaspersky'
function Test-FVpnAdapter($ad) {
  if (-not $ad) { return $false }
  $t = [string]$ad.InterfaceDescription + ' ' + [string]$ad.Name
  if ($t -match $VpnRx) { return $true }
  # software adapters that carry the default route are tunnels (Hyper-V vEthernet is not)
  return ((-not $ad.HardwareInterface) -and ($t -notmatch 'Hyper-V|vEthernet|VirtualBox|VMware'))
}
# route that wins for the internet: VPNs often use 0.0.0.0/1 + 128.0.0.0/1 instead of /0
function Get-FMainRoute {
  $r = @(Get-NetRoute -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { @('0.0.0.0/0', '0.0.0.0/1', '128.0.0.0/1') -contains $_.DestinationPrefix })
  @($r | Sort-Object @{ e = { if ($_.DestinationPrefix -eq '0.0.0.0/0') { 1 } else { 0 } } }, @{ e = { [int]$_.RouteMetric + [int]$_.InterfaceMetric } }) | Select-Object -First 1
}
function Get-FIfDns([int]$idx) { @((Get-DnsClientServerAddress -InterfaceIndex $idx -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses) -join ', ' }
function Get-FIfIp([int]$idx) { [string](@(Get-NetIPAddress -InterfaceIndex $idx -AddressFamily IPv4 -ErrorAction SilentlyContinue) | Select-Object -First 1).IPAddress }
function Get-FNetInfo {
  $res = @{ Ok = $false; Vpn = $false }
  $rt = Get-FMainRoute
  $main = if ($rt) { Get-NetAdapter -InterfaceIndex $rt.ifIndex -ErrorAction SilentlyContinue } else { $null }
  if ($main -and (Test-FVpnAdapter $main)) {
    $res.Vpn = $true; $res.Ok = $true
    $res.VpnName = [string]$main.Name; $res.VpnDesc = [string]$main.InterfaceDescription
    $res.VpnIp = Get-FIfIp $main.ifIndex; $res.VpnDns = Get-FIfDns $main.ifIndex
    $main = $null
  }
  # the real (physical) connection under the tunnel
  $phys = @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' })
  $ad = $main
  $gw = ''
  if (-not $ad -or -not (@($phys | ForEach-Object { $_.ifIndex }) -contains $ad.ifIndex)) {
    $best = $null; $bm = [int]::MaxValue
    foreach ($p in $phys) {
      foreach ($pr in @(Get-NetRoute -InterfaceIndex $p.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue)) {
        $m = [int]$pr.RouteMetric + [int]$pr.InterfaceMetric
        if ($m -lt $bm) { $bm = $m; $best = $p; $gw = [string]$pr.NextHop }
      }
    }
    if (-not $best -and $phys.Count) { $best = $phys[0] }
    if ($best) { $ad = $best }
  } elseif ($rt) { $gw = [string]$rt.NextHop }
  if ($ad) {
    if (-not $gw) { $gw = [string](@(Get-NetRoute -InterfaceIndex $ad.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue) | Select-Object -First 1).NextHop }
    if ($gw -eq '0.0.0.0') { $gw = '' }
    $wifi = ([string]$ad.PhysicalMediaType -match '802\.11') -or ([string]$ad.InterfaceDescription -match 'Wi-?Fi|Wireless|WLAN|802\.11')
    $ssid = ''; $sig = -1
    if ($wifi) {
      foreach ($l in @(& netsh.exe wlan show interfaces 2>$null)) {
        if ($l -match '^\s*SSID\s*:\s*(.+)$') { $ssid = $Matches[1].Trim() }
        elseif ($sig -lt 0 -and $l -match ':\s*(\d{1,3})\s*%') { $sig = [int]$Matches[1] }
      }
    }
    $res.Ok = $true; $res.Phys = $true
    $res.Name = [string]$ad.Name; $res.Desc = [string]$ad.InterfaceDescription; $res.Wifi = [bool]$wifi; $res.Speed = [string]$ad.LinkSpeed
    $res.Ip = Get-FIfIp $ad.ifIndex; $res.Gw = $gw; $res.Dns = Get-FIfDns $ad.ifIndex; $res.Ssid = $ssid; $res.Signal = $sig
    $res.Dhcp = [string](Get-NetIPInterface -InterfaceIndex $ad.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).Dhcp
  }
  return $res
}
# one ping round to every host at once; -1 = no answer
function Get-FPing([string]$list) {
  $res = @{}; $jobs = @()
  foreach ($h in ($list -split ',')) {
    if (-not $h) { continue }
    try { $jobs += ,@($h, (New-Object System.Net.NetworkInformation.Ping).SendPingAsync($h, 1000)) } catch { $res[$h] = -1 }
  }
  foreach ($j in $jobs) {
    try { [void]$j[1].Wait(1600); $r = $j[1].Result; $res[$j[0]] = $(if ($r.Status -eq 'Success') { [int]$r.RoundtripTime } else { -1 }) } catch { $res[$j[0]] = -1 }
  }
  return $res
}
# DNS response time (best of several names), key = index from Get-FDns; -1 = no answer
function Test-FDnsSpeed {
  $srv = @{ '1' = '1.1.1.1'; '2' = '8.8.8.8'; '3' = '9.9.9.9'; '4' = '94.140.14.14' }
  if ((Get-FDns) -eq 0) {
    $a0 = Get-FNetAdapters | Select-Object -First 1
    if ($a0) { $cur = @((Get-DnsClientServerAddress -InterfaceIndex $a0.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses) | Select-Object -First 1; if ($cur) { $srv['0'] = [string]$cur } }
  }
  $res = @{}
  foreach ($k in $srv.Keys) {
    $best = -1
    foreach ($n in 'microsoft.com', 'wikipedia.org', 'github.com', 'yandex.ru') {
      $sw = [Diagnostics.Stopwatch]::StartNew()
      try { [void](Resolve-DnsName -Name $n -Server $srv[$k] -DnsOnly -NoHostsFile -QuickTimeout -ErrorAction Stop); $ms = [int]$sw.ElapsedMilliseconds; if ($best -lt 0 -or $ms -lt $best) { $best = $ms } } catch {}
    }
    $res[$k] = $best
  }
  return $res
}
function Get-FIfGuids { @(Get-FNetAdapters | ForEach-Object { [string]$_.InterfaceGuid }) }
function Get-FNagleOff {
  $g = Get-FIfGuids; if ($g.Count -eq 0) { return $false }
  foreach ($x in $g) { if ((Get-FReg "$TcpIf\$x" 'TcpAckFrequency') -ne 1 -or (Get-FReg "$TcpIf\$x" 'TCPNoDelay') -ne 1) { return $false } }
  return $true
}
function Set-FNagleOff([bool]$on) {
  foreach ($x in (Get-FIfGuids)) {
    if ($on) { Set-FReg "$TcpIf\$x" 'TcpAckFrequency' 1; Set-FReg "$TcpIf\$x" 'TCPNoDelay' 1 }
    else { Remove-FReg "$TcpIf\$x" 'TcpAckFrequency'; Remove-FReg "$TcpIf\$x" 'TCPNoDelay' }
  }
}
function Get-FNetbiosOff {
  $k = @(Get-ChildItem -Path $NbtIf -ErrorAction SilentlyContinue); if ($k.Count -eq 0) { return $false }
  foreach ($x in $k) { if ((Get-FReg "$NbtIf\$($x.PSChildName)" 'NetbiosOptions') -ne 2) { return $false } }
  return $true
}
function Set-FNetbiosOff([bool]$on) {
  foreach ($x in @(Get-ChildItem -Path $NbtIf -ErrorAction SilentlyContinue)) { Set-FReg "$NbtIf\$($x.PSChildName)" 'NetbiosOptions' $(if ($on) { 2 } else { 0 }) }
}
function Get-FNicPowerOff {
  $a = Get-FNetAdapters; if ($a.Count -eq 0) { return $false }
  foreach ($n in $a) {
    $pm = Get-NetAdapterPowerManagement -Name $n.Name -ErrorAction SilentlyContinue
    if ($pm -and @('Disabled', 'Unsupported') -notcontains [string]$pm.AllowComputerToTurnOffDevice) { return $false }
  }
  return $true
}
function Set-FNicPowerOff([bool]$on) {
  foreach ($n in (Get-FNetAdapters)) {
    try { Set-NetAdapterPowerManagement -Name $n.Name -AllowComputerToTurnOffDevice $(if ($on) { 'Disabled' } else { 'Enabled' }) -NoRestart -ErrorAction Stop } catch { Write-FLog "NIC PM $($n.Name): $($_.Exception.Message)" }
  }
}
function Get-FTcpGlobal([string]$prop) { try { return [string](Get-NetTCPSetting -SettingName Internet -ErrorAction Stop).$prop } catch { return '' } }
function Get-FTcpAuto { switch (Get-FTcpGlobal 'AutoTuningLevelLocal') { 'Normal' { 0 } 'Restricted' { 1 } 'HighlyRestricted' { 1 } 'Disabled' { 2 } default { -1 } } }
function Set-FTcpAuto([int]$i) { [void](Invoke-FNative 'netsh.exe' ('int tcp set global autotuninglevel=' + @('normal', 'restricted', 'disabled')[$i])) }
function Get-FRss { try { return ([string](Get-NetOffloadGlobalSetting -ErrorAction Stop).ReceiveSideScaling -eq 'Enabled') } catch { return $false } }
function Get-FTeredoOff { try { return ([string](Get-NetTeredoConfiguration -ErrorAction Stop).Type -eq 'Disabled') } catch { return $false } }

Add-FTweakFn 'nagle' 'Алгоритм Нейгла выкл.' { Get-FNagleOff } { param($on) Set-FNagleOff $on }
Add-FTweak 'netthrottle' 'Ограничение сети для мультимедиа снято' @("$MMProfile|NetworkThrottlingIndex|-1") @("$MMProfile|NetworkThrottlingIndex|10") -Rb
Add-FTweakFnChoice 'tcpauto' 'Автонастройка окна TCP' { Get-FTcpAuto } { param($i) Set-FTcpAuto $i } @('Нормальная', 'Ограниченная', 'Выключена')
Add-FTweakFn 'rss' 'Распределение сети по ядрам (RSS)' { Get-FRss } { param($on) [void](Invoke-FNative 'netsh.exe' ('int tcp set global rss=' + $(if ($on) { 'enabled' } else { 'disabled' }))) }
Add-FTweakFn 'ecn' 'ECN (уведомление о перегрузке)' { (Get-FTcpGlobal 'EcnCapability') -eq 'Enabled' } { param($on) [void](Invoke-FNative 'netsh.exe' ('int tcp set global ecncapability=' + $(if ($on) { 'enabled' } else { 'disabled' }))) }
Add-FTweak 'qosres' 'Резерв канала QoS 0%' @('HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched|NonBestEffortLimit|0') @('HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched|NonBestEffortLimit|-') -Rb
Add-FTweakFn 'nicpower' 'Энергосбережение сетевой карты выкл.' { Get-FNicPowerOff } { param($on) Set-FNicPowerOff $on } -Rb
Add-FTweak 'ipv4pref' 'IPv4 в приоритете над IPv6' @('HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters|DisabledComponents|32') @('HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters|DisabledComponents|-') -Rb
Add-FTweak 'doh' 'Шифрованный DNS (DoH)' @("$DnsPol|DoHPolicy|2") @("$DnsPol|DoHPolicy|-")
Add-FTweak 'llmnr' 'LLMNR выкл.' @("$DnsPol|EnableMulticast|0") @("$DnsPol|EnableMulticast|-")
Add-FTweakFn 'netbios' 'NetBIOS через TCP/IP выкл.' { Get-FNetbiosOff } { param($on) Set-FNetbiosOff $on } -Rb
Add-FTweak 'hotspot' 'Автоподключение к открытым Wi-Fi выкл.' @('HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config|AutoConnectAllowedOEM|0') @('HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config|AutoConnectAllowedOEM|-')
Add-FTweakFn 'teredo' 'Teredo выкл.' { Get-FTeredoOff } { param($on) [void](Invoke-FNative 'netsh.exe' ('interface teredo set state ' + $(if ($on) { 'disabled' } else { 'default' }))) }

# ======================================================================
#  RESTORE: protection state, space, automatic points, removing points
# ======================================================================
$SRKey = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
function Get-FRestoreOn {
  $v = Get-FReg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients' '{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}'
  if (-not $v) { return $false }
  return ((@($v) -join ' ') -match [regex]::Escape($env:SystemDrive))
}
function Get-FShadowSpace {
  $r = @{ Used = [double]0; Max = [double]0; Cap = [double]0 }
  try {
    $vol = Get-CimInstance -ClassName Win32_Volume -Filter ("DriveLetter='" + $env:SystemDrive + "'") -ErrorAction Stop | Select-Object -First 1
    $r.Cap = [double]$vol.Capacity
    foreach ($s in @(Get-CimInstance -ClassName Win32_ShadowStorage -ErrorAction Stop)) {
      if ([string]$s.Volume.DeviceID -eq [string]$vol.DeviceID) { $r.Used = [double]$s.UsedSpace; $r.Max = [double]$s.MaxSpace }
    }
  } catch {}
  return $r
}
function Get-FRestoreInfo {
  $sp = Get-FShadowSpace
  return @{ On = [bool](Get-FRestoreOn); Used = $sp.Used; Max = $sp.Max; Cap = $sp.Cap; Drive = [string]$env:SystemDrive; Auto = (Get-FRestoreAuto) }
}
function Enable-FRestore {
  Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
  if (Get-FRestoreOn) { Show-Ok "Защита системы включена для диска $env:SystemDrive"; return $true }
  Show-Err 'Windows не дала включить защиту системы'; return $false
}
$RpSizes = @(3, 5, 10, 15)
function Get-FRestoreSpaceIdx {
  $sp = Get-FShadowSpace
  if ($sp.Cap -le 0 -or $sp.Max -le 0) { return -1 }
  $pc = $sp.Max * 100 / $sp.Cap
  for ($i = 0; $i -lt $RpSizes.Count; $i++) { if ([math]::Abs($pc - $RpSizes[$i]) -lt 0.8) { return $i } }
  return -1
}
function Set-FRestoreSpaceIdx([int]$i) {
  Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
  $d = $env:SystemDrive
  [void](Invoke-FNative 'vssadmin.exe' "resize shadowstorage /for=$d /on=$d /maxsize=$($RpSizes[$i])%")
}
function Get-FRestoreAuto {
  $t = Get-ScheduledTask -TaskPath '\WinWeaver\' -TaskName 'Restore point' -ErrorAction SilentlyContinue
  if (-not $t) { return 0 }
  if (@($t.Triggers) | Where-Object { [string]$_.CimClass.CimClassName -like '*Weekly*' }) { return 1 }
  return 2
}
function Set-FRestoreAuto([int]$i) {
  Unregister-ScheduledTask -TaskName 'Restore point' -TaskPath '\WinWeaver\' -Confirm:$false -ErrorAction SilentlyContinue
  if ($i -le 0) { return }
  Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
  Set-FReg $SRKey 'SystemRestorePointCreationFrequency' 0
  try {
    $act = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -Command `"Checkpoint-Computer -Description 'WinWeaver auto' -RestorePointType MODIFY_SETTINGS`""
    $trg = if ($i -eq 1) { New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At '12:00' } else { New-ScheduledTaskTrigger -Daily -At '12:00' }
    $prn = New-ScheduledTaskPrincipal -UserId 'S-1-5-18' -RunLevel Highest
    $set = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName 'Restore point' -TaskPath '\WinWeaver\' -Action $act -Trigger $trg -Principal $prn -Settings $set -Force -ErrorAction Stop | Out-Null
  } catch { Show-Err "Задача не создана: $($_.Exception.Message)" }
}
function Initialize-FSR {
  if (-not ('WinWeaverNative.SR' -as [type])) {
    Add-Type -Namespace 'WinWeaverNative' -Name 'SR' -MemberDefinition '[DllImport("srclient.dll")] public static extern int SRRemoveRestorePoint(int index);' -ErrorAction Stop
  }
}
function Remove-FRestorePoint([int]$seq) {
  try { Initialize-FSR; $rc = [WinWeaverNative.SR]::SRRemoveRestorePoint($seq) } catch { $rc = -1 }
  if ($rc -eq 0) { Write-FLog "RESTORE removed $seq"; Show-Ok "Точка №$seq удалена"; return $true }
  Show-Err "Не удалось удалить точку №$seq (код $rc)"; return $false
}
function Remove-FOldRestorePoints {
  $n = 0
  try { Initialize-FSR } catch { Show-Err 'Удаление точек недоступно'; return 0 }
  foreach ($p in @(Get-FRestorePoints | Select-Object -Skip 1)) { if ([WinWeaverNative.SR]::SRRemoveRestorePoint([int]$p.SequenceNumber) -eq 0) { $n++ } }
  Write-FLog "RESTORE removed old: $n"
  Show-Ok "Удалено старых точек: $n. Последняя осталась"
  return $n
}
Add-FTweak 'rpfreq' 'Точки восстановления без лимита «раз в сутки»' @("$SRKey|SystemRestorePointCreationFrequency|0") @("$SRKey|SystemRestorePointCreationFrequency|-")
Add-FTweakFnChoice 'rpauto' 'Автоматические точки' { Get-FRestoreAuto } { param($i) Set-FRestoreAuto $i } @('Выкл.', 'Раз в неделю', 'Каждый день')
Add-FTweakFnChoice 'rpspace' 'Место под точки' { Get-FRestoreSpaceIdx } { param($i) Set-FRestoreSpaceIdx $i } @('3%', '5%', '10%', '15%')

# ---- restore points (plain data for the UI) ----
function Get-FRestoreList {
  $r = @()
  foreach ($p in (Get-FRestorePoints)) {
    $ct = [string]$p.CreationTime; $dt = $ct
    if ($ct -match '^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})') { $dt = "$($Matches[3]).$($Matches[2]).$($Matches[1])  $($Matches[4]):$($Matches[5])" }
    $iso = ''
    if ($ct -match '^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})') {
      # WMI time is UTC: convert to local
      try { $iso = ([datetime]::SpecifyKind([datetime]::ParseExact($ct.Substring(0, 14), 'yyyyMMddHHmmss', $null), 'Utc')).ToLocalTime().ToString('s'); $l = [datetime]$iso; $dt = $l.ToString('dd.MM.yyyy  HH:mm') } catch {}
    }
    $r += @{ Seq = [int]$p.SequenceNumber; Date = $dt; Desc = [string]$p.Description; Iso = $iso; Type = [int]$p.RestorePointType }
  }
  return ,$r
}
function New-FRestorePointNow {
  Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
  Set-FReg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' 'SystemRestorePointCreationFrequency' 0
  try { Checkpoint-Computer -Description "WinWeaver $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Show-Ok 'Точка восстановления создана'; return $true }
  catch { Show-Err "Точка не создана: $($_.Exception.Message)"; return $false }
}

# ---- one-click profiles ----
function Invoke-FGameProfile {
  Start-FGroup 'Игровой профиль'
  try {
    Step-FSet 'Схема питания — WinWeaver' 'Get-FPowerIdx' 'Set-FPowerIdx $v' 0 'game'
    Step-FSet 'Игровой режим включён' "(Get-GameMode) -eq 'ВКЛ'" 'Set-GameMode $v' $true 'game'
    Step-FSet 'Фоновая запись игр выключена' "(Get-Dvr) -eq 'ВКЛ'" 'Set-Dvr $v' $false 'game'
    Step-FSet 'Ускорение мыши выключено' "(Get-MouseAcc) -eq 'ВКЛ'" 'Set-MouseAcc $v' $false 'game'
    Step-FTweaks @('windowed', 'sticky') 'game'
  } finally { Stop-FGroup }
  Show-Ok 'Игровой профиль применён'
}
function Invoke-FPrivacyAll {
  Start-FGroup 'Конфиденциальность: выключить всё'
  try {
    Step-FSet 'Телеметрия выключена' "(Get-Telemetry) -eq 'ВЫКЛ'" 'Set-Telemetry $v' $true 'privacy'
    Step-FSet 'Copilot и Recall выключены' "(Get-AI) -eq 'ВЫКЛ'" 'Set-AI $v' $true 'privacy'
    Step-FSet 'Реклама и рекомендации выключены' "(Get-Ads) -eq 'ВЫКЛ'" 'Set-Ads $v' $true 'privacy'
    Step-FSet 'Bing в поиске выключен' "(Get-Bing) -eq 'ВЫКЛ'" 'Set-Bing $v; Restart-FExplorer' $true 'privacy'
    Step-FTweaks @('tailored', 'feedback', 'activity', 'inking', 'speech', 'lockads', 'scoobe', 'syncads', 'searchhl') 'privacy'
  } finally { Stop-FGroup }
  Show-Ok 'Телеметрия, ИИ, реклама и Bing отключены'
}

# ---- index helpers for segmented controls ----
function Get-FSearchIdx { switch (Get-Search) { 'Значок' { 0 } 'Кнопка' { 0 } 'Поле поиска' { 1 } 'Скрыт' { 2 } default { -1 } } }
function Set-FSearchIdx([int]$i) { Set-Search (@(1, 2, 0)[$i]) }
function Get-FPowerIdx { switch (Get-Power) { 'WinWeaver' { 0 } 'Максимальная производительность' { 1 } 'Высокая производительность' { 2 } 'Сбалансированная' { 3 } default { -1 } } }
function Set-FPowerIdx([int]$i) { Set-Power (@('winweaver', 'ultimate', 'high', 'balanced')[$i]) }

# ---- panel autostart task (elevated, no UAC prompt) ----
# the task runs elevated, so it may only point to the copy in Program Files (writable by administrators only)
function Test-FInstalledExe {
  $pf = [Environment]::GetFolderPath('ProgramFiles')
  return ([bool]$WwExe -and $WwExe.StartsWith((Join-Path $pf 'WinWeaver\'), [StringComparison]::OrdinalIgnoreCase))
}
function Install-FPanelTask {
  if (-not (Test-FInstalledExe)) { Show-Warn 'Автозапуск доступен только для установленной версии (Настройки → Установить в Program Files)'; return $false }
  try {
    $act = New-ScheduledTaskAction -Execute $WwExe -Argument '/autostart'
    $trg = New-ScheduledTaskTrigger -AtLogOn
    $prn = New-ScheduledTaskPrincipal -GroupId 'S-1-5-32-545' -RunLevel Highest
    $set = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName 'WinWeaver' -TaskPath '\WinWeaver\' -Action $act -Trigger $trg -Principal $prn -Settings $set -Force -ErrorAction Stop | Out-Null
    return $true
  } catch { Write-FLog "TASK ERR $_"; return $false }
}
function Remove-FPanelTask {
  Unregister-ScheduledTask -TaskName 'WinWeaver' -TaskPath '\WinWeaver\' -Confirm:$false -ErrorAction SilentlyContinue
  foreach ($l in @((Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'WinWeaver.lnk'), (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'WinWeaver.lnk'))) { Remove-Item -Path $l -Force -ErrorAction SilentlyContinue }
  Show-Ok 'Автозапуск и ярлыки WinWeaver убраны'
}
function New-FShortcut {
  $ws = New-Object -ComObject WScript.Shell
  $l = $ws.CreateShortcut((Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'WinWeaver.lnk'))
  $l.TargetPath = $WwExe; $l.IconLocation = "$WwExe,0"; $l.Description = 'WinWeaver - настройка Windows 11'; $l.Save()
  $sm = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'WinWeaver.lnk'
  $m = $ws.CreateShortcut($sm); $m.TargetPath = $WwExe; $m.IconLocation = "$WwExe,0"; $m.Description = 'WinWeaver - настройка Windows 11'; $m.Save()
}
function New-FDesktopShortcut {
  try { New-FShortcut; Show-Ok 'Ярлыки созданы на Рабочем столе и в Пуске' } catch { Show-Err "Ярлык не создан: $($_.Exception.Message)" }
}

# ======================================================================
#  AUTO-TUNE: levels  (light = Базовая / optimal = Оптимальная / gamer = Максимальная)
#  optimal = the WeavOS set of changes for a clean Windows 11, applied to an installed system
#  gamer   = optimal + maximum trimming for games
#  every step is a separate entry in the change history (Get-FHistory / Undo-FHistory / Undo-FHistGroup)
# ======================================================================
$FLevelKey = 'HKLM:\SOFTWARE\WinWeaver'
$GameBk    = 'HKLM:\SOFTWARE\WinWeaver\GameBackup'
$FLevels   = @{ light = 'Базовая'; optimal = 'Оптимальная'; gamer = 'Максимальная' }
$FLevelText = @{
  light   = 'Приватность без удаления программ: телеметрия, Copilot и Recall, реклама и Bing выключены, автошифрование BitLocker запрещено, обои и название WeavOS.'
  optimal = 'Чистая и быстрая Windows: всё из базовой, удаление OneDrive и предустановленного мусора, тёмная тема, классическое меню, чистая панель задач, выбранный браузер вместо Edge, обои и название WeavOS. Перед началом создаётся точка восстановления.'
  gamer   = 'Всё из оптимальной + максимум FPS: питание, приоритеты и таймер для игр, без MPO, ускорения мыши, анимаций, фоновых приложений, индексирования и лишних служб. Античиты работают — защита ядра (HVCI/VBS) не трогается. Ради скорости жертвуются удобства: поиск, уведомления части приложений, гибернация. Игровые твики откатываются в «Истории».'
}
$GameSvcs = [ordered]@{ SysMain = 4; MapsBroker = 4; lfsvc = 4; RetailDemo = 4; WerSvc = 4; DoSvc = 3 }

# runs a console program without a window, writes its output to the log, returns the exit code
function Invoke-FNative([string]$exe, [string]$argLine) {
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $exe
  $psi.Arguments = $argLine
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  try { $enc = [System.Text.Encoding]::GetEncoding([System.Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage); $psi.StandardOutputEncoding = $enc; $psi.StandardErrorEncoding = $enc } catch {}
  try { $p = [System.Diagnostics.Process]::Start($psi) } catch { Write-FLog "    cannot start $exe : $($_.Exception.Message)"; return -1 }
  $so = $p.StandardOutput.ReadToEndAsync()
  $se = $p.StandardError.ReadToEndAsync()
  $p.WaitForExit()
  $txt = ($so.Result + "`n" + $se.Result) -replace "`r(?!`n)", "`n"
  foreach ($l in ($txt -split "`r?`n")) { $l = $l.Trim(); if ($l) { Write-FLog ("    " + $l.Substring(0, [Math]::Min(300, $l.Length))) } }
  return $p.ExitCode
}
function Write-FFile([string]$path, [string]$text, [bool]$utf8bom = $false) {
  $d = Split-Path $path -Parent
  if ($d -and -not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
  $text = $text -replace "`r?`n", "`r`n"
  if ($utf8bom) { $enc = New-Object System.Text.UTF8Encoding($true) } else { $enc = New-Object System.Text.ASCIIEncoding }
  [System.IO.File]::WriteAllText($path, $text, $enc)
}

# ---- building blocks ----
# WeavOS wallpapers: several variants are built into the program, $EngineUI.Wallpaper(i) returns the JPEG bytes
$WeavWallDir  = "$env:WINDIR\Web\Wallpaper\WeavOS"
$WeavWallFile = "$WeavWallDir\WeavOS.jpg"
function Save-FWallpaperFile([int]$variant = 0) {
  if (-not (Test-Path $WeavWallDir)) { New-Item -ItemType Directory -Path $WeavWallDir -Force | Out-Null }
  $bytes = $null
  try { if ($EngineUI) { $bytes = $EngineUI.Wallpaper($variant) } } catch {}
  if ($bytes -and $bytes.Length -gt 0) { [System.IO.File]::WriteAllBytes($WeavWallFile, [byte[]]$bytes) }
  return (Test-Path $WeavWallFile)
}
function Update-FWallpaper {
  $wp = [string](Get-FReg 'HKCU:\Control Panel\Desktop' 'Wallpaper')
  [void][WNative]::SpiString(20, 0, $wp, 3)
}
function Set-FWallpaper([int]$variant = 0) {
  if (-not (Save-FWallpaperFile $variant)) { Show-Warn 'Файл обоев WeavOS не найден'; return }
  $wp = $WeavWallFile
  Set-FReg "$CU\DesktopSpotlight\Settings" 'EnabledState' 0
  Set-FReg "$CU\Explorer\Wallpapers" 'BackgroundType' 0
  Set-FReg 'HKCU:\Control Panel\Desktop' 'Wallpaper' $wp 'String'
  Set-FReg 'HKCU:\Control Panel\Desktop' 'WallpaperStyle' '10' 'String'
  Set-FReg 'HKCU:\Control Panel\Desktop' 'TileWallpaper' '0' 'String'
  Remove-Item "$env:APPDATA\Microsoft\Windows\Themes\TranscodedWallpaper" -Force -ErrorAction SilentlyContinue
  [void][WNative]::SpiString(20, 0, $wp, 3)
  $csp = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP'
  Set-FReg $csp 'LockScreenImagePath' $wp 'String'
  Set-FReg $csp 'LockScreenImageUrl' $wp 'String'
  Set-FReg $csp 'LockScreenImageStatus' 1
  Show-Ok 'Обои WeavOS установлены'
}
# WeavOS name: shown in Settings -> System (manufacturer/model) and in the registered organization
function Set-FSystemName {
  $oem = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\OEMInformation'
  Set-FReg $oem 'Manufacturer' 'WeavOS' 'String'
  Set-FReg $oem 'Model' 'WeavOS' 'String'
  Set-FReg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' 'RegisteredOrganization' 'WeavOS' 'String'
  Show-Ok 'Название системы: WeavOS'
}
# computer name: 1-15 characters, latin letters, digits and hyphens, not only digits
function Test-FDeviceName([string]$n) { return ($n -match '^[A-Za-z0-9-]{1,15}$' -and $n -notmatch '^[0-9]+$' -and -not $n.StartsWith('-') -and -not $n.EndsWith('-')) }
function Get-FDeviceName { return [string]$env:COMPUTERNAME }
function Set-FDeviceName([string]$n) {
  if (-not (Test-FDeviceName $n)) { Show-Err 'Имя компьютера: только латиница, цифры и дефис, до 15 символов'; return $false }
  if ($n -ieq $env:COMPUTERNAME) { return $true }
  try { Rename-Computer -NewName $n -Force -ErrorAction Stop | Out-Null; $global:NeedReboot = $true; Show-Ok ('Имя компьютера: {0} (после перезагрузки)' -f $n); return $true }
  catch { Show-Err ('Не удалось переименовать компьютер: {0}' -f $_.Exception.Message); return $false }
}
# the account that is signed in on the console (the engine itself may run under another administrator)
function Get-FConsoleUser {
  $u = ''
  try { $u = [string](Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop).UserName } catch {}
  if (-not $u) { $u = "$env:COMPUTERNAME\$env:USERNAME" }
  return $u
}
# sets the sign-in password of the console user; only local accounts (Microsoft accounts change it online)
function Set-FUserPassword([string]$pw) {
  if (-not $pw) { return $false }
  $full = Get-FConsoleUser
  $parts = $full.Split('\')
  $name = $parts[-1]
  if ($parts.Count -gt 1 -and $parts[0] -ine $env:COMPUTERNAME) { Show-Warn 'Пароль не изменён: это доменная учётная запись'; return $false }
  $lu = $null
  try { $lu = Get-LocalUser -Name $name -ErrorAction Stop } catch {}
  if (-not $lu) { Show-Warn 'Пароль не изменён: учётная запись не найдена'; return $false }
  if ([string]$lu.PrincipalSource -eq 'MicrosoftAccount') { Show-Warn 'Пароль не изменён: вход через учётную запись Microsoft — её пароль меняется на account.microsoft.com'; return $false }
  try {
    Set-LocalUser -Name $name -Password (ConvertTo-SecureString -String $pw -AsPlainText -Force) -ErrorAction Stop
    Write-FLog "PASSWORD changed for $name"
    Show-Ok ('Пароль для входа задан: {0}' -f $name)
    return $true
  } catch { Show-Err 'Не удалось задать пароль: Windows отклонила его (проверь требования к сложности)'; Write-FLog "PASSWORD change failed for $name"; return $false }
}

# ---- browser instead of Edge ----
$FBrowsers = [ordered]@{
  firefox = @{ Name = 'Firefox'; Winget = 'Mozilla.Firefox'; Exe = @("$env:ProgramFiles\Mozilla Firefox\firefox.exe", "$env:LOCALAPPDATA\Mozilla Firefox\firefox.exe");
               Url = 'https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=ru'; File = 'FirefoxSetup.exe'; Args = '/S'; Signer = 'Mozilla Corporation' }
  chrome  = @{ Name = 'Google Chrome'; Winget = 'Google.Chrome'; Exe = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe", "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe");
               Url = 'https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi'; File = 'ChromeSetup.msi'; Args = '/qn /norestart'; Signer = 'Google LLC' }
  brave   = @{ Name = 'Brave'; Winget = 'Brave.Brave'; Exe = @("$env:ProgramFiles\BraveSoftware\Brave-Browser\Application\brave.exe", "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\Application\brave.exe");
               Url = 'https://github.com/brave/brave-browser/releases/latest/download/BraveBrowserStandaloneSilentSetup.exe'; File = 'BraveSetup.exe'; Args = ''; Signer = 'Brave Software' }
  yandex  = @{ Name = 'Яндекс Браузер'; Winget = 'Yandex.Browser'; Exe = @("$env:LOCALAPPDATA\Yandex\YandexBrowser\Application\browser.exe", "$env:ProgramFiles\Yandex\YandexBrowser\Application\browser.exe", "${env:ProgramFiles(x86)}\Yandex\YandexBrowser\Application\browser.exe"); Url = '' }
  operagx = @{ Name = 'Opera GX'; Winget = 'Opera.OperaGX'; Exe = @("$env:LOCALAPPDATA\Programs\Opera GX\opera.exe", "$env:ProgramFiles\Opera GX\opera.exe"); Url = '' }
}
# 7-Zip installers are not Authenticode-signed, so the direct download is pinned to an exact file hash.
# When updating the version, download the file and put its SHA-256 here.
$SevenZipUrl    = 'https://github.com/ip7z/7zip/releases/download/25.01/7z2501-x64.exe'
$SevenZipSha256 = '78AFA2A1C773CAF3CF7EDF62F857D2A8A5DA55FB0FFF5DA416074C0D28B2B55F'
function Get-FBrowserName([string]$key) { if ($FBrowsers.Contains($key)) { return $FBrowsers[$key].Name }; return 'Edge' }
function Find-FBrowserExe([string]$key) {
  if (-not $FBrowsers.Contains($key)) { return $null }
  foreach ($e in $FBrowsers[$key].Exe) { if ($e -and (Test-Path $e)) { return $e } }
  return $null
}
function Get-FWinget {
  $c = Get-Command winget.exe -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($c) { return $c.Source }
  $p = "$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe"
  if (Test-Path $p) { return $p }
  return $null
}
# downloads $url into the protected download folder; returns the path or $null
function Get-FDownload([string]$url, [string]$file) {
  if (-not (Test-Path $DlDir)) { New-Item -ItemType Directory -Path $DlDir -Force | Out-Null }
  $dl = Join-Path $DlDir $file
  Remove-Item -LiteralPath $dl -Force -ErrorAction SilentlyContinue
  $rc = Invoke-FNative 'curl.exe' ('-L --fail -s --proto =https --tlsv1.2 -o "{0}" "{1}"' -f $dl, $url)
  if ($rc -eq 0 -and (Test-Path -LiteralPath $dl)) { return $dl }
  Remove-Item -LiteralPath $dl -Force -ErrorAction SilentlyContinue
  return $null
}
# a downloaded installer may run only if its SHA-256 matches the pinned value, or (when no hash is pinned)
# it carries a valid Authenticode signature from the expected publisher
function Test-FInstaller([string]$path, [string]$sha256 = '', [string]$signer = '') {
  if ($sha256) {
    $h = ''
    try { $h = (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash } catch {}
    $ok = ($h -and $h -ieq $sha256)
    Write-FLog "VERIFY $path sha256=$h ok=$ok"
    return $ok
  }
  $sig = Get-AuthenticodeSignature -LiteralPath $path
  $ok = ($sig.Status -eq 'Valid' -and (-not $signer -or ($sig.SignerCertificate.Subject -match [regex]::Escape($signer))))
  Write-FLog "VERIFY $path signature=$($sig.Status) ok=$ok"
  return $ok
}
# generic silent app install: winget first, then a verified direct download. returns 'fresh' / 'present' / ''
function Install-FApp([string]$name, [string]$wingetId, [string[]]$exes, [string]$url = '', [string]$instArgs = '', [string]$sha256 = '', [string]$signer = '') {
  $has = { foreach ($e in $exes) { if (Test-Path $e) { return $true } }; return $false }
  if (& $has) { Write-FLog "APP $name already installed"; return 'present' }
  $wg = Get-FWinget
  if ($wg) {
    Show-Ok ('Устанавливаю {0} через winget…' -f $name)
    $rc = Invoke-FNative $wg ('install --id {0} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity' -f $wingetId)
    Write-FLog "APP winget $wingetId rc=$rc"
  }
  if (-not (& $has) -and $url) {
    Show-Ok ('Скачиваю {0}…' -f $name)
    $dl = Get-FDownload $url ([IO.Path]::GetFileName(($url -split '\?')[0]))
    if ($dl) {
      if (Test-FInstaller $dl $sha256 $signer) {
        if ($instArgs) { Start-Process -FilePath $dl -ArgumentList $instArgs -Wait } else { Start-Process -FilePath $dl -Wait }
      } else { Show-Err ('Установщик {0} не прошёл проверку подлинности — файл удалён' -f $name) }
      Remove-Item -LiteralPath $dl -Force -ErrorAction SilentlyContinue
    }
  }
  if (& $has) { Show-Ok ('{0} установлен' -f $name); return 'fresh' }
  Show-Warn ('{0} не установился (нет интернета?). Остальные шаги продолжаются' -f $name); return ''
}
function Install-FGameLibs {
  $wg = Get-FWinget
  if (-not $wg) { Show-Warn 'Библиотеки для игр: нет winget — пропущено'; return }
  $done = @()
  foreach ($id in 'Microsoft.VCRedist.2015+.x64', 'Microsoft.VCRedist.2015+.x86', 'Microsoft.DirectX') {
    Show-Ok ('Устанавливаю {0}…' -f $id)
    $rc = Invoke-FNative $wg ('install --id {0} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity' -f $id)
    Write-FLog "LIBS $id rc=$rc"
    if ($rc -eq 0) { $done += $id }
  }
  if ($done.Count) { Add-FHist 'Установлены библиотеки для игр (Visual C++, DirectX)' 'apps' '' $null 'Удалять их не нужно — их используют игры и программы' }
}
# returns 'fresh' (installed now), 'present' (was already installed) or '' (failed)
function Install-FBrowser([string]$key) {
  if (-not $FBrowsers.Contains($key)) { return '' }
  $b = $FBrowsers[$key]
  if (Find-FBrowserExe $key) { Write-FLog "BROWSER $key already installed"; return 'present' }
  $wg = Get-FWinget
  if ($wg) {
    Show-Ok ('Устанавливаю {0} через winget…' -f $b.Name)
    $rc = Invoke-FNative $wg ('install --id {0} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity' -f $b.Winget)
    Write-FLog "BROWSER winget $($b.Winget) rc=$rc"
  }
  if (-not (Find-FBrowserExe $key) -and $b.Url) {
    Show-Ok ('Скачиваю {0}…' -f $b.Name)
    $dl = Get-FDownload $b.Url $b.File
    if ($dl) {
      if (-not (Test-FInstaller $dl '' $b.Signer)) {
        Remove-Item $dl -Force -ErrorAction SilentlyContinue
        Show-Err ('Подпись установщика {0} недействительна — файл удалён' -f $b.Name)
      } else {
        if ($dl -like '*.msi') { Start-Process -FilePath 'msiexec.exe' -ArgumentList ('/i "{0}" {1}' -f $dl, $b.Args) -Wait }
        elseif ($b.Args) { Start-Process -FilePath $dl -ArgumentList $b.Args -Wait }
        else { Start-Process -FilePath $dl -Wait }
        Remove-Item $dl -Force -ErrorAction SilentlyContinue
      }
    }
  }
  $exe = Find-FBrowserExe $key
  if (-not $exe) { Show-Warn ('{0} не установился (нет интернета?). Edge оставлен, остальные шаги продолжаются' -f $b.Name); return '' }
  if ($key -eq 'firefox') {
    Write-FFile ((Split-Path $exe -Parent) + '\distribution\policies.json') '{ "policies": { "DisableTelemetry": true, "DisableFirefoxStudies": true, "DontCheckDefaultBrowser": true, "DisablePocket": true } }'
  }
  Show-Ok ('{0} установлен' -f $b.Name)
  return 'fresh'
}
function Find-FStartLnk([string]$exe) {
  if (-not $exe) { return $null }
  $ws = New-Object -ComObject WScript.Shell
  foreach ($root in @([Environment]::GetFolderPath('CommonPrograms'), [Environment]::GetFolderPath('Programs'))) {
    if (-not $root) { continue }
    foreach ($f in @(Get-ChildItem -Path $root -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue)) {
      try { if ($ws.CreateShortcut($f.FullName).TargetPath -ieq $exe) { return $f.FullName } } catch {}
    }
  }
  return $null
}
function Set-FTaskbarPins([string]$key) {
  $exe = Find-FBrowserExe $key
  if (-not $exe) { Write-FLog 'PINS skipped: browser not found'; return }
  $lnk = Find-FStartLnk $exe
  if (-not $lnk) {
    $lnk = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) ((Get-FBrowserName $key) + '.lnk')
    $ws = New-Object -ComObject WScript.Shell; $l = $ws.CreateShortcut($lnk); $l.TargetPath = $exe; $l.Save()
  }
  $xmlLnk = [Security.SecurityElement]::Escape($lnk)
  Write-FFile "$env:WINDIR\OEM\TaskbarLayoutModification.xml" @"
<?xml version="1.0" encoding="utf-8"?>
<LayoutModificationTemplate xmlns="http://schemas.microsoft.com/Start/2014/LayoutModification" xmlns:defaultlayout="http://schemas.microsoft.com/Start/2014/FullDefaultLayout" xmlns:start="http://schemas.microsoft.com/Start/2014/StartLayout" xmlns:taskbar="http://schemas.microsoft.com/Start/2014/TaskbarLayout" Version="1">
  <CustomTaskbarLayoutCollection PinListPlacement="Replace">
    <defaultlayout:TaskbarLayout>
      <taskbar:TaskbarPinList>
        <taskbar:DesktopApp DesktopApplicationLinkPath="$xmlLnk" />
      </taskbar:TaskbarPinList>
    </defaultlayout:TaskbarLayout>
  </CustomTaskbarLayoutCollection>
</LayoutModificationTemplate>
"@ $true
  $pe = 'HKCU:\Software\Policies\Microsoft\Windows\Explorer'
  Set-FReg $pe 'StartLayoutFile' "$env:WINDIR\OEM\TaskbarLayoutModification.xml" 'ExpandString'
  Set-FReg $pe 'LockedStartLayout' 1
  Remove-Item "$CU\Explorer\Taskband" -Recurse -Force -ErrorAction SilentlyContinue
  $defer = $global:FDeferExplorer; $global:FDeferExplorer = $false
  Restart-FExplorer
  $global:FDeferExplorer = $defer
  Start-Sleep -Seconds 6
  Remove-FReg $pe 'StartLayoutFile'
  Remove-FReg $pe 'LockedStartLayout'
}
function Test-FSystemSsd {
  try {
    $d = Get-Partition -DriveLetter ($env:SystemDrive.TrimEnd(':')) -ErrorAction Stop | Get-Disk -ErrorAction Stop | Get-PhysicalDisk -ErrorAction Stop
    return ($d.MediaType -eq 'SSD')
  } catch { return $false }
}

# ---- gaming blocks (each one reversible) ----
function Set-FGameServices([bool]$off) {
  if ($off -and -not (Test-Path $GameBk)) { New-Item -Path $GameBk -Force | Out-Null }
  $ssd = Test-FSystemSsd
  foreach ($n in $GameSvcs.Keys) {
    if ($off) {
      if ($n -eq 'SysMain' -and -not $ssd) { continue }
      $cur = Get-FSvcStart $n
      if ($null -eq $cur) { continue }
      if ($null -eq (Get-FReg $GameBk "Svc_$n")) { Set-FReg $GameBk "Svc_$n" $cur }
      Stop-Service -Name $n -Force -ErrorAction SilentlyContinue
      [void](Set-FSvcStart $n $GameSvcs[$n])
    } else {
      $v = Get-FReg $GameBk "Svc_$n"
      if ($null -ne $v) { [void](Set-FSvcStart $n $v) }
    }
  }
}
function Set-FPowerExtras {
  [void](Invoke-FNative 'powercfg.exe' '/setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0')
  [void](Invoke-FNative 'powercfg.exe' '/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0')
  [void](Invoke-FNative 'powercfg.exe' '/setactive SCHEME_CURRENT')
}
function Set-FGamePerf([bool]$on) {
  $pc = 'HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl'
  $mm = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile'
  $gm = "$mm\Tasks\Games"
  $gcs = 'HKCU:\System\GameConfigStore'
  if ($on) {
    Set-FReg $pc 'Win32PrioritySeparation' 38
    Set-FReg $mm 'SystemResponsiveness' 0
    Save-FRegPrev $mm 'NetworkThrottlingIndex'
    [void](Invoke-FNative 'reg.exe' 'add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" /v NetworkThrottlingIndex /t REG_DWORD /d 4294967295 /f')
    Set-FReg $gm 'GPU Priority' 8
    Set-FReg $gm 'Priority' 6
    Set-FReg $gm 'Scheduling Category' 'High' 'String'
    Set-FReg $gm 'SFIO Priority' 'High' 'String'
    Set-FReg $gcs 'GameDVR_FSEBehaviorMode' 2
    Set-FReg $gcs 'GameDVR_HonorUserFSEBehaviorMode' 1
    Set-FReg $gcs 'GameDVR_DXGIHonorFSEWindowsCompatible' 1
    Set-FReg "$HKP\Windows\GameDVR" 'AllowGameDVR' 0
    Set-FReg "$HKP\Windows\DeliveryOptimization" 'DODownloadMode' 0
    Set-FReg "$HKP\Windows\AppPrivacy" 'LetAppsRunInBackground' 2
    Set-FReg "$CU\BackgroundAccessApplications" 'GlobalUserDisabled' 1
    Set-FReg "$HKP\Dsh" 'AllowNewsAndInterests' 0
  } else {
    Set-FReg $pc 'Win32PrioritySeparation' 2
    Set-FReg $mm 'SystemResponsiveness' 20
    Set-FReg $mm 'NetworkThrottlingIndex' 10
    Set-FReg $gm 'GPU Priority' 8
    Set-FReg $gm 'Priority' 2
    Set-FReg $gm 'Scheduling Category' 'Medium' 'String'
    Set-FReg $gm 'SFIO Priority' 'Normal' 'String'
    Remove-FReg $gcs 'GameDVR_FSEBehaviorMode'
    Remove-FReg $gcs 'GameDVR_HonorUserFSEBehaviorMode'
    Remove-FReg $gcs 'GameDVR_DXGIHonorFSEWindowsCompatible'
    Remove-FReg "$HKP\Windows\GameDVR" 'AllowGameDVR'
    Remove-FReg "$HKP\Windows\DeliveryOptimization" 'DODownloadMode'
    Remove-FReg "$HKP\Windows\AppPrivacy" 'LetAppsRunInBackground'
    Set-FReg "$CU\BackgroundAccessApplications" 'GlobalUserDisabled' 0
    Remove-FReg "$HKP\Dsh" 'AllowNewsAndInterests'
  }
}
function Set-FVisualFx([bool]$perf) {
  $d = 'HKCU:\Control Panel\Desktop'
  if ($perf) {
    Set-FReg "$CU\Explorer\VisualEffects" 'VisualFXSetting' 2
    Set-FReg $d 'UserPreferencesMask' ([byte[]](0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00)) 'Binary'
    Set-FReg $d 'FontSmoothing' '2' 'String'
    Set-FReg $d 'MenuShowDelay' '0' 'String'
    Set-FReg "$d\WindowMetrics" 'MinAnimate' '0' 'String'
    Set-FReg $ADV 'TaskbarAnimations' 0
    Set-FReg $ADV 'ListviewAlphaSelect' 0
    Set-FReg $ADV 'ListviewShadow' 0
    Set-FReg "$CU\Themes\Personalize" 'EnableTransparency' 0
  } else {
    Set-FReg "$CU\Explorer\VisualEffects" 'VisualFXSetting' 0
    Set-FReg $d 'UserPreferencesMask' ([byte[]](0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00)) 'Binary'
    Set-FReg $d 'MenuShowDelay' '400' 'String'
    Set-FReg "$d\WindowMetrics" 'MinAnimate' '1' 'String'
    Set-FReg $ADV 'TaskbarAnimations' 1
    Set-FReg $ADV 'ListviewAlphaSelect' 1
    Set-FReg $ADV 'ListviewShadow' 1
    Set-FReg "$CU\Themes\Personalize" 'EnableTransparency' 1
  }
  Send-FSettingChange 'ImmersiveColorSet'
}
function Save-FGamePrev {
  if (-not (Test-Path $GameBk)) { New-Item -Path $GameBk -Force | Out-Null }
  if ($null -ne (Get-FReg $GameBk 'Prev_Saved')) { return }
  Set-FReg $GameBk 'Prev_Hvci' $(if (Get-HvciWanted) { 1 } else { 0 })
  $h = Get-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode'
  Set-FReg $GameBk 'Prev_Hags' $(if ($null -eq $h) { -1 } else { [int]$h })
  Set-FReg $GameBk 'Prev_FastBoot' $(if ((Get-FastBoot) -eq 'ВКЛ') { 1 } else { 0 })
  Set-FReg $GameBk 'Prev_Saved' 1
}
function Undo-FGameMax {
  Show-Ok 'Откатываю игровые твики…'
  Set-FGameServices $false
  Set-Power 'balanced'
  Set-FGamePerf $false
  Set-FVisualFx $false
  $v = Get-FReg $GameBk 'Prev_Hvci'; if ($null -ne $v) { Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' 'Enabled' ([int]$v) }
  $v = Get-FReg $GameBk 'Prev_Hags'
  if ($null -ne $v) { if ([int]$v -lt 0) { Remove-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' } else { Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' ([int]$v) } }
  $v = Get-FReg $GameBk 'Prev_FastBoot'; if ($null -ne $v -and [int]$v -eq 1) { Set-FastBoot $true }
  Set-MouseAcc $true
  Remove-Item $GameBk -Recurse -Force -ErrorAction SilentlyContinue
  if ((Get-FReg $FLevelKey 'Level') -eq 'gamer') { Set-FReg $FLevelKey 'Level' 'optimal' 'String' }
  $global:NeedReboot = $true
  Restart-FExplorer
  Show-Ok 'Игровые твики откачены (часть изменений — после перезагрузки)'
}

# ---- plans ----
# every step writes its own history entry, so the auto-tune can be rolled back step by step
function Step-FSet([string]$title, [string]$get, [string]$set, $value, [string]$cat) { Invoke-FSetting -Title $title -Get $get -Set $set -Value $value -Cat $cat }
function Step-FTweaks([string[]]$ids, [string]$cat) {
  foreach ($id in $ids) {
    $t = $FTweaks[$id]; if (-not $t) { continue }
    Step-FSet $t.T "Get-FTweak '$id'" "Set-FTweak '$id' `$v" $true $cat
  }
}
function Get-FPlan([string]$lvl, [string]$browser, [string]$opts = '') {
  $p = @()
  $op = @(($opts.Split(';')[0]).Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
  $def = 0; if ($opts -match 'def=(\d)') { $def = [int]$Matches[1] }
  $wpv = 0; if ($opts -match 'wp=(\d+)') { $wpv = [int]$Matches[1] }
  $full = ($lvl -ne 'light')
  if ($full) { $p += @{ N = 'Точка восстановления'; S = { if (New-FRestorePointNow) { Add-FHist 'Создана точка восстановления' 'restore' '' $null 'К ней можно откатить всю систему в разделе «Восстановление»' } } } }
  $p += @{ N = 'Телеметрия';              S = { Step-FSet 'Телеметрия выключена' "(Get-Telemetry) -eq 'ВЫКЛ'" 'Set-Telemetry $v' $true 'privacy' } }
  $p += @{ N = 'Copilot и Recall';        S = { Step-FSet 'Copilot и Recall выключены' "(Get-AI) -eq 'ВЫКЛ'" 'Set-AI $v' $true 'privacy' } }
  $p += @{ N = 'Реклама и рекомендации';  S = { Step-FSet 'Реклама и рекомендации выключены' "(Get-Ads) -eq 'ВЫКЛ'" 'Set-Ads $v' $true 'privacy' } }
  $p += @{ N = 'Bing в поиске';           S = { Step-FSet 'Bing в поиске выключен' "(Get-Bing) -eq 'ВЫКЛ'" 'Set-Bing $v; Restart-FExplorer' $true 'privacy' } }
  $p += @{ N = 'Сбор данных и советы';     S = { Step-FTweaks @('tailored', 'feedback', 'activity', 'inking', 'speech') 'privacy' } }
  $p += @{ N = 'Реклама в системе';        S = { Step-FTweaks @('lockads', 'scoobe', 'syncads', 'searchhl', 'startiris') 'privacy' } }
  $p += @{ N = 'Автошифрование BitLocker'; S = { Invoke-FTracked 'Запрещено автошифрование BitLocker' "Set-FReg 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption' 1" -Cat 'protect' } }
  if ($full) {
    $p += @{ N = 'Мусорные приложения';   S = { $global:FJunkRemoved = 0; Remove-Junk; if ($global:FJunkRemoved -gt 0) { Add-FHist ('Удалены встроенные приложения: ' + $global:FJunkRemoved) 'apps' '' $null 'Вернуть можно из Microsoft Store' } } }
    $p += @{ N = 'OneDrive';              S = { if ((Get-OneDrive) -ne 'удалён') { Invoke-FTracked 'Удалён OneDrive' 'Remove-OneDrive' -Cat 'apps' -NoUndo -Note 'Установщик OneDrive можно скачать с сайта Microsoft' } } }
    $p += @{ N = 'Тёмная тема';           S = { Step-FSet 'Тёмная тема' "(Get-Theme) -eq 'Тёмная'" 'Set-Theme $v' $true 'ui' } }
    $p += @{ N = 'Классическое меню';     S = { Step-FSet 'Классическое контекстное меню' "(Get-Menu) -like 'Класс*'" 'Set-Menu $v' $true 'ui' } }
    $p += @{ N = 'Поиск значком';         S = { Step-FSet 'Поиск на панели задач — значок' 'Get-FSearchIdx' 'Set-FSearchIdx $v' 0 'ui' } }
    $p += @{ N = 'Представление задач';   S = { Step-FSet 'Кнопка «Представление задач» скрыта' "(Get-TaskView) -eq 'ВКЛ'" 'Set-TaskView $v' $false 'ui' } }
    $p += @{ N = 'Виджеты и Copilot';     S = { Step-FTweaks @('widgets') 'ui'; if ([Environment]::OSVersion.Version.Build -lt 26100) { Step-FTweaks @('teamschat') 'ui' } ; Invoke-FTracked 'Кнопка Copilot скрыта' "Set-FReg `$ADV 'ShowCopilotButton' 0" -Cat 'ui' } }
    $p += @{ N = 'Обновления';            S = { Step-FTweaks @('dop2p', 'noreboot') 'update' } }
    $p += @{ N = 'Защита сети';           S = { Step-FTweaks @('doh', 'llmnr', 'hotspot') 'net' } }
    $p += @{ N = '«Завершить задачу»';    S = { Step-FSet '«Завершить задачу» в меню панели задач' "(Get-EndTask) -eq 'ВКЛ'" 'Set-EndTask $v' $true 'ui' } }
  }
  if ($op -notcontains 'nowall') { $p += @{ N = 'Обои WeavOS'; S = { Invoke-FTracked 'Обои WeavOS на рабочем столе и экране блокировки' "Set-FWallpaper $wpv" -Undo 'Update-FWallpaper' -KeepReg -Cat 'ui' }.GetNewClosure() } }
  if ($op -notcontains 'noname') { $p += @{ N = 'Название WeavOS'; S = { Invoke-FTracked 'Название системы — WeavOS' 'Set-FSystemName' -Cat 'ui' } } }
  if ($browser -and $browser -ne 'keep') {
    $p += @{ N = (Get-FBrowserName $browser); S = {
        $r = Install-FBrowser $browser
        $global:FBrowserOk = [bool]$r
        if ($r -eq 'fresh') { $global:FFreshBrowser = Get-FBrowserName $browser; Add-FHist ('Установлен ' + (Get-FBrowserName $browser)) 'apps' '' $null 'Удалить можно в «Параметры → Приложения»' }
      } }
    $p += @{ N = 'Microsoft Edge';        S = {
        if (-not $global:FBrowserOk) { Write-FLog 'EDGE kept: no replacement browser'; return }
        if ((Get-Edge) -ne 'удалён') { Invoke-FTracked 'Удалён Microsoft Edge' 'Remove-Edge' -Cat 'apps' -NoUndo -Note 'WebView2 оставлен. Edge можно скачать заново с microsoft.com/edge' }
        else { Set-FReg "$HKP\EdgeUpdate" 'InstallDefault' 0 }
      } }
  }
  if ($full) {
    $p += @{ N = 'Ярлыки WinWeaver';   S = { Invoke-FTracked 'Ярлыки WinWeaver на Рабочем столе и в Пуске' "New-FShortcut; if ((Get-FReg 'HKCU:\Software\WinWeaver' 'AutostartChoice') -eq 'yes') { [void](Install-FPanelTask) }" -Undo "Remove-FPanelTask" -Cat 'app' } }
  }
  if ($browser -and $browser -ne 'keep') {
    $p += @{ N = 'Закреп на панели задач'; S = { if ($global:FBrowserOk) { Set-FTaskbarPins $browser; Add-FHist ('На панели задач закреплён ' + (Get-FBrowserName $browser)) 'ui' '' $null 'Закрепы меняются правой кнопкой по значку' } } }
  }
  if ($lvl -eq 'optimal') {
    # RULE for «Оптимальная»: as responsive and as fast in games as possible, but nothing that can break
    # everyday work — video editing, 3D, DAWs, streaming, games with anti-cheat. So: no HVCI/VBS/Spectre,
    # no disabled services/indexing/background apps/SysMain, no FSO/MPO/dynamic tick, mouse is not touched.
    # Laptops keep their power plan and power throttling (battery and heat); kernel paging only with 16+ GB.
    $p += @{ N = 'Игры без помех';        S = { Step-FSet 'Игровой режим включён' "(Get-GameMode) -eq 'ВКЛ'" 'Set-GameMode $v' $true 'game'; Step-FSet 'Фоновая запись игр выключена' "(Get-Dvr) -eq 'ВКЛ'" 'Set-Dvr $v' $false 'game'; Step-FTweaks @('windowed', 'gameprio', 'gamedvrpol') 'game'; Step-FSet 'Аппаратное планирование GPU включено' "(Get-Hags) -eq 'ВКЛ'" 'Set-Hags $v' $true 'game' } }
    $p += @{ N = 'Отклик системы';        S = { Step-FTweaks @('sysresp', 'startdelay', 'menudelay', 'netthrottle', 'lastaccess') 'game' } }
    $p += @{ N = 'Питание и память';      S = {
        $laptop = $false
        try { $laptop = [bool](Get-CimInstance Win32_Battery -ErrorAction Stop) } catch { }
        $ram = 0
        try { $ram = [math]::Round((Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).TotalPhysicalMemory / 1GB) } catch { }
        if (-not $laptop) {
          Step-FSet 'Схема питания — Максимальная производительность' 'Get-FPowerIdx' 'Set-FPowerIdx $v' 1 'game'
          Step-FTweaks @('pthrottle', 'timerres') 'game'
        } else { Write-FLog 'OPTIMAL: laptop — power plan and throttling kept' }
        if ($ram -ge 16) { Step-FTweaks @('pagingexec') 'game' }
      } }
  }
  if ($lvl -eq 'gamer') {
    # RULE for «Максимальная»: maximum FPS, anti-cheats (FACEIT, Vanguard, EAC, BattlEye) must keep working —
    # so HVCI/VBS/Spectre, Secure Boot, TPM, test-signing and Xbox services are never touched here.
    # Everyday comfort may be sacrificed: search index, background apps, animations, hibernation, power use.
    $p += @{ N = 'Резервная копия игровых настроек'; S = { Save-FGamePrev } }
    $p += @{ N = 'Схема питания';         S = { Step-FSet 'Схема питания — Максимальная производительность' 'Get-FPowerIdx' 'Set-FPowerIdx $v; if ($v -eq 1) { Set-FPowerExtras }' 1 'game' } }
    $p += @{ N = 'Игровой режим';         S = { Step-FSet 'Игровой режим включён' "(Get-GameMode) -eq 'ВКЛ'" 'Set-GameMode $v' $true 'game' } }
    $p += @{ N = 'Game DVR';              S = { Step-FSet 'Фоновая запись игр выключена' "(Get-Dvr) -eq 'ВКЛ'" 'Set-Dvr $v' $false 'game' } }
    $p += @{ N = 'Ускорение мыши';        S = { Step-FSet 'Ускорение мыши выключено' "(Get-MouseAcc) -eq 'ВКЛ'" 'Set-MouseAcc $v' $false 'game' } }
    $p += @{ N = 'GPU scheduling';        S = { Step-FSet 'Аппаратное планирование GPU включено' "(Get-Hags) -eq 'ВКЛ'" 'Set-Hags $v' $true 'game' } }
    $p += @{ N = 'Приоритет игр';         S = { Invoke-FTracked 'Приоритет игр, сети и без фоновых приложений' 'Set-FGamePerf $true' -Cat 'game' } }
    $p += @{ N = 'Визуальные эффекты';    S = { Invoke-FTracked 'Минимум анимаций и прозрачности' 'Set-FVisualFx $true' -Undo "Send-FSettingChange 'ImmersiveColorSet'" -KeepReg -Cat 'game' } }
    $p += @{ N = 'Лишние службы';         S = { Invoke-FTracked 'Выключены лишние службы (SysMain, карты, геолокация…)' 'Set-FGameServices $true' -Undo '$global:NeedReboot = $true' -KeepReg -Cat 'game' } }
    $p += @{ N = 'Отклик системы';        S = { Step-FTweaks @('startdelay', 'menudelay', 'netthrottle', 'sticky', 'windowed', 'fso', 'pthrottle', 'bgapps') 'game' } }
    $p += @{ N = 'Процессор для игр';     S = { Step-FSet 'Распределение процессорного времени — игры' "Get-FTweakIdx 'prisep'" "Set-FTweakIdx 'prisep' `$v" 2 'game'; Step-FTweaks @('gameprio', 'sysresp') 'game' } }
    $p += @{ N = 'Таймер и кадры';        S = { Step-FTweaks @('timerres', 'dyntick', 'mpo', 'gamedvrpol') 'game' } }
    $p += @{ N = 'Память и диск';         S = { Step-FTweaks @('sysmain', 'wsearch', 'memcomp', 'pagingexec', 'lastaccess', 'junksvc') 'game' } }
    $p += @{ N = 'Быстрый запуск';        S = { Step-FSet 'Быстрый запуск и гибернация выключены' "(Get-FastBoot) -eq 'ВКЛ'" 'Set-FastBoot $v' $false 'game' } }
    $p += @{ N = 'Версия Windows';        S = { Step-FSet 'Windows остаётся на текущей версии' "(Get-WU) -like 'остаётся*'" 'Set-WUStay $v' $true 'update' } }
  }
  if ($op -contains 'ext')  { $p += @{ N = 'Расширения файлов';  S = { Step-FSet 'Расширения файлов показываются' "(Get-Ext) -eq 'Показывать'" 'Set-Ext $v' $true 'ui' } } }
  if ($op -contains 'drv')  { $p += @{ N = 'Драйверы';           S = { Step-FTweaks @('wudrivers') 'update' } } }
  if ($def -eq 1) { $p += @{ N = 'Defender тише';  S = { Invoke-FTracked 'Defender: без отправки образцов и лишних уведомлений' "Set-FReg `"`$DefPol\Spynet`" 'SubmitSamplesConsent' 2; Set-FReg `"`$DefPol\Spynet`" 'SpynetReporting' 0; Set-FReg `$DefNtf 'DisableEnhancedNotifications' 1" -Cat 'protect' } } }
  if ($def -eq 2) { $p += @{ N = 'Defender';       S = { if (Get-Tamper) { Show-Warn 'Defender не выключен: сначала выключи «Защиту от подделки», затем повтори в разделе «Защита»'; Start-Process 'windowsdefender://threatsettings'; return }; Invoke-FTracked 'Defender: защита в реальном времени выключена' 'Set-DefenderOff $false' -Undo 'Set-DefenderOn' -KeepReg -Cat 'protect' } } }
  if ($op -contains '7zip') { $p += @{ N = '7-Zip';               S = { $r = Install-FApp '7-Zip' '7zip.7zip' @("$env:ProgramFiles\7-Zip\7z.exe") $SevenZipUrl '/S' $SevenZipSha256; if ($r -eq 'fresh') { Add-FHist 'Установлен 7-Zip' 'apps' '' $null 'Удалить можно в «Параметры → Приложения»' } } } }
  if ($op -contains 'vc')   { $p += @{ N = 'Библиотеки для игр'; S = { Install-FGameLibs } } }
  if ($op -contains 'temp') { $p += @{ N = 'Временные файлы';    S = { Invoke-FTracked 'Очищены временные файлы' 'Clear-FTemp' -Cat 'clean' -NoUndo } } }
  return ,$p
}
# step names for the "choose steps" list (same order as Invoke-FLevel runs them)
function Get-FPlanNames([string]$lvl, [string]$browser = '', [string]$opts = '') {
  if (-not $browser) { $browser = if ($lvl -eq 'light') { 'keep' } else { 'firefox' } }
  return ,@((Get-FPlan $lvl $browser $opts) | ForEach-Object { [string]$_.N })
}
# $dev / $pw: optional computer name and sign-in password from the WeavOS step (never logged or stored)
function Invoke-FLevel([string]$lvl, [string]$browser = '', [string]$opts = '', [string]$dev = '', [string]$pw = '') {
  if (-not $FLevels.ContainsKey($lvl)) { Show-Err 'Неизвестный уровень'; return $false }
  if (-not $browser) { $browser = if ($lvl -eq 'light') { 'keep' } else { 'firefox' } }
  if ($browser -ne 'keep' -and -not $FBrowsers.Contains($browser)) { $browser = 'keep' }
  if (-not $opts) { $opts = switch ($lvl) { 'light' { '7zip' } 'gamer' { '7zip,vc,ext,temp,drv' } default { '7zip,ext,temp' } } }
  $plan = Get-FPlan $lvl $browser $opts
  # steps the user unticked in the "choose steps" list: skip=2.5.9 (indexes in plan order)
  if ($opts -match 'skip=([0-9.]+)') {
    $skip = @($Matches[1].Split('.') | Where-Object { $_ } | ForEach-Object { [int]$_ })
    $keep = @(); for ($k = 0; $k -lt $plan.Count; $k++) { if ($skip -notcontains $k) { $keep += $plan[$k] } }
    $plan = $keep
  }
  if ($dev -and $dev -ine $env:COMPUTERNAME) {
    $oldName = $env:COMPUTERNAME
    $plan += @{ N = 'Имя компьютера'; S = { if (Set-FDeviceName $dev) { Add-FHist ('Имя компьютера: ' + $dev) 'ui' ("[void](Set-FDeviceName '" + $oldName.Replace("'", "''") + "')") $null 'Новое имя появится после перезагрузки' } }.GetNewClosure() }
  }
  if ($pw) {
    $plan += @{ N = 'Пароль для входа'; S = { if (Set-FUserPassword $pw) { Add-FHist 'Задан пароль для входа в Windows' 'protect' '' $null 'Сам пароль нигде не сохраняется' } }.GetNewClosure() }
  }
  $global:FFreshBrowser = ''
  $global:FBrowserOk = $false
  Write-FLog "=== LEVEL $lvl ($browser) START ==="
  $n = $plan.Count; $i = 0; $fail = @()
  Start-FGroup ('Автонастройка «{0}»' -f $FLevels[$lvl])
  try {
    foreach ($s in $plan) {
      $i++
      Show-Ok ('Автонастройка «{0}» [{1}/{2}]: {3}…' -f $FLevels[$lvl], $i, $n, $s.N)
      try { & $s.S | Out-Null } catch { $fail += $s.N; Write-FLog "STEP FAIL $($s.N): $($_.Exception.Message)" }
    }
  } finally {
    if ($global:FExplorerPending) { Show-Ok ('Автонастройка «{0}»: перезапуск Проводника…' -f $FLevels[$lvl]) }
    Stop-FGroup
  }
  Set-FReg $FLevelKey 'Level' $lvl 'String'
  Set-FReg $FLevelKey 'LevelDate' (Get-Date -Format 'dd.MM.yyyy HH:mm') 'String'
  Set-FReg $FLevelKey 'LevelBrowser' $browser 'String'
  Write-FLog "=== LEVEL $lvl DONE, failed: $($fail -join ', ') ==="
  $global:NeedReboot = $true   # policies, services, removed apps and the system name all settle only after a restart
  if ($fail.Count -gt 0) { Show-Warn ('Автонастройка выполнена, но не удалось: ' + ($fail -join ', ') + '. Подробности в журнале') }
  else { Show-Ok ('Автонастройка «{0}» выполнена. Чтобы всё вступило в силу, нужна перезагрузка' -f $FLevels[$lvl]) }
  if ($global:FFreshBrowser) {
    $bn = $global:FFreshBrowser
    if (Confirm-F ("$bn установлен. Windows не позволяет программам самим назначать браузер по умолчанию — открыть настройки, где нужно выбрать $bn?") 'Браузер по умолчанию' 'Открыть настройки' 'Позже') { Start-Process 'ms-settings:defaultapps' }
  }
  return $true
}
function Get-FLevelCard([string]$lvl) {
  $t = $FLevelText[$lvl]
  if ((Get-FReg $FLevelKey 'Level') -eq $lvl) { $t += "`nПрименён: " + (Get-FReg $FLevelKey 'LevelDate') }
  return $t
}
function Get-FLevelId { $l = Get-FReg $FLevelKey 'Level'; if ($l -and $FLevels.ContainsKey([string]$l)) { return [string]$l }; return '' }
function Get-FLevelNow {
  $l = Get-FLevelId
  if ($l) {
    $t = 'Последняя автонастройка: «{0}», {1}' -f $FLevels[$l], (Get-FReg $FLevelKey 'LevelDate')
    if (Test-Path $GameBk) { $t += '  •  игровые твики применены, их можно откатить' }
    return $t
  }
  return 'Автонастройка ещё не запускалась. Выбери уровень выше: перед изменениями создаётся точка восстановления'
}

# ======================================================================
#  WinWeaver 1.1: protection & updates overview, extra tweaks, one-click fixes,
#  disk cleanup by category, installed programs (uninstall + leftovers), flexible startup
# ======================================================================
$UX  = 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
$WUP = "$HKP\Windows\WindowsUpdate"
$SYSP = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
$PEXP = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer'
$MMG = "$MMProfile\Tasks\Games"
$MEMM = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'

# ---- protection ----
Add-FTweak 'smartscreen' 'SmartScreen для приложений и файлов' @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer|SmartScreenEnabled|Warn|String', "$HKP\Windows\System|EnableSmartScreen|-") @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer|SmartScreenEnabled|Off|String', "$HKP\Windows\System|EnableSmartScreen|0")
Add-FTweakFn 'pua' 'Блокировка нежелательных программ (PUA)' { [int](Get-MpPreference -ErrorAction Stop).PUAProtection -eq 1 } { param($on) Set-MpPreference -PUAProtection $(if ($on) { 1 } else { 0 }) -ErrorAction SilentlyContinue }
Add-FTweakFn 'samples' 'Не отправлять образцы файлов в Microsoft' { [int](Get-MpPreference -ErrorAction Stop).SubmitSamplesConsent -eq 2 } { param($on) Set-MpPreference -SubmitSamplesConsent $(if ($on) { 2 } else { 1 }) -ErrorAction SilentlyContinue }
Add-FTweak 'autorun' 'Автозапуск со съёмных дисков выкл.' @("$PEXP|NoDriveTypeAutoRun|255", "$PEXP|NoAutorun|1") @("$PEXP|NoDriveTypeAutoRun|-", "$PEXP|NoAutorun|-")
Add-FTweak 'remoteassist' 'Удалённый помощник выкл.' @('HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance|fAllowToGetHelp|0') @('HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance|fAllowToGetHelp|1')
Add-FTweakFn 'smb1' 'Протокол SMBv1 выкл.' { -not [bool](Get-SmbServerConfiguration -ErrorAction Stop).EnableSMB1Protocol } { param($on) Set-SmbServerConfiguration -EnableSMB1Protocol (-not $on) -Force -Confirm:$false -ErrorAction SilentlyContinue }
Add-FTweakChoice 'uac' 'Контроль учётных записей' @(@("$SYSP|EnableLUA|1", "$SYSP|ConsentPromptBehaviorAdmin|2", "$SYSP|PromptOnSecureDesktop|1"), @("$SYSP|EnableLUA|1", "$SYSP|ConsentPromptBehaviorAdmin|5", "$SYSP|PromptOnSecureDesktop|1"), @("$SYSP|EnableLUA|1", "$SYSP|ConsentPromptBehaviorAdmin|5", "$SYSP|PromptOnSecureDesktop|0"), @("$SYSP|EnableLUA|1", "$SYSP|ConsentPromptBehaviorAdmin|0", "$SYSP|PromptOnSecureDesktop|0")) @('Всегда', 'Как обычно', 'Без затемнения', 'Не спрашивать')
# ---- updates ----
Add-FTweakChoice 'wumode' 'Установка обновлений' @(@("$WUP\AU|NoAutoUpdate|-", "$WUP\AU|AUOptions|-"), @("$WUP\AU|NoAutoUpdate|0", "$WUP\AU|AUOptions|3"), @("$WUP\AU|NoAutoUpdate|0", "$WUP\AU|AUOptions|2")) @('Автоматически', 'Скачивать, ставить по кнопке', 'Только уведомлять')
Add-FTweakChoice 'wudefer' 'Отсрочка обновлений качества' @(@("$WUP|DeferQualityUpdates|-", "$WUP|DeferQualityUpdatesPeriodInDays|-"), @("$WUP|DeferQualityUpdates|1", "$WUP|DeferQualityUpdatesPeriodInDays|7"), @("$WUP|DeferQualityUpdates|1", "$WUP|DeferQualityUpdatesPeriodInDays|14"), @("$WUP|DeferQualityUpdates|1", "$WUP|DeferQualityUpdatesPeriodInDays|30")) @('Нет', '7 дней', '14 дней', '30 дней')
Add-FTweak 'wudrivers' 'Драйверы через Центр обновления выкл.' @("$WUP|ExcludeWUDriversInQualityUpdate|1") @("$WUP|ExcludeWUDriversInQualityUpdate|-")
Add-FTweak 'wuearly' 'Новые функции — не раньше всех' @("$UX|IsContinuousInnovationOptedIn|0") @("$UX|IsContinuousInnovationOptedIn|1")
Add-FTweak 'wunotify' 'Уведомлять о перезагрузке для обновлений' @("$UX|RestartNotificationsAllowed2|1") @("$UX|RestartNotificationsAllowed2|0")
Add-FTweak 'wumsupd' 'Обновлять другие продукты Microsoft' @("$UX|AllowMUUpdateService|1") @("$UX|AllowMUUpdateService|0")
# ---- performance: responsiveness ----
Add-FTweak 'sysresp' 'Приоритет активным программам (MMCSS)' @("$MMProfile|SystemResponsiveness|10") @("$MMProfile|SystemResponsiveness|20")
Add-FTweakChoice 'prisep' 'Распределение процессорного времени' @(@('HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl|Win32PrioritySeparation|2'), @('HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl|Win32PrioritySeparation|38'), @('HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl|Win32PrioritySeparation|42'))  @('Стандарт', 'Активные окна', 'Игры')
Add-FTweak 'winanim' 'Анимация сворачивания окон выкл.' @('HKCU:\Control Panel\Desktop\WindowMetrics|MinAnimate|0|String') @('HKCU:\Control Panel\Desktop\WindowMetrics|MinAnimate|1|String') -Rb
Add-FTweak 'hungapp' 'Быстрое закрытие зависших программ' @('HKCU:\Control Panel\Desktop|HungAppTimeout|2000|String', 'HKCU:\Control Panel\Desktop|WaitToKillAppTimeout|5000|String') @('HKCU:\Control Panel\Desktop|HungAppTimeout|-', 'HKCU:\Control Panel\Desktop|WaitToKillAppTimeout|-') -Rb
Add-FTweak 'fastoff' 'Быстрое выключение ПК' @('HKLM:\SYSTEM\CurrentControlSet\Control|WaitToKillServiceTimeout|2000|String') @('HKLM:\SYSTEM\CurrentControlSet\Control|WaitToKillServiceTimeout|5000|String') -Rb
Add-FTweak 'kbdrepeat' 'Быстрый повтор клавиш' @('HKCU:\Control Panel\Keyboard|KeyboardDelay|0|String', 'HKCU:\Control Panel\Keyboard|KeyboardSpeed|31|String') @('HKCU:\Control Panel\Keyboard|KeyboardDelay|1|String', 'HKCU:\Control Panel\Keyboard|KeyboardSpeed|31|String') -Rb
Add-FTweak 'hovertime' 'Быстрые подсказки при наведении' @('HKCU:\Control Panel\Mouse|MouseHoverTime|100|String') @('HKCU:\Control Panel\Mouse|MouseHoverTime|400|String') -Rb
# ---- performance: games ----
Add-FTweak 'gameprio' 'Высокий приоритет игр (MMCSS)' @("$MMG|Priority|6", "$MMG|Scheduling Category|High|String", "$MMG|SFIO Priority|High|String", "$MMG|GPU Priority|8") @("$MMG|Priority|2", "$MMG|Scheduling Category|Medium|String", "$MMG|SFIO Priority|Normal|String", "$MMG|GPU Priority|8")
Add-FTweak 'mpo' 'MPO (многоплоскостное наложение) выкл.' @('HKLM:\SOFTWARE\Microsoft\Windows\Dwm|OverlayTestMode|5') @('HKLM:\SOFTWARE\Microsoft\Windows\Dwm|OverlayTestMode|-') -Rb
Add-FTweak 'timerres' 'Точный системный таймер для игр' @('HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel|GlobalTimerResolutionRequests|1') @('HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel|GlobalTimerResolutionRequests|-') -Rb
Add-FTweakFn 'dyntick' 'Динамический тик выкл.' { ((& bcdedit.exe /enum '{current}') -join "`n") -match 'disabledynamictick\s+(Yes|Да)' } { param($on) if ($on) { [void](Invoke-FNative 'bcdedit.exe' '/set disabledynamictick yes') } else { [void](Invoke-FNative 'bcdedit.exe' '/deletevalue disabledynamictick') } } -Rb
$FXbox = @('XblAuthManager', 'XblGameSave', 'XboxNetApiSvc', 'XboxGipSvc')
Add-FTweakFn 'xboxsvc' 'Службы Xbox выкл.' { $n = 0; $k = 0; foreach ($s in $FXbox) { $v = Get-FSvcStart $s; if ($null -ne $v) { $n++; if ($v -eq 4) { $k++ } } }; ($n -gt 0 -and $k -eq $n) } { param($on) foreach ($s in $FXbox) { [void](Set-FSvcStart $s $(if ($on) { 4 } else { 3 })) } } -Rb
Add-FTweak 'gamedvrpol' 'Запись игр запрещена политикой' @("$HKP\Windows\GameDVR|AllowGameDVR|0") @("$HKP\Windows\GameDVR|AllowGameDVR|-")
# ---- performance: system ----
Add-FTweak 'pagingexec' 'Ядро системы не выгружается из памяти' @("$MEMM|DisablePagingExecutive|1") @("$MEMM|DisablePagingExecutive|0") -Rb
Add-FTweakFn 'memcomp' 'Сжатие памяти выкл.' { -not [bool](Get-MMAgent -ErrorAction Stop).MemoryCompression } { param($on) if ($on) { Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue } else { Enable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue } } -Rb
Add-FTweakFn 'lastaccess' 'Метка последнего доступа к файлам выкл.' { ((& fsutil.exe behavior query disablelastaccess) -join ' ') -match '=\s*[13]\b' } { param($on) [void](Invoke-FNative 'fsutil.exe' ('behavior set disablelastaccess ' + $(if ($on) { 1 } else { 2 }))) }
Add-FTweak 'shortnames' 'Короткие имена 8.3 выкл.' @('HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem|NtfsDisable8dot3NameCreation|1') @('HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem|NtfsDisable8dot3NameCreation|2')
Add-FTweak 'vbs' 'Виртуализация (VBS) выкл.' @('HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard|EnableVirtualizationBasedSecurity|0') @('HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard|EnableVirtualizationBasedSecurity|-') -Rb
Add-FTweak 'spectre' 'Защита от Spectre и Meltdown выкл.' @("$MEMM|FeatureSettingsOverride|3", "$MEMM|FeatureSettingsOverrideMask|3") @("$MEMM|FeatureSettingsOverride|-", "$MEMM|FeatureSettingsOverrideMask|-") -Rb
$FJunkSvc = [ordered]@{ MapsBroker = 2; RetailDemo = 3; dmwappushservice = 3; WMPNetworkSvc = 3; RemoteRegistry = 4; Fax = 3; WpcMonSvc = 3; PhoneSvc = 3 }
Add-FTweakFn 'junksvc' 'Лишние службы выкл.' { $n = 0; $k = 0; foreach ($s in $FJunkSvc.Keys) { $v = Get-FSvcStart $s; if ($null -ne $v) { $n++; if ($v -eq 4) { $k++ } } }; ($n -gt 0 -and $k -eq $n) } { param($on) foreach ($s in $FJunkSvc.Keys) { [void](Set-FSvcStart $s $(if ($on) { 4 } else { [int]$FJunkSvc[$s] })) } } -Rb
Add-FTweakFn 'reserved' 'Зарезервированное хранилище выкл.' { [string](Get-WindowsReservedStorageState -ErrorAction Stop).ReservedStorageState -eq 'Disabled' } { param($on) Set-WindowsReservedStorageState -State $(if ($on) { 'Disabled' } else { 'Enabled' }) -ErrorAction SilentlyContinue | Out-Null }
Add-FTweak 'storagesense' 'Контроль памяти (автоочистка)' @("$CU\StorageSense\Parameters\StoragePolicy|01|1") @("$CU\StorageSense\Parameters\StoragePolicy|01|0")

# ---- overviews for the "Privacy and protection" page ----
function Test-FSafe([scriptblock]$b) { try { return [bool](& $b) } catch { return $false } }
function Get-FPrivInfo {
  $trk = 0; $trkN = 0
  foreach ($x in @({ (Get-Telemetry) -eq 'ВЫКЛ' }, { (Get-AI) -eq 'ВЫКЛ' }, { (Get-Bing) -eq 'ВЫКЛ' })) { $trkN++; if (Test-FSafe $x) { $trk++ } }
  foreach ($id in 'location', 'tailored', 'speech', 'inking', 'activity', 'feedback') { $trkN++; if (Get-FTweak $id) { $trk++ } }
  $ad = 0; $adN = 1; if (Test-FSafe { (Get-Ads) -eq 'ВЫКЛ' }) { $ad++ }
  foreach ($id in 'lockads', 'searchhl', 'scoobe', 'syncads') { $adN++; if (Get-FTweak $id) { $ad++ } }
  $rtp = Get-Rtp
  $paused = $false; $pe = Get-FReg $UX 'PauseUpdatesExpiryTime'
  if ($pe) { try { $paused = ([datetime]::Parse([string]$pe, [Globalization.CultureInfo]::InvariantCulture)) -gt (Get-Date) } catch {} }
  $stay = (Get-FReg $WUP 'TargetReleaseVersion') -eq 1
  $left = ($trkN - $trk) + ($adN - $ad)
  $t = @(
    @{ L = 'СЛЕЖКА'; V = "$trk из $trkN"; H = $(if ($trk -eq $trkN) { 'всё выключено' } else { 'источников выключено' }); T = $(if ($trk -eq $trkN) { 'ok' } elseif ($trk -ge $trkN / 2) { 'info' } else { 'warn' }); P = [double]($trk / [math]::Max(1, $trkN)) },
    @{ L = 'РЕКЛАМА'; V = "$ad из $adN"; H = $(if ($ad -eq $adN) { 'рекламы нет' } else { 'мест без рекламы' }); T = $(if ($ad -eq $adN) { 'ok' } elseif ($ad -ge 2) { 'info' } else { 'warn' }); P = [double]($ad / [math]::Max(1, $adN)) },
    @{ L = 'ЗАЩИТА'; V = $(if ($rtp) { 'Defender вкл.' } else { 'Defender выкл.' }); H = $(if ($rtp) { 'защита в реальном времени' } else { 'без антивируса Microsoft' }); T = $(if ($rtp) { 'ok' } else { 'warn' }); P = -1.0 },
    @{ L = 'ОБНОВЛЕНИЯ'; V = $(if ($paused) { 'На паузе' } elseif ($stay) { 'Без новых версий' } else { 'Автоматически' }); H = $(if ($stay) { 'только исправления безопасности' } else { 'как решит Microsoft' }); T = 'info'; P = -1.0 }
  )
  $score = [double](($trk + $ad) / [math]::Max(1, $trkN + $adN))
  $title = if ($left -eq 0) { 'Windows почти ничего о тебе не знает' } elseif ($left -le 3) { 'Почти закрыто — осталось ' + $left } else { 'Windows собирает данные и показывает рекламу' }
  $sub = if ($left -eq 0) { 'Телеметрия, реклама и сбор данных выключены. Любой переключатель можно вернуть в «Истории»' } else { 'Открыто источников слежки и рекламы: ' + $left + '. Кнопка «Выключить всё» закроет основные одним действием' }
  return @{ Title = $title; Sub = $sub; Tone = $(if ($left -eq 0) { 'ok' } elseif ($left -le 3) { 'info' } else { 'warn' }); Score = $score; Tiles = $t }
}
function Get-FSecInfo {
  if ($script:FSecCache -and ((Get-Date) - $script:FSecTime).TotalSeconds -lt 10) { return $script:FSecCache }
  $t = @(); $bad = 0
  $mp = $null; try { $mp = Get-MpComputerStatus -ErrorAction Stop } catch {}
  if ($mp -and [bool]$mp.RealTimeProtectionEnabled) {
    $age = 0; try { $age = [int]$mp.AntivirusSignatureAge } catch {}
    $t += @{ L = 'АНТИВИРУС'; V = 'Работает'; H = $(if ($age -le 1) { 'базы свежие' } else { "базы: $age дн. назад" }); T = $(if ($age -le 7) { 'ok' } else { 'warn' }) }
    if ($age -gt 7) { $bad++ }
  } else { $bad++; $t += @{ L = 'АНТИВИРУС'; V = 'Выключен'; H = $(if ($mp) { 'защита в реальном времени выкл.' } else { 'Defender не отвечает' }); T = 'warn' } }
  $fw = @(Get-NetFirewallProfile -ErrorAction SilentlyContinue); $fon = @($fw | Where-Object { [string]$_.Enabled -eq 'True' }).Count
  if ($fw.Count -gt 0 -and $fon -eq $fw.Count) { $t += @{ L = 'БРАНДМАУЭР'; V = 'Включён'; H = 'все профили сети'; T = 'ok' } }
  elseif ($fon -gt 0) { $bad++; $t += @{ L = 'БРАНДМАУЭР'; V = "$fon из $($fw.Count)"; H = 'часть профилей выключена'; T = 'warn' } }
  else { $bad++; $t += @{ L = 'БРАНДМАУЭР'; V = 'Выключен'; H = 'входящие подключения не фильтруются'; T = 'warn' } }
  $ss = [string](Get-FReg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' 'SmartScreenEnabled'); $ssp = Get-FReg "$HKP\Windows\System" 'EnableSmartScreen'
  $ssOn = ($ssp -ne 0) -and ($ss -ne 'Off')
  if (-not $ssOn) { $bad++ }
  $t += @{ L = 'SMARTSCREEN'; V = $(if ($ssOn) { 'Включён' } else { 'Выключен' }); H = $(if ($ssOn) { 'проверяет скачанные файлы' } else { 'файлы не проверяются' }); T = $(if ($ssOn) { 'ok' } else { 'warn' }) }
  $lua = Get-FReg $SYSP 'EnableLUA'; $cp = Get-FReg $SYSP 'ConsentPromptBehaviorAdmin'
  if ($lua -eq 0) { $bad++; $t += @{ L = 'UAC'; V = 'Выключен'; H = 'программы получают права админа молча'; T = 'warn' } }
  elseif ($cp -eq 0) { $t += @{ L = 'UAC'; V = 'Без вопросов'; H = 'запросы прав не показываются'; T = 'info' } }
  else { $t += @{ L = 'UAC'; V = 'Включён'; H = 'спрашивает перед изменениями'; T = 'ok' } }
  $sb = $null; try { $sb = Confirm-SecureBootUEFI -ErrorAction Stop } catch {}
  $tpm = $false; try { $tp = Get-Tpm -ErrorAction Stop; $tpm = [bool]$tp.TpmPresent -and [bool]$tp.TpmReady } catch {}
  $t += @{ L = 'ЗАГРУЗКА'; V = $(if ($sb) { 'Secure Boot' } elseif ($null -eq $sb) { 'Legacy / нет данных' } else { 'Secure Boot выкл.' }); H = $(if ($tpm) { 'TPM готов' } else { 'TPM не найден' }); T = $(if ($sb -and $tpm) { 'ok' } else { 'info' }) }
  $bl = $null; try { $bl = Get-BitLockerVolume -MountPoint $env:SystemDrive -ErrorAction Stop } catch {}
  $enc = $bl -and [string]$bl.VolumeStatus -ne 'FullyDecrypted'
  $t += @{ L = 'BITLOCKER'; V = $(if (-not $bl) { 'Нет данных' } elseif ($enc) { 'Диск зашифрован' } else { 'Не зашифрован' }); H = $(if ($enc) { 'нужен ключ восстановления' } else { "системный диск $env:SystemDrive" }); T = 'info' }
  $title = if ($bad -eq 0) { 'Компьютер защищён' } elseif ($bad -eq 1) { 'Есть одно слабое место' } else { "Слабых мест: $bad" }
  $sub = if ($bad -eq 0) { 'Антивирус, брандмауэр и SmartScreen работают. Ниже — тонкая настройка и усиление защиты' } else { 'Жёлтым отмечено то, что выключено. Если это сделано специально — всё в порядке' }
  $r = @{ Title = $title; Sub = $sub; Tone = $(if ($bad -eq 0) { 'ok' } else { 'warn' }); Tiles = $t }
  $script:FSecCache = $r; $script:FSecTime = Get-Date
  return $r
}
function Get-FUpdInfo {
  $cv = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
  $disp = [string](Get-FReg $cv 'DisplayVersion'); $build = [string](Get-FReg $cv 'CurrentBuild'); $ubr = [string](Get-FReg $cv 'UBR')
  $pdt = $null; $pe = Get-FReg $UX 'PauseUpdatesExpiryTime'
  if ($pe) { try { $pdt = [datetime]::Parse([string]$pe, [Globalization.CultureInfo]::InvariantCulture) } catch {} }
  $paused = $pdt -and $pdt -gt (Get-Date)
  $stay = (Get-FReg $WUP 'TargetReleaseVersion') -eq 1
  $mi = Get-FTweakIdx 'wumode'; $mode = @('Автоматически', 'По кнопке', 'Уведомлять')[[math]::Max(0, $mi)]
  if ((Get-FReg "$WUP\AU" 'NoAutoUpdate') -eq 1) { $mode = 'Выключены' }
  if (-not $script:FLastKb -or ((Get-Date) - $script:FLastKbT).TotalMinutes -gt 5) {
    $script:FLastKb = 'none'; $script:FLastKbT = Get-Date
    try { $hf = Get-HotFix -ErrorAction Stop | Where-Object { $_.InstalledOn } | Sort-Object InstalledOn -Descending | Select-Object -First 1; if ($hf) { $script:FLastKb = $hf.InstalledOn } } catch {}
  }
  $last = $script:FLastKb
  $reboot = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
  $lastV = '—'; $lastH = 'нет данных'; $lt = 'info'
  if ($last -is [datetime]) { $d = [int]((Get-Date).Date - $last.Date).TotalDays; $lastV = $last.ToString('dd.MM.yyyy'); $lastH = $(if ($d -le 0) { 'сегодня' } elseif ($d -eq 1) { 'вчера' } else { "$d дн. назад" }); if ($d -gt 60) { $lt = 'warn' } else { $lt = 'ok' } }
  $t = @(
    @{ L = 'ВЕРСИЯ'; V = $(if ($disp) { "Windows 11 $disp" } else { 'Windows 11' }); H = "сборка $build.$ubr"; T = 'info' },
    @{ L = 'УСТАНОВКА'; V = $mode; H = $(if ($stay) { "без перехода с $disp" } else { 'с переходом на новые версии' }); T = 'info' },
    @{ L = 'ПАУЗА'; V = $(if ($paused) { if ($pdt.Year -ge 2077) { 'Навсегда' } elseif ($pdt.Year -ne (Get-Date).Year) { 'до ' + $pdt.ToString('dd.MM.yy') } else { 'до ' + $pdt.ToString('dd.MM') } } else { 'Нет' }); H = $(if ($paused) { 'обновления не скачиваются' } else { 'обновления приходят' }); T = $(if ($paused) { 'warn' } else { 'ok' }) },
    @{ L = 'ПОСЛЕДНЕЕ'; V = $lastV; H = $lastH; T = $lt }
  )
  $title = if ($reboot) { 'Обновление ждёт перезагрузки' } elseif ($paused) { 'Обновления на паузе до ' + $pdt.ToString('d MMMM', [Globalization.CultureInfo]'ru-RU') } elseif ($stay) { 'Windows обновляется, но остаётся на ' + $disp } else { 'Windows обновляется автоматически' }
  $sub = if ($reboot) { 'Перезагрузи компьютер, чтобы закончить установку' } elseif ($paused) { 'Исправления безопасности тоже не приходят — не держи паузу слишком долго' } else { 'Здесь можно поставить паузу, отложить обновления и решить, когда перезагружаться' }
  return @{ Title = $title; Sub = $sub; Tone = $(if ($reboot -or $paused) { 'warn' } else { 'ok' }); Tiles = $t; Paused = [bool]$paused }
}
function Get-FUpdPause {
  $ps = $null; $pe = $null
  try { $v = Get-FReg $UX 'PauseUpdatesExpiryTime'; if ($v) { $pe = [datetime]::Parse([string]$v, [Globalization.CultureInfo]::InvariantCulture) } } catch {}
  try { $v = Get-FReg $UX 'PauseUpdatesStartTime'; if ($v) { $ps = [datetime]::Parse([string]$v, [Globalization.CultureInfo]::InvariantCulture) } } catch {}
  $now = Get-Date
  $paused = [bool]($pe -and $pe -gt $now)
  $max = 35; try { $m = Get-FReg $UX 'FlightSettingsMaxPauseDays'; if ($m) { $max = [int]$m } } catch {}
  $r = @{ Paused = $paused; Max = $max; Now = $now.ToString('yyyy-MM-ddTHH:mm:ss') }
  if ($paused) {
    if (-not $ps -or $ps -gt $now) { $ps = $now }
    $r.Start = $ps.ToString('yyyy-MM-ddTHH:mm:ss'); $r.Until = $pe.ToString('yyyy-MM-ddTHH:mm:ss')
  }
  return $r
}
function Set-FUpdPause([int]$days) {
  $names = 'PauseUpdatesStartTime', 'PauseUpdatesExpiryTime', 'PauseFeatureUpdatesStartTime', 'PauseFeatureUpdatesEndTime', 'PauseQualityUpdatesStartTime', 'PauseQualityUpdatesEndTime'
  if ($days -le 0) { foreach ($n in $names) { if ($null -ne (Get-FReg $UX $n)) { Remove-FReg $UX $n } }; Show-Ok 'Обновления возобновлены'; return }
  if ($days -gt 35) { Set-FReg $UX 'FlightSettingsMaxPauseDays' ([int]($days + 1)) }
  $f = 'yyyy-MM-ddTHH:mm:ssZ'; $now = (Get-Date).ToUniversalTime(); $end = $now.AddDays($days)
  foreach ($n in 'PauseUpdatesStartTime', 'PauseFeatureUpdatesStartTime', 'PauseQualityUpdatesStartTime') { Set-FReg $UX $n $now.ToString($f) 'String' }
  foreach ($n in 'PauseUpdatesExpiryTime', 'PauseFeatureUpdatesEndTime', 'PauseQualityUpdatesEndTime') { Set-FReg $UX $n $end.ToString($f) 'String' }
  Show-Ok ('Обновления приостановлены до ' + $end.ToLocalTime().ToString('dd.MM.yyyy'))
}

# ---- one-click fixes ----
function Stop-FExplorerQuiet { Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 700 }
function Start-FExplorerQuiet { Start-Sleep -Milliseconds 400; if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process -FilePath "$env:WINDIR\explorer.exe" } }
function Restart-FSvc([string[]]$names) { foreach ($n in $names) { Restart-Service -Name $n -Force -ErrorAction SilentlyContinue } }
function Register-FAppx([string]$name) {
  $n = 0
  foreach ($p in @(Get-AppxPackage -AllUsers -Name $name -ErrorAction SilentlyContinue)) {
    $m = Join-Path $p.InstallLocation 'AppXManifest.xml'
    if (Test-Path $m) { try { Add-AppxPackage -DisableDevelopmentMode -Register $m -ErrorAction Stop; $n++ } catch { Write-FLog "    appx $name : $($_.Exception.Message)" } }
  }
  return $n
}
function Invoke-FFix([string]$id) {
  switch ($id) {
    'net' {
      foreach ($a in 'winsock reset', 'int ip reset', 'int ipv6 reset', 'winhttp reset proxy') { [void](Invoke-FNative 'netsh.exe' $a) }
      foreach ($a in '/release', '/flushdns', '/renew', '/registerdns') { [void](Invoke-FNative 'ipconfig.exe' $a) }
      $global:NeedReboot = $true; Show-Ok 'Сеть сброшена. Перезагрузи компьютер, чтобы закончить'
    }
    'dns' { [void](Invoke-FNative 'ipconfig.exe' '/flushdns'); Clear-DnsClientCache -ErrorAction SilentlyContinue; Restart-FSvc 'Dnscache'; Show-Ok 'Кэш DNS очищен' }
    'time' {
      [void](Set-FSvcStart 'W32Time' 3); Start-Service W32Time -ErrorAction SilentlyContinue
      [void](Invoke-FNative 'w32tm.exe' '/config /update /manualpeerlist:"time.windows.com,0x9 pool.ntp.org,0x9" /syncfromflags:manual')
      [void](Invoke-FNative 'w32tm.exe' '/resync /force'); Show-Ok ('Время синхронизировано: ' + (Get-Date -Format 'HH:mm:ss'))
    }
    'wu' {
      $svc = 'wuauserv', 'bits', 'cryptsvc', 'msiserver', 'usosvc'
      foreach ($s in $svc) { Stop-Service -Name $s -Force -ErrorAction SilentlyContinue }
      $sd = "$env:WINDIR\SoftwareDistribution"; $cr = "$env:WINDIR\System32\catroot2"
      foreach ($p in $sd, $cr) { if (Test-Path "$p.old") { Remove-Item "$p.old" -Recurse -Force -ErrorAction SilentlyContinue }; if (Test-Path $p) { Rename-Item -Path $p -NewName ((Split-Path $p -Leaf) + '.old') -Force -ErrorAction SilentlyContinue } }
      [void](Invoke-FNative 'sc.exe' 'config wuauserv start= demand'); [void](Invoke-FNative 'sc.exe' 'config bits start= delayed-auto')
      foreach ($s in 'cryptsvc', 'bits', 'wuauserv', 'usosvc') { Start-Service -Name $s -ErrorAction SilentlyContinue }
      Show-Ok 'Центр обновления сброшен: кэш очищен, службы перезапущены'
    }
    'store' {
      [void](Register-FAppx 'Microsoft.WindowsStore'); [void](Register-FAppx 'Microsoft.StorePurchaseApp')
      Get-ChildItem "$env:LOCALAPPDATA\Packages\Microsoft.WindowsStore_*\LocalCache" -Force -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item (Join-Path $_.FullName '*') -Recurse -Force -ErrorAction SilentlyContinue }
      Start-Process -FilePath "$env:WINDIR\System32\wsreset.exe" -WindowStyle Hidden -ErrorAction SilentlyContinue
      Show-Ok 'Microsoft Store сброшен — он откроется сам через несколько секунд'
    }
    'start' {
      foreach ($n in 'Microsoft.Windows.StartMenuExperienceHost', 'Microsoft.Windows.ShellExperienceHost', 'MicrosoftWindows.Client.CBS', 'Microsoft.Windows.Search') { [void](Register-FAppx $n) }
      foreach ($p in 'StartMenuExperienceHost', 'SearchHost', 'SearchApp', 'ShellExperienceHost') { Stop-Process -Name $p -Force -ErrorAction SilentlyContinue }
      Restart-FSvc 'WSearch'
      Show-Ok 'Пуск и поиск перезапущены'
    }
    'icons' {
      Stop-FExplorerQuiet
      $ex = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
      for ($i = 0; $i -lt 3; $i++) { Remove-Item "$env:LOCALAPPDATA\IconCache.db" -Force -ErrorAction SilentlyContinue; Remove-Item "$ex\iconcache_*.db" -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 200 }
      Start-FExplorerQuiet; [void](Invoke-FNative 'ie4uinit.exe' '-show')
      Show-Ok 'Кэш значков пересобран'
    }
    'thumbs' {
      Stop-FExplorerQuiet
      for ($i = 0; $i -lt 3; $i++) { Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db" -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 200 }
      Start-FExplorerQuiet; Show-Ok 'Кэш эскизов очищен — миниатюры появятся заново'
    }
    'folders' {
      $b = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell'
      Stop-FExplorerQuiet
      foreach ($k in "$b\Bags", "$b\BagMRU") { Remove-Item -Path $k -Recurse -Force -ErrorAction SilentlyContinue }
      Start-FExplorerQuiet; Show-Ok 'Вид папок сброшен'
    }
    'explorer' { Stop-FExplorerQuiet; Start-FExplorerQuiet; Show-Ok 'Проводник перезапущен' }
    'fonts' {
      Stop-Service -Name FontCache, FontCache3.0.0.0 -Force -ErrorAction SilentlyContinue
      Remove-Item "$env:WINDIR\ServiceProfiles\LocalService\AppData\Local\FontCache\*" -Recurse -Force -ErrorAction SilentlyContinue
      Remove-Item "$env:WINDIR\System32\FNTCACHE.DAT" -Force -ErrorAction SilentlyContinue
      Start-Service -Name FontCache -ErrorAction SilentlyContinue; $global:NeedReboot = $true
      Show-Ok 'Кэш шрифтов очищен — после перезагрузки он соберётся заново'
    }
    'audio' { Restart-FSvc 'AudioEndpointBuilder', 'Audiosrv'; Start-Service Audiosrv -ErrorAction SilentlyContinue; Show-Ok 'Звук перезапущен' }
    'print' {
      Stop-Service -Name Spooler -Force -ErrorAction SilentlyContinue
      Remove-Item "$env:WINDIR\System32\spool\PRINTERS\*" -Force -Recurse -ErrorAction SilentlyContinue
      Start-Service -Name Spooler -ErrorAction SilentlyContinue; Show-Ok 'Очередь печати очищена'
    }
    'bt' { Restart-FSvc 'bthserv', 'BTAGService', 'BthAvctpSvc'; Show-Ok 'Службы Bluetooth перезапущены' }
    'usb' { [void](Invoke-FNative 'pnputil.exe' '/scan-devices'); Show-Ok 'Устройства переобнаружены' }
    'sechealth' { [void](Register-FAppx 'Microsoft.SecHealthUI'); Restart-FSvc 'SecurityHealthService'; Show-Ok '«Безопасность Windows» перерегистрирована' }
    'searchidx' {
      if ((Get-FSvcStart 'WSearch') -eq 4) { Show-Warn 'Индексирование выключено в «Производительности» — включи его, чтобы перестроить индекс'; return }
      Stop-Service WSearch -Force -ErrorAction SilentlyContinue
      Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows Search' -Name 'SetupCompletedSuccessfully' -Value 0 -Type DWord -ErrorAction SilentlyContinue
      Start-Service WSearch -ErrorAction SilentlyContinue; Show-Ok 'Индекс поиска перестраивается в фоне'
    }
    'wmi' {
      $o = (& winmgmt.exe /verifyrepository) -join ' '
      if ($o -match 'inconsistent|несогласован') { [void](Invoke-FNative 'winmgmt.exe' '/salvagerepository'); Show-Ok 'Репозиторий WMI восстановлен' } else { Show-Ok 'Репозиторий WMI в порядке' }
    }
    'tray' {
      $k = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify'
      Stop-FExplorerQuiet; foreach ($n in 'IconStreams', 'PastIconsStream') { Remove-ItemProperty -Path $k -Name $n -Force -ErrorAction SilentlyContinue }; Start-FExplorerQuiet
      Show-Ok 'Значки в трее сброшены'
    }
    'clipboard' { try { Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::Clear() } catch {}; Restart-FSvc 'cbdhsvc*'; Show-Ok 'Буфер обмена очищен' }
    default { Show-Err "Неизвестное исправление: $id" }
  }
}

# ---- disk cleanup by category ----
function Get-FDirSize([string[]]$paths) {
  $s = 0.0
  foreach ($p in $paths) { try { $m = Get-ChildItem -Path $p -Recurse -Force -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum; if ($m.Sum) { $s += [double]$m.Sum } } catch {} }
  return $s
}
function Get-FBrowserCacheDirs {
  $l = $env:LOCALAPPDATA; $out = @()
  foreach ($b in "$l\Google\Chrome\User Data", "$l\Microsoft\Edge\User Data", "$l\BraveSoftware\Brave-Browser\User Data", "$l\Yandex\YandexBrowser\User Data", "$l\Vivaldi\User Data") {
    if (Test-Path $b) { foreach ($p in @(Get-ChildItem -Path $b -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'Default' -or $_.Name -like 'Profile *' })) { foreach ($c in 'Cache', 'Code Cache', 'GPUCache') { $d = Join-Path $p.FullName $c; if (Test-Path $d) { $out += "$d\*" } } } }
  }
  foreach ($p in @(Get-ChildItem -Path "$l\Mozilla\Firefox\Profiles" -Directory -ErrorAction SilentlyContinue)) { $d = Join-Path $p.FullName 'cache2'; if (Test-Path $d) { $out += "$d\*" } }
  $o = "$env:APPDATA\Opera Software"; foreach ($p in @(Get-ChildItem -Path $o -Directory -ErrorAction SilentlyContinue)) { $d = Join-Path $p.FullName 'Cache'; if (Test-Path $d) { $out += "$d\*" } }
  return $out
}
function Get-FCleanCats {
  $w = $env:WINDIR; $l = $env:LOCALAPPDATA
  [ordered]@{
    temp    = @('Временные файлы', 'Папка TEMP твоего профиля', $true, @("$env:TEMP\*"))
    wintemp = @('Временные файлы Windows', 'Windows\Temp и остатки установщиков', $true, @("$w\Temp\*"))
    wucache = @('Кэш обновлений', 'Уже установленные пакеты Центра обновления', $true, @("$w\SoftwareDistribution\Download\*"))
    docache = @('Оптимизация доставки', 'Файлы, которые Windows раздаёт другим ПК', $true, @("$w\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache\*"))
    dumps   = @('Дампы и отчёты об ошибках', 'Снимки памяти после сбоев и отчёты Windows Error Reporting', $true, @("$w\Minidump\*", "$w\MEMORY.DMP", "$l\CrashDumps\*", "$env:ProgramData\Microsoft\Windows\WER\ReportArchive\*", "$env:ProgramData\Microsoft\Windows\WER\ReportQueue\*", "$l\Microsoft\Windows\WER\*"))
    logs    = @('Журналы установки', 'Старые журналы CBS, DISM и обновлений', $true, @("$w\Logs\CBS\*.log", "$w\Logs\CBS\*.cab", "$w\Logs\DISM\*", "$w\Logs\MeasuredBoot\*", "$w\Panther\*.log"))
    thumbs  = @('Эскизы картинок', 'Пересоздадутся сами при открытии папок', $false, @("$l\Microsoft\Windows\Explorer\thumbcache_*.db"))
    shader  = @('Кэш шейдеров', 'DirectX, NVIDIA и AMD. Игры соберут его заново — первые запуски будут дольше', $false, @("$l\D3DSCache\*", "$l\NVIDIA\DXCache\*", "$l\NVIDIA\GLCache\*", "$l\AMD\DxCache\*", "$l\AMD\DxcCache\*"))
    browser = @('Кэш браузеров', 'Chrome, Edge, Firefox, Яндекс, Opera, Brave. Пароли и вкладки не трогаются', $false, @(Get-FBrowserCacheDirs))
    recycle = @('Корзина', 'Удалённые файлы во всех корзинах', $false, @())
  }
}
function Get-FRecycleSize { $s = 0.0; try { foreach ($i in @((New-Object -ComObject Shell.Application).NameSpace(10).Items())) { try { $s += [double]$i.Size } catch {} } } catch {}; return $s }
function Get-FCleanScan {
  $out = @()
  $cats = Get-FCleanCats
  foreach ($k in $cats.Keys) {
    $c = $cats[$k]
    $size = if ($k -eq 'recycle') { Get-FRecycleSize } else { Get-FDirSize $c[3] }
    $out += @{ Id = $k; Name = $c[0]; Desc = $c[1]; Def = [bool]$c[2]; Size = [double]$size }
  }
  return ,$out
}
function Get-FDiskInfo {
  $d = Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='$env:SystemDrive'" -ErrorAction SilentlyContinue
  if (-not $d) { return @{ Drive = $env:SystemDrive; Free = 0.0; Size = 0.0 } }
  return @{ Drive = $env:SystemDrive; Free = [double]$d.FreeSpace; Size = [double]$d.Size }
}
function Clear-FItems([string]$ids) {
  $before = [double](Get-FDiskInfo).Free
  $cats = Get-FCleanCats; $done = @()
  foreach ($k in ($ids -split ',')) {
    $k = $k.Trim(); if (-not $cats.Contains($k)) { continue }
    if ($k -eq 'recycle') { Clear-RecycleBin -Force -ErrorAction SilentlyContinue; $done += $cats[$k][0]; continue }
    if ($k -eq 'wucache') { Stop-Service -Name wuauserv, bits -Force -ErrorAction SilentlyContinue }
    if ($k -eq 'docache') { try { Delete-DeliveryOptimizationCache -Force -ErrorAction Stop } catch {} }
    if ($k -eq 'thumbs') { Stop-FExplorerQuiet }
    foreach ($p in $cats[$k][3]) { Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue }
    if ($k -eq 'thumbs') { Start-FExplorerQuiet }
    if ($k -eq 'wucache') { Start-Service -Name bits, wuauserv -ErrorAction SilentlyContinue }
    $done += $cats[$k][0]
  }
  $freed = [math]::Max(0.0, [double](Get-FDiskInfo).Free - $before)
  Show-Ok ('Очищено: {0}. Освобождено {1}' -f ($done -join ', '), (Format-FSize $freed))
  return $freed
}
function Format-FSize([double]$b) {
  if ($b -ge 1GB) { return ('{0:N1} ГБ' -f ($b / 1GB)) }
  if ($b -ge 1MB) { return ('{0:N0} МБ' -f ($b / 1MB)) }
  return ('{0:N0} КБ' -f ($b / 1KB))
}

# ---- installed programs ----
$FUninstRoots = [ordered]@{ HKLM = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'; HKLM32 = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'; HKCU = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' }
function Get-FInstalled {
  $out = New-Object System.Collections.ArrayList; $seen = @{}
  foreach ($loc in $FUninstRoots.Keys) {
    foreach ($k in @(Get-ChildItem -Path $FUninstRoots[$loc] -ErrorAction SilentlyContinue)) {
      $p = $null; try { $p = Get-ItemProperty -LiteralPath $k.PSPath -ErrorAction Stop } catch { continue }
      $name = [string]$p.DisplayName; if (-not $name) { continue }
      if ($p.SystemComponent -eq 1 -or $p.ParentKeyName -or [string]$p.ReleaseType -match 'Update|Hotfix' -or $name -match '^(Security Update|Update for|Hotfix|Обновление для|Обновление безопасности)') { continue }
      if (-not $p.UninstallString -and -not $p.QuietUninstallString) { continue }
      $key = ($name + '|' + [string]$p.DisplayVersion).ToLower(); if ($seen.ContainsKey($key)) { continue }; $seen[$key] = 1
      $size = 0.0; try { if ($p.EstimatedSize) { $size = [double]$p.EstimatedSize * 1024 } } catch {}
      $date = ''; if ([string]$p.InstallDate -match '^(\d{4})(\d{2})(\d{2})$') { $date = "$($Matches[1])-$($Matches[2])-$($Matches[3])" }
      $icon = ([string]$p.DisplayIcon -replace ',\s*-?\d+$', '').Trim('"', ' ')
      if (-not $icon -or -not ($icon -match '\.(exe|ico)$')) { $icon = '' }
      [void]$out.Add(@{ Id = "$loc|$($k.PSChildName)"; Name = $name; Pub = ([string]$p.Publisher).Trim(); Ver = [string]$p.DisplayVersion; Size = $size; Date = $date; Loc = ([string]$p.InstallLocation).Trim('"', ' '); Icon = $icon; Kind = 'app'; Quiet = [bool]$p.QuietUninstallString })
    }
  }
  try {
    $names = @{}; foreach ($s in @(Get-StartApps -ErrorAction Stop)) { if ([string]$s.AppID -match '^([^!]+)!') { if (-not $names.ContainsKey($Matches[1])) { $names[$Matches[1]] = [string]$s.Name } } }
    foreach ($a in @(Get-AppxPackage -ErrorAction Stop)) {
      if ($a.IsFramework -or $a.IsResourcePackage -or $a.NonRemovable -or [string]$a.SignatureKind -eq 'System') { continue }
      $nm = $names[[string]$a.PackageFamilyName]; if (-not $nm) { continue }
      $pub = ''; if ([string]$a.Publisher -match 'CN=([^,]+)') { $pub = $Matches[1].Trim('"') }
      if ($pub -match '^[0-9A-F]{8}-') { $pub = 'Microsoft Store' }
      $date = ''; try { $date = (Get-Item -LiteralPath $a.InstallLocation -ErrorAction Stop).CreationTime.ToString('yyyy-MM-dd') } catch {}
      [void]$out.Add(@{ Id = "APPX|$($a.PackageFullName)"; Name = $nm; Pub = $pub; Ver = [string]$a.Version; Size = 0.0; Date = $date; Loc = [string]$a.InstallLocation; Icon = ''; Kind = 'store'; Quiet = $true })
    }
  } catch {}
  return ,($out.ToArray())
}
function Get-FUninstallEntry([string]$id) {
  $p = $id.Split('|', 2); if ($p.Count -lt 2 -or -not $FUninstRoots.Contains($p[0])) { return $null }
  return (Get-ItemProperty -LiteralPath (Join-Path $FUninstRoots[$p[0]] $p[1]) -ErrorAction SilentlyContinue)
}
function Split-FCommand([string]$cmd) {
  $c = [Environment]::ExpandEnvironmentVariables($cmd.Trim())
  if ($c -match '^"([^"]+)"\s*(.*)$') { return @($Matches[1], $Matches[2]) }
  if ($c -match '^(.+?\.exe)\s*(.*)$') { return @($Matches[1], $Matches[2]) }
  return @($c, '')
}
function Uninstall-FProgram([string]$id) {
  if ($id -like 'APPX|*') {
    $full = $id.Substring(5)
    try { Remove-AppxPackage -Package $full -ErrorAction Stop; Show-Ok 'Приложение удалено'; return @{ Ok = $true } }
    catch { Show-Err ('Не удалось удалить приложение: ' + $_.Exception.Message); return @{ Ok = $false } }
  }
  $e = Get-FUninstallEntry $id
  if (-not $e) { Show-Ok 'Программа уже удалена'; return @{ Ok = $true } }
  $name = [string]$e.DisplayName
  $cmd = [string]$e.QuietUninstallString; if (-not $cmd) { $cmd = [string]$e.UninstallString }
  if ($cmd -match 'msiexec' -and $cmd -match '\{[0-9A-Fa-f-]{36}\}') { $exe = "$env:WINDIR\System32\msiexec.exe"; $arg = '/x ' + $Matches[0] + ' /passive /norestart' }
  else { $sp = Split-FCommand $cmd; $exe = $sp[0]; $arg = $sp[1] }
  if (-not (Test-Path -LiteralPath $exe)) { Show-Err "Деинсталлятор не найден: $exe"; return @{ Ok = $false; Missing = $true } }
  Write-FLog "UNINSTALL $name : $exe $arg"
  Show-Ok "Удаляю «$name» — если появится окно установщика, следуй его шагам…"
  try {
    if ($arg) { $pr = Start-Process -FilePath $exe -ArgumentList $arg -PassThru -ErrorAction Stop } else { $pr = Start-Process -FilePath $exe -PassThru -ErrorAction Stop }
    $pr.WaitForExit()
  } catch { Show-Err ('Не удалось запустить деинсталлятор: ' + $_.Exception.Message); return @{ Ok = $false } }
  # many uninstallers (NSIS, Inno) copy themselves to TEMP and exit at once: wait while they are running
  $t0 = Get-Date
  while (((Get-Date) - $t0).TotalMinutes -lt 20) {
    if (-not (Get-FUninstallEntry $id)) { break }
    $busy = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(Au_|Un_A|unins\d*|uninst|uninstall|_iu14D2N|un_|setup|msiexec)' -and $_.StartTime -gt $t0.AddSeconds(-5) })
    if ($busy.Count -eq 0) { Start-Sleep -Seconds 1; break }
    Start-Sleep -Seconds 1
  }
  $gone = -not (Get-FUninstallEntry $id)
  if ($gone) { Show-Ok "«$name» удалена" } else { Show-Warn "«$name» всё ещё в списке установленных — возможно, удаление отменено" }
  return @{ Ok = $gone }
}
function Get-FNormName([string]$s) {
  $s = $s -replace '\(.*?\)', '' -replace '\b(x64|x86|64-bit|32-bit|amd64)\b', ''
  $s = $s -replace '\s+v?\d+([\.\-]\d+)+.*$', ''
  return (($s -replace '[^\p{L}\p{Nd}]', '').ToLowerInvariant())
}
function Get-FLeftBases {
  $b = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData, $env:APPDATA, $env:LOCALAPPDATA, "$env:LOCALAPPDATA\Programs", (Join-Path $env:USERPROFILE 'Documents'))
  return @($b | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique)
}
function Test-FLeftPath([string]$p) {
  if (-not $p -or $p.Length -lt 8) { return $false }
  $full = ''; try { $full = [IO.Path]::GetFullPath($p).TrimEnd('\') } catch { return $false }
  $deny = @($env:WINDIR, $env:SystemDrive, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData, $env:APPDATA, $env:LOCALAPPDATA, $env:USERPROFILE, "$env:LOCALAPPDATA\Programs", "$env:ProgramData\Microsoft", "$env:APPDATA\Microsoft", "$env:LOCALAPPDATA\Microsoft", "$env:ProgramFiles\Common Files", "${env:ProgramFiles(x86)}\Common Files", "$env:ProgramFiles\WindowsApps", $Dir, (Join-Path $env:USERPROFILE 'Documents'), (Join-Path $env:USERPROFILE 'Desktop'))
  foreach ($d in $deny) { if ($d -and $full -ieq $d.TrimEnd('\')) { return $false } }
  if ($full -like "$env:WINDIR\*" -or $full -like "$Dir\*" -or $full -like "$env:ProgramFiles\WindowsApps\*") { return $false }
  foreach ($b in (Get-FLeftBases)) { if ($full -like ($b.TrimEnd('\') + '\*')) { return $true } }
  return $false
}
function Test-FLeftReg([string]$k) {
  if ($k -notmatch '^(HKCU|HKLM):\\SOFTWARE\\(WOW6432Node\\)?[^\\]+') { return $false }
  $leaf = ($k -split '\\')[-1]
  if ($leaf -match '^(Microsoft|Windows|Classes|Policies|WOW6432Node|Wow6432Node|Clients|RegisteredApplications|Intel|NVIDIA Corporation|Realtek|WinWeaver|ODBC|Khronos|Google|Mozilla|Valve|Wow6432)$') { return $false }
  return $true
}
function Find-FLeftovers([string]$name, [string]$pub, [string]$loc) {
  $n = Get-FNormName $name; $pn = Get-FNormName ($pub -replace '\b(Inc|LLC|Ltd|Corp|Corporation|GmbH|Co|Limited|S\.A|AB|Software|Technologies)\b\.?', '')
  $out = New-Object System.Collections.ArrayList; $seen = @{}
  if ($n.Length -lt 3) { return ,@() }
  # folders still used by other installed programs are never offered
  $used = @(); foreach ($r in $FUninstRoots.Values) { foreach ($k in @(Get-ChildItem -Path $r -ErrorAction SilentlyContinue)) { $l = [string](Get-ItemProperty -LiteralPath $k.PSPath -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation; if ($l) { $used += $l.Trim('"', ' ').TrimEnd('\') } } }
  $add = {
    param($path, $kind)
    if ($seen.ContainsKey($path.ToLower())) { return }
    if ($kind -eq 'reg') { if (-not (Test-FLeftReg $path) -or -not (Test-Path -LiteralPath $path)) { return } }
    else {
      if (-not (Test-FLeftPath $path) -or -not (Test-Path -LiteralPath $path)) { return }
      foreach ($u in $used) { if ($u -and ($u -ieq $path.TrimEnd('\') -or $u -like ($path.TrimEnd('\') + '\*'))) { return } }
    }
    $seen[$path.ToLower()] = 1
    $sz = 0.0; if ($kind -eq 'dir') { $sz = Get-FDirSize @("$path\*") } elseif ($kind -eq 'file') { try { $sz = [double](Get-Item -LiteralPath $path -Force).Length } catch {} }
    [void]$out.Add(@{ Path = $path; Kind = $kind; Size = $sz })
  }
  if ($loc -and (Test-Path -LiteralPath $loc)) { & $add $loc.TrimEnd('\') 'dir' }
  foreach ($b in (Get-FLeftBases)) {
    foreach ($d in @(Get-ChildItem -LiteralPath $b -Directory -Force -ErrorAction SilentlyContinue)) {
      $dn = Get-FNormName $d.Name
      if ($dn -eq $n) { & $add $d.FullName 'dir'; continue }
      if ($pn.Length -ge 3 -and $dn -eq $pn) {
        foreach ($s in @(Get-ChildItem -LiteralPath $d.FullName -Directory -Force -ErrorAction SilentlyContinue)) { if ((Get-FNormName $s.Name) -eq $n) { & $add $s.FullName 'dir' } }
      }
    }
  }
  foreach ($sm in "$env:APPDATA\Microsoft\Windows\Start Menu\Programs", "$env:ProgramData\Microsoft\Windows\Start Menu\Programs", [Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('CommonDesktopDirectory')) {
    if (-not $sm -or -not (Test-Path $sm)) { continue }
    foreach ($f in @(Get-ChildItem -LiteralPath $sm -Force -ErrorAction SilentlyContinue)) {
      $fn = Get-FNormName ([IO.Path]::GetFileNameWithoutExtension($f.Name))
      if ($fn -ne $n) { continue }
      if ($f.PSIsContainer) { $seen[$f.FullName.ToLower()] = 1; [void]$out.Add(@{ Path = $f.FullName; Kind = 'dir'; Size = 0.0 }) }
      elseif ($f.Extension -eq '.lnk') { $seen[$f.FullName.ToLower()] = 1; [void]$out.Add(@{ Path = $f.FullName; Kind = 'file'; Size = [double]$f.Length }) }
    }
  }
  foreach ($root in 'HKCU:\SOFTWARE', 'HKLM:\SOFTWARE', 'HKLM:\SOFTWARE\WOW6432Node') {
    foreach ($k in @(Get-ChildItem -Path $root -ErrorAction SilentlyContinue)) {
      $kn = Get-FNormName $k.PSChildName
      if ($kn -eq $n) { & $add ("$root\" + $k.PSChildName) 'reg' }
      elseif ($pn.Length -ge 3 -and $kn -eq $pn) { foreach ($s in @(Get-ChildItem -Path $k.PSPath -ErrorAction SilentlyContinue)) { if ((Get-FNormName $s.PSChildName) -eq $n) { & $add ("$root\" + $k.PSChildName + '\' + $s.PSChildName) 'reg' } } }
    }
  }
  return ,($out.ToArray())
}
function Remove-FLeftovers([string]$list) {
  $n = 0; $freed = 0.0
  foreach ($p in ($list -split "`n")) {
    $p = $p.Trim(); if (-not $p) { continue }
    $isReg = $p -match '^(HKCU|HKLM):\\'
    $isLnk = $p -match '\\(Start Menu\\Programs|Desktop)\\' -and ($p -like '*.lnk' -or (Test-Path -LiteralPath $p -PathType Container))
    if ($isReg) { if (-not (Test-FLeftReg $p)) { Write-FLog "LEFTOVER skip $p"; continue } }
    elseif (-not $isLnk -and -not (Test-FLeftPath $p)) { Write-FLog "LEFTOVER skip $p"; continue }
    if (-not $isReg) { try { $freed += Get-FDirSize @($p, "$p\*") } catch {} }
    Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path -LiteralPath $p)) { $n++; Write-FLog "LEFTOVER removed $p" }
  }
  Show-Ok ('Удалено остатков: {0}, освобождено {1}' -f $n, (Format-FSize $freed))
  return $n
}

# ---- startup: scheduled tasks at logon, delayed start, removal with undo ----
$FDelayPath = '\WinWeaver\Delayed\'
$FStartBk = "$Dir\StartupBackup"
function Get-FDelayName([string]$id) { return ('d_' + (($id -replace '[^\w\.\-]', '_'))) }
function Get-FLogonTasks {
  $out = @()
  try {
    foreach ($t in @(Get-ScheduledTask -ErrorAction Stop | Where-Object { $_.TaskPath -notlike '\Microsoft\*' -and $_.TaskPath -notlike '\WinWeaver\*' })) {
      $logon = @($t.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }).Count -gt 0
      if (-not $logon) { continue }
      $a = @($t.Actions | Where-Object { $_.Execute }) | Select-Object -First 1
      if (-not $a) { continue }
      $exe = [Environment]::ExpandEnvironmentVariables(([string]$a.Execute).Trim('"'))
      $out += @{ Task = $t; Exe = $exe; Cmd = ('"' + $exe + '" ' + [string]$a.Arguments).Trim() }
    }
  } catch {}
  return $out
}
function Get-FStartupMore {
  $out = @()
  foreach ($x in (Get-FLogonTasks)) {
    $t = $x.Task
    $out += (Add-FStartupInfo @{ Id = "TASK|$($t.TaskPath)$($t.TaskName)"; Name = [string]$t.TaskName; Command = $x.Cmd; Source = 'Планировщик задач'; Enabled = ([string]$t.State -ne 'Disabled') } $x.Exe)
  }
  return $out
}
function Get-FStartupDelays {
  $r = @{}
  try { foreach ($t in @(Get-ScheduledTask -TaskPath $FDelayPath -ErrorAction Stop)) { $d = [string]$t.Description; if ($d -match '^WW:(.+)\|(\d+)$') { $r[$Matches[1]] = [int]$Matches[2] } } } catch {}
  return $r
}
function Set-FStartupTask([string]$id, [bool]$on) {
  $full = $id.Substring(5); $i = $full.LastIndexOf('\'); $tp = $full.Substring(0, $i + 1); $tn = $full.Substring($i + 1)
  if ($on) { Enable-ScheduledTask -TaskPath $tp -TaskName $tn -ErrorAction SilentlyContinue | Out-Null } else { Disable-ScheduledTask -TaskPath $tp -TaskName $tn -ErrorAction SilentlyContinue | Out-Null }
}
function Get-FStartupCmd([string]$id) {
  foreach ($x in (Get-FStartup)) { if ($x.Id -eq $id) { return $x } }
  return $null
}
function Set-FStartupDelay([string]$id, [int]$sec) {
  $tn = Get-FDelayName $id
  Unregister-ScheduledTask -TaskPath $FDelayPath -TaskName $tn -Confirm:$false -ErrorAction SilentlyContinue
  if ($sec -le 0) { Set-FStartup $id $true; Show-Ok 'Запуск без задержки'; return }
  $it = Get-FStartupCmd $id; if (-not $it) { Show-Err 'Запись автозагрузки не найдена'; return }
  if ($id -like 'UFOLD|*' -or $id -like 'CFOLD|*') { $exe = "$env:WINDIR\explorer.exe"; $arg = '"' + [string]$it.Command + '"' }
  else { $sp = Split-FCommand ([string]$it.Command); $exe = $sp[0]; $arg = $sp[1] }
  $user = "$env:USERDOMAIN\$env:USERNAME"
  try {
    $a = if ($arg) { New-ScheduledTaskAction -Execute $exe -Argument $arg } else { New-ScheduledTaskAction -Execute $exe }
    $tr = New-ScheduledTaskTrigger -AtLogOn -User $user; $tr.Delay = "PT$($sec)S"
    $pr = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
    $st = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskPath $FDelayPath -TaskName $tn -Action $a -Trigger $tr -Principal $pr -Settings $st -Description "WW:$id|$sec" -Force -ErrorAction Stop | Out-Null
  } catch { Show-Err ('Не удалось создать отложенный запуск: ' + $_.Exception.Message); return }
  Set-FStartup $id $false
  Show-Ok ("«{0}» будет запускаться через {1} с после входа" -f $it.Name, $sec)
}
function Remove-FStartup([string]$id) {
  $p = $id.Split('|', 2)
  Unregister-ScheduledTask -TaskPath $FDelayPath -TaskName (Get-FDelayName $id) -Confirm:$false -ErrorAction SilentlyContinue
  if ($p[0] -eq 'TASK') {
    $full = $p[1]; $i = $full.LastIndexOf('\')
    if (-not (Test-Path $FStartBk)) { New-Item -ItemType Directory -Path $FStartBk -Force | Out-Null }
    try { Export-ScheduledTask -TaskPath $full.Substring(0, $i + 1) -TaskName $full.Substring($i + 1) | Set-Content -Path (Join-Path $FStartBk ((Get-FDelayName $id) + '.xml')) -Encoding Unicode } catch {}
    Unregister-ScheduledTask -TaskPath $full.Substring(0, $i + 1) -TaskName $full.Substring($i + 1) -Confirm:$false -ErrorAction SilentlyContinue
  } elseif ($p[0] -eq 'UFOLD' -or $p[0] -eq 'CFOLD') {
    $src = Get-FStartupSources | Where-Object { $_.Loc -eq $p[0] } | Select-Object -First 1
    if (-not (Test-Path $FStartBk)) { New-Item -ItemType Directory -Path $FStartBk -Force | Out-Null }
    Move-Item -LiteralPath (Join-Path $src.Folder $p[1]) -Destination (Join-Path $FStartBk ($p[0] + '_' + $p[1])) -Force -ErrorAction SilentlyContinue
  } else {
    $src = Get-FStartupSources | Where-Object { $_.Loc -eq $p[0] } | Select-Object -First 1
    if ($src) { Remove-FReg $src.Run $p[1]; if ($null -ne (Get-FReg $src.Appr $p[1])) { Remove-FReg $src.Appr $p[1] } }
  }
  Show-Ok ('Убрано из автозагрузки: ' + $p[1])
}
function Restore-FStartup([string]$id) {
  $p = $id.Split('|', 2)
  if ($p[0] -eq 'TASK') {
    $f = Join-Path $FStartBk ((Get-FDelayName $id) + '.xml'); $full = $p[1]; $i = $full.LastIndexOf('\')
    if (Test-Path $f) { Register-ScheduledTask -Xml ((Get-Content $f -Raw)) -TaskPath $full.Substring(0, $i + 1) -TaskName $full.Substring($i + 1) -Force -ErrorAction SilentlyContinue | Out-Null }
  } elseif ($p[0] -eq 'UFOLD' -or $p[0] -eq 'CFOLD') {
    $src = Get-FStartupSources | Where-Object { $_.Loc -eq $p[0] } | Select-Object -First 1
    $b = Join-Path $FStartBk ($p[0] + '_' + $p[1]); if (Test-Path -LiteralPath $b) { Move-Item -LiteralPath $b -Destination (Join-Path $src.Folder $p[1]) -Force -ErrorAction SilentlyContinue }
  }
}

# ======================================================================
#  Uninstall WinWeaver: optionally roll back every change, then remove tasks, power plan and settings.
#  The program files and the data folder are deleted by the exe after the engine stops.
# ======================================================================
function Uninstall-FWinWeaver([bool]$undoAll = $false) {
  if ($undoAll) {
    $list = Get-FHistData
    $items = @(); for ($i = $list.Count - 1; $i -ge 0; $i--) { $x = $list[$i]; if (-not [bool]$x.Done -and [bool]$x.Can) { $items += $x } }
    $global:FDeferExplorer = $true; $global:FExplorerPending = $false
    $k = 0
    try { foreach ($x in $items) { $k++; Show-Ok ('Откат [{0}/{1}]: {2}…' -f $k, $items.Count, $x.Title); [void](Undo-FHistItem $x) } } finally { $global:FDeferExplorer = $false }
    Save-FHist
    if ((Get-Content "$Dir\defender.mode" -ErrorAction SilentlyContinue)) { Set-DefenderOn }
  }
  Show-Ok 'Удаляю задачи планировщика…'
  Get-ScheduledTask -TaskPath '\WinWeaver\' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
  try { $svc = New-Object -ComObject 'Schedule.Service'; $svc.Connect(); $svc.GetFolder('\').DeleteFolder('WinWeaver', 0) } catch {}
  Remove-FPanelTask
  # own power plan: switch to Balanced first if it is active
  $act = [string](& powercfg.exe /getactivescheme 2>$null)
  if ($act -match $GuidWW) { & powercfg.exe /setactive '381b4222-f694-41f0-9685-ff5bb260df2e' 2>&1 | Out-Null }
  & powercfg.exe /delete $GuidWW 2>&1 | Out-Null
  Remove-FReg $RunOnce '*WinWeaverDefender'
  & bcdedit.exe /deletevalue '{current}' safeboot 2>&1 | Out-Null
  Remove-Item -Path 'HKLM:\SOFTWARE\WinWeaver' -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -Path 'HKCU:\Software\WinWeaver' -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinWeaver' -Recurse -Force -ErrorAction SilentlyContinue
  Write-FLog 'UNINSTALL done'
  Show-Ok 'WinWeaver удалён из системы'
  return $true
}

