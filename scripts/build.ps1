param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$ArachneBin,
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [string]$TensileLibPath,
    [string]$NativeIglPath,
    [string]$RhinoSystemDir,
    [string]$GrasshopperDir,
    [switch]$RhinoTests,
    [string]$FemmSourcePath,
    [switch]$RequireFemm,
    [string]$ReleaseSet
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Build-Paths.ps1')
$outputRoot = Get-ArachneOutputRoot $projectRoot $ArachneBin $Configuration -Plugin
Push-Location $projectRoot
try {
    $properties = @("-p:ArachneBin=$($outputRoot.TrimEnd([char[]]'\/'))")
    $gpcDirectory = Join-Path $projectRoot 'lib/GPC'
    if ($ReleaseSet) {
        $ReleaseSet = (Resolve-Path -LiteralPath $ReleaseSet).Path
        if ([IO.Path]::GetFileName($ReleaseSet) -ne 'release-set.json') { throw 'ReleaseSet must point to release-set.json next to the GPC DLLs.' }
        $gpcDirectory = Split-Path -Parent $ReleaseSet
        $properties += "-p:GpcSetDirectory=$gpcDirectory"
    }
    & (Join-Path $PSScriptRoot 'Verify-GpcSet.ps1') -SetDirectory $gpcDirectory -GeometryPath $GpcGeometryPath -UtilitiesPath $GpcUtilitiesPath
    foreach ($key in @('GpcGeometryPath','GpcUtilitiesPath','TensileLibPath','NativeIglPath','RhinoSystemDir','GrasshopperDir')) {
        $value = Get-Variable -Name $key -ValueOnly
        if ($value) { $properties += "-p:${key}=$value" }
    }
    $tensilePath = if ($TensileLibPath) { $TensileLibPath } else { Join-Path $projectRoot '..\tensilelib\TensileLib\bin\Release\TensileLib.dll' }
    if (!(Test-Path -LiteralPath $tensilePath)) { throw "TensileLib non trovata: $tensilePath" }
    # The plug-in is the output folder itself; its build record is written only when every check passes
    $pluginDir = Join-Path $outputRoot "Arachne.Grasshopper\$Configuration\net48"
    $buildRecord = Join-Path $pluginDir 'BUILD.json'
    if (Test-Path -LiteralPath $buildRecord) { Remove-Item -LiteralPath $buildRecord }
    if ($RequireFemm -and !$RhinoTests) { throw '-RequireFemm richiede -RhinoTests.' }
    $femmPath = if ($FemmSourcePath) { $FemmSourcePath } else { Join-Path $projectRoot '..\FeMM 30-5-2024\Test1\FeMM' }
    $femmPath = [IO.Path]::GetFullPath($femmPath)
    if ($RequireFemm -and !(Test-Path -LiteralPath (Join-Path $femmPath 'FeMM.Grasshopper'))) { throw "Sorgenti FeMM richiesti ma assenti: $femmPath" }
    & dotnet restore Arachne.sln --configfile NuGet.Config @properties
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Arachne.sln -c $Configuration --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & (Join-Path $outputRoot "Arachne.Arap.Tests\$Configuration\net48\Arachne.Arap.Tests.exe") | Tee-Object -FilePath (Join-Path $pluginDir 'ARAP-CHECKS.log')
    if ($LASTEXITCODE -ne 0) { throw 'Managed ARAP checks failed.' }
    $arapSummary = [regex]::Match((Get-Content -Raw (Join-Path $pluginDir 'ARAP-CHECKS.log')), 'ARAP checks: (\d+) passed, (\d+) failed\.')
    if (!$arapSummary.Success) { throw 'ARAP summary missing.' }
    $rhinoReport = $null
    $reportPath = Join-Path $pluginDir 'RHINO-CHECKS.json'
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
    if($RhinoTests) {
        $rhinoDir=if($RhinoSystemDir){$RhinoSystemDir}else{'C:\Program Files\Rhino 8\System'}
        $ghDir=if($GrasshopperDir){$GrasshopperDir}else{'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'}
        # Components, FeMM inputs/outputs, form finding and patterning, in a headless Rhino
        $checkArgs = @($projectRoot, $rhinoDir, $ghDir, '--tests-only', '--femm', $femmPath, '--report', $reportPath)
        if ($RequireFemm) { $checkArgs += '--require-femm' }
        & (Join-Path $outputRoot "Arachne.ExampleWriter\$Configuration\net48\Arachne.ExampleWriter.exe") @checkArgs | Tee-Object -FilePath (Join-Path $pluginDir 'RHINO-CHECKS.log')
        if($LASTEXITCODE -ne 0){throw 'Rhino/Grasshopper checks failed.'}
        $rhinoReport = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
        if ($rhinoReport.Status -notin @('Passed','PassedWithSkips')) { throw 'Rhino check report did not pass.' }
        if ($RequireFemm -and $rhinoReport.FemmComparison -ne 'Passed') { throw 'FeMM comparison incomplete.' }
    }
    $files = @('Arachne.Grasshopper.gha','Arachne.Common.dll','Arachne.Arap.dll','GPCGeometry.dll','GPCUtilities.dll','DelaunayMesh.dll','GPCModel.dll','GPC.Model.Persistence.dll','GPC.Archive.dll','MathNet.Numerics.dll','TensileLib.dll','alglibnet2.dll')
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
    $set = Get-Content -LiteralPath (Join-Path $gpcDirectory 'release-set.json') -Raw | ConvertFrom-Json
    foreach ($entry in @($manifest | Where-Object { $_.File -like 'GPC*' -or $_.File -eq 'DelaunayMesh.dll' })) {
        $expected = @($set.files | Where-Object { $_.file -eq $entry.File })
        if ($expected.Count -ne 1 -or $entry.SHA256 -ne $expected[0].sha256) { throw "Distributed GPC DLL differs from release set: $($entry.File)" }
    }
    $unlisted = @(Get-ChildItem -LiteralPath $pluginDir -File | Where-Object { $_.Extension -in '.dll','.gha' -and $_.Name -notin $files })
    if ($unlisted.Count) { throw "Unexpected plug-in assemblies: $($unlisted.Name -join ', ')" }
    Copy-Item -LiteralPath (Join-Path $gpcDirectory 'release-set.json') -Destination (Join-Path $pluginDir 'gpc-release-set.json') -Force
    $revision = & (Join-Path $PSScriptRoot 'Get-Revision.ps1')
    [pscustomobject]@{
        ArachneCommit=$revision
        GpcReleaseSet=[pscustomobject]@{Created=$set.created;SHA256=(Get-FileHash -LiteralPath (Join-Path $gpcDirectory 'release-set.json')).Hash;Candidate=$false}
        RhinoChecksPerformed=[bool]$RhinoTests
        ManagedArapChecksPerformed=$true
        ManagedArapChecks=[pscustomobject]@{Status='Passed';Passed=[int]$arapSummary.Groups[1].Value;Failed=[int]$arapSummary.Groups[2].Value;Skipped=0}
        RhinoChecks=if($rhinoReport){$rhinoReport}else{[pscustomobject]@{Status='NotRun';FemmComparison='NotRun'}}
        BuiltUtc=[DateTime]::UtcNow.ToString('o')
        Framework='net48';Platform='Windows x64';ArapEngine='Managed C# netstandard2.0'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $buildRecord -Encoding utf8
    Write-Host "Plugin: $pluginDir"
}
finally { Pop-Location }
