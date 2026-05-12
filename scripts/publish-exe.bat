@echo off
setlocal
cd /d "%~dp0"

echo.
echo RetroModem Bridge EXE Publisher
echo ================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo The .NET SDK was not found.
  echo.
  echo Install the .NET 8 SDK from Microsoft, then run this again.
  echo.
  pause
  exit /b 1
)

echo Restoring packages...
dotnet restore RetroModemBridge.sln
if errorlevel 1 goto fail

echo.
echo Publishing self-contained Windows x64 EXE...
dotnet publish RetroModemBridge\RetroModemBridge.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
if errorlevel 1 goto fail

echo.
echo Done.
echo.
echo Your EXE is here:
echo RetroModemBridge\bin\Release\net8.0-windows\win-x64\publish\RetroModemBridge.exe
echo.
pause
exit /b 0

:fail
echo.
echo Publish failed. Check the error message above.
echo.
pause
exit /b 1
