# Installs the guitar plugin on a jailbroken PS4 over FTP. Double-click INSTALL.bat to run it.
param([string]$Ip, [switch]$SelfTest)

trap { Write-Host ''; Write-Host "ERROR: $($_.Exception.Message)"; Read-Host 'Press Enter to close'; exit 1 }

$Section = '[CUSA02084]'
$PluginLine = '/data/GoldHEN/plugins/OrbisInstrumentalizer.prx'
$Rb4dxLine = '/data/GoldHEN/plugins/RB4DX-Plugin.prx'

# Adds missing lines under [CUSA02084] and leaves everything else exactly as it was.
function Update-Ini([string]$text, [string[]]$need) {
    $L = New-Object System.Collections.Generic.List[string]
    if ([string]::IsNullOrWhiteSpace($text)) { $text = "[settings]`nshow_load_notification=true" }
    foreach ($x in ($text -split "`r?`n")) { $L.Add($x) }
    while ($L.Count -gt 0 -and $L[$L.Count - 1].Trim() -eq '') { $L.RemoveAt($L.Count - 1) }
    $idx = -1
    for ($i = 0; $i -lt $L.Count; $i++) { if ($L[$i].Trim() -eq $Section) { $idx = $i; break } }
    if ($idx -lt 0) { $L.Add($Section); $idx = $L.Count - 1 }
    $end = $idx + 1
    while ($end -lt $L.Count -and $L[$end].Trim() -notmatch '^\[') { $end++ }
    $changed = $false
    foreach ($n in $need) {
        $has = $false
        for ($i = $idx + 1; $i -lt $end; $i++) { $t = $L[$i].Trim(); if ($t -eq $n -or $t -like "$n=*") { $has = $true } }
        if (-not $has) { $L.Insert($end, $n); $end++; $changed = $true }
    }
    @{ Text = (($L -join "`n") + "`n"); Changed = $changed }
}

if ($SelfTest) {
    '--- empty'; (Update-Ini '' @($Rb4dxLine, $PluginLine)).Text
    '--- other plugins, no section'; (Update-Ini "[settings]`r`nshow_load_notification=true`r`n[CUSA00419]`r`n/a.prx`r`n" @($PluginLine)).Text
    '--- section exists, middle of file'; (Update-Ini "[CUSA02084]`n/data/GoldHEN/plugins/RB4DX-Plugin.prx`n[CUSA11100]`n/b.prx`n" @($Rb4dxLine, $PluginLine)).Text
    '--- already complete'; $r = Update-Ini "[CUSA02084]`n$Rb4dxLine`n$PluginLine`n[X]`n/c.prx`n" @($Rb4dxLine, $PluginLine); "changed: $($r.Changed)"
    return
}

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$prx = Join-Path $here 'PS4_Files\data\GoldHEN\plugins\OrbisInstrumentalizer.prx'
if (-not (Test-Path $prx)) { $prx = Join-Path $here 'OrbisInstrumentalizer.prx' }
if (-not (Test-Path $prx)) { throw 'Cannot find OrbisInstrumentalizer.prx. Extract the whole zip first, then run INSTALL.bat from the extracted folder.' }

Write-Host ''
Write-Host 'Guitar plugin installer'
Write-Host '-----------------------'
Write-Host 'Before you start: jailbreak the PS4 and turn on the FTP server in the GoldHEN menu.'
Write-Host 'The PS4 IP address is in Settings > Network > View Connection Status.'
Write-Host ''
if (-not $Ip) { $Ip = (Read-Host 'Type the PS4 IP address (example 192.168.1.27) and press Enter').Trim() }
$base = "ftp://${Ip}:2121"

function Ftp-Get($path, $out) { & curl.exe -s --connect-timeout 8 -o $out "$base$path" 2>$null; $LASTEXITCODE }
function Ftp-Put($file, $path) { & curl.exe -s --connect-timeout 8 --ftp-create-dirs -T $file "$base$path" 2>$null; $LASTEXITCODE }

$tmp = Join-Path $env:TEMP 'guitar_install'
New-Item -ItemType Directory $tmp -Force | Out-Null

$list = & curl.exe -s --connect-timeout 8 "$base/data/GoldHEN/" 2>$null
if ($LASTEXITCODE -ne 0) { throw "Could not connect to $Ip on port 2121. Is the PS4 jailbroken, is FTP turned on in the GoldHEN menu, and is the IP right?" }

Write-Host 'Uploading the plugin...'
if ((Ftp-Put $prx '/data/GoldHEN/plugins/OrbisInstrumentalizer.prx') -ne 0) { throw 'Upload of the plugin failed.' }

$plugins = & curl.exe -s --connect-timeout 8 "$base/data/GoldHEN/plugins/" 2>$null
$need = @()
if ($plugins -match 'RB4DX-Plugin\.prx') { $need += $Rb4dxLine }
$need += $PluginLine

$iniLocal = Join-Path $tmp 'plugins.ini'
Remove-Item $iniLocal -ErrorAction SilentlyContinue
$null = Ftp-Get '/data/GoldHEN/plugins.ini' $iniLocal
$old = ''
if (Test-Path $iniLocal) { $old = [IO.File]::ReadAllText($iniLocal) }
$res = Update-Ini $old $need
if ($res.Changed) {
    if ($old) {
        $bk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'plugins_backup.ini'
        [IO.File]::WriteAllText($bk, $old, (New-Object Text.UTF8Encoding $false))
        Write-Host "Your old plugins.ini was saved to $bk"
    }
    $newLocal = Join-Path $tmp 'plugins_new.ini'
    [IO.File]::WriteAllText($newLocal, $res.Text, (New-Object Text.UTF8Encoding $false))
    if ((Ftp-Put $newLocal '/data/GoldHEN/plugins.ini') -ne 0) { throw 'Updating plugins.ini failed.' }
    Write-Host 'plugins.ini updated.'
} else {
    Write-Host 'plugins.ini was already correct.'
}

$map = Join-Path ([Environment]::GetFolderPath('Desktop')) 'guitar_map.ini'
if (-not (Test-Path $map)) { $map = Join-Path $here 'guitar_map.ini' }
if (Test-Path $map) {
    Write-Host 'Found guitar_map.ini, uploading it...'
    if ((Ftp-Put $map '/data/GoldHEN/guitar_map.ini') -ne 0) { throw 'Upload of guitar_map.ini failed.' }
    Write-Host 'guitar_map.ini uploaded.'
} else {
    Write-Host 'No guitar_map.ini found (that is fine). If your buttons do not work, run 2_ONLY_IF_BUTTONS_DONT_WORK.bat, then run 1_INSTALL.bat again.'
}

Write-Host ''
Write-Host 'DONE. Plug in the guitar, fully close Rock Band 4, and start it again.'
Read-Host 'Press Enter to close'
