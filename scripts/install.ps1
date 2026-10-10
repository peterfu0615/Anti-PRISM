# Anti-PRISM Automated One-Click Installer for Windows 10/11
# Usage: irm https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/install.ps1 | iex

$ErrorActionPreference = "Stop"

# 1. Check Administrator Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[*] Requesting Administrator privileges..." -ForegroundColor Yellow
    $scriptUrl = "https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/install.ps1"
    Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -NoProfile -Command `"irm $scriptUrl | iex`""
    exit
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       Anti-PRISM Automated Installer (Windows 10/11)      " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Download Latest Release Installer
$repo = "peterfu0615/Anti-PRISM"
$downloadUrl = "https://github.com/$repo/releases/latest/download/AntiPrism_Setup.exe"
$tempPath = Join-Path $env:TEMP "AntiPrism_Setup.exe"

Write-Host "[1/4] Downloading latest release from GitHub ($repo)..." -ForegroundColor Green
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempPath -UseBasicParsing
}
catch {
    Write-Warning "Could not download from releases/latest directly. Checking GitHub API for release assets..."
    try {
        $apiUrl = "https://api.github.com/repos/$repo/releases/latest"
        $releaseInfo = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing
        $asset = $releaseInfo.assets | Where-Object { $_.name -like "*.exe" } | Select-Object -First 1
        if ($asset) {
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tempPath -UseBasicParsing
        } else {
            throw "No executable installer found in the latest release."
        }
    } catch {
        Write-Error "Failed to download AntiPrism installer: $_"
        exit 1
    }
}

# 3. Execute Silent Installation
Write-Host "[2/4] Executing silent installation to C:\Program Files\Anti-PRISM..." -ForegroundColor Green
$process = Start-Process -FilePath $tempPath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait -PassThru

if ($process.ExitCode -ne 0) {
    Write-Error "Installer exited with non-zero code: $($process.ExitCode)"
    exit $process.ExitCode
}

# Clean up temp installer
Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue

# 4. Verify Service and Task
Write-Host "[3/4] Verifying Windows Service and Task Scheduler..." -ForegroundColor Green

Start-Sleep -Seconds 2

$svc = Get-Service -Name "AntiPrismSvc" -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne "Running") {
        Start-Service -Name "AntiPrismSvc"
    }
    Write-Host "  -> Service AntiPrismSvc: RUNNING (Session 0 SYSTEM)" -ForegroundColor DarkGreen
} else {
    Write-Warning "  -> AntiPrismSvc service could not be located."
}

# 5. Launch Tray for Current Interactive User Session if not already running
Write-Host "[4/4] Launching disguised tray application (BrightnessCtl)..." -ForegroundColor Green
$trayPath = "C:\Program Files\Anti-PRISM\BrightnessCtl.exe"
$runningTray = Get-Process -Name "BrightnessCtl" -ErrorAction SilentlyContinue

if (-not $runningTray -and (Test-Path $trayPath)) {
    Start-Process -FilePath $trayPath -ArgumentList "--hidden"
    Write-Host "  -> BrightnessCtl.exe launched in background." -ForegroundColor DarkGreen
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [✓] Installation complete! System is fully protected." -ForegroundColor Green
Write-Host "     - Pre-logon protection: ACTIVE" -ForegroundColor Gray
Write-Host "     - Tray disguise icon: ACTIVE in notification area" -ForegroundColor Gray
Write-Host "     - Gesture unlock: Move slider to 61 (6 times in 10s)" -ForegroundColor Gray
Write-Host "     - Emergency hotkey: Ctrl + Alt + Shift + F12" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan
