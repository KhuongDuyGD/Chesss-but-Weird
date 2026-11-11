param([string]$UnityPath = 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$auditRoot = Join-Path $sourceRoot ('Logs/multiplayer-headless/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path "$auditRoot/Assets/Editor", "$auditRoot/Assets/Plugins", "$auditRoot/Packages", "$auditRoot/ProjectSettings" | Out-Null
# Inspect freshly compiled production code in a separate edit-mode project.
# No user scenes, account config, resources, startup editor hooks or MCP are copied.
Copy-Item -LiteralPath "$sourceRoot/Logs/loading-compile/runtime-player.dll", "$sourceRoot/Logs/loading-compile/Chess.Domain.dll" -Destination "$auditRoot/Assets/Plugins"
Copy-Item -LiteralPath "$sourceRoot/Tests/Chess.Multiplayer.Headless/MultiplayerAudit.cs" -Destination "$auditRoot/Assets/Editor"
Copy-Item -LiteralPath "$sourceRoot/ProjectSettings/ProjectVersion.txt" -Destination "$auditRoot/ProjectSettings"
$dependencies = [ordered]@{}
foreach ($package in @('com.unity.ugui','com.unity.inputsystem','com.unity.nuget.newtonsoft-json','com.unity.addressables','com.unity.render-pipelines.universal')) {
    $cached = Get-ChildItem -LiteralPath "$sourceRoot/Library/PackageCache" -Directory | Where-Object { $_.Name.StartsWith($package + '@') } | Select-Object -First 1
    if (!$cached) { throw "Missing cached $package" }
    $dependencies[$package] = 'file:' + $cached.FullName.Replace('\','/')
}
$manifest = Get-Content -LiteralPath "$sourceRoot/Packages/manifest.json" -Raw | ConvertFrom-Json
foreach ($property in $manifest.dependencies.PSObject.Properties) {
    if ($property.Name.StartsWith('com.unity.modules.')) { $dependencies[$property.Name] = $property.Value }
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$auditRoot/Packages/manifest.json" -Encoding utf8
$arguments = @('-batchmode','-nographics','-projectPath',('"' + $auditRoot + '"'),'-executeMethod','MultiplayerAudit.Run','-logFile',('"' + "$auditRoot/Editor.log" + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Isolated multiplayer audit: $auditRoot; PID $($process.Id)"
# Polling is bounded so the caller can keep reporting progress.
$process.Id | Set-Content -LiteralPath "$auditRoot/process-id.txt"
Write-Output 'Audit started. Check audit-result.txt and Editor.log in this directory.'
