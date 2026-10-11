# USB Guitar Plugin: Use a PS2 Guitar in Rock Band 4 on PS4

**Made by CoronaSend** | [Join the Discord](https://discord.gg/mGRCKG9BB)

A GoldHEN plugin that lets a **PS2 guitar on a generic PS2-to-USB adapter** work in **Rock Band 4** on a jailbroken PS4.

Tested with a generic adapter (USB ID `2563:0575`) on a jailbroken PS4 with GoldHEN and Rock Band 4 version **02.21**, USA (`CUSA02084`), together with RB4DX.

## What does this do?

Rock Band 4 on PS4 only accepts official PS4 instruments (plus some PS3-era ones). An old PS2 guitar on a cheap PS2-to-USB adapter is not recognized, so the game ignores it.

This plugin loads into Rock Band 4 through GoldHEN and sits between the game and the PS4's USB stack. When it sees the adapter, it:

1. Makes the game think the adapter is a PS3 Rock Band guitar.
2. Reads the adapter's raw button data and translates it into the format a PS3 Rock Band guitar sends (frets, strum, whammy, Start/Options).

You then play with your PS2 guitar as if it were a supported guitar. Nothing on the PS4 is permanently changed, and removing the plugin restores normal behavior.

## Requirements

- Jailbroken PS4 running GoldHEN (2.3.0 or newer) with the plugin loader enabled
- Rock Band 4 **version 02.21** (in-game 2.3.7). The plugin refuses to run on any other version.
- A PS2 guitar and a PS2-to-USB adapter that shows up as USB ID `2563:0575` (see "Using a different adapter" below if yours is different)

## Install

1. Back up your PS4's `/data/GoldHEN/plugins/OrbisInstrumentalizer.prx` first if it already exists.
2. Copy `OrbisInstrumentalizer.prx` from this folder to `/data/GoldHEN/plugins/` on the PS4 (FTP, for example `ftp://<PS4-IP>:2121`).
3. Open `/data/GoldHEN/plugins.ini` on the PS4, or create it if it doesn't exist.
4. Under the `[CUSA02084]` heading, add this line, keeping any plugins that are already listed:

   ```ini
   [CUSA02084]
   /data/GoldHEN/plugins/OrbisInstrumentalizer.prx
   ```

   If you also use RB4DX, both lines go under the same heading:

   ```ini
   [CUSA02084]
   /data/GoldHEN/plugins/RB4DX-Plugin.prx
   /data/GoldHEN/plugins/OrbisInstrumentalizer.prx
   ```

   For the PAL version of the game, use `[CUSA02901]` instead. The PAL version has not been tested.

   Don't replace an existing `plugins.ini` with the sample one in this folder. It exists only to show the format.
5. **Plug the guitar adapter into the PS4 before launching the game.**
6. Launch Rock Band 4. A "USB Guitar Plugin - Made by CoronaSend - Discord: @CoronaSend" notification means the plugin loaded.

## Uninstall

Delete the `OrbisInstrumentalizer.prx` line from `plugins.ini`, and delete the `.prx` file (or restore your backup).

## Default controls

| Guitar | Rock Band 4 |
|---|---|
| Green, Red, Yellow, Blue, Orange | Same color fret |
| Strum up / down | Strum up / down |
| Whammy bar | Whammy |
| Start or Select | Options / Start (both do the same thing) |
| Tilt | Not mapped |

## Remapping

The mapping is in `source/usbd_hooks_rb4.c`, in the function `ParsePS2Callback`. Rebuilding takes a few minutes (see "Rebuilding").

### What the adapter sends (raw report bytes)

| Byte | Meaning |
|---|---|
| 0, bit 0 (`0x01`) | Yellow |
| 0, bit 1 (`0x02`) | Red |
| 0, bit 2 (`0x04`) | Blue |
| 0, bit 3 (`0x08`) | Orange |
| 0, bit 7 (`0x80`) | Green |
| 0, bit 6 (`0x40`) | Flickers randomly: ignore it |
| 1, bit 0 (`0x01`) | Select |
| 1, bit 1 (`0x02`) | Start |
| 2 | Strum hat: `6` idle, `5` strum down, `7` strum up |
| 4 | Whammy: `0x7F` at rest, down to `0x00` fully pressed |

### What Rock Band 4 expects (output bits)

These were found by testing in-game:

| Output | Meaning |
|---|---|
| `BIT(0)` | Red |
| `BIT(1)` | Green |
| `BIT(2)` | Blue |
| `BIT(3)` | Orange |
| `BIT(4)` | Yellow |
| `BIT(8)` | Back |
| `BIT(9)` | Start / Options |
| `hat` `0x00` / `0x04` / `0x08` | Strum up / strum down / idle |

### Example: swap two frets

If your game shows Red and Blue the wrong way round, find these lines in `ParsePS2Callback` and swap the output bits:

```c
if ((raw[0] & 0x02) != 0) parsed_report.buttons |= BIT(0); // red
if ((raw[0] & 0x04) != 0) parsed_report.buttons |= BIT(2); // blue
```

### Example: make Select act as Back instead of Options

Replace the Start/Select line with:

```c
if ((raw[1] & 0x01) != 0) parsed_report.buttons |= BIT(8); // select -> back
if ((raw[1] & 0x02) != 0) parsed_report.buttons |= BIT(9); // start -> options
```

After any change, rebuild, copy the new `.prx` to the PS4, and **fully restart the game** (plugins only load at game launch).

## Using a different adapter

1. Plug the adapter into a Windows PC, then run `tools\read_raw_report.ps1`:

   ```powershell
   .\tools\read_raw_report.ps1 -VendorId XXXX -ProductId YYYY
   ```

   Find the IDs in Device Manager > the device > Properties > Details > Hardware Ids (`VID_xxxx&PID_yyyy`).
2. Run it once with nothing pressed (idle), then once while holding each button. Compare the bytes to find which ones change. This gives you a table like the "What the adapter sends" one above.
3. In `source/usbd_hooks_rb4.c`, change `PS2_ADAPTER_VID` and `PS2_ADAPTER_PID` to your adapter's IDs, then edit `ParsePS2Callback` to match your bytes.
4. Rebuild.

This only works if the PS4 can see the adapter as a USB device at all. Adapters that need a driver on Windows may not work.

## Rebuilding

You need a Windows PC with:

- LLVM: `winget install LLVM.LLVM`
- The [OpenOrbis PS4 Toolchain](https://github.com/OpenOrbis/OpenOrbis-PS4-Toolchain/releases) release, extracted
- The [GoldHEN Plugins SDK](https://github.com/GoldHEN/GoldHEN_Plugins_SDK), cloned

Then in PowerShell, from this folder (the bypass flag is needed if script execution is disabled):

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Toolchain "C:\path\to\OpenOrbis\PS4Toolchain" -GoldHenSdk "C:\path\to\GoldHEN_Plugins_SDK"
```

The result is `bin\OrbisInstrumentalizer.prx`.

## Troubleshooting

- **No notification:** check that the `plugins.ini` heading matches your game's title ID, and that the game is version 02.21.
- **RB4DX stopped loading:** a new `plugins.ini` replaces GoldHEN's defaults. List every plugin you use under the heading, including `RB4DX-Plugin.prx`.
- **Guitar not detected:** unplug the adapter, plug it into the PS4 **before** starting the game, and restart the game.
- **A fret is the wrong color or a button does nothing:** see "Remapping".
- **Menus can't be navigated with the guitar:** use a normal controller for menus. Start/Select work for Options only.

## Credits and license

This is a modified version of [OrbisInstrumentalizer](https://github.com/InvoxiPlayGames/OrbisInstrumentalizer) by **InvoxiPlayGames**, which does the USB hooking for Xbox 360 and Wii instruments. PS2 adapter support, the remapping and the notification text were added by **CoronaSend**.

Licensed under the **GNU Lesser General Public License v2.1 or later**. See `LICENSE.txt`. The full source is included in `source/` and `include/`, as the license requires.

This is for use on consoles you own. Use at your own risk.
