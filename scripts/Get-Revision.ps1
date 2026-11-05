# Commit of the working copy, for the build records. Without git on the PATH it is read from .git
# (a directory in a checkout, a "gitdir:" file in a worktree, whose refs are in the common git directory)
$root = Split-Path -Parent $PSScriptRoot
if (Get-Command git -ErrorAction SilentlyContinue) { return (& git -C $root rev-parse HEAD) }
$gitDir = Join-Path $root '.git'
if (Test-Path -LiteralPath $gitDir -PathType Leaf) {
    $line = (Get-Content -LiteralPath $gitDir -TotalCount 1).Trim()
    if ($line -notlike 'gitdir:*') { return '' }
    $gitDir = [IO.Path]::GetFullPath((Join-Path $root $line.Substring(7).Trim()))
}
$commonDir = $gitDir
$commonFile = Join-Path $gitDir 'commondir'
if (Test-Path -LiteralPath $commonFile) { $commonDir = [IO.Path]::GetFullPath((Join-Path $gitDir (Get-Content -LiteralPath $commonFile -TotalCount 1).Trim())) }
$headPath = Join-Path $gitDir 'HEAD'
if (!(Test-Path -LiteralPath $headPath)) { return '' }
$head = (Get-Content -LiteralPath $headPath -TotalCount 1).Trim()
if ($head -notlike 'ref: *') { return $head }
$ref = $head.Substring(5)
foreach ($dir in @($gitDir, $commonDir)) {
    $refPath = Join-Path $dir $ref
    if (Test-Path -LiteralPath $refPath) { return (Get-Content -LiteralPath $refPath -TotalCount 1).Trim() }
}
$packedPath = Join-Path $commonDir 'packed-refs'
if (Test-Path -LiteralPath $packedPath) {
    $line = Select-String -LiteralPath $packedPath -SimpleMatch " $ref" | Select-Object -First 1
    if ($line) { return $line.Line.Split(' ')[0] }
}
return ''
