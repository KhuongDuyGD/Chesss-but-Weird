# Verify the explicit cleanup ledger; this script never deletes or moves files.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$reportFolder = Join-Path $projectRoot 'Documentation/UI/MenuAssetCleanup'
$manifest = Get-Content -LiteralPath (Join-Path $reportFolder 'MenuAssetCleanupManifest.json') -Raw | ConvertFrom-Json
$checks = [Collections.Generic.List[string]]::new()

function Assert-Cleanup([bool]$condition, [string]$message) {
    $checks.Add($(if ($condition) { 'PASS ' } else { 'FAIL ' }) + $message)
    if (-not $condition) { throw $message }
}

Push-Location $projectRoot
try {
    foreach ($asset in $manifest.deletedTextures) {
        Assert-Cleanup (-not (Test-Path -LiteralPath $asset.path) -and -not (Test-Path -LiteralPath ($asset.path + '.meta'))) ('Retired texture and metadata are absent: ' + $asset.path)
    }
    foreach ($asset in $manifest.preservedMenuTextures) {
        Assert-Cleanup ((Test-Path -LiteralPath $asset.path) -and (Test-Path -LiteralPath ($asset.path + '.meta'))) ('Protected texture and metadata exist: ' + $asset.path)
        $actualHash = (Get-FileHash -LiteralPath $asset.path -Algorithm SHA256).Hash
        $actualGuid = [regex]::Match((Get-Content -LiteralPath ($asset.path + '.meta') -Raw), '(?m)^guid: (\w+)').Groups[1].Value
        Assert-Cleanup ($actualHash -eq $asset.sha256 -and $actualGuid -eq $asset.guid) ('Protected artwork bytes and GUID are unchanged: ' + $asset.path)
    }
    $logoHash = (Get-FileHash -LiteralPath $manifest.preservedOriginalLogo -Algorithm SHA256).Hash
    Assert-Cleanup ($logoHash -eq $manifest.preservedOriginalLogoSha256) 'The single retained logo matches the original drawing byte for byte'

    $patternPath = Join-Path $projectRoot 'Logs/menu-cleanup-retired-guids.txt'
    $manifest.deletedTextures.guid | Set-Content -LiteralPath $patternPath
    $guidReferences = @(& rg --files-with-matches --fixed-strings --ignore-case --file $patternPath Assets ProjectSettings Packages)
    $scanExitCode = $LASTEXITCODE
    Assert-Cleanup ($scanExitCode -eq 1 -and $guidReferences.Count -eq 0) ('No deleted texture GUID remains referenced in game files' + $(if ($guidReferences.Count) { ': ' + ($guidReferences -join ', ') } else { '' }))

    $group = Get-Content -LiteralPath 'Assets/AddressableAssetsData/AssetGroups/CoreUI.asset' -Raw
    foreach ($asset in ($manifest.deletedTextures | Where-Object path -Like 'Assets/Materials/*')) {
        Assert-Cleanup (-not $group.Contains($asset.path)) ('CoreUI no longer preloads: ' + $asset.path)
    }
    foreach ($entry in [regex]::Matches($group, '(?ms)^  - m_GUID: (?<guid>[a-f0-9]+)\r?\n    m_Address: (?<path>[^\r\n]+)')) {
        $assetPath = $entry.Groups['path'].Value
        Assert-Cleanup ((Test-Path -LiteralPath $assetPath) -and (Test-Path -LiteralPath ($assetPath + '.meta'))) ('CoreUI entry resolves to an existing asset: ' + $assetPath)
        $assetGuid = [regex]::Match((Get-Content -LiteralPath ($assetPath + '.meta') -Raw), '(?m)^guid: (\w+)').Groups[1].Value
        Assert-Cleanup ($assetGuid -eq $entry.Groups['guid'].Value) ('CoreUI entry retains the correct GUID: ' + $assetPath)
    }
    Write-Output "PASS $($checks.Count) asset cleanup and preservation checks."
    Write-Output "Removed $($manifest.deletedTextures.Count) obsolete textures; preserved $($manifest.preservedMenuTextures.Count) menu textures."
} finally {
    $checks | Set-Content -LiteralPath (Join-Path $reportFolder 'MenuAssetCleanupReferenceChecks.txt') -Encoding utf8
    Pop-Location
}
