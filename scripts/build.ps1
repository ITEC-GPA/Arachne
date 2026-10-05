param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [string]$TensileLibPath,
    [string]$RhinoSystemDir,
    [string]$GrasshopperDir,
    [switch]$RequireTensile
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $properties = @()
    foreach ($key in @('GpcGeometryPath','GpcUtilitiesPath','TensileLibPath','RhinoSystemDir','GrasshopperDir')) {
        $value = Get-Variable -Name $key -ValueOnly
        if ($value) { $properties += "-p:${key}=$value" }
    }
    $tensilePath = if ($TensileLibPath) { $TensileLibPath } else { Join-Path $projectRoot '..\tensilelib\TensileLib\bin\Release\TensileLib.dll' }
    if ($RequireTensile -and !(Test-Path -LiteralPath $tensilePath)) { throw "TensileLib non trovata: $tensilePath" }
    & dotnet restore Arachne.sln --configfile NuGet.Config @properties
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Arachne.sln -c $Configuration --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & "tests\Arachne.Core.Tests\bin\$Configuration\net48\Arachne.Core.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    $sourceDir = Join-Path $projectRoot "src\Arachne.Grasshopper\bin\$Configuration\net48"
    $destination = Join-Path $projectRoot "artifacts\$Configuration\Arachne"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $files = @('Arachne.Grasshopper.gha','Arachne.Core.dll','GPCGeometry.dll','GPCUtilities.dll','MathNet.Numerics.dll','System.Drawing.Common.dll')
    if (Test-Path -LiteralPath $tensilePath) { $files += @('TensileLib.dll','alglibnet2.dll') }
    foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $sourceDir $name) -Destination $destination -Force }
    # A previous complete build must not leave a stale solver in a skeleton package.
    foreach ($name in @('TensileLib.dll','alglibnet2.dll')) {
        $packagedTensile = Join-Path $destination $name
        if (!(Test-Path -LiteralPath $tensilePath) -and (Test-Path -LiteralPath $packagedTensile)) { Remove-Item -LiteralPath $packagedTensile }
    }
    $status = if (Test-Path -LiteralPath $tensilePath) { 'TensileLib GPC included. See docs/CHANGELOG.md for tests and validation limits.' } else { 'TensileLib missing. Geometry/model components available; no equilibrium calculation.' }
    Set-Content -LiteralPath (Join-Path $destination 'BUILD-STATUS.txt') -Value $status -Encoding utf8
    Write-Host "Plugin: $destination"
    Write-Host $status
}
finally { Pop-Location }
