param([string]$UnityPath='D:/Unity Editor/6000.4.7f1/Editor/Unity.exe',[string]$AuditDirectory='')
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$auditRoot=if($AuditDirectory){[IO.Path]::GetFullPath($AuditDirectory)}else{Join-Path $sourceRoot ('Logs/settings-audit/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$allowedRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot 'Logs/settings-audit'))+[IO.Path]::DirectorySeparatorChar
if(!$auditRoot.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Audit project must be inside Logs/settings-audit.'}
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Editor", "$auditRoot/Assets/Fixture", "$auditRoot/Packages", "$auditRoot/ProjectSettings" | Out-Null
$files=@('Gameplay/GameRuntimeSettings.cs','Gameplay/UI/SettingsMenuController.cs','Gameplay/UI/SettingsScrollFocus.cs','Gameplay/UI/SettingsPrompt.cs',
 'Gameplay/UI/SketchbookUI.cs','Gameplay/UI/SketchbookDoodle.cs','Gameplay/UI/MenuDesignFrame.cs','Gameplay/UI/ResponsiveSafeArea.cs',
 'Profile/HandDrawnRoundedGraphic.cs','UI/AntialiasedUIGraphic.cs','UI/AntialiasedMenuImage.cs','UI/UIEdgeMesh.cs','UI/ChessFontCatalog.cs','Gameplay/ARAM/AramBuffDefinition.cs')
$files+=Get-ChildItem -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Gameplay/Settings" -Filter '*.cs' | ForEach-Object {'Gameplay/Settings/'+$_.Name}
$hashes=@()
foreach($relative in $files){
 $path=Join-Path $sourceRoot ('Assets/Scripts/Chess/'+$relative)
 Copy-Item -LiteralPath $path -Destination "$auditRoot/Assets/Fixture"
 if(Test-Path -LiteralPath ($path+'.meta')){Copy-Item -LiteralPath ($path+'.meta') -Destination "$auditRoot/Assets/Fixture"}
 $hashes+=((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash+' '+$relative)
}
$hashes | Set-Content -LiteralPath "$auditRoot/source-sha256.txt"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Settings.Tests/SettingsRuntimeFixtures.cs" -Destination "$auditRoot/Assets/Fixture"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Settings.Tests/SettingsUiAuditRunner.cs" -Destination "$auditRoot/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/Assets/TextMesh Pro", "$sourceRoot/Assets/Fonts" -Destination "$auditRoot/Assets" -Recurse -Force
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Resources/Chess", "$auditRoot/Assets/Resources/UI" | Out-Null
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/Chess/ChessFontCatalog.asset" -Destination "$auditRoot/Assets/Resources/Chess"
Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/UI/MenuEdgeAntialiasing.shader" -Destination "$auditRoot/Assets/Resources/UI"
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Resources/Localization" | Out-Null
# Synthetic long translation only in the fixture; no Vietnamese translation is shipped by this audit.
@{entries=@(@{key='language.name';text='Language and international text preferences'},@{key='language.description';text='This deliberately long translated description checks wrapping and row height when the interface is enlarged. The control must remain readable and the content must continue to scroll without overlapping its neighboring rows.'})} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$auditRoot/Assets/Resources/Localization/Settings_vi.json" -Encoding utf8
Copy-Item -LiteralPath "$sourceRoot/ProjectSettings/ProjectVersion.txt" -Destination "$auditRoot/ProjectSettings"
$dependencies=[ordered]@{}
foreach($package in @('com.unity.ugui','com.unity.inputsystem','com.unity.render-pipelines.universal','com.unity.render-pipelines.core','com.unity.shadergraph')){
 $cached=Get-ChildItem -LiteralPath "$sourceRoot/Library/PackageCache" -Directory | Where-Object {$_.Name.StartsWith($package+'@')} | Select-Object -First 1
 if(!$cached){throw "Import $package in the main project first."}
 $dependencies[$package]='file:'+$cached.FullName.Replace('\','/')
}
foreach($module in @('ui','imgui','uielements','jsonserialize','audio','imageconversion','physics','animation','particlesystem')){$dependencies['com.unity.modules.'+$module]='1.0.0'}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$auditRoot/Packages/manifest.json" -Encoding utf8
$editorLog=Join-Path $auditRoot 'Editor.log'
$args=@('-batchmode','-force-d3d11','-projectPath',('"'+$auditRoot+'"'),'-executeMethod','SettingsUiAuditRunner.Run','-logFile',('"'+$editorLog+'"'))
$process=Start-Process -FilePath $UnityPath -ArgumentList $args -WindowStyle Hidden -PassThru
Write-Output "Isolated settings audit: $auditRoot; supervised process $($process.Id)."
if(!$process.WaitForExit(300000)){$process.Kill();throw "Settings audit exceeded five minutes. See $editorLog"}
$result=Join-Path $auditRoot 'audit-result.txt'
if(Test-Path -LiteralPath $result){Get-Content -LiteralPath $result}
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)){
 if(Test-Path -LiteralPath $editorLog){Get-Content -LiteralPath $editorLog -Tail 40}
 throw "Settings audit failed (exit $($process.ExitCode)). See $editorLog"
}
