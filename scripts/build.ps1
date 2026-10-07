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
    # The plug-in is the output folder itself; its build record is written only when every check passes
    $pluginDir = Join-Path $projectRoot "bin\Arachne.Grasshopper\$Configuration\net48"
    $buildRecord = Join-Path $pluginDir 'BUILD.json'
    if (Test-Path -LiteralPath $buildRecord) { Remove-Item -LiteralPath $buildRecord }
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
        if($LASTEXITCODE -ne 0){throw 'Rhino/Grasshopper checks failed.'}
    }
    $files = @('Arachne.Grasshopper.gha','Arachne.Common.dll','Arachne.Arap.dll','GPCGeometry.dll','GPCUtilities.dll','MathNet.Numerics.dll','System.Drawing.Common.dll','TensileLib.dll','alglibnet2.dll')
    $manifest = foreach ($name in $files) {
        $binaryPath = Join-Path $pluginDir $name
        [pscustomobject]@{
            File = $name
            Version = [Reflection.AssemblyName]::GetAssemblyName($binaryPath).Version.ToString()
            SHA256 = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash
            Bytes = (Get-Item -LiteralPath $binaryPath).Length
        }
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pluginDir 'DEPENDENCIES.json') -Encoding utf8
    $revision = & (Join-Path $PSScriptRoot 'Get-Revision.ps1')
    [pscustomobject]@{ArachneCommit=$revision;RhinoChecksPerformed=[bool]$RhinoTests;ManagedArapChecksPerformed=$true;BuiltUtc=[DateTime]::UtcNow.ToString('o');Framework='net48';Platform='Windows x64';ArapEngine='Managed C# netstandard2.0'} | ConvertTo-Json | Set-Content -LiteralPath $buildRecord -Encoding utf8
    Write-Host "Plugin: $pluginDir"
}
finally { Pop-Location }
