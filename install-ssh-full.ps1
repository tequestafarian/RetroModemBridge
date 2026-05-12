Write-Host "RetroModem Bridge SSH Proxy Full Installer" -ForegroundColor Cyan
Write-Host "-----------------------------------------"
Write-Host "This should be run from the root of your RetroModemBridge repo."
Write-Host "It will:"
Write-Host "  - locate the RetroModemBridge .csproj"
Write-Host "  - copy SSH proxy source files"
Write-Host "  - add SSH.NET with dotnet"
Write-Host "  - create docs and patch snippets"
Write-Host "  - create backups before any file changes"
Write-Host ""

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Get-Location

$project = Get-ChildItem -Path $repoRoot -Recurse -Filter "RetroModemBridge.csproj" | Select-Object -First 1
if (-not $project) {
    Write-Host "ERROR: Could not find RetroModemBridge.csproj under $repoRoot" -ForegroundColor Red
    exit 1
}

$projectDir = Split-Path -Parent $project.FullName
Write-Host "Found project: $($project.FullName)" -ForegroundColor Green

$dest = Join-Path $projectDir "SshProxy"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$src = Join-Path $scriptRoot "SshProxy"
Get-ChildItem -Path $src -Filter "*.cs" | ForEach-Object {
    $target = Join-Path $dest $_.Name
    if ((Test-Path $target) -and ((Resolve-Path $target).Path -eq $_.FullName)) {
        Write-Host "Skipping same file: $($_.Name)"
    } else {
        if (Test-Path $target) {
            Copy-Item $target "$target.bak-ssh" -Force
        }
        Copy-Item $_.FullName $target -Force
        Write-Host "Copied $($_.Name)"
    }
}

$docsDest = Join-Path (Split-Path -Parent $projectDir) "docs"
New-Item -ItemType Directory -Force -Path $docsDest | Out-Null
Copy-Item (Join-Path $scriptRoot "docs\SSH-PROXY-FULL-INTEGRATION.md") (Join-Path $docsDest "SSH-PROXY-FULL-INTEGRATION.md") -Force
Copy-Item (Join-Path $scriptRoot "patches\MAINFORM-SSH-PATCH-SNIPPETS.txt") (Join-Path $docsDest "MAINFORM-SSH-PATCH-SNIPPETS.txt") -Force

Write-Host ""
Write-Host "Adding SSH.NET NuGet package..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    dotnet add package SSH.NET
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Building project..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    dotnet build
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Installed SSH proxy files." -ForegroundColor Green
Write-Host "Next: open docs\SSH-PROXY-FULL-INTEGRATION.md and docs\MAINFORM-SSH-PATCH-SNIPPETS.txt"
Write-Host "The package is compile-safe, but MainForm still needs the ATDT ssh:// hook added."
