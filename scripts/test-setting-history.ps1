$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'WinWeaver\Resources\engine.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
foreach ($name in @('Invoke-FSetting', 'Invoke-FTracked', 'ConvertTo-FLiteral', 'Undo-FHistItem')) {
    $fn = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true)
    Invoke-Expression $fn.Extent.Text
}
# All OS and persistence operations are mocked; only production orchestration is loaded.
function Start-FCapture { $global:FExplorerTouched = $true }
function Stop-FCapture { return ,@(@{ P = 'mock'; N = 'value'; E = $false }) }
function Test-FRegChanged { param($capture) return $true }
function Write-FLog { param($text) }
function Show-Err { param($text) }
function Add-FHist {
    param($Title, $Cat, $Undo, $Reg, $Note, $Explorer, $Get, $Set, $Val)
    $script:record = @{ Title = $Title; Cat = $Cat; Undo = $Undo; Reg = $Reg; Note = $Note; Ex = $Explorer }
}
Invoke-FSetting 'same-value' '$true' '$null = 1' $true
if (@($script:record.Reg).Count -ne 1 -or -not $script:record.Ex) { throw 'Exact snapshot or explorer state lost' }
$script:record = $null
$failed = $false
try { Invoke-FSetting 'partial' '$false' "throw 'simulated'" $true } catch { $failed = $true }
if (-not $failed -or @($script:record.Reg).Count -ne 1 -or $script:record.Undo) { throw 'Partial failure snapshot lost' }
$script:order = New-Object System.Collections.ArrayList
function Restore-FRegList { param($list) [void]$script:order.Add('registry') }
function Send-FSettingChange { param($area) }
function Restart-FExplorer { [void]$script:order.Add('explorer') }
$entry = @{ Done = $false; Can = $true; Reg = @(@{ P = 'mock' }); Undo = "[void]`$script:order.Add('setter')"; Ex = $true; Title = 'order' }
if (-not (Undo-FHistItem $entry) -or ($script:order -join ',') -ne 'setter,registry,explorer') { throw 'Incorrect exact-restore ordering' }
$script:record = $null
try { Invoke-FTracked 'partial tracked' "throw 'simulated'" } catch {}
if (-not $script:record.Ex -or @($script:record.Reg).Count -ne 1) { throw 'Tracked partial failure lost Explorer state' }
$fn = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Test-FRegChanged' }, $true)
Invoke-Expression $fn.Extent.Text
function Get-FRegNow { param($Path, $Name) return $script:now }
$capture = @(@{ P = 'mock'; N = 'value'; E = $true; K = 'String'; V = 'Value' })
$script:now = @{ E = $true; K = 'String'; V = 'Value' }
if (Test-FRegChanged $capture) { throw 'Unchanged value detected as changed' }
$script:now.V = 'value'
if (-not (Test-FRegChanged $capture)) { throw 'Case-only change lost' }
$script:now.V = 'Value'; $script:now.K = 'ExpandString'
if (-not (Test-FRegChanged $capture)) { throw 'Registry type-only change lost' }
Write-Host 'PASS: setting snapshot, same-value registry changes, partial failure, exact rollback order (mocked)'
Write-Host 'PASS: tracked partial failure, registry case and type changes (mocked)'
function Test-FRegChanged { param($capture) return $false }
$script:record = $null
Invoke-FTracked 'non-registry action' '$null = 1' '$null = 2' -KeepReg
if (-not $script:record -or $script:record.Undo -ne '$null = 2') { throw 'KeepReg lost non-registry undo' }
Write-Host 'PASS: non-registry undo with KeepReg (mocked)'
$fn = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Remove-FReg' }, $true)
Invoke-Expression $fn.Extent.Text
function Save-FRegPrev { param($Path, $Name) }
function Test-Path { param($LiteralPath, $ErrorAction) return $script:keyExists }
function Get-Item {
    param($LiteralPath, $ErrorAction)
    $key = New-Object PSObject
    $key | Add-Member ScriptMethod GetValueNames { return @('value') }
    return $key
}
function Remove-ItemProperty { param($LiteralPath, $Name, $ErrorAction) throw 'Access denied (mock)' }
$script:keyExists = $false
Remove-FReg 'mock' 'value'
$script:keyExists = $true
$failed = $false
try { Remove-FReg 'mock' 'value' } catch { $failed = $true }
if (-not $failed) { throw 'Registry deletion failure was suppressed' }
$entry = @{ Done = $false; Can = $true; Reg = @(); Undo = "Remove-FReg 'mock' 'value'"; Ex = $false; Title = 'access denied' }
if ((Undo-FHistItem $entry) -or $entry.Done) { throw 'Denied deletion marked rolled back' }
Write-Host 'PASS: absent key is harmless; denied deletion fails rollback (mocked)'
foreach ($name in @('Restore-FRegList', 'Set-FDns')) {
    $fn = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true)
    Invoke-Expression $fn.Extent.Text
}
function Set-FSvcStart { param($name, $value) return $false }
$failed = $false
try { Restore-FRegList @(@{ P = 'HKLM:\SYSTEM\CurrentControlSet\Services\mock'; N = 'Start'; E = $true; K = 'DWord'; V = 2 }) } catch { $failed = $true }
if (-not $failed) { throw 'Failed service restore ignored' }
function Get-FNetAdapters { return ,@(@{ ifIndex = 1 }) }
function Set-DnsClientServerAddress { param($InterfaceIndex, $ServerAddresses, [switch]$ResetServerAddresses, $ErrorAction) throw 'DNS denied (mock)' }
foreach ($value in @(-1, 5, 0)) {
    $failed = $false
    try { Set-FDns $value } catch { $failed = $true }
    if (-not $failed) { throw "DNS error was suppressed: $value" }
}
Write-Host 'PASS: service restore failure, DNS bounds and denied DNS update (mocked)'
$fn = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Set-FastBoot' }, $true)
Invoke-Expression $fn.Extent.Text
function Invoke-FNative { param($exe, $argLine) return 5 }
function Set-FReg { param($Path, $Name, $Value) throw 'Registry must not be touched after failed powercfg' }
foreach ($value in @($true, $false)) {
    $failed = $false
    try { Set-FastBoot $value } catch { $failed = $_.Exception.Message -eq 'powercfg failed: 5' }
    if (-not $failed) { throw 'Fast boot did not propagate native error before registry write' }
}
Write-Host 'PASS: failed powercfg stops fast boot before registry update (mocked)'