PS2 GUITAR FOR ROCK BAND 4 ON PS4 - READ ME FIRST
=================================================

You need: a jailbroken PS4 with GoldHEN, Rock Band 4 version 02.21 (US),
a PS2 guitar + USB adapter, and a Windows PC on the same network as the PS4.

STEP 1 - INSTALL (about 2 minutes)
  a) Right-click this zip and choose "Extract All". Do not run files from inside the zip.
  b) On the PS4: jailbreak it, then in the GoldHEN menu turn ON the FTP server.
     Find the PS4's IP address: Settings > Network > View Connection Status.
  c) On the PC, double-click   1_INSTALL.bat
     Type the PS4's IP address and press Enter. It does the rest.
  d) Plug the guitar adapter into the PS4, fully close Rock Band 4, start it again.

Try the guitar. If it works, you are finished.

STEP 2 - ONLY IF THE GAME SEES THE GUITAR BUT THE BUTTONS DO NOTHING (or are wrong)
  a) Unplug the adapter from the PS4 and plug it into the PC.
  b) Double-click   2_ONLY_IF_BUTTONS_DONT_WORK.bat
     Follow the prompts: press Enter, then hold the named button for 3 seconds.
     Strum UP and strum DOWN are the two directions of the strum bar.
     Hold the whammy bar all the way down for the last one.
  c) Plug the adapter back into the PS4.
  d) Double-click   1_INSTALL.bat   again. It finds the map file the tool made
     and sends it to the PS4.
  e) Fully close Rock Band 4 and start it again.

PROBLEMS
  - "Could not connect": the PS4 isn't jailbroken yet, FTP is off, or the IP is wrong.
  - Only Rock Band 4 version 02.21 works. Other versions do nothing.
  - Only ONE guitar adapter should be plugged in while testing.
  - To undo the map: delete /data/GoldHEN/guitar_map.ini on the PS4 (FTP) and restart the game.
  - Help: Discord https://discord.gg/mGRCKG9BB (@CoronaSend). Attach guitar_capture.txt
    (made on your Desktop by step 2) if the buttons are wrong.

HONEST NOTES
  - Verified on one adapter and guitar (USB ID 2563:0575). Other adapters and
    guitars are untested; the step 2 tool is the way to teach it yours.
  - The installer backs up your plugins.ini to your Desktop (plugins_backup.ini)
    before changing it, and only adds the lines it needs.
  - If you use RB4DX, the installer keeps its line in plugins.ini. It is not
    included here; install RB4DX yourself.
  - Tilt (star power by tilting) is not supported. Use the Select button.
