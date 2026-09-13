param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe'
)

# Compile source against the installed project's Unity references without starting
# or modifying the open Editor. This does not replace a player build or play test.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$unityData = Join-Path (Split-Path $UnityPath -Parent) 'Data'
$compiler = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
$dotnet = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$outputRoot = Join-Path $projectRoot 'Logs/loading-compile'

Push-Location $projectRoot
try {
    if (-not (Test-Path -LiteralPath $compiler)) { throw "Missing Unity compiler: $compiler" }
    $runtimeResponse = Get-ChildItem Library/Bee/artifacts -Filter Assembly-CSharp.rsp -Recurse |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $runtimeResponse) { throw 'Import the project in Unity once to generate compiler response files.' }
    $editorResponse = Join-Path $runtimeResponse.DirectoryName 'Assembly-CSharp-Editor.rsp'
    if (-not (Test-Path -LiteralPath $editorResponse)) { throw 'Missing Editor compiler response file.' }
    New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

    function Invoke-SourceCompile([string]$sourceResponse, [string]$name, [bool]$player, [bool]$editor) {
        $lines = [System.Collections.Generic.List[string]]::new()
        $sources = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($line in (Get-Content -LiteralPath $sourceResponse)) {
            if ($line -match '^-(out|refout):') { continue }
            if ($player -and $line -match '^-define:UNITY_EDITOR($|_)') { continue }
            if ($line -match '^"(.+\.cs)"$') {
                if (Test-Path -LiteralPath $Matches[1]) {
                    $sources.Add($Matches[1].Replace('\', '/')) | Out-Null
                }
                continue
            }
            if ($editor -and $line -match '^-r:.*[/\\]Assembly-CSharp\.ref\.dll"$') {
                $lines.Add('-r:"Logs/loading-compile/runtime-editor.ref.dll"')
            } else {
                $lines.Add($line)
            }
        }

        # Unity's existing rsp may predate newly authored scripts. Include only
        # the game's un-asmdefed directories, keeping editor code out of runtime.
        $scanRoot = if ($editor) { 'Assets/Editor' } else { 'Assets/Scripts/Chess' }
        foreach ($file in (Get-ChildItem -LiteralPath $scanRoot -Recurse -Filter '*.cs')) {
            $directory = $file.Directory
            $hasAssemblyDefinition = $false
            while ($directory -and $directory.FullName.Length -gt $projectRoot.Length) {
                if (Get-ChildItem -LiteralPath $directory.FullName -Filter '*.asmdef') {
                    $hasAssemblyDefinition = $true
                    break
                }
                $directory = $directory.Parent
            }
            if (-not $hasAssemblyDefinition) {
                $relative = [IO.Path]::GetRelativePath($projectRoot, $file.FullName).Replace('\', '/')
                $sources.Add($relative) | Out-Null
            }
        }
        foreach ($source in ($sources | Sort-Object)) { $lines.Add('"' + $source + '"') }
        $lines.Add('-out:"Logs/loading-compile/' + $name + '.dll"')
        $lines.Add('-refout:"Logs/loading-compile/' + $name + '.ref.dll"')
        $responsePath = Join-Path $outputRoot ($name + '.rsp')
        $lines | Set-Content -LiteralPath $responsePath -Encoding utf8
        $logPath = Join-Path $outputRoot ($name + '.log')
        & $dotnet exec $compiler /nostdlib /noconfig ('@' + $responsePath) 2>&1 | Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0) { throw "$name compilation failed: $logPath" }
        Write-Output "PASS $name C# compilation ($($sources.Count) source files)."
    }

    Invoke-SourceCompile $runtimeResponse.FullName 'runtime-editor' $false $false
    Invoke-SourceCompile $runtimeResponse.FullName 'runtime-player' $true $false
    Invoke-SourceCompile $editorResponse 'editor-tools' $false $true
    Write-Output 'C# branch checks only: no asset build, IL post-processing, rendering or runtime coverage is implied.'
} finally {
    Pop-Location
}
