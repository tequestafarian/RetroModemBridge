$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "RetroModem Bridge EXE Publisher"
Write-Host "================================"
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "The .NET SDK was not found."
    Write-Host "Install the .NET 8 SDK from Microsoft, then run this again."
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Restoring packages..."
dotnet restore .\RetroModemBridge.sln

Write-Host ""
Write-Host "Publishing self-contained Windows x64 EXE..."
dotnet publish .\RetroModemBridge\RetroModemBridge.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

Write-Host ""
Write-Host "Done."
Write-Host ""
Write-Host "Your EXE is here:"
Write-Host ".\RetroModemBridge\bin\Release\net8.0-windows\win-x64\publish\RetroModemBridge.exe"
Write-Host ""
Read-Host "Press Enter to exit"
