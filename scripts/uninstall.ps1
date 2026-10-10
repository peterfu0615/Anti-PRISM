# Anti-PRISM Automated One-Click Uninstaller
# Usage: irm https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/uninstall.ps1 | iex

$ErrorActionPreference = "SilentlyContinue"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[*] Requesting Administrator privileges to uninstall..." -ForegroundColor Yellow
    $scriptUrl = "https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/uninstall.ps1"
    Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -NoProfile -Command `"irm $scriptUrl | iex`""
    exit
}

Write-Host "==========================================================" -ForegroundColor Red
Write-Host "       Anti-PRISM Automated Uninstaller & Unbrick          " -ForegroundColor Red
Write-Host "==========================================================" -ForegroundColor Red

# 1. Run emergency camera restore (Unbrick)
Write-Host "[1/4] Restoring all camera hardware devices..." -ForegroundColor Yellow
$svcExe = "C:\Program Files\Anti-PRISM\AntiPrism.Svc.exe"
if (Test-Path $svcExe) {
    Start-Process -FilePath $svcExe -ArgumentList "--unbrick" -Wait -WindowStyle Hidden
}

# 2. Terminate running processes
Write-Host "[2/4] Terminating processes..." -ForegroundColor Yellow
Stop-Process -Name "BrightnessCtl" -Force
Stop-Process -Name "AntiPrism.Svc" -Force

# 3. Stop and delete service and scheduled task
Write-Host "[3/4] Removing Windows Service and Task Scheduler..." -ForegroundColor Yellow
Stop-Service -Name "AntiPrismSvc" -Force
sc.exe delete AntiPrismSvc | Out-Null
schtasks.exe /delete /tn "AntiPrismTray" /f | Out-Null

# 4. Remove installation files
Write-Host "[4/4] Removing program files..." -ForegroundColor Yellow
$unins = Get-ChildItem "C:\Program Files\Anti-PRISM\unins*.exe" | Select-Object -First 1
if ($unins) {
    Start-Process -FilePath $unins.FullName -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait
} else {
    Remove-Item -Path "C:\Program Files\Anti-PRISM" -Recurse -Force
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " [✓] Anti-PRISM uninstalled successfully. Cameras restored." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
