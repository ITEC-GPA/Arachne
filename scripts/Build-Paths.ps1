# Shared by the build entry points; relative output paths are rooted at the checkout.
function Get-ArachneOutputRoot([string]$ProjectRoot, [string]$ArachneBin, [string]$Configuration, [switch]$Plugin) {
    $candidate = if ($ArachneBin) { $ArachneBin } else { 'bin' }
    if (![IO.Path]::IsPathRooted($candidate)) { $candidate = Join-Path $ProjectRoot $candidate }
    $output = [IO.Path]::GetFullPath($candidate).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $installed = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'bin')).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    if ($Plugin -and $Configuration -eq 'Debug' -and $output.StartsWith($installed, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Debug richiede -ArachneBin esterno alla cartella bin caricata da Grasshopper.'
    }
    return $output
}
