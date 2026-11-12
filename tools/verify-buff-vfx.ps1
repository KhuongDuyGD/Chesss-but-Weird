param([string]$UnityPath='D:/Unity Editor/6000.4.7f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$auditRoot=Join-Path $sourceRoot ('Logs/buff-vfx-qa/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Editor","$auditRoot/Assets/Fixture","$auditRoot/Assets/Resources/Chess","$auditRoot/Assets/Pieces","$auditRoot/Packages","$auditRoot/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Presentation/BuffVfx" -Destination "$auditRoot/Assets/Fixture" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs","$sourceRoot/Assets/Scripts/Chess/UI/ChessFontCatalog.cs","$sourceRoot/Tests/Chess.BuffVfx/FixturePorts.cs" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Gameplay/Settings/SettingsLocalization.cs" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/Localization" -Destination "$auditRoot/Assets/Resources" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Scripts/Chess/UI/ChessFontCatalog.cs.meta" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.BuffVfx/BuffVfxAudit.cs" -Destination "$auditRoot/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.BuffVfx/BuffVfxLandscapeAudit.cs","$sourceRoot/Tests/Chess.BuffVfx/BuffVfxStrategyAudit.cs" -Destination "$auditRoot/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/BuffVfx" -Destination "$auditRoot/Assets/Resources" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Config" -Destination "$auditRoot/Assets" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/TextMesh Pro","$sourceRoot/Assets/Fonts" -Destination "$auditRoot/Assets" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/Chess/ChessFontCatalog.asset" -Destination "$auditRoot/Assets/Resources/Chess"
foreach($kind in @('King','Queen','Rook','Bishop','Knight','Pawn')){Copy-Item -LiteralPath "$sourceRoot/Assets/Skins/Tazji'sLowPoly/Chess_Set/Chess_$kind.fbx" -Destination "$auditRoot/Assets/Pieces"}
Copy-Item -LiteralPath "$sourceRoot/ProjectSettings/ProjectVersion.txt" -Destination "$auditRoot/ProjectSettings"
Copy-Item -LiteralPath "$sourceRoot/Logs/BuffVfx/backend-fixtures.json" -Destination "$auditRoot/backend-fixtures.json"
Copy-Item -LiteralPath "$sourceRoot/Logs/BuffVfx/backend-landscape-fixtures.json" -Destination "$auditRoot/backend-landscape-fixtures.json"
Copy-Item -LiteralPath "$sourceRoot/Logs/BuffVfx/backend-strategy-fixtures.json" -Destination "$auditRoot/backend-strategy-fixtures.json"
$dependencies=[ordered]@{}
foreach($name in @('com.unity.ugui','com.unity.nuget.newtonsoft-json','com.unity.render-pipelines.universal','com.unity.render-pipelines.core','com.unity.render-pipelines.universal-config','com.unity.shadergraph','com.unity.burst','com.unity.collections','com.unity.mathematics','com.unity.searcher')) {
    $cached=Get-ChildItem -LiteralPath "$sourceRoot/Library/PackageCache" -Directory | Where-Object {$_.Name.StartsWith($name+'@')} | Select-Object -First 1
    if(!$cached){throw "Import $name in the main project first."}
    $dependencies[$name]='file:'+$cached.FullName.Replace('\','/')
}
foreach($module in @('ui','imgui','uielements','jsonserialize','physics','particlesystem','imageconversion','terrain','terrainphysics','animation','xr')){$dependencies['com.unity.modules.'+$module]='1.0.0'}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 4 | Set-Content "$auditRoot/Packages/manifest.json"
Get-ChildItem "$sourceRoot/Assets/Scripts/Chess/Presentation/BuffVfx" -File | ForEach-Object {if($_.Extension -eq '.cs'){(Get-FileHash -LiteralPath $_.FullName).Hash+' '+$_.Name}} | Set-Content "$auditRoot/source-sha256.txt"
$arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$auditRoot+'"'),'-executeMethod','BuffVfxAudit.Run','-logFile',('"'+$auditRoot+'/Editor.log"'))
$process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Buff VFX QA project: $auditRoot (process $($process.Id))"
if(!$process.WaitForExit(300000)){$process.Kill();throw "VFX QA timeout: $auditRoot/Editor.log"}
if(Test-Path "$auditRoot/audit-result.txt"){Get-Content "$auditRoot/audit-result.txt"}
if($process.ExitCode -ne 0 -or !(Test-Path "$auditRoot/audit-result.txt")){throw "VFX QA failed: $auditRoot/Editor.log"}
