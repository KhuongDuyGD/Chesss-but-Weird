param([switch]$Visible, [string]$OutputDirectory = 'Logs/cosmetics-runtime-visible')
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$outputPath = [IO.Path]::GetFullPath((Join-Path $projectDirectory $OutputDirectory))
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$playerPath = Join-Path $projectDirectory 'Builds/CosmeticsVerification/Chess.exe'
$arguments = @('--verify-cosmetics', '--verification-output', ('"' + $outputPath + '"'),
    '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
    '-force-d3d11', '-logFile', ('"' + (Join-Path $outputPath 'player.log') + '"'))
$windowMode = if ($Visible) { 'Normal' } else { 'Hidden' }
$process = Start-Process -FilePath $playerPath -WorkingDirectory $projectDirectory -ArgumentList $arguments -WindowStyle $windowMode -PassThru
$samples = [Collections.Generic.List[object]]::new()
$watch = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.HasExited) {
    $process.Refresh()
    if (-not $process.HasExited) {
        $stage = 'boot'
        $reportPath = Join-Path $outputPath 'report.json'
        if (Test-Path -LiteralPath $reportPath) {
            try { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json; if ($report.samples.Count) { $stage = $report.samples[-1].stage } } catch {}
        }
        $samples.Add([pscustomobject]@{ seconds = $watch.Elapsed.TotalSeconds; stage = $stage; privateMiB = $process.PrivateMemorySize64 / 1MB; workingSetMiB = $process.WorkingSet64 / 1MB })
    }
    Start-Sleep -Milliseconds 500
}
$process.WaitForExit()
$samples | Export-Csv -LiteralPath (Join-Path $outputPath 'process-memory.csv') -NoTypeInformation
[pscustomobject]@{ exitCode = $process.ExitCode; peakPrivateMiB = ($samples | Measure-Object privateMiB -Maximum).Maximum; output = $outputPath } | ConvertTo-Json
exit $process.ExitCode
