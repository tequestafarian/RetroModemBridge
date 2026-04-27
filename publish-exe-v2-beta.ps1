# Builds RetroModem Bridge v2 Beta as a self-contained Windows EXE.
# Run from the repository folder in PowerShell.

$ErrorActionPreference = "Stop"

$Project = "RetroModemBridge/RetroModemBridge.csproj"
$Output = "publish/v2-beta"

Write-Host "Building RetroModem Bridge v2 Beta..."
dotnet publish $Project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishReadyToRun=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $Output

Write-Host ""
Write-Host "Done. EXE should be here: $Output/RetroModemBridge-v2-beta.exe"
