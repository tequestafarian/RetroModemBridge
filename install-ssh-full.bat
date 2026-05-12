@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0install-ssh-full.ps1"
pause
