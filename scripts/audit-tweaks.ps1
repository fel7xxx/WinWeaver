$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'WinWeaver\Resources\engine.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
# Static inventory only. Never dot-source the engine or execute its initialization.
$rows = foreach ($fn in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    $body = $fn.Extent.Text
    $commands = @($fn.Body.FindAll({ param($n) $n -is [System.Management.Automation.Language.CommandAst] }, $true) | ForEach-Object { $_.GetCommandName() } | Where-Object { $_ } | Sort-Object -Unique)
    $flags = @()
    if ($body -match '(?i)SilentlyContinue|2>\s*\$null') { $flags += 'suppressed-errors' }
    if ($body -match '(?i)\b(netsh|powercfg|bcdedit|dism|sc|reg|manage-bde)(\.exe)?\b') { $flags += 'native-command-review' }
    if ($body -match '(?i)Remove-|Clear-|Delete|Format-') { $flags += 'destructive-operation-review' }
    if ($body -match '(?i)Invoke-WebRequest|Download|Start-Process') { $flags += 'external-process-or-download' }
    [pscustomobject]@{
        Function = $fn.Name
        Line = $fn.Extent.StartLineNumber
        Commands = $commands -join '; '
        ReviewFlags = $flags -join '; '
        Status = 'INVENTORIED; manual and VM review pending'
    }
}
$out = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$rows | Export-Csv (Join-Path $out 'tweak-inventory.csv') -NoTypeInformation -Encoding UTF8
Write-Host "Inventory: $(@($rows).Count) engine functions. Flags are review candidates, not confirmed defects."