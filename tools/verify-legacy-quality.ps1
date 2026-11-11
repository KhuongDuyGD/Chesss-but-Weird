param([string]$UnityPath = 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$qualityPath = Join-Path $sourceRoot 'ProjectSettings/QualitySettings.asset'
$originalQuality = [IO.File]::ReadAllBytes($qualityPath)
$mcpPath = Join-Path $sourceRoot 'ProjectSettings/McpUnitySettings.json'
$originalMcp = if (Test-Path -LiteralPath $mcpPath) { [IO.File]::ReadAllBytes($mcpPath) } else { $null }
try {
    $arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $sourceRoot + '"'), '-executeMethod', 'LegacyQualityAudit.Verify', '-quit', '-logFile', ('"' + $sourceRoot + '/Logs/legacy-quality-verify.log"'))
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(300000)) { $process.Kill(); $process.WaitForExit(); throw 'Legacy quality audit timed out.' }
    if ($process.ExitCode -ne 0) { throw 'Legacy quality audit failed. See Logs/legacy-quality-verify.log.' }
    Get-Content -LiteralPath (Join-Path $sourceRoot 'Logs/legacy-quality/checks.txt')
} finally {
    # Unity serializes temporary native quality overrides on batch exit. Preserve the user's asset exactly.
    [IO.File]::WriteAllBytes($qualityPath, $originalQuality)
    if ($null -ne $originalMcp) { [IO.File]::WriteAllBytes($mcpPath, $originalMcp) }
}
