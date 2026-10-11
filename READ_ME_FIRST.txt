PS2 GUITAR FOR ROCK BAND 4 ON PS4 - READ ME FIRST
=================================================

You need: a jailbroken PS4 with GoldHEN, Rock Band 4 version 02.21 (US),
a PS2 guitar + USB adapter, and a Windows PC on the same network as the PS4.

Everything is in ONE program: GuitarTool.exe  (the plugin is built into it).

STEP 1 - INSTALL (about 2 minutes)
  a) Right-click the zip and choose "Extract All".
  b) On the PS4: jailbreak it, then in the GoldHEN menu turn ON the FTP server.
     Find the PS4's IP address: Settings > Network > View Connection Status.
  c) On the PC, double-click GuitarTool.exe and type 1, press Enter.
     Type the PS4's IP address, press Enter. It does the rest.
  d) Plug the guitar adapter into the PS4, fully close Rock Band 4, start it again.

Try the guitar. If it works, you are finished.

STEP 2 - ONLY IF THE GAME SEES THE GUITAR BUT THE BUTTONS DO NOTHING (or are wrong)
  a) Unplug the adapter from the PS4 and plug it into the PC.
  b) Double-click GuitarTool.exe and type 2, press Enter.
     Follow the prompts: press Enter, then hold the named button when it says
     HOLD NOW. Strum UP and DOWN are the two directions of the strum bar.
     Hold the whammy bar all the way down for the last one.
  c) The tool saves guitar_map.ini (exactly that name) on your Desktop and offers
     to send it to the PS4 for you: type y and enter the PS4 IP.
     (Or run option 1 later; it sends the map file for you.)
  d) Plug the adapter back into the PS4, fully close Rock Band 4, start it again.

WHAT THE INSTALLER CHANGES ON THE PS4
  /data/GoldHEN/plugins/OrbisInstrumentalizer.prx   (the plugin)
  /data/GoldHEN/plugins.ini                         (adds the plugin line under [CUSA02084];
                                                     your old file is saved to your Desktop first)
  /data/GoldHEN/guitar_map.ini                      (only if you made a map in step 2)
If you use RB4DX, its line in plugins.ini is kept. RB4DX itself is not included.

DOING IT BY HAND (no tool): copy the files in the PS4_Files folder to the same
paths on the PS4 with any FTP program (port 2121) and add the plugin line to plugins.ini.

PROBLEMS
  - "Could not connect": the PS4 isn't jailbroken yet, FTP is off, or the IP is wrong.
  - Only Rock Band 4 version 02.21 works. Other versions do nothing.
  - Only ONE guitar adapter should be plugged in while testing.
  - To undo the map: delete /data/GoldHEN/guitar_map.ini from the PS4 and restart the game.
  - Help: Discord https://discord.gg/mGRCKG9BB (@CoronaSend). Attach guitar_capture.txt
    (made on your Desktop by step 2) if the buttons are wrong.

HONEST NOTES
  - Verified on one adapter and guitar (USB ID 2563:0575). Other adapters and
    guitars are untested; step 2 is how you teach it yours.
  - Tilt (star power by tilting) does not work. Press the Select button on the guitar to activate star power instead.
  - Windows may show a SmartScreen warning for an unsigned program: More info > Run anyway.
