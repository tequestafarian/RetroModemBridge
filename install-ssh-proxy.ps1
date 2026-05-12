$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$projectFolder = Join-Path $repoRoot "RetroModemBridge"

if (!(Test-Path $projectFolder)) {
    Write-Host "ERROR: Could not find the RetroModemBridge project folder." -ForegroundColor Red
    Write-Host "Run this from the root of your RetroModemBridge repo, the folder that contains the RetroModemBridge subfolder."
    exit 1
}

$csproj = Get-ChildItem -Path $projectFolder -Filter "*.csproj" -Recurse | Select-Object -First 1
if (!$csproj) {
    Write-Host "ERROR: Could not find a .csproj file under RetroModemBridge." -ForegroundColor Red
    exit 1
}

Write-Host "Found project: $($csproj.FullName)" -ForegroundColor Green

$src = Join-Path $PSScriptRoot "RetroModemBridge\SshProxy"
$dest = Join-Path $projectFolder "SshProxy"

if (!(Test-Path $src)) {
    Write-Host "ERROR: Could not find source folder: $src" -ForegroundColor Red
    Write-Host "Make sure the extracted add-on folder still contains RetroModemBridge\SshProxy."
    exit 1
}

if (!(Test-Path $dest)) {
    New-Item -ItemType Directory -Path $dest | Out-Null
}

$srcResolved = (Resolve-Path $src).Path.TrimEnd('\')
$destResolved = (Resolve-Path $dest).Path.TrimEnd('\')

$copied = 0
$skipped = 0

Get-ChildItem -Path $src -Filter "*.cs" | ForEach-Object {
    $target = Join-Path $dest $_.Name
    $sourcePath = $_.FullName

    $sameFile = $false
    if (Test-Path $target) {
        $targetPath = (Resolve-Path $target).Path
        if ($targetPath -ieq $sourcePath) {
            $sameFile = $true
        }
    }

    if ($sameFile) {
        Write-Host "Skipping $($_.Name), already in place." -ForegroundColor Yellow
        $script:skipped++
    }
    else {
        Copy-Item -Path $sourcePath -Destination $target -Force
        Write-Host "Copied $($_.Name)" -ForegroundColor Green
        $script:copied++
    }
}

Write-Host "SSH proxy source step complete. Copied: $copied. Skipped: $skipped." -ForegroundColor Green

Write-Host ""
Write-Host "Trying to add SSH.NET NuGet package..."
try {
    dotnet add $csproj.FullName package SSH.NET
    Write-Host "SSH.NET package added or already present." -ForegroundColor Green
}
catch {
    Write-Host "Could not run 'dotnet add package SSH.NET' automatically." -ForegroundColor Yellow
    Write-Host "In Visual Studio, right-click the project, choose Manage NuGet Packages, and install: SSH.NET"
}

Write-Host ""
Write-Host "Installed source files. Next, apply the MainForm/AppSettings snippets from:"
Write-Host "  docs\SSH-PROXY-INTEGRATION.md"
Write-Host ""
Write-Host "This pack does not blindly patch MainForm.cs because that file changes a lot between versions."
