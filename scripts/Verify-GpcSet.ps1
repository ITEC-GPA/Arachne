param(
    [Parameter(Mandatory=$true)][string]$SetDirectory,
    [string]$GeometryPath,
    [string]$UtilitiesPath
)
$ErrorActionPreference = 'Stop'
$manifest = Join-Path $SetDirectory 'release-set.json'
$set = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($set.schema -ne 1 -or $set.candidate -ne $false) { throw 'Arachne requires a stable GPC release set (schema 1, candidate=false).' }
foreach ($name in @('GPCUtilities.dll','GPCGeometry.dll','DelaunayMesh.dll','GPCModel.dll','GPC.Model.Persistence.dll','GPC.Archive.dll')) {
    $entry = @($set.files | Where-Object { $_.file -eq $name })
    if ($entry.Count -ne 1) { throw "Missing or duplicate GPC manifest entry: $name" }
    $path = Join-Path $SetDirectory $name
    if ($name -eq 'GPCGeometry.dll' -and $GeometryPath) { $path = $GeometryPath }
    if ($name -eq 'GPCUtilities.dll' -and $UtilitiesPath) { $path = $UtilitiesPath }
    $stream = [IO.File]::OpenRead($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
    if ($hash -ne $entry[0].sha256) { throw "GPC release-set hash mismatch: $name" }
    if ([Reflection.AssemblyName]::GetAssemblyName($path).Version.ToString() -ne $entry[0].assemblyVersion) { throw "GPC release-set version mismatch: $name" }
}
Write-Host "GPC release set verified: $($set.created)"
