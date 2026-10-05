param(
    [string]$UnityPath = 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$auditRoot=Join-Path $sourceRoot ('Logs/aram-headless/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Editor", "$auditRoot/Assets/Fixture", "$auditRoot/Packages", "$auditRoot/ProjectSettings" | Out-Null
# Keep every mutation in this new test project. No main-project startup hooks,
# scene edits, MCP, account code, screenshots or graphics device are loaded.
$uiFiles=@(
    'Gameplay/UI/MatchHudStyle.cs',
    'Gameplay/UI/MatchHudTooltip.cs',
    'Gameplay/UI/ResponsiveSafeArea.cs',
    'Gameplay/UI/MenuDesignFrame.cs',
    'Gameplay/ARAM/AramAbilityView.cs',
    'Gameplay/ARAM/AramBuffDraftView.cs',
    'Gameplay/ARAM/AramBuffDefinition.cs',
    'Gameplay/ARAM/AramBuffRuntime.cs',
    'Gameplay/ARAM/AramBuffRuntime.Extended.cs',
    'Gameplay/ARAM/AramBuffRuntime.DocumentRules.cs',
    'Gameplay/ARAM/AramBuffRuntime.Progress.cs',
    'Gameplay/ARAM/AramBuffRuntime.Repetition.cs',
    'Gameplay/ARAM/AramBuffRuntime.Rifle.cs',
    'Gameplay/ARAM/AramBuffPieceMarker.cs',
    'Gameplay/ARAM/AramDecoyTag.cs',
    'Core/ChessGame.Aram.cs',
    'Profile/HandDrawnRoundedGraphic.cs',
    'UI/AntialiasedUIGraphic.cs',
    'UI/UIEdgeMesh.cs',
    'Application/MatchStatistics.cs',
    'UI/ChessFontCatalog.cs',
    'Pieces/ChessPiece.cs',
    'Pieces/PawnPiece.cs',
    'Rules/UnityBoardAdapter.cs',
    'Rules/UnityBuffContextAdapter.cs',
    'Rules/ChessMoveRules.cs'
)
$hashes=@()
foreach($relative in $uiFiles) {
    $path=Join-Path $sourceRoot ('Assets/Scripts/Chess/'+$relative)
    Copy-Item -LiteralPath $path -Destination "$auditRoot/Assets/Fixture"
    if(Test-Path -LiteralPath ($path+'.meta')) {Copy-Item -LiteralPath ($path+'.meta') -Destination "$auditRoot/Assets/Fixture"}
    $hashes+=((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash+' '+$relative)
}
$coreText=Get-Content -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Core/ChessGame.cs" -Raw -Encoding utf8
$methods=@('private bool DoesMoveKeepTeamKingSafe(', 'private bool IsTeamInCheck(', 'private bool IsSquareUnderAttack(', 'private ChessPiece FindKing(', 'private bool IsCastlingMove(', 'private bool TryGetCastlingRookMove(', 'private void AddSpecialCandidateMoves(', 'private void AddSpecialCandidateIfLegal(', 'private void RecordCurrentPosition(', 'private string BuildDrawPositionKey(', 'private bool HasThreefoldRepetition(')
$extracted="using System.Collections.Generic; using UnityEngine; public partial class ChessGame {`n"
foreach($signature in $methods){
    $start=$coreText.IndexOf($signature)
    if($start -lt 0){throw "Missing Core method $signature"}
    $index=$coreText.IndexOf('{',$start);$statementEnd=$coreText.IndexOf(';',$start)
    if($statementEnd -lt $index){$end=$statementEnd+1}
    else{
        $depth=1;$end=$index+1
        while($depth -gt 0){if($coreText[$end] -eq '{'){$depth++}elseif($coreText[$end] -eq '}'){$depth--};$end++}
    }
    $extracted+=$coreText.Substring($start,$end-$start)+"`n"
}
$extracted+="}"
$extracted | Set-Content -LiteralPath "$auditRoot/Assets/Fixture/ChessGame.Extracted.cs" -Encoding utf8
$hashes+=((Get-FileHash -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Core/ChessGame.cs" -Algorithm SHA256).Hash+' Core/ChessGame.cs (11 extracted rules)')
$hashes | Set-Content -LiteralPath "$auditRoot/source-sha256.txt"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Aram.Headless/AramFixture.cs" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Aram.Headless/AramAuditRunner.cs" -Destination "$auditRoot/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Logs/loading-compile/Chess.Domain.dll" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Assets/TextMesh Pro" -Destination "$auditRoot/Assets" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Fonts" -Destination "$auditRoot/Assets" -Recurse
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Resources/Chess" | Out-Null
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/Chess/ChessFontCatalog.asset" -Destination "$auditRoot/Assets/Resources/Chess"
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Resources/UI" | Out-Null
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/UI/MenuEdgeAntialiasing.shader" -Destination "$auditRoot/Assets/Resources/UI"
Copy-Item -LiteralPath "$sourceRoot/ProjectSettings/ProjectVersion.txt" -Destination "$auditRoot/ProjectSettings"
$dependencies=[ordered]@{}
foreach($package in @('com.unity.ugui','com.unity.inputsystem')) {
    $cached=Get-ChildItem -LiteralPath "$sourceRoot/Library/PackageCache" -Directory | Where-Object {$_.Name.StartsWith($package+'@')} | Select-Object -First 1
    if(!$cached){throw "Import $package in the main project first."}
    $dependencies[$package]='file:'+ $cached.FullName.Replace('\','/')
}
foreach($module in @('ui','imgui','uielements','jsonserialize','physics','particlesystem')) {$dependencies['com.unity.modules.'+$module]='1.0.0'}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$auditRoot/Packages/manifest.json" -Encoding utf8
$editorLog=Join-Path $auditRoot 'Editor.log'
$args=@('-batchmode','-nographics','-projectPath',('"'+$auditRoot+'"'),'-executeMethod','AramAuditRunner.Run','-logFile',('"'+$editorLog+'"'))
$process=Start-Process -FilePath $UnityPath -ArgumentList $args -WindowStyle Hidden -PassThru
Write-Output "Isolated headless HUD project: $auditRoot"
Write-Output "Only this new process ($($process.Id)) is supervised; the user's Editor is untouched."
if(!$process.WaitForExit(300000)) {
    $process.Kill()
    throw "Headless audit exceeded five minutes. See $editorLog"
}
$result=Join-Path $auditRoot 'audit-result.txt'
if(Test-Path -LiteralPath $result){Get-Content -LiteralPath $result}
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)){throw "Headless audit failed (exit $($process.ExitCode)). See $editorLog"}

