param(
    [switch]$Unity,
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    & dotnet run --project Tests/Chess.Domain.Tests/Chess.Domain.Tests.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Domain verification failed.' }
    & node OnlineServer/aram-rules.test.js
    if ($LASTEXITCODE -ne 0) { throw 'Existing server reference verification failed.' }
    if ($Unity) {
        if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable missing: $UnityPath" }
        New-Item -ItemType Directory -Force -Path Logs | Out-Null
        $logPath = Join-Path $projectRoot 'Logs/architecture-smoke.log'
        $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList "-batchmode -nographics -projectPath `"$projectRoot`" -executeMethod ArchitectureSmokeRunner.Run -logFile `"$logPath`"" -WindowStyle Hidden -PassThru
        if (-not $unityProcess.WaitForExit(300000)) {
            $unityProcess.Kill()
            throw "Unity verification timed out. Read $logPath"
        }
        $unityProcess.Refresh()
        if ($unityProcess.ExitCode -ne 0) { throw "Unity verification failed. Read $logPath" }
        Get-Content Logs/architecture-smoke-results.txt
    }
} finally { Pop-Location }
