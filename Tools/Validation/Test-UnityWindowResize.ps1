param([Parameter(Mandatory=$true)][int]$UnityProcessId, [int]$Cycles=12)
$ErrorActionPreference='Stop'
$target=Get-CimInstance Win32_Process -Filter "ProcessId=$UnityProcessId"
if ($target.Name -ne 'Unity.exe' -or $target.CommandLine -notmatch 'D:[\\/]project[\\/]MTGB') { throw 'Target is not the MTGB Unity Editor.' }
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MTGBResizeProbe {
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int x,y;}
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int left,top,right,bottom;}
 [StructLayout(LayoutKind.Sequential)] public struct Placement {public int length,flags,showCmd;public Point min,max;public Rect normal;}
 [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h,ref Placement p);
 [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr h,ref Placement p);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool repaint);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int command);
}
'@
$process=Get-Process -Id $UnityProcessId
$handle=$process.MainWindowHandle
if($handle -eq 0){throw 'Editor window is unavailable.'}
$placement=New-Object MTGBResizeProbe+Placement
$placement.length=[Runtime.InteropServices.Marshal]::SizeOf($placement)
if(-not [MTGBResizeProbe]::GetWindowPlacement($handle,[ref]$placement)){throw 'Cannot preserve window placement.'}
$rows=@()
try {
 [MTGBResizeProbe]::ShowWindow($handle,9) | Out-Null
 for($i=0;$i -lt $Cycles;$i++) {
  $memory=Get-CimInstance Win32_OperatingSystem
  if($memory.FreeVirtualMemory -lt 2GB/1KB){$rows+=@{step=$i;status='STOP: system commit headroom below 2 GiB'};break}
  $width=if($i%2 -eq 0){1180}else{1580}
  $height=if($i%3 -eq 0){720}else{920}
  $ok=[MTGBResizeProbe]::MoveWindow($handle,40,40,$width,$height,$true)
  Start-Sleep -Milliseconds 500
  $process=Get-Process -Id $UnityProcessId -ErrorAction Stop
  $rows+=@{step=$i;width=$width;height=$height;moved=$ok;privateMB=[math]::Round($process.PrivateMemorySize64/1MB);freeCommitMB=[math]::Round($memory.FreeVirtualMemory/1KB);status='alive'}
 }
} finally {
 [MTGBResizeProbe]::SetWindowPlacement($handle,[ref]$placement) | Out-Null
 $report=Join-Path $PSScriptRoot 'Reports/resize-window.json'
 New-Item -ItemType Directory -Force (Split-Path $report) | Out-Null
 $rows | ConvertTo-Json | Set-Content -LiteralPath $report -Encoding utf8
}
$rows | ForEach-Object { [pscustomobject]$_ } | Format-Table step,width,height,privateMB,freeCommitMB,status
