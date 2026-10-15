param(
    [switch]$Apply,
    [switch]$IncludeCompilerCache,
    [switch]$IncludeVerificationBuild
)

# Run with Unity closed. Defaults to a size report; -Apply removes only the
# regenerable locations listed below. Source assets, packages and Git are kept.
$ErrorActionPreference = 'Stop'
$cleanupRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent)).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath (Join-Path $cleanupRoot 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The script must be located in this Unity project tools directory.'
}
if ($Apply -and (Get-Process -Name Unity -ErrorAction SilentlyContinue)) {
    throw 'Close Unity before clearing generated project files.'
}
$cleanupLocations = @('Library/BuildCache', 'Library/BurstCache', 'Library/ShaderCache', 'Library/PlayerDataCache')
if ($IncludeCompilerCache) { $cleanupLocations += 'Library/Bee' }
if ($IncludeVerificationBuild) { $cleanupLocations += 'Builds/CosmeticsVerification' }

# Resolve and validate every target before deleting any of them.
$cleanupPlan = foreach ($cleanupRelative in $cleanupLocations) {
    $cleanupAbsolute = [IO.Path]::GetFullPath((Join-Path $cleanupRoot $cleanupRelative))
    if (-not $cleanupAbsolute.StartsWith($cleanupRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path outside the project: $cleanupAbsolute"
    }
    if (-not (Test-Path -LiteralPath $cleanupAbsolute)) { continue }
    $cleanupItem = Get-Item -LiteralPath $cleanupAbsolute -Force
    if (-not $cleanupItem.PSIsContainer -or ($cleanupItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Unexpected cache directory: $cleanupAbsolute"
    }
    $cleanupFiles = @(Get-ChildItem -LiteralPath $cleanupAbsolute -Recurse -File -Force)
    [pscustomobject]@{
        relativePath = $cleanupRelative
        absolutePath = $cleanupAbsolute
        files = $cleanupFiles.Count
        bytes = [long](($cleanupFiles | Measure-Object -Property Length -Sum).Sum)
    }
}
foreach ($cleanupTarget in $cleanupPlan) {
    if ($Apply) { Remove-Item -LiteralPath $cleanupTarget.absolutePath -Recurse -Force }
}
[pscustomobject]@{
    applied = [bool]$Apply
    bytes = [long](($cleanupPlan | Measure-Object -Property bytes -Sum).Sum)
    targets = @($cleanupPlan)
} | ConvertTo-Json -Depth 4
