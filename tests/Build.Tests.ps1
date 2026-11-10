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
$partialRoot = Join-Path $work 'partial'
New-Item -ItemType Directory -Path (Join-Path $partialRoot 'FeMM.Grasshopper') -Force | Out-Null
$partial = Join-Path $work 'partial.json'
& $exe $root $RhinoSystemDir $GrasshopperDir --tests-only --femm $partialRoot --require-femm --report $partial *> (Join-Path $work 'partial.log')
if ($LASTEXITCODE -ne 1) { throw 'Incomplete required reference sources must fail' }
$report = Get-Content -LiteralPath $partial -Raw | ConvertFrom-Json
if ($report.Status -ne 'Failed' -or $report.FemmComparison -ne 'Failed' -or $report.Suites[0].Failed -le 0) { throw 'Incomplete references were not reported' }

# A failed preflight must not leave a success manifest from an earlier build.
$staleRoot = Join-Path $work 'stale'
$plugin = Join-Path $staleRoot 'Arachne.Grasshopper/Release/net48'
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
$manifest = Join-Path $plugin 'BUILD.json'
'{"RhinoChecksPerformed":true}' | Set-Content -LiteralPath $manifest
$rejected = $false
try { & (Join-Path $root 'scripts/build.ps1') -ArachneBin $staleRoot -RhinoTests -RequireFemm -FemmSourcePath (Join-Path $work 'missing') }
catch { if ($_ -notmatch 'Sorgenti FeMM richiesti ma assenti') { throw }; $rejected = $true }
if (!$rejected -or (Test-Path -LiteralPath $manifest)) { throw 'Failed preflight left a success manifest' }

# A changed dependency must fail before compilation and invalidate success too.
$badSet = Join-Path $work 'bad-gpc-set'
New-Item -ItemType Directory -Path $badSet -Force | Out-Null
Copy-Item -Path (Join-Path $root 'lib/GPC/*') -Destination $badSet
[IO.File]::AppendAllText((Join-Path $badSet 'GPCModel.dll'), 'changed')
'{"RhinoChecksPerformed":true}' | Set-Content -LiteralPath $manifest
$rejected = $false
try { & (Join-Path $root 'scripts/build.ps1') -ArachneBin $staleRoot -ReleaseSet (Join-Path $badSet 'release-set.json') }
catch { if ($_ -notmatch 'SHA|hash') { throw }; $rejected = $true }
if (!$rejected -or (Test-Path -LiteralPath $manifest)) { throw 'Changed GPC library accepted or stale success retained' }
Write-Output "Build checks passed. Reports: $work"
