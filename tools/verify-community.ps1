param([string]$UnityPath='D:/Unity Editor/6000.4.7f1/Editor/Unity.exe', [switch]$NoGraphics)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$auditRoot=Join-Path $sourceRoot ('Logs/community-qa/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Editor","$auditRoot/Assets/Fixture","$auditRoot/Assets/Resources/Chess","$auditRoot/Assets/Resources/UI","$auditRoot/Packages","$auditRoot/ProjectSettings" | Out-Null
$ui=@('Gameplay/UI/SketchbookUI.cs','Gameplay/UI/SketchbookDoodle.cs','Gameplay/UI/ResponsiveSafeArea.cs','Gameplay/UI/MenuDesignFrame.cs','Gameplay/UI/MainMenuPresentation.cs','Gameplay/UI/ModeSelectionPresentation.cs','Gameplay/UI/MainMenuPaperGraphic.cs','Gameplay/UI/MainMenuInkGraphic.cs','Gameplay/UI/ModeMenuDoodleGraphic.cs','Gameplay/UI/MainMenuButtonMotion.cs','Gameplay/UI/HandDrawnIdleWiggle.cs','Gameplay/UI/MenuMusicWidget.cs','Gameplay/UI/MusicRecordGraphic.cs','Profile/HandDrawnRoundedGraphic.cs','UI/AntialiasedUIGraphic.cs','UI/UIEdgeMesh.cs','UI/AntialiasedMenuImage.cs','UI/ChessFontCatalog.cs')
$hashes=@()
foreach($relative in $ui) {
    $path=Join-Path $sourceRoot ('Assets/Scripts/Chess/'+$relative)
    Copy-Item -LiteralPath $path -Destination "$auditRoot/Assets/Fixture"
    if(Test-Path -LiteralPath ($path+'.meta')){Copy-Item -LiteralPath ($path+'.meta') -Destination "$auditRoot/Assets/Fixture"}
    $hashes+=((Get-FileHash -LiteralPath $path).Hash+' '+$relative)
}
Get-ChildItem -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Community" -Filter '*.cs' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination "$auditRoot/Assets/Fixture"; $hashes+=((Get-FileHash -LiteralPath $_.FullName).Hash+' '+$_.Name) }
$hashes | Set-Content "$auditRoot/source-sha256.txt"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Community.Headless/CommunityPorts.cs" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Community.Headless/CommunityAudit.cs" -Destination "$auditRoot/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Assets/TextMesh Pro","$sourceRoot/Assets/Fonts" -Destination "$auditRoot/Assets" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/Community","$sourceRoot/Assets/Resources/MainMenu" -Destination "$auditRoot/Assets/Resources" -Recurse
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/Chess/ChessFontCatalog.asset" -Destination "$auditRoot/Assets/Resources/Chess"
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/UI/MenuEdgeAntialiasing.shader" -Destination "$auditRoot/Assets/Resources/UI"
Copy-Item -LiteralPath "$sourceRoot/ProjectSettings/ProjectVersion.txt" -Destination "$auditRoot/ProjectSettings"
$dependencies=[ordered]@{}
foreach($name in @('com.unity.ugui','com.unity.inputsystem','com.unity.nuget.newtonsoft-json')) {
    $cached=Get-ChildItem -LiteralPath "$sourceRoot/Library/PackageCache" -Directory | Where-Object {$_.Name.StartsWith($name+'@')} | Select-Object -First 1
    if(!$cached){throw "Import $name in the main project first."}
    $dependencies[$name]='file:'+$cached.FullName.Replace('\','/')
}
foreach($module in @('ui','imgui','uielements','jsonserialize')) {$dependencies['com.unity.modules.'+$module]='1.0.0'}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 4 | Set-Content "$auditRoot/Packages/manifest.json"
$arguments=@('-batchmode','-projectPath',('"'+$auditRoot+'"'),'-executeMethod','CommunityAudit.Run','-logFile',('"'+$auditRoot+'/Editor.log"'))
if($NoGraphics){$arguments+='-nographics'}else{$arguments+='-force-d3d11'}
$process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Community QA isolated project: $auditRoot (process $($process.Id))"
if(!$process.WaitForExit(300000)){$process.Kill();throw "QA timeout: $auditRoot/Editor.log"}
if(Test-Path "$auditRoot/audit-result.txt"){Get-Content "$auditRoot/audit-result.txt"}
if($process.ExitCode -ne 0 -or !(Test-Path "$auditRoot/audit-result.txt")){throw "Community QA failed: $auditRoot/Editor.log"}
