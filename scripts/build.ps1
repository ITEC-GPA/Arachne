param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [string]$TensileLibPath,
    [string]$NativeIglPath,
    [string]$RhinoSystemDir,
    [string]$GrasshopperDir,
    [switch]$RhinoTests
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $properties = @()
    foreach ($key in @('GpcGeometryPath','GpcUtilitiesPath','TensileLibPath','NativeIglPath','RhinoSystemDir','GrasshopperDir')) {
        $value = Get-Variable -Name $key -ValueOnly
        if ($value) { $properties += "-p:${key}=$value" }
    }
    $tensilePath = if ($TensileLibPath) { $TensileLibPath } else { Join-Path $projectRoot '..\tensilelib\TensileLib\bin\Release\TensileLib.dll' }
    if (!(Test-Path -LiteralPath $tensilePath)) { throw "TensileLib non trovata: $tensilePath" }
    & dotnet restore Arachne.sln --configfile NuGet.Config @properties
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Arachne.sln -c $Configuration --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & "bin\Arachne.Arap.Tests\$Configuration\net48\Arachne.Arap.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Managed ARAP checks failed.' }
    if($RhinoTests) {
        $rhinoDir=if($RhinoSystemDir){$RhinoSystemDir}else{'C:\Program Files\Rhino 8\System'}
        $ghDir=if($GrasshopperDir){$GrasshopperDir}else{'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'}
        # Components, FeMM inputs/outputs, form finding and patterning, in a headless Rhino
        & "bin\Arachne.ExampleWriter\$Configuration\net48\Arachne.ExampleWriter.exe" $projectRoot $rhinoDir $ghDir --tests-only
        if($LASTEXITCODE -ne 0){throw 'Rhino/Grasshopper checks failed. Release package was not updated.'}
    }
    $sourceDir = Join-Path $projectRoot "bin\Arachne.Grasshopper\$Configuration\net48"
    $destination = Join-Path $projectRoot "artifacts\$Configuration\Arachne"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    # The previous packages contained Arachne.Core.dll: the whole folder is replaced
    Get-ChildItem -LiteralPath $destination -File | Remove-Item
    $files = @('Arachne.Grasshopper.gha','Arachne.Common.dll','Arachne.Arap.dll','GPCGeometry.dll','GPCUtilities.dll','MathNet.Numerics.dll','System.Drawing.Common.dll','TensileLib.dll','alglibnet2.dll')
    foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $sourceDir $name) -Destination $destination -Force }
    $manifest = foreach ($name in $files) {
        $binaryPath = Join-Path $destination $name
        [pscustomobject]@{
            File = $name
            Version = [Reflection.AssemblyName]::GetAssemblyName($binaryPath).Version.ToString()
            SHA256 = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash
            Bytes = (Get-Item -LiteralPath $binaryPath).Length
        }
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'DEPENDENCIES.json') -Encoding utf8
    foreach($doc in @('PORTING.md','ARAP-REWRITE.md')){
        $docPath=Join-Path $projectRoot "docs\$doc"
        if(Test-Path -LiteralPath $docPath){Copy-Item -LiteralPath $docPath -Destination $destination -Force}
    }
    $git = Get-Command git -ErrorAction SilentlyContinue
    $revision = if ($git) { & git rev-parse HEAD } else { '' }
    [pscustomobject]@{ArachneCommit=$revision;RhinoChecksPerformed=[bool]$RhinoTests;ManagedArapChecksPerformed=$true;BuiltUtc=[DateTime]::UtcNow.ToString('o');Framework='net48';Platform='Windows x64';ArapEngine='Managed C# netstandard2.0'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'BUILD.json') -Encoding utf8
    Write-Host "Plugin: $destination"
}
finally { Pop-Location }
