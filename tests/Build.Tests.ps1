param(
    [string]$ArachneBin = 'bin',
    [string]$Configuration = 'Release',
    [string]$RhinoSystemDir = 'C:\Program Files\Rhino 8\System',
    [string]$GrasshopperDir = 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'scripts/Build-Paths.ps1')
foreach ($path in @('', 'bin', 'bin/nested', 'bin/../bin/nested')) {
    $rejected = $false
    try { $null = Get-ArachneOutputRoot $root $path Debug -Plugin }
    catch { if ($_ -notmatch 'Debug richiede') { throw }; $rejected = $true }
    if (!$rejected) { throw "Debug was accepted in installed bin: $path" }
}
$expected = Join-Path $root 'artifacts\output with spaces\'
if ((Get-ArachneOutputRoot $root 'artifacts/output with spaces' Debug -Plugin) -ne $expected) { throw 'Relative path resolution failed' }

$output = Get-ArachneOutputRoot $root $ArachneBin $Configuration
$exe = Join-Path $output "Arachne.ExampleWriter/$Configuration/net48/Arachne.ExampleWriter.exe"
if (!(Test-Path -LiteralPath $exe)) { throw 'Build ExampleWriter before running these checks.' }
$work = Join-Path $root ('artifacts/build-checks/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$arguments = @($root, $RhinoSystemDir, $GrasshopperDir, '--tests-only', '--femm', (Join-Path $work 'missing'))
$strict = Join-Path $work 'strict.json'
& $exe @arguments --require-femm --report $strict *> (Join-Path $work 'strict.log')
if ($LASTEXITCODE -ne 1) { throw 'Missing required sources must fail' }
$report = Get-Content -LiteralPath $strict -Raw | ConvertFrom-Json
if ($report.Status -ne 'Failed' -or $report.FemmComparison -ne 'Failed') { throw 'Required FeMM failure was not reported' }
$optional = Join-Path $work 'optional.json'
& $exe @arguments --report $optional *> (Join-Path $work 'optional.log')
if ($LASTEXITCODE -ne 0) { throw 'Optional missing sources must not prevent other checks' }
$report = Get-Content -LiteralPath $optional -Raw | ConvertFrom-Json
if ($report.Status -ne 'PassedWithSkips' -or $report.FemmComparison -ne 'Skipped' -or $report.Suites[0].Skipped -le 0) { throw 'Skipped FeMM comparison was not reported' }
Write-Output "Build checks passed. Reports: $work"
