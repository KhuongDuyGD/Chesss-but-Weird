param(
    [string]$UnityPath='D:/Unity Editor/6000.4.7f1/Editor/Unity.exe',
    [string]$CaptureProject,
    [switch]$Install
)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
if(-not $CaptureProject){$CaptureProject=Join-Path $sourceRoot ('Logs/cinematic-capture/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$CaptureProject=[IO.Path]::GetFullPath($CaptureProject)
$allowed=[IO.Path]::GetFullPath((Join-Path $sourceRoot 'Logs'))+[IO.Path]::DirectorySeparatorChar
if(-not $CaptureProject.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Capture project must stay under Logs.'}
& python (Join-Path $PSScriptRoot 'prepare-cosmetic-capture.py') $CaptureProject
if($LASTEXITCODE -ne 0){throw 'Capture preparation failed.'}
$log=Join-Path $CaptureProject 'cinematic.log'
$process=Start-Process -FilePath $UnityPath -WindowStyle Hidden -ArgumentList @('-batchmode','-quit','-projectPath',$CaptureProject,'-executeMethod','CosmeticThumbnailCapture.Capture','-logFile',$log) -PassThru
Write-Output "Photography process: $($process.Id). Log: $log"
while(-not $process.HasExited){Start-Sleep -Seconds 3;$process.Refresh()}
if($process.ExitCode -ne 0){Get-Content -LiteralPath $log -Tail 35;throw "Capture failed ($($process.ExitCode))."}
$captureLog=Get-Content -LiteralPath $log -Raw
$begin=$captureLog.IndexOf('[CinematicPhotography] Beginning native photography.')
if($begin -lt 0){throw 'Photography did not start.'}
if($captureLog.Substring($begin) -match 'Render Graph Execution error|NullReferenceException|InvalidOperationException|Shader error'){throw "Rendering error; inspect $log"}
$result=Join-Path $CaptureProject 'Photography/captures.json'
if(-not (Test-Path -LiteralPath $result)){throw "Missing capture report. See $log"}
$captures=Get-Content -LiteralPath $result -Raw|ConvertFrom-Json
if(@($captures.images).Count -ne 14){throw 'Expected 14 catalog previews.'}
if(@($captures.images|Where-Object {$_.sourceWidth -ne 4096 -or $_.sourceHeight -ne 3072}).Count){throw 'Expected genuine 4K renders for every preview.'}
if($Install){
    foreach($entry in $captures.images){
        $source=Join-Path $CaptureProject $entry.previewPath
        $target=Join-Path $sourceRoot $entry.previewPath
        $backup=Join-Path $CaptureProject ('PreviousMenuArt/'+$entry.previewPath)
        New-Item -ItemType Directory -Force -Path (Split-Path $backup -Parent)|Out-Null
        if(-not (Test-Path -LiteralPath $backup)){Copy-Item -LiteralPath $target -Destination $backup}
        Copy-Item -LiteralPath $source -Destination $target
    }
    Write-Output 'Installed all 14 cinematic previews. Existing sprite GUIDs and importer settings retained.'
}
Write-Output "Cinematic masters, previews and report: $(Join-Path $CaptureProject 'Photography')"
