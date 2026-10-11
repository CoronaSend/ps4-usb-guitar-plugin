$ErrorActionPreference = 'Stop'
# Builds GuitarTool.exe with the C# compiler that ships with Windows. The plugin is embedded in the exe.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$prx = Join-Path $root 'bin\OrbisInstrumentalizer.prx'
$out = Join-Path $root 'bin\GuitarTool.exe'
& $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$out" "/resource:$prx,plugin.prx" "$here\GuitarTool.cs"
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }
"Built $out"
