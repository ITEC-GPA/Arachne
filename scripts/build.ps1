param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [string]$TensileLibPath,
    [string]$NativeIglPath,
    [string]$RhinoSystemDir,
    [string]$GrasshopperDir,
    [switch]$RequireTensile,
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
    if ($RequireTensile -and !(Test-Path -LiteralPath $tensilePath)) { throw "TensileLib non trovata: $tensilePath" }
    & dotnet restore Arachne.sln --configfile NuGet.Config @properties
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Arachne.sln -c $Configuration --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & "bin\Arachne.Core.Tests\$Configuration\net48\Arachne.Core.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    & "bin\Arachne.Arap.Tests\$Configuration\net48\Arachne.Arap.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Managed ARAP checks failed.' }
    if($RhinoTests) {
        if($Configuration -ne 'Release'){throw 'The Rhino verification tool currently requires Release.'}
        $rhinoDir=if($RhinoSystemDir){$RhinoSystemDir}else{'C:\Program Files\Rhino 8\System'}
        $ghDir=if($GrasshopperDir){$GrasshopperDir}else{'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'}
        & "bin\Arachne.ExampleWriter\Release\net48\Arachne.ExampleWriter.exe" $projectRoot $rhinoDir $ghDir --tests-only
        if($LASTEXITCODE -ne 0){throw 'Rhino/Grasshopper checks failed. Release package was not updated.'}
    }
    $sourceDir = Join-Path $projectRoot "bin\Arachne.Grasshopper\$Configuration\net48"
    $destination = Join-Path $projectRoot "artifacts\$Configuration\Arachne"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $files = @('Arachne.Grasshopper.gha','Arachne.Core.dll','Arachne.Arap.dll','GPCGeometry.dll','GPCUtilities.dll','MathNet.Numerics.dll','System.Drawing.Common.dll')
    if (Test-Path -LiteralPath $tensilePath) { $files += @('TensileLib.dll','alglibnet2.dll') }
    foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $sourceDir $name) -Destination $destination -Force }
    $manifest = foreach ($name in $files) {
        $binaryPath = Join-Path $destination $name
        try { $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($binaryPath).Version.ToString() }
        catch { $assemblyVersion = 'native x64' }
        [pscustomobject]@{
            File = $name
            Version = $assemblyVersion
            SHA256 = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash
            Bytes = (Get-Item -LiteralPath $binaryPath).Length
        }
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'DEPENDENCIES.json') -Encoding utf8
    # The original native binary remains available only for optional comparison tests.
    foreach($obsolete in @('Arachne.IGL.dll','NATIVE-BUILD.json')) {
        $obsoletePath=Join-Path $destination $obsolete
        if(Test-Path -LiteralPath $obsoletePath){Remove-Item -LiteralPath $obsoletePath}
    }
    foreach($doc in @('RELEASE-1.md','RESULTS.md','ARAP-REWRITE.md','FIVE-EXAMPLES.md')){
        $docPath=Join-Path $projectRoot "docs\$doc"
        if(Test-Path -LiteralPath $docPath){Copy-Item -LiteralPath $docPath -Destination $destination -Force}
    }
    $revision=& git rev-parse HEAD
    [pscustomobject]@{ArachneCommit=$revision;RhinoChecksPerformed=[bool]$RhinoTests;ManagedArapChecksPerformed=$true;BuiltUtc=[DateTime]::UtcNow.ToString('o');Framework='net48';Platform='Windows x64';ArapEngine='Managed C# netstandard2.0'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'BUILD.json') -Encoding utf8
    # A previous complete build must not leave a stale solver in a skeleton package.
    foreach ($name in @('TensileLib.dll','alglibnet2.dll')) {
        $packagedTensile = Join-Path $destination $name
        if (!(Test-Path -LiteralPath $tensilePath) -and (Test-Path -LiteralPath $packagedTensile)) { Remove-Item -LiteralPath $packagedTensile }
    }
    $status = if (Test-Path -LiteralPath $tensilePath) { 'Arachne release candidate with TensileLib GPC and managed C# ARAP. Read ARAP-REWRITE.md, RELEASE-1.md and RESULTS.md for verified behavior and remaining validation.' } else { 'TensileLib missing. Geometry/model components available; no equilibrium calculation.' }
    Set-Content -LiteralPath (Join-Path $destination 'BUILD-STATUS.txt') -Value $status -Encoding utf8
    Write-Host "Plugin: $destination"
    Write-Host $status
}
finally { Pop-Location }
