# The running client copies this installed helper into its verified staging folder.
$ErrorActionPreference = 'Stop'
$stage = [IO.Path]::GetFullPath($PSScriptRoot)
if ([IO.Path]::GetFileName($stage) -cnotmatch '^\.mtgb-update-[0-9a-f]{32}$') { throw 'Invalid update directory.' }
$root = [IO.Directory]::GetParent($stage).FullName
$payload = Join-Path $stage 'payload'
$backup = Join-Path $stage 'backup'
$client = Join-Path $root 'MTGB.exe'
$ready = Join-Path $stage 'ready'
$commit = Join-Path $stage 'commit'
$entries = Get-Content -LiteralPath (Join-Path $root 'MTGB_Data/StreamingAssets/ClientProgramFiles.txt')
$old = [Collections.Generic.List[string]]::new()
$new = [Collections.Generic.List[string]]::new()
$lock = $null
$exited = $false
$clean = $false

function Move-Entry([string]$from, [string]$to) {
    if ([IO.Directory]::Exists($from)) { [IO.Directory]::Move($from, $to) }
    else { [IO.File]::Move($from, $to) }
}

try {
    Set-Location -LiteralPath $root
    if (!(Test-Path -LiteralPath $client -PathType Leaf)) { throw 'Client executable is missing.' }
    # Keep the process handle across the handshake, even if Windows later recycles its PID.
    $player = [Diagnostics.Process]::GetProcessById([int]$env:MTGB_UPDATE_PID)
    $null = $player.Handle
    $lock = [IO.FileStream]::new((Join-Path $root '.mtgb-install.lock'), 'OpenOrCreate', 'ReadWrite', 'None', 4096, 'DeleteOnClose')
    $files = @(Get-ChildItem -LiteralPath $payload -Force)
    foreach ($item in $files) {
        if ($entries -cnotcontains $item.Name -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected update entry.' }
        $target = Join-Path $root $item.Name
        if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Cannot replace a linked program entry.'
        }
    }
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    [IO.File]::WriteAllText($ready, '')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (![IO.File]::Exists($commit)) {
        if (($player.HasExited -and ![IO.File]::Exists($commit)) -or ![IO.Directory]::Exists($stage) -or $timer.Elapsed.TotalSeconds -gt 40) { return }
        Start-Sleep -Milliseconds 100
    }
    if (!$player.WaitForExit(120000)) { throw 'The game is still running. Close it and retry the update.' }
    $exited = $true
    foreach ($item in $files) {
        $target = Join-Path $root $item.Name
        if (Test-Path -LiteralPath $target) {
            Move-Entry $target (Join-Path $backup $item.Name)
            $old.Add($item.Name)
        }
        Move-Entry $item.FullName $target
        $new.Add($item.Name)
    }
    Start-Process -FilePath $client -WorkingDirectory $root
    $clean = $true
} catch {
    $reason = $_.Exception.Message
    if ($exited) {
        try {
            foreach ($name in $new) { Move-Entry (Join-Path $root $name) (Join-Path $payload $name) }
            foreach ($name in $old) { Move-Entry (Join-Path $backup $name) (Join-Path $root $name) }
            Start-Process -FilePath $client -WorkingDirectory $root
            $clean = $true
        } catch { $reason += [Environment]::NewLine + $_.Exception.Message + [Environment]::NewLine + $backup }
    }
    if ([IO.File]::Exists($commit)) {
        Add-Type -AssemblyName System.Windows.Forms
        [Windows.Forms.MessageBox]::Show($reason, 'MTGB - Update failed') | Out-Null
    }
    exit 1
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
    if ($clean -and ([IO.Directory]::GetParent($stage).FullName -eq $root) -and ([IO.Path]::GetFileName($stage) -cmatch '^\.mtgb-update-[0-9a-f]{32}$')) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
