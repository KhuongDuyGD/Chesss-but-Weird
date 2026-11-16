param(
    [string]$UnityPath = 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe',
    [string]$FixturePath,
    [switch]$PlayMode
)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
if (!$FixturePath) {
    # Reuse only an isolated, completed regression fixture, never the user's project.
    $fixture = Get-ChildItem -LiteralPath "$sourceRoot/Logs/multiplayer-headless" -Directory |
        Where-Object { Test-Path -LiteralPath ($_.FullName + '/audit-result.txt') } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (!$fixture) { throw 'Run verify-multiplayer-headless.ps1 first to prepare an isolated Unity fixture.' }
    $FixturePath = $fixture.FullName
}
$resolvedFixture = [IO.Path]::GetFullPath($FixturePath)
$allowedRoot = [IO.Path]::GetFullPath("$sourceRoot/Logs/multiplayer-headless") + [IO.Path]::DirectorySeparatorChar
if (!$resolvedFixture.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must be inside Logs/multiplayer-headless.' }
Copy-Item -LiteralPath "$sourceRoot/Logs/loading-compile/runtime-player.dll", "$sourceRoot/Logs/loading-compile/Chess.Domain.dll" -Destination "$resolvedFixture/Assets/Plugins"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Export.Tests/MatchSharingUnityAudit.cs" -Destination "$resolvedFixture/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Export.Tests/MatchSharingPlayModeAudit.cs" -Destination "$resolvedFixture/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Export.Tests/MatchClipboardAudit.cs" -Destination "$resolvedFixture/Assets/Editor"
$resultName = if ($PlayMode) { 'sharing-playmode-result.txt' } else { 'sharing-audit-result.txt' }
$logName = if ($PlayMode) { 'sharing-playmode-editor.log' } else { 'sharing-editor.log' }
$method = if ($PlayMode) { 'MatchSharingPlayModeAudit.Run' } else { 'MatchSharingUnityAudit.Run' }
if (Test-Path -LiteralPath "$resolvedFixture/$resultName") { Remove-Item -LiteralPath "$resolvedFixture/$resultName" }
$arguments = @('-batchmode','-projectPath',('"' + $resolvedFixture + '"'),'-executeMethod',$method,'-logFile',('"' + "$resolvedFixture/$logName" + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Sharing render audit: $resolvedFixture; PID $($process.Id)"
Write-Output "Check $resultName and $logName. Graphics are enabled; no user scenes or server sessions are loaded."
