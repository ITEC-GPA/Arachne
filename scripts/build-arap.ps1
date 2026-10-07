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
    if($LASTEXITCODE -ne 0){throw 'ARAP checks failed.'}
    # The library is its output folder; the build record goes next to it
    $destination=Join-Path $projectRoot "bin\Arachne.Arap\$Configuration\netstandard2.0"
    $files=@('Arachne.Arap.dll','Arachne.Arap.xml','GPCGeometry.dll','GPCUtilities.dll') | Where-Object { Test-Path -LiteralPath (Join-Path $destination $_) }
    $manifest=foreach($file in $files){[pscustomobject]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path $destination $file)).Hash}}
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'DEPENDENCIES.json') -Encoding utf8
    [pscustomobject]@{Commit=(& (Join-Path $PSScriptRoot 'Get-Revision.ps1'));BuiltUtc=[DateTime]::UtcNow.ToString('o');Framework='netstandard2.0';ManagedChecksPerformed=$true;NativeComparisonsPerformed=[bool]$CompareNative} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'BUILD.json') -Encoding utf8
    Write-Host "Standalone managed ARAP: $destination"
}
finally {Pop-Location}
