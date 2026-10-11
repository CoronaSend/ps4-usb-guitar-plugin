@echo off
title Guitar Mapping Tool
if not exist "%~dp0guided_capture.ps1" (
  echo.
  echo guided_capture.ps1 was not found next to this file.
  echo Right-click the downloaded zip, choose "Extract All", then run RUN_ME.bat from the extracted folder.
  echo.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0guided_capture.ps1"
echo.
echo (If you saw an error above, copy it and send it to the author.)
pause
