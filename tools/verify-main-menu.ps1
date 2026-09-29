param(
    [string]$UnityPath = 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe',
    [ValidateSet('Main', 'Modes', 'Antialiasing', 'Auth', 'Cleanup')]
    [string]$Menu = 'Main',
    [ValidateSet('Direct3D11', 'Direct3D12')]
    [string]$GraphicsApi = 'Direct3D11'
)

# Compile and render the actual menu builder in a small isolated Unity project.
# This leaves the user's open scene, play session and project settings untouched.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$previewRoot = Join-Path $projectRoot 'Logs/MainMenuPreviewProject'
$outputFolder = switch ($Menu) { 'Cleanup' { 'Documentation/UI/MenuAssetCleanup' } 'Auth' { 'Documentation/UI/AuthMenuRendering' } 'Modes' { 'Documentation/UI/ModeSelectionRedesign' } 'Antialiasing' { 'Documentation/UI/MenuAntialiasing' } default { 'Documentation/UI/MainMenuRedesign' } }
$auditClass = switch ($Menu) { 'Cleanup' { 'MenuAssetCleanupAudit' } 'Auth' { 'AuthMenuRenderingAudit' } 'Modes' { 'ModeSelectionRedesignAudit' } 'Antialiasing' { 'MenuAntialiasingAudit' } default { 'MainMenuRedesignAudit' } }
$checksName = switch ($Menu) { 'Cleanup' { 'MenuAssetCleanupRuntimeChecks.txt' } 'Auth' { 'AuthMenuRenderingChecks.txt' } 'Modes' { 'ModeSelectionAuditChecks.txt' } 'Antialiasing' { 'MenuAntialiasingAuditChecks.txt' } default { 'MainMenuAuditChecks.txt' } }
$outputVariable = switch ($Menu) { 'Cleanup' { 'CHESS_MENU_ASSET_CLEANUP_OUTPUT' } 'Auth' { 'CHESS_AUTH_MENU_AUDIT_OUTPUT' } 'Modes' { 'CHESS_MODE_MENU_AUDIT_OUTPUT' } 'Antialiasing' { 'CHESS_MENU_AA_AUDIT_OUTPUT' } default { 'CHESS_MAIN_MENU_AUDIT_OUTPUT' } }
$outputRoot = Join-Path $projectRoot $outputFolder
$logPath = Join-Path $projectRoot ('Logs/' + $Menu.ToLowerInvariant() + '-menu-render.log')
New-Item -ItemType Directory -Force -Path $previewRoot, $outputRoot | Out-Null
foreach ($folder in @('Assets/Editor', 'Assets/Scripts', 'Assets/Resources/MainMenu', 'Assets/Resources/Chess', 'Assets/Resources/UI', 'Assets/Fonts/Schoolbell', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $previewRoot $folder) | Out-Null
}

function Copy-MenuFile([string]$source, [string]$destination) {
    $sourcePath = Join-Path $projectRoot $source
    $targetPath = Join-Path $previewRoot $destination
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    if (Test-Path -LiteralPath ($sourcePath + '.meta')) {
        Copy-Item -LiteralPath ($sourcePath + '.meta') -Destination ($targetPath + '.meta') -Force
    }
}

foreach ($file in @('MainMenuPresentation', 'ModeSelectionPresentation', 'ModeMenuDoodleGraphic', 'MainMenuLogoOriginal', 'MainMenuButtonMotion', 'MainMenuPaperGraphic', 'MainMenuInkGraphic', 'HandDrawnIdleWiggle', 'MenuDesignFrame', 'ResponsiveSafeArea', 'SketchbookUI', 'SketchbookDoodle')) {
    Copy-MenuFile ('Assets/Scripts/Chess/Gameplay/UI/' + $file + '.cs') ('Assets/Scripts/' + $file + '.cs')
}
Copy-MenuFile 'Assets/Scripts/Chess/Profile/HandDrawnRoundedGraphic.cs' 'Assets/Scripts/HandDrawnRoundedGraphic.cs'
Copy-MenuFile 'Assets/Scripts/Chess/Gameplay/UI/HandDrawnMenuAssets.cs' 'Assets/Scripts/HandDrawnMenuAssets.cs'
# Only the Addressables cache is adapted: exercise the production menu loader's
# actual file/Resources fallback without loading game scenes or cosmetic models.
@'
using UnityEngine;
public static class CoreArtworkCache
{
    public static Texture2D GetTexture(string path) => null;
}
'@ | Set-Content -LiteralPath (Join-Path $previewRoot 'Assets/Scripts/CoreArtworkCachePreviewAdapter.cs') -Encoding utf8
Copy-MenuFile 'Assets/Scripts/Chess/UI/ChessFontCatalog.cs' 'Assets/Scripts/ChessFontCatalog.cs'
Copy-MenuFile 'Assets/Scripts/Chess/UI/AntialiasedUIGraphic.cs' 'Assets/Scripts/AntialiasedUIGraphic.cs'
Copy-MenuFile 'Assets/Scripts/Chess/UI/AntialiasedMenuImage.cs' 'Assets/Scripts/AntialiasedMenuImage.cs'
Copy-MenuFile 'Assets/Scripts/Chess/UI/UIEdgeMesh.cs' 'Assets/Scripts/UIEdgeMesh.cs'
Copy-MenuFile 'Assets/Scripts/Chess/UI/MenuTextureSampling.cs' 'Assets/Scripts/MenuTextureSampling.cs'
Copy-MenuFile 'Assets/Resources/UI/MenuEdgeAntialiasing.shader' 'Assets/Resources/UI/MenuEdgeAntialiasing.shader'
Copy-MenuFile 'Assets/Editor/MainMenuRedesignAudit.cs' 'Assets/Editor/MainMenuRedesignAudit.cs'
Copy-MenuFile 'Assets/Editor/ModeSelectionRedesignAudit.cs' 'Assets/Editor/ModeSelectionRedesignAudit.cs'
Copy-MenuFile 'Assets/Editor/MenuTextureImportSettings.cs' 'Assets/Editor/MenuTextureImportSettings.cs'
Copy-MenuFile 'Assets/Editor/MenuAntialiasingAudit.cs' 'Assets/Editor/MenuAntialiasingAudit.cs'
Copy-MenuFile 'Assets/Editor/MenuArtworkMipAudit.shader' 'Assets/Editor/MenuArtworkMipAudit.shader'
if ($Menu -eq 'Cleanup') {
    Copy-MenuFile 'Assets/Editor/MenuAssetCleanupAudit.cs' 'Assets/Editor/MenuAssetCleanupAudit.cs'
    Copy-MenuFile 'Assets/Scripts/Chess/Gameplay/UI/BotDifficultyAssetCatalog.cs' 'Assets/Scripts/BotDifficultyAssetCatalog.cs'
    Copy-MenuFile 'Assets/Resources/Chess/BotDifficultyAssets.asset' 'Assets/Resources/Chess/BotDifficultyAssets.asset'
    New-Item -ItemType Directory -Force -Path (Join-Path $previewRoot 'Assets/Materials/Main_Menu'), (Join-Path $previewRoot 'Assets/Materials/LANUI') | Out-Null
    foreach ($texture in (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Materials/Main_Menu') -Filter '*.png')) {
        Copy-MenuFile ('Assets/Materials/Main_Menu/' + $texture.Name) ('Assets/Materials/Main_Menu/' + $texture.Name)
    }
    Copy-MenuFile 'Assets/Materials/LANUI/LANUIBlank.png' 'Assets/Materials/LANUI/LANUIBlank.png'
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Resources/Main_Menu') -Destination (Join-Path $previewRoot 'Assets/Resources') -Recurse -Force
}
if ($Menu -eq 'Auth') {
    Copy-MenuFile 'Assets/Editor/AuthMenuRenderingAudit.cs' 'Assets/Editor/AuthMenuRenderingAudit.cs'
    Copy-MenuFile 'Assets/Scripts/Chess/Profile/MainMenuAuthUI.cs' 'Assets/Scripts/MainMenuAuthUI.cs'
    Copy-MenuFile 'Assets/Scripts/Chess/Profile/AuthNotificationView.cs' 'Assets/Scripts/AuthNotificationView.cs'
}
# Keep this adapter current across preview runs, including older Auth fixtures
# which declared a placeholder HandDrawnMenuAssets instead of the real loader.
$inventorySource = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs') -Raw
$fitter = $inventorySource.Substring($inventorySource.IndexOf('public sealed class InventoryContentRootFitter'))
('using UnityEngine;' + [Environment]::NewLine + $fitter) |
    Set-Content -LiteralPath (Join-Path $previewRoot 'Assets/Scripts/AuthAuditFixtureTypes.cs') -Encoding utf8
Copy-MenuFile 'Assets/Resources/Chess/ChessFontCatalog.asset' 'Assets/Resources/Chess/ChessFontCatalog.asset'
Copy-MenuFile 'Assets/Resources/MainMenu/MainMenuLogoOriginal.png' 'Assets/Resources/MainMenu/MainMenuLogoOriginal.png'
Copy-MenuFile 'Assets/Resources/MainMenu/MainMenuCredits.txt' 'Assets/Resources/MainMenu/MainMenuCredits.txt'
Copy-MenuFile 'Assets/Fonts/Schoolbell/Schoolbell-Regular.ttf' 'Assets/Fonts/Schoolbell/Schoolbell-Regular.ttf'
Copy-MenuFile 'Assets/Fonts/Schoolbell/Schoolbell TMP.asset' 'Assets/Fonts/Schoolbell/Schoolbell TMP.asset'
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/TextMesh Pro') -Destination (Join-Path $previewRoot 'Assets') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $previewRoot 'ProjectSettings/ProjectVersion.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectSettings.asset') -Destination (Join-Path $previewRoot 'ProjectSettings/ProjectSettings.asset') -Force

$dependencies = [ordered]@{}
foreach ($package in @('com.unity.ugui', 'com.unity.inputsystem')) {
    $cached = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Library/PackageCache') -Directory |
        Where-Object Name -Like ($package + '@*') | Select-Object -First 1
    if (-not $cached) { throw "Missing cached Unity package: $package" }
    $dependencies[$package] = 'file:' + $cached.FullName.Replace('\', '/')
}
foreach ($module in @('ui', 'imgui', 'uielements', 'imageconversion', 'screencapture', 'physics', 'physics2d', 'animation', 'audio', 'jsonserialize')) {
    $dependencies['com.unity.modules.' + $module] = '1.0.0'
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $previewRoot 'Packages/manifest.json') -Encoding utf8

$oldOutput = [Environment]::GetEnvironmentVariable($outputVariable, 'Process')
try {
    [Environment]::SetEnvironmentVariable($outputVariable, $outputRoot, 'Process')
    $graphicsFlag = if ($GraphicsApi -eq 'Direct3D12') { '-force-d3d12' } else { '-force-d3d11' }
    $isPlayModeAudit = $Menu -in @('Auth', 'Cleanup')
    $method = if ($isPlayModeAudit) { '.Run' } else { '.Capture' }
    $arguments = @('-batchmode', $graphicsFlag, '-projectPath', ('"' + $previewRoot + '"'),
        '-executeMethod', ($auditClass + $method), '-logFile', ('"' + $logPath + '"'))
    if (-not $isPlayModeAudit) { $arguments += '-quit' } # Play-mode audits own their asynchronous lifecycle.
    Write-Output "Rendering the production $Menu menu in an isolated Unity preview project..."
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $previewRoot -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Unity render exited with code $($process.ExitCode). See $logPath" }
    $checksPath = Join-Path $outputRoot $checksName
    if (-not (Test-Path -LiteralPath $checksPath)) { throw "No menu audit result. See $logPath" }
    $checks = Get-Content -LiteralPath $checksPath
    if ($checks | Where-Object { $_ -like 'FAIL *' }) { throw "Menu audit failed. See $checksPath" }
    Write-Output "PASS $($checks.Count) interaction and layout checks. Captures: $outputRoot"
} finally {
    [Environment]::SetEnvironmentVariable($outputVariable, $oldOutput, 'Process')
}
