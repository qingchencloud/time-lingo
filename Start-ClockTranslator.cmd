@echo off
setlocal
start "" powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0RedFrameClockTranslator.ps1"
