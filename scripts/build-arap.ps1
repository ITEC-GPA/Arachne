param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [switch]$CompareNative,
    [string]$NativeIglPath
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $properties=@()
    foreach($key in @('GpcGeometryPath','GpcUtilitiesPath','NativeIglPath')) {
        $value=Get-Variable -Name $key -ValueOnly
        if($value){$properties+="-p:${key}=$value"}
    }
    & dotnet restore Arachne.Arap.sln --configfile NuGet.Config @properties
    if($LASTEXITCODE -ne 0){throw 'ARAP restore failed.'}
    & dotnet build Arachne.Arap.sln -c $Configuration --no-restore @properties
    if($LASTEXITCODE -ne 0){throw 'ARAP build failed.'}
    $testArgs=@()
    if($CompareNative){$testArgs+='--native'}
    & "bin\Arachne.Arap.Tests\$Configuration\net48\Arachne.Arap.Tests.exe" @testArgs
    if($LASTEXITCODE -ne 0){throw 'ARAP checks failed; package not updated.'}
    $destination=Join-Path $projectRoot "artifacts\$Configuration\Arachne.Arap"
    $source=Join-Path $projectRoot "bin\Arachne.Arap\$Configuration\netstandard2.0"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $files=@('Arachne.Arap.dll','Arachne.Arap.xml','GPCGeometry.dll','GPCUtilities.dll')
    foreach($file in $files){Copy-Item -LiteralPath (Join-Path $source $file) -Destination $destination -Force}
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Arachne.Arap\README.md') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\ARAP-REWRITE.md') -Destination $destination -Force
    $manifest=foreach($file in $files){[pscustomobject]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path $destination $file)).Hash}}
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'DEPENDENCIES.json') -Encoding utf8
    [pscustomobject]@{Commit=(& git rev-parse HEAD);BuiltUtc=[DateTime]::UtcNow.ToString('o');Framework='netstandard2.0';ManagedChecksPerformed=$true;NativeComparisonsPerformed=[bool]$CompareNative} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'BUILD.json') -Encoding utf8
    Write-Host "Standalone managed ARAP: $destination"
}
finally {Pop-Location}
