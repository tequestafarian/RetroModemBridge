@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install-irc-feature-v3.5.ps1" -Build
pause
