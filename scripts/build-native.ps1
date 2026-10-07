param(
    [Parameter(Mandatory=$true)][string]$ArapSourceDir,
    [Parameter(Mandatory=$true)][string]$IglIncludeDir
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if(!(Test-Path -LiteralPath $vswhere)){throw 'Visual Studio C++ build tools are required.'}
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$msbuild){throw 'Install the Visual Studio C++ x64 build tools and Windows SDK.'}
$source=(Resolve-Path -LiteralPath $ArapSourceDir).Path
$includes=(Resolve-Path -LiteralPath $IglIncludeDir).Path
& $msbuild (Join-Path $projectRoot 'native\Arachne.IGL.vcxproj') -t:Rebuild -p:Configuration=Release -p:Platform=x64 "-p:ArapSourceDir=$source" "-p:IglIncludeDir=$includes" -v:minimal
if($LASTEXITCODE -ne 0){throw 'Native ARAP build failed.'}
$output=Join-Path $projectRoot 'bin\Arachne.IGL\Release'
$provenance=[ordered]@{Source=$source;Includes=$includes;SourceSHA256=(Get-FileHash -LiteralPath (Join-Path $source 'mm_arap.cpp')).Hash;HeaderSHA256=(Get-FileHash -LiteralPath (Join-Path $source 'mm_arap.h')).Hash;BinarySHA256=(Get-FileHash -LiteralPath (Join-Path $output 'Arachne.IGL.dll')).Hash}
$provenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'NATIVE-BUILD.json') -Encoding utf8
