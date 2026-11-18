param(
    [string]$ArachneBin = 'bin',
    [string]$Configuration = 'Release',
    [string]$RhinoSystemDir = 'C:\Program Files\Rhino 8\System',
    [string]$GrasshopperDir = 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper',
    # As build.ps1 -GpcAllowCandidate: lib/GPC holds a candidate set that is being validated.
    [switch]$GpcAllowCandidate
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
$runner = Join-Path $output "Arachne.ExampleWriter/$Configuration/$ArachneFramework/Arachne.ExampleWriter.dll"
if (!(Test-Path -LiteralPath $runner)) { throw 'Build ExampleWriter before running these checks.' }
$work = Join-Path $root ('artifacts/build-checks/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
# Windows PowerShell 5.1 turns the stderr of a redirected native command into a terminating error under Stop.
# The expected failures print their exception on stderr: the exit code and the report decide.
function Invoke-Checks([string]$Log, [object[]]$Arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & dotnet $runner @Arguments *> $Log } finally { $ErrorActionPreference = $previous }
}
$arguments = @($root, $RhinoSystemDir, $GrasshopperDir, '--tests-only', '--femm', (Join-Path $work 'missing'))
$strict = Join-Path $work 'strict.json'
Invoke-Checks (Join-Path $work 'strict.log') ($arguments + @('--require-femm', '--report', $strict))
if ($LASTEXITCODE -ne 1) { throw 'Missing required sources must fail' }
$report = Get-Content -LiteralPath $strict -Raw | ConvertFrom-Json
if ($report.Status -ne 'Failed' -or $report.FemmComparison -ne 'Failed') { throw 'Required FeMM failure was not reported' }
$optional = Join-Path $work 'optional.json'
Invoke-Checks (Join-Path $work 'optional.log') ($arguments + @('--report', $optional))
if ($LASTEXITCODE -ne 0) { throw 'Optional missing sources must not prevent other checks' }
$report = Get-Content -LiteralPath $optional -Raw | ConvertFrom-Json
if ($report.Status -ne 'PassedWithSkips' -or $report.FemmComparison -ne 'Skipped' -or $report.Suites[0].Skipped -le 0) { throw 'Skipped FeMM comparison was not reported' }
$partialRoot = Join-Path $work 'partial'
New-Item -ItemType Directory -Path (Join-Path $partialRoot 'FeMM.Grasshopper') -Force | Out-Null
$partial = Join-Path $work 'partial.json'
Invoke-Checks (Join-Path $work 'partial.log') @($root, $RhinoSystemDir, $GrasshopperDir, '--tests-only', '--femm', $partialRoot, '--require-femm', '--report', $partial)
if ($LASTEXITCODE -ne 1) { throw 'Incomplete required reference sources must fail' }
$report = Get-Content -LiteralPath $partial -Raw | ConvertFrom-Json
if ($report.Status -ne 'Failed' -or $report.FemmComparison -ne 'Failed' -or $report.Suites[0].Failed -le 0) { throw 'Incomplete references were not reported' }

# A failed preflight must not leave a success manifest from an earlier build.
$staleRoot = Join-Path $work 'stale'
$plugin = Join-Path $staleRoot "Arachne.Grasshopper/Release/$ArachneFramework"
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
$manifest = Join-Path $plugin 'BUILD.json'
'{"RhinoChecksPerformed":true}' | Set-Content -LiteralPath $manifest
$rejected = $false
try { & (Join-Path $root 'scripts/build.ps1') -ArachneBin $staleRoot -RhinoTests -RequireFemm -FemmSourcePath (Join-Path $work 'missing') -GpcAllowCandidate:$GpcAllowCandidate }
catch { if ($_ -notmatch 'Sorgenti FeMM richiesti ma assenti') { throw }; $rejected = $true }
if (!$rejected -or (Test-Path -LiteralPath $manifest)) { throw 'Failed preflight left a success manifest' }

# A changed dependency must fail before compilation and invalidate success too.
$badSet = Join-Path $work 'bad-gpc-set'
New-Item -ItemType Directory -Path $badSet -Force | Out-Null
Copy-Item -Path (Join-Path $root 'lib/GPC/*') -Destination $badSet
[IO.File]::AppendAllText((Join-Path $badSet 'GPCModel.dll'), 'changed')
'{"RhinoChecksPerformed":true}' | Set-Content -LiteralPath $manifest
$rejected = $false
try { & (Join-Path $root 'scripts/build.ps1') -ArachneBin $staleRoot -ReleaseSet (Join-Path $badSet 'release-set.json') -GpcAllowCandidate:$GpcAllowCandidate }
catch { if ($_ -notmatch 'SHA|hash') { throw }; $rejected = $true }
if (!$rejected -or (Test-Path -LiteralPath $manifest)) { throw 'Changed GPC library accepted or stale success retained' }

# A candidate set must be refused without the explicit flag, before compilation, and invalidate success too.
$candidateSet = Join-Path $work 'candidate-gpc-set'
New-Item -ItemType Directory -Path $candidateSet -Force | Out-Null
Copy-Item -Path (Join-Path $root 'lib/GPC/*') -Destination $candidateSet
$candidateManifest = Join-Path $candidateSet 'release-set.json'
$candidate = Get-Content -LiteralPath $candidateManifest -Raw | ConvertFrom-Json
$candidate.candidate = $true
$candidate | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $candidateManifest
'{"RhinoChecksPerformed":true}' | Set-Content -LiteralPath $manifest
$rejected = $false
try { & (Join-Path $root 'scripts/build.ps1') -ArachneBin $staleRoot -ReleaseSet $candidateManifest }
catch { if ($_ -notmatch 'GpcAllowCandidate') { throw }; $rejected = $true }
if (!$rejected -or (Test-Path -LiteralPath $manifest)) { throw 'Candidate GPC set accepted without GpcAllowCandidate or stale success retained' }
Write-Output "Build checks passed. Reports: $work"
