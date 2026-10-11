# Prints the raw HID report bytes of a USB gamepad/guitar adapter plugged into this PC.
# Use it to work out which byte/bit each button uses before editing the mapping.
#
# Usage:
#   .\read_raw_report.ps1                       (defaults to VID 2563 / PID 0575)
#   .\read_raw_report.ps1 -VendorId 2563 -ProductId 0575 -Seconds 4
#
# Hold ONE input (a fret, strum, Start...) and run it. The most common report is printed first.
# Compare it with the idle report (nothing held) to see which bits changed.
param(
    [string]$VendorId = '2563',
    [string]$ProductId = '0575',
    [int]$Seconds = 4
)

Add-Type @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public class RawHid {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(string f, uint a, uint s, IntPtr sa, uint d, uint fl, IntPtr t);
}
'@

$dev = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like "HID\VID_$VendorId&PID_$ProductId*" } | Select-Object -First 1
if (-not $dev) { Write-Host "Device $VendorId`:$ProductId not found. Is it plugged into this PC?"; return }

$path = '\\?\hid#' + ($dev.InstanceId.Substring(4) -replace '\\', '#') + '#{4d1e55b2-f16f-11cf-88cb-001111000030}'
$h = [RawHid]::CreateFile($path, [uint32]2147483648, 3, [IntPtr]::Zero, 3, 0x40000000, [IntPtr]::Zero)
if ($h.IsInvalid) { Write-Host 'Could not open the device.'; return }

$fs = New-Object IO.FileStream($h, [IO.FileAccess]::Read, 64, $true)
$buf = New-Object byte[] 64
$seen = @{}
$end = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $end) {
    $t = $fs.ReadAsync($buf, 0, 64)
    if (-not $t.Wait(500)) { continue }
    # byte 0 is the Windows report-ID byte; the plugin sees the bytes after it
    $key = ($buf[1..($t.Result - 1)] | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
    if ($seen.ContainsKey($key)) { $seen[$key]++ } else { $seen[$key] = 1 }
}
$seen.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 6 | ForEach-Object { "$($_.Value)x  $($_.Key)" }
