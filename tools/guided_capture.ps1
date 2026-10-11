# Guitar mapping tool. Run it on a Windows PC with the guitar adapter plugged in and follow the prompts.
# It captures every button, works out the mapping, and writes two files to your Desktop:
#   guitar_map.ini      - drag this onto the PS4 at /data/GoldHEN/guitar_map.ini
#   guitar_capture.txt  - the raw capture (send this to the plugin author if something is wrong)
#
# Double-click RUN_ME.bat to start it.
param([switch]$SelfTest)

trap { Write-Host ''; Write-Host "ERROR: $($_.Exception.Message)"; Write-Host $_.ScriptStackTrace; Read-Host 'Press Enter to close'; exit 1 }

function ToBytes($s) { [byte[]]($s -split ' ' | Where-Object { $_ } | ForEach-Object { [Convert]::ToByte($_, 16) }) }
function Hx($v) { '{0:x}' -f $v }
function PopCount($v) { $n = 0; while ($v) { $n += $v -band 1; $v = $v -shr 1 }; $n }

# Pressed when (report[byte] & mask) == val. Uses the lowest byte that changed, which is the digital button byte.
function Get-ButtonEntry($idle, $pressed) {
    for ($i = 0; $i -lt [Math]::Min($idle.Length, $pressed.Length); $i++) {
        if ($idle[$i] -ne $pressed[$i]) {
            $mask = $idle[$i] -bxor $pressed[$i]
            return @{ Byte = $i; Mask = $mask; Val = ($pressed[$i] -band $mask) }
        }
    }
    $null
}

# Strum is often a hat value (several states in one byte), so compare the whole nibble/byte instead of one bit.
function Get-StrumEntries($idle, $up, $down) {
    $ub = Get-ButtonEntry $idle $up
    $db = Get-ButtonEntry $idle $down
    if (-not $ub -or -not $db) { return $null }
    if ($ub.Byte -ne $db.Byte) { return @{ Up = $ub; Down = $db } }
    $b = $ub.Byte
    $changed = $ub.Mask -bor $db.Mask
    $mask = 0xFF
    if (($changed -band 0xF0) -eq 0 -and ($idle[$b] -band 0xF0) -eq ($up[$b] -band 0xF0)) { $mask = 0x0F }
    elseif (($changed -band 0x0F) -eq 0 -and ($idle[$b] -band 0x0F) -eq ($up[$b] -band 0x0F)) { $mask = 0xF0 }
    @{ Up = @{ Byte = $b; Mask = $mask; Val = ($up[$b] -band $mask) }; Down = @{ Byte = $b; Mask = $mask; Val = ($down[$b] -band $mask) } }
}

function Get-WhammyEntry($idle, $pressed) {
    $best = -1; $diff = 0
    for ($i = 0; $i -lt [Math]::Min($idle.Length, $pressed.Length); $i++) {
        $d = [Math]::Abs([int]$idle[$i] - [int]$pressed[$i])
        if ($d -gt $diff) { $diff = $d; $best = $i }
    }
    if ($best -lt 0 -or $diff -lt 16) { return $null }
    @{ Byte = $best; Rest = $idle[$best]; Full = $pressed[$best] }
}

function Build-Map($vid, $pid1, $cap) {
    $idle = ToBytes $cap['IDLE']
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("id=$($vid.ToLower()),$($pid1.ToLower())")
    $missing = @()
    foreach ($k in 'green', 'red', 'yellow', 'blue', 'orange', 'start', 'select') {
        $e = Get-ButtonEntry $idle (ToBytes $cap[$k])
        if ($e) { $lines.Add(("{0}={1},{2},{3}" -f $k, (Hx $e.Byte), (Hx $e.Mask), (Hx $e.Val))) } else { $missing += $k }
    }
    $s = Get-StrumEntries $idle (ToBytes $cap['strum_up']) (ToBytes $cap['strum_down'])
    if ($s) {
        $lines.Add(("strum_up={0},{1},{2}" -f (Hx $s.Up.Byte), (Hx $s.Up.Mask), (Hx $s.Up.Val)))
        $lines.Add(("strum_down={0},{1},{2}" -f (Hx $s.Down.Byte), (Hx $s.Down.Mask), (Hx $s.Down.Val)))
    } else { $missing += 'strum' }
    $w = Get-WhammyEntry $idle (ToBytes $cap['whammy'])
    if ($w) { $lines.Add(("whammy={0},{1},{2}" -f (Hx $w.Byte), (Hx $w.Rest), (Hx $w.Full))) } else { $missing += 'whammy' }
    @{ Lines = $lines; Missing = $missing }
}

if ($SelfTest) {
    # report layout of the adapter/guitar we verified on a real PS4
    $cap = @{
        IDLE = '00 00 06 7F 7F 7F 00 00'; green = '80 00 06 7F 7F 7F 00 00'; red = '02 00 06 7F 7F 7F 00 00'
        yellow = '01 00 06 7F 7F 7F 00 00'; blue = '04 00 06 7F 7F 7F 00 00'; orange = '08 00 06 7F 7F 7F 00 00'
        start = '00 02 06 7F 7F 7F 00 00'; select = '00 01 06 7F 7F 7F 00 00'
        strum_up = '00 00 07 7F 7F 7F 00 00'; strum_down = '00 00 05 7F 7F 7F 00 00'; whammy = '00 00 06 7F 00 7F 00 00'
    }
    $r = Build-Map '2563' '0575' $cap
    $r.Lines
    'missing: ' + ($r.Missing -join ',')
    return
}

Add-Type @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public class RawHid2 {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(string f, uint a, uint s, IntPtr sa, uint d, uint fl, IntPtr t);
}
'@

$raw = New-Object System.Collections.Generic.List[string]
function Say($t) { Write-Host $t; $raw.Add($t) }
function Quit($m) { Write-Host $m; Read-Host 'Press Enter to close'; exit }

$all = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -like 'HID\VID_*' -and $_.InstanceId -notmatch '&COL0[2-9]' })
$list = @($all | Where-Object { $_.FriendlyName -notmatch 'keyboard|mouse|touch|pen' -or $_.InstanceId -match 'VID_2563|VID_0810' })
if (-not $list) { Quit 'No game controller found. Is the adapter plugged in?' }

$pick = -1
for ($i = 0; $i -lt $list.Count; $i++) { if ($list[$i].InstanceId -match 'VID_2563&PID_0575|VID_0810') { $pick = $i; break } }
if ($pick -lt 0) {
    Write-Host 'Devices found:'
    for ($i = 0; $i -lt $list.Count; $i++) { Write-Host ("  [{0}] {1}  ({2})" -f $i, $list[$i].FriendlyName, (($list[$i].InstanceId -split '\\')[1])) }
    $pick = 0
    if ($list.Count -gt 1) { $pick = [int](Read-Host 'Type the number of your guitar adapter') }
}
$dev = $list[$pick]
$null = $dev.InstanceId -match 'VID_([0-9A-F]{4})&PID_([0-9A-F]{4})'
$vid = $Matches[1]; $pid1 = $Matches[2]
Say "Device: $($dev.FriendlyName)  VID=$vid PID=$pid1"

$path = '\\?\hid#' + ($dev.InstanceId.Substring(4) -replace '\\', '#') + '#{4d1e55b2-f16f-11cf-88cb-001111000030}'
$h = [RawHid2]::CreateFile($path, [uint32]2147483648, 3, [IntPtr]::Zero, 3, 0x40000000, [IntPtr]::Zero)
if ($h.IsInvalid) { Quit 'Could not open the device.' }
$fs = New-Object IO.FileStream($h, [IO.FileAccess]::Read, 64, $true)
$buf = New-Object byte[] 64

function Capture($seconds) {
    $seen = @{}
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) {
        $t = $fs.ReadAsync($buf, 0, 64)
        if (-not $t.Wait(500)) { continue }
        # byte 0 is the Windows report-ID byte; the PS4 plugin sees the bytes after it
        $key = ($buf[1..($t.Result - 1)] | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
        if ($seen.ContainsKey($key)) { $seen[$key]++ } else { $seen[$key] = 1 }
    }
    ($seen.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
}

$cap = @{}
Write-Host ''
Read-Host 'Put the guitar DOWN and touch nothing, then press Enter'
$cap['IDLE'] = Capture 3
Say "IDLE: $($cap['IDLE'])"
if (-not $cap['IDLE']) { Quit 'No data received from the adapter. Try another USB port.' }

$steps = [ordered]@{
    green = 'GREEN fret'; red = 'RED fret'; yellow = 'YELLOW fret'; blue = 'BLUE fret'; orange = 'ORANGE fret'
    strum_up = 'STRUM UP'; strum_down = 'STRUM DOWN'; start = 'START'; select = 'SELECT'
    whammy = 'WHAMMY bar pushed all the way down'
}
foreach ($k in $steps.Keys) {
    Write-Host ''
    Read-Host "Get ready to HOLD: $($steps[$k]). Press Enter, then hold it for 3 seconds"
    Write-Host '  HOLD NOW...'
    $cap[$k] = Capture 3
    Say "$($steps[$k]) : $($cap[$k])"
}

$r = Build-Map $vid $pid1 $cap
$desk = [Environment]::GetFolderPath('Desktop')
$utf8 = New-Object Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $desk 'guitar_map.ini'), (($r.Lines -join "`n") + "`n"), $utf8)
[IO.File]::WriteAllText((Join-Path $desk 'guitar_capture.txt'), (($raw -join "`n") + "`n"), $utf8)

Write-Host ''
if ($r.Missing.Count) { Write-Host "WARNING: could not detect: $($r.Missing -join ', '). Send guitar_capture.txt to the plugin author." }
Write-Host 'Done. Two files were saved on your Desktop:'
Write-Host '  guitar_map.ini      -> put this on the PS4 in /data/GoldHEN/'
Write-Host '  guitar_capture.txt  -> keep, in case something needs fixing'
Read-Host 'Press Enter to close'
