param(
    [ValidateSet('All','Play','Build','Player')][string]$Mode = 'All',
    [switch]$ShowPanel,
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$work = Join-Path $repo '.tools/forge-proof'
$project = Join-Path $PSScriptRoot 'UnityProject'
$config = Join-Path $work 'runtime/bridge-config.json'
$player = Join-Path $work 'player/ForgeProof.exe'
if (!(Test-Path $config)) { throw 'Run Build-Bridge.ps1 first.' }
function Invoke-ProofProcess([string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds) {
    $quoted = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $style = if ($ShowPanel -and $Executable -eq $player) { 'Normal' } else { 'Hidden' }
    $process = Start-Process -FilePath $Executable -ArgumentList $quoted -WindowStyle $style -PassThru
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        # Terminate only this probe's process tree, including its JVM if it hung.
        & taskkill /PID $process.Id /T /F | Out-Null
        throw "Probe process timed out: $Executable"
    }
    if ($process.ExitCode -ne 0) { throw "Probe process failed ($($process.ExitCode)): $Executable; inspect $work logs." }
}
if ($Mode -in @('All','Play')) {
    Invoke-ProofProcess $Unity @('-batchmode','-nographics','-projectPath',$project,'-executeMethod','ProofBuild.Play','-proof-auto','-proof-config',$config,'-proof-report',"$work/play-report.json",'-logFile',"$work/unity-play.log") 300
    $report = Get-Content "$work/play-report.json" -Raw | ConvertFrom-Json
    if (!$report.passed -or $report.runs.Count -ne 3) { throw 'Play Mode acceptance failed.' }
    Write-Output 'Unity Play Mode: 3/3 passed.'
}
if ($Mode -in @('All','Build')) {
    Invoke-ProofProcess $Unity @('-batchmode','-nographics','-quit','-projectPath',$project,'-executeMethod','ProofBuild.Build','-proof-build',$player,'-logFile',"$work/unity-build.log") 600
    Write-Output "Windows build ready: $player"
}
if ($Mode -in @('All','Player')) {
    Invoke-ProofProcess $player @('-proof-auto','-proof-config',$config,'-proof-report',"$work/player-report.json",'-logFile',"$work/unity-player.log") 300
    $report = Get-Content "$work/player-report.json" -Raw | ConvertFrom-Json
    if (!$report.passed -or $report.runs.Count -ne 3) { throw 'Windows Player acceptance failed.' }
    Write-Output 'Windows Player: 3/3 passed.'
}
