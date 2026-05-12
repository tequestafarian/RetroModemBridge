# Builds RetroModem Bridge v3.4 as a self-contained Windows EXE.
# Run from the repository folder in PowerShell.

$ErrorActionPreference = "Stop"

$Project = "RetroModemBridge/RetroModemBridge.csproj"
$Output = "publish/v3.4"

Write-Host "Building RetroModem Bridge v3.4..."
dotnet publish $Project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishReadyToRun=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $Output

Write-Host ""
Write-Host "Done. EXE should be here: $Output/RetroModemBridge-v3.4.exe"
