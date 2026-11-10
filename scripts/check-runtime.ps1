param(
    [string]$ArachneBin = 'bin',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$FemmSourcePath,
    [switch]$RequireFemm,
    [switch]$DisableBinaryFormatter,
    [string]$RhinoSystemDir = 'C:\Program Files\Rhino 8\System',
    [string]$GrasshopperDir = 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Build-Paths.ps1')
$output = Get-ArachneOutputRoot $root $ArachneBin $Configuration -Plugin
$exe = Join-Path $output "Arachne.ExampleWriter/$Configuration/net48/Arachne.ExampleWriter.exe"
if (!(Test-Path -LiteralPath $exe)) { throw 'Build Arachne with scripts/build.ps1 first.' }
Push-Location $root
try {
    & dotnet build tools/Arachne.RuntimeChecks/Arachne.RuntimeChecks.csproj -c $Configuration --configfile NuGet.Config "-p:ArachneBin=$($output.TrimEnd([char[]]'\/'))"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime host build failed.' }
    $directory = Join-Path $output "Arachne.RuntimeChecks/$Configuration/net8.0-windows"
    $suffix = if ($DisableBinaryFormatter) { 'disabled' } else { 'desktop' }
    $arguments = @($exe, $root, $RhinoSystemDir, $GrasshopperDir, '--tests-only', '--report', (Join-Path $directory "CHECKS-$suffix.json"))
    if ($FemmSourcePath) { $arguments += @('--femm', $FemmSourcePath) }
    if ($RequireFemm) { $arguments += '--require-femm' }
    if ($DisableBinaryFormatter) { $arguments += '--disable-binary-formatter' }
    & (Join-Path $directory 'Arachne.RuntimeChecks.exe') @arguments | Tee-Object -FilePath (Join-Path $directory "CHECKS-$suffix.log")
    if ($LASTEXITCODE -ne 0) { throw "Runtime checks failed; see $directory/CHECKS-$suffix.json" }
}
finally { Pop-Location }
