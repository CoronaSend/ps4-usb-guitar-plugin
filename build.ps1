# Rebuilds OrbisInstrumentalizer.prx from ./source (Windows, PowerShell).
# Requirements:
#   - LLVM (clang, ld.lld, llvm-ar):   winget install LLVM.LLVM
#   - OpenOrbis PS4 Toolchain (extracted):  https://github.com/OpenOrbis/OpenOrbis-PS4-Toolchain/releases
#   - GoldHEN Plugins SDK (cloned):         https://github.com/GoldHEN/GoldHEN_Plugins_SDK
#
# Usage:
#   .\build.ps1 -Toolchain "C:\path\to\OpenOrbis\PS4Toolchain" -GoldHenSdk "C:\path\to\GoldHEN_Plugins_SDK"
param(
    [Parameter(Mandatory = $true)][string]$Toolchain,
    [Parameter(Mandatory = $true)][string]$GoldHenSdk
)

$ErrorActionPreference = 'Stop'
$env:PATH = "C:\Program Files\LLVM\bin;$env:PATH"
$env:OO_PS4_TOOLCHAIN = $Toolchain
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$bin = Join-Path $root 'bin'

# Build the GoldHEN SDK static library once
if (-not (Test-Path "$GoldHenSdk\libGoldHEN_Hook.a")) {
    Write-Host 'Building GoldHEN SDK library...'
    Push-Location $GoldHenSdk
    Remove-Item build -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory build | Out-Null
    $sdkFlags = '--target=x86_64-pc-freebsd12-elf', '-DORBIS=1', '-D__ORBIS__=1', '-DPS4=1', '-DOO=1', '-D__PS4__=1', '-DOOPS4=1', '-D__OOPS4__=1', '-D__BSD_VISIBLE=1', '-D_BSD_SOURCE=1', '-march=btver2', '-mtune=btver2', '-fPIC', '-std=c11', '-isysroot', $Toolchain, '-isystem', "$Toolchain\include", '-Iinclude'
    foreach ($f in Get-ChildItem source\*.c) { clang @sdkFlags -c $f.FullName -o "build\$($f.BaseName).o" }
    clang -target x86_64-pc-linux-gnu -ffreestanding -nostdlib -fno-builtin -fPIC -isysroot $Toolchain -isystem "$Toolchain\include" -Iinclude -c crt\crtprx.c -o build\crtprx.o
    llvm-ar --format=bsd rcs libGoldHEN_Hook.a (Get-ChildItem build\*.o).FullName
    Pop-Location
}

# The plugin includes headers as <GoldHEN/...>, so mirror the SDK include folder under that name
$shim = Join-Path $root 'build_shim'
Remove-Item $shim -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $shim | Out-Null
Copy-Item "$GoldHenSdk\include" "$shim\GoldHEN" -Recurse

Remove-Item $build, $bin -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $build, $bin | Out-Null

$flags = '--target=x86_64-pc-freebsd12-elf', '-fPIC', '-funwind-tables', '-c', '-D__USE_KLOG__', '-D__FINAL__=1', '-isysroot', $Toolchain, '-isystem', "$Toolchain\include", "-I$shim", "-I$GoldHenSdk\include", "-I$root\include", '-O3'
foreach ($f in Get-ChildItem "$root\source\*.c") {
    clang @flags -o "$build\$($f.BaseName).o" $f.FullName
    if ($LASTEXITCODE) { throw "Compile failed: $($f.Name)" }
}

ld.lld "$GoldHenSdk\build\crtprx.o" (Get-ChildItem "$build\*.o").FullName -o "$bin\OrbisInstrumentalizer.elf" -m elf_x86_64 -pie --script "$Toolchain\link.x" -e _init --eh-frame-hdr "-L$Toolchain\lib" "-L$GoldHenSdk" -lGoldHEN_Hook -lkernel -lSceLibcInternal -lSceSysmodule -lSceUsbd
if ($LASTEXITCODE) { throw 'Link failed' }

cmd /c "`"$Toolchain\bin\windows\create-fself.exe`" -in=$bin\OrbisInstrumentalizer.elf -out=$bin\OrbisInstrumentalizer.oelf --lib=$bin\OrbisInstrumentalizer.prx --paid 0x3800000000000011"
if (-not (Test-Path "$bin\OrbisInstrumentalizer.prx")) { throw 'create-fself failed' }

Remove-Item $shim -Recurse -Force
Write-Host "Done: $bin\OrbisInstrumentalizer.prx"
