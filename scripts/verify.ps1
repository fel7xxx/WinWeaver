$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'WinWeaver\Resources\engine.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
# Load only the rollback function, never the system-changing engine initialization.
$function = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Undo-FHistItem' }, $true)
Invoke-Expression $function.Extent.Text
function Write-FLog { param($text) }
$entry = @{ Done = $false; Can = $true; Reg = @(); Undo = "Write-Error 'simulated failure'"; Ex = $false; Title = 'test' }
if ((Undo-FHistItem $entry) -or $entry.Done) { throw 'Failed rollback was marked successful' }
$entry.Undo = '$null = 1'
if (-not (Undo-FHistItem $entry) -or -not $entry.Done) { throw 'Successful rollback was not recorded' }
# Unique outputs prevent a previous reflection test in the host from locking the next build.
$buildDir = Join-Path $root ('artifacts\verify-' + [Guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $root 'WinWeaver\WinWeaver.csproj') -c Release --nologo "-p:BaseOutputPath=$buildDir\"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$extra = Get-ChildItem (Join-Path $buildDir 'Release\net48') -File | Where-Object { $_.Name -ne 'WinWeaver.exe' }
if ($extra) { throw ('net48 must be a single WinWeaver.exe, found: ' + ($extra.Name -join ', ')) }
# Exercise production helpers without starting the app or applying system settings.
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $buildDir 'Release\net48\WinWeaver.exe'))
$localization = $assembly.GetType('WinWeaver.L')
$localization.GetMethod('Set').Invoke($null, @('en', $false))
$settingsLabel = -join ([char[]](0x41d,0x430,0x441,0x442,0x440,0x43e,0x439,0x43a,0x438))
if ($localization.GetMethod('T').Invoke($null, @($settingsLabel)) -ne 'Settings') { throw 'Embedded English localization failed' }
$parse = $assembly.GetType('WinWeaver.UpdateChecker').GetMethod('Parse')
foreach ($case in @(
    @('v1.2.3', '1.2.3.0'), @('1.2', '1.2.0.0'), @('1.2.3.4', '1.2.3.4'),
    @('garbage1.2.3', '0.0'), @('1.2.3-beta', '0.0'), @('999999999999.2', '0.0')
)) {
    $actual = $parse.Invoke($null, @($case[0])).ToString()
    if ($actual -ne $case[1]) { throw "Version parsing failed: $($case[0]) -> $actual" }
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('WinWeaver-tests-' + [Guid]::NewGuid().ToString('N'))
try {
    $source = Join-Path $temp 'source'
    $target = Join-Path $temp 'target'
    New-Item -ItemType Directory -Path $source, (Join-Path $source 'lang'), (Join-Path $source 'x64') -Force | Out-Null
    foreach ($name in @('runtime.dll', 'WinWeaver.exe.config', 'WinWeaver.deps.json', 'profile.json', 'tweaker.log', 'lang\custom.tsv', 'x64\native.dll')) {
        [IO.File]::WriteAllText((Join-Path $source $name), 'fixture')
    }
    $copy = $assembly.GetType('WinWeaver.Installer').GetMethod('CopyDependencies', [Reflection.BindingFlags]'Static,NonPublic')
    $copy.Invoke($null, @([string]$source, [string]$target))
    $copy.Invoke($null, @([string]$source, [string]$source))
    if (-not (Test-Path (Join-Path $target 'lang\custom.tsv'))) { throw 'Missing translation: lang\custom.tsv' }
    # Single-file build: nothing except translations is copied next to the installed exe.
    foreach ($name in @('runtime.dll', 'WinWeaver.exe.config', 'WinWeaver.deps.json', 'x64\native.dll', 'profile.json', 'tweaker.log')) {
        if (Test-Path (Join-Path $target $name)) { throw "Unexpected user file copied: $name" }
    }
} finally {
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
}
Write-Host 'PASS: PowerShell syntax, rollback failure/success, both target frameworks, release versions, single-file exe, installer copying'