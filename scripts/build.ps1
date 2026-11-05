param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$GpcGeometryPath,
    [string]$GpcUtilitiesPath,
    [string]$TensileLibPath,
    [string]$NativeIglPath,
    [string]$RhinoSystemDir,
    [string]$GrasshopperDir,
    [switch]$RhinoTests,
    # FeMM sources of the inventory checks (default ../FeMM 30-5-2024/Test1/FeMM)
    [string]$FemmSources,
    # release-set.json of the GPC set (Checker/build/Release-Set.ps1): the GPC libraries of the plug-in must be its files
    [string]$ReleaseSet
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    # The GPC projects of ../Model are outside Arachne.sln: without this a solution build compiles them in Debug
    # (Directory.Build.props sets it for Arachne's projects; here it reaches the Model projects too).
    $properties = @('-p:ShouldUnsetParentConfigurationAndPlatform=false')
    foreach ($key in @('GpcGeometryPath','GpcUtilitiesPath','TensileLibPath','NativeIglPath','RhinoSystemDir','GrasshopperDir')) {
        $value = Get-Variable -Name $key -ValueOnly
        if ($value) { $properties += "-p:${key}=$value" }
    }
    $tensilePath = if ($TensileLibPath) { $TensileLibPath } else { Join-Path $projectRoot '..\tensilelib\TensileLib\bin\Release\TensileLib.dll' }
    if (!(Test-Path -LiteralPath $tensilePath)) { throw "TensileLib non trovata: $tensilePath" }
    if ($ReleaseSet -and !(Test-Path -LiteralPath $ReleaseSet)) { throw "Set GPC non trovato: $ReleaseSet" }
    # The plug-in is the output folder itself; its build record is written only when every check passes
    $pluginDir = Join-Path $projectRoot "bin\Arachne.Grasshopper\$Configuration\net48"
    $buildRecord = Join-Path $pluginDir 'BUILD.json'
    if (Test-Path -LiteralPath $buildRecord) { Remove-Item -LiteralPath $buildRecord }
    & dotnet restore Arachne.sln --configfile NuGet.Config @properties
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Arachne.sln -c $Configuration --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    # What the plug-in folder must hold, and nothing of what it must not (Gmsh is for the unit tests of Model only;
    # NRBF and its packages left with the FeMM payloads)
    $files = @('Arachne.Grasshopper.gha','Arachne.Common.dll','Arachne.Arap.dll',
        'GPCGeometry.dll','GPCUtilities.dll','DelaunayMesh.dll','GPCModel.dll','GPC.Model.Persistence.dll','GPC.Archive.dll',
        'TensileLib.dll','alglibnet2.dll','MathNet.Numerics.dll')
    $missing = @($files | Where-Object { !(Test-Path -LiteralPath (Join-Path $pluginDir $_)) })
    if ($missing.Count -gt 0) { throw "Mancano nel plug-in: $($missing -join ', ')" }
    $forbidden = @(Get-ChildItem -LiteralPath $pluginDir -File | Where-Object {
        $_.Name -match '^(gmsh|GMsh\.Net|UnsafeEx|GMesh)' -or $_.Name -match '^System\.(Formats\.Nrbf|Reflection\.Metadata|Collections\.Immutable|Memory|Buffers)\.dll$' -or $_.Name -eq 'Microsoft.Bcl.HashCode.dll' })
    if ($forbidden.Count -gt 0) { throw "Non devono stare nel plug-in: $($forbidden.Name -join ', ')" }
    $assemblies = @(Get-ChildItem -LiteralPath $pluginDir -File | Where-Object { $_.Extension -in '.dll','.gha' } | ForEach-Object { $_.Name })
    $unlisted = @($assemblies | Where-Object { $files -notcontains $_ })
    if ($unlisted.Count -gt 0) { throw "DLL non previste nel plug-in: $($unlisted -join ', ')" }

    # The shared GPC libraries: the files of the set (same SHA-256), otherwise the Release outputs of the GPC checkouts
    $setRecord = $null
    $shared = @($files | Where-Object { $_ -like 'GPC*' -or $_ -eq 'DelaunayMesh.dll' })
    if ($ReleaseSet) {
        $set = Get-Content -LiteralPath $ReleaseSet -Raw | ConvertFrom-Json
        $differences = foreach ($name in $shared) {
            $entry = @($set.files | Where-Object { $_.file -eq $name })
            $actual = (Get-FileHash -LiteralPath (Join-Path $pluginDir $name) -Algorithm SHA256).Hash
            if ($entry.Count -ne 1) { "$name manca nel set" }
            elseif ($entry[0].sha256 -ne $actual) { "$name $($entry[0].assemblyVersion): SHA-256 $actual invece di $($entry[0].sha256)" }
        }
        if ($differences) { throw "Le librerie GPC del plug-in non sono quelle del set ${ReleaseSet}: $($differences -join '; ')" }
        $setRecord = [pscustomobject]@{ Created = $set.created; Candidate = [bool]$set.candidate; SHA256 = (Get-FileHash -LiteralPath $ReleaseSet -Algorithm SHA256).Hash }
    }

    & "bin\Arachne.Arap.Tests\$Configuration\net48\Arachne.Arap.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Managed ARAP checks failed.' }
    if($RhinoTests) {
        $rhinoDir=if($RhinoSystemDir){$RhinoSystemDir}else{'C:\Program Files\Rhino 8\System'}
        $ghDir=if($GrasshopperDir){$GrasshopperDir}else{'C:\Program Files\Rhino 8\Plug-ins\Grasshopper'}
        $femm = if ($FemmSources) { @('--femm', $FemmSources) } else { @() }
        # Components, FeMM inputs/outputs, form finding and patterning, in a headless Rhino
        & "bin\Arachne.ExampleWriter\$Configuration\net48\Arachne.ExampleWriter.exe" $projectRoot $rhinoDir $ghDir --tests-only @femm
        if($LASTEXITCODE -ne 0){throw 'Rhino/Grasshopper checks failed.'}
    }
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
    [pscustomobject]@{ArachneCommit=$revision;RhinoChecksPerformed=[bool]$RhinoTests;ManagedArapChecksPerformed=$true;GpcReleaseSet=$setRecord;BuiltUtc=[DateTime]::UtcNow.ToString('o');Framework='net48';Platform='Windows x64';ArapEngine='Managed C# netstandard2.0'} | ConvertTo-Json | Set-Content -LiteralPath $buildRecord -Encoding utf8
    Write-Host "Plugin: $pluginDir"
}
finally { Pop-Location }
