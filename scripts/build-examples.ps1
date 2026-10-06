param(
    [string]$RhinoSystemDir = 'C:\Program Files\Rhino 8\System',
    [string]$GrasshopperDir = 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & dotnet build tools/Arachne.ExampleWriter/Arachne.ExampleWriter.csproj -c Release --configfile NuGet.Config "-p:RhinoSystemDir=$RhinoSystemDir" "-p:GrasshopperDir=$GrasshopperDir"
    if ($LASTEXITCODE -ne 0) { throw 'Example writer build failed.' }
    $validationPath = Join-Path $projectRoot 'examples\VALIDATION.txt'
    if (Test-Path -LiteralPath $validationPath) { Remove-Item -LiteralPath $validationPath }
    & bin/Arachne.ExampleWriter/Release/net48/Arachne.ExampleWriter.exe $projectRoot $RhinoSystemDir $GrasshopperDir --five-examples
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $validationPath)) { throw 'Examples did not complete all calculation and archive checks.' }
    Write-Host 'Five examples calculated, saved and reopened in both GH and GHX; all 43 components covered.'
}
finally { Pop-Location }
