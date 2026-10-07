# Commit of the working copy, for the build records. Without git on the PATH it is read from .git
$root = Split-Path -Parent $PSScriptRoot
if (Get-Command git -ErrorAction SilentlyContinue) { return (& git -C $root rev-parse HEAD) }
$gitDir = Join-Path $root '.git'
$headPath = Join-Path $gitDir 'HEAD'
if (!(Test-Path -LiteralPath $headPath)) { return '' }
$head = (Get-Content -LiteralPath $headPath -TotalCount 1).Trim()
if ($head -notlike 'ref: *') { return $head }
$ref = $head.Substring(5)
$refPath = Join-Path $gitDir $ref
if (Test-Path -LiteralPath $refPath) { return (Get-Content -LiteralPath $refPath -TotalCount 1).Trim() }
$packedPath = Join-Path $gitDir 'packed-refs'
if (Test-Path -LiteralPath $packedPath) {
    $line = Select-String -LiteralPath $packedPath -SimpleMatch " $ref" | Select-Object -First 1
    if ($line) { return $line.Line.Split(' ')[0] }
}
return ''
