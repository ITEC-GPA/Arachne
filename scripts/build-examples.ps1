param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$ArachneBin,
    [string]$ExamplesRoot,
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [string]$TensileLibPath,
    [string]$RhinoSystemDir = 'C:\Program Files\Rhino 8\System',
    [string]$GrasshopperDir = 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Build-Paths.ps1')
$outputRoot = Get-ArachneOutputRoot $projectRoot $ArachneBin $Configuration -Plugin
$exampleDirectory = $projectRoot
if ($ExamplesRoot) {
    $candidate = if ([IO.Path]::IsPathRooted($ExamplesRoot)) { $ExamplesRoot } else { Join-Path $projectRoot $ExamplesRoot }
    $exampleDirectory = [IO.Path]::GetFullPath($candidate)
}
$properties = @("-p:ArachneBin=$($outputRoot.TrimEnd([char[]]'\/'))")
foreach ($key in @('GpcGeometryPath','GpcUtilitiesPath','TensileLibPath','RhinoSystemDir','GrasshopperDir')) {
    $value = Get-Variable -Name $key -ValueOnly
    if ($value) { $properties += "-p:${key}=$value" }
}
Push-Location $projectRoot
try {
    & dotnet build tools/Arachne.ExampleWriter/Arachne.ExampleWriter.csproj -c $Configuration --configfile NuGet.Config @properties
    if ($LASTEXITCODE -ne 0) { throw 'Example writer build failed.' }
    # Writes examples 01-03 and docs/ICONS.svg; every example is reopened and recalculated
    New-Item -ItemType Directory -Force -Path (Join-Path $exampleDirectory 'examples'), (Join-Path $exampleDirectory 'docs') | Out-Null
    & (Join-Path $outputRoot "Arachne.ExampleWriter/$Configuration/net48/Arachne.ExampleWriter.exe") $exampleDirectory $RhinoSystemDir $GrasshopperDir --examples
    if ($LASTEXITCODE -ne 0) { throw 'Examples did not complete all calculation and archive checks.' }
    Write-Host 'Examples calculated, saved and reopened in both GH and GHX.'
}
finally { Pop-Location }
