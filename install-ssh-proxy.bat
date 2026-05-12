@echo off
setlocal

echo.
echo RetroModem Bridge SSH Proxy Add-on Installer
echo --------------------------------------------
echo This should be run from the root of your RetroModemBridge repo.
echo It will:
echo   - copy the SSH proxy source files
echo   - try to add the SSH.NET NuGet package with dotnet
echo   - create docs and patch snippets
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0install-ssh-proxy.ps1"

echo.
pause
