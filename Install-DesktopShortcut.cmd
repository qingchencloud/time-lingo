@echo off
setlocal
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File "%~dp0RedFrameClockTranslator.ps1" -InstallShortcut
pause
