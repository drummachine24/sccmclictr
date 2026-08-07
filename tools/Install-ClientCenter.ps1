#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs Client Center for Configuration Manager to Program Files.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "${env:ProgramFiles}\Client Center for Configuration Manager"
)

$ErrorActionPreference = "Stop"
$sourceDir = $PSScriptRoot
$exeName = "SCCMCliCtrWPF.exe"
$exePath = Join-Path $sourceDir $exeName

if (-not (Test-Path $exePath)) {
    throw "Could not find $exeName in $sourceDir. Run this script from the extracted release folder."
}

$productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
Write-Host "Installing Client Center v$productVersion" -ForegroundColor Cyan
Write-Host "Destination: $InstallDir" -ForegroundColor Cyan

# Stop a running instance so files can be replaced.
Get-Process -Name "SCCMCliCtrWPF" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping running Client Center (PID $($_.Id))..." -ForegroundColor Yellow
    $_.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 2
    if (-not $_.HasExited) { Stop-Process -Id $_.Id -Force }
}

if (Test-Path $InstallDir) {
    Write-Host "Removing previous installation..." -ForegroundColor Yellow
    Remove-Item $InstallDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Copy payload, but skip packaging leftovers if present next to the scripts.
Get-ChildItem -Path $sourceDir -Force | Where-Object {
    $_.Name -notmatch '\.zip$' -and $_.Name -ne 'artifacts'
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $InstallDir -Recurse -Force
}

$installedExe = Join-Path $InstallDir $exeName
$shell = New-Object -ComObject WScript.Shell
$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
New-Item -ItemType Directory -Path $shortcutDir -Force | Out-Null

$shortcut = $shell.CreateShortcut((Join-Path $shortcutDir "Client Center.lnk"))
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "Client Center for Configuration Manager v$productVersion"
$shortcut.Save()

$uninstallShortcut = $shell.CreateShortcut((Join-Path $shortcutDir "Uninstall Client Center.lnk"))
$uninstallShortcut.TargetPath = "powershell.exe"
$uninstallShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'Uninstall-ClientCenter.ps1')`""
$uninstallShortcut.WorkingDirectory = $InstallDir
$uninstallShortcut.Description = "Uninstall Client Center for Configuration Manager"
$uninstallShortcut.Save()

$desktop = [Environment]::GetFolderPath("Desktop")
$desktopShortcut = $shell.CreateShortcut((Join-Path $desktop "Client Center.lnk"))
$desktopShortcut.TargetPath = $installedExe
$desktopShortcut.WorkingDirectory = $InstallDir
$desktopShortcut.Description = "Client Center for Configuration Manager v$productVersion"
$desktopShortcut.Save()

# Register for Apps & Features / Add or Remove Programs.
$uninstallRegPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClientCenterForConfigMgr"
New-Item -Path $uninstallRegPath -Force | Out-Null
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayName" -Value "Client Center for Configuration Manager"
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayVersion" -Value $productVersion
Set-ItemProperty -Path $uninstallRegPath -Name "Publisher" -Value "drummachine24"
Set-ItemProperty -Path $uninstallRegPath -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayIcon" -Value $installedExe
Set-ItemProperty -Path $uninstallRegPath -Name "UninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'Uninstall-ClientCenter.ps1')`""
Set-ItemProperty -Path $uninstallRegPath -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallRegPath -Name "NoRepair" -Value 1 -Type DWord
try {
    $sizeKb = [math]::Round(((Get-ChildItem $InstallDir -Recurse -File | Measure-Object Length -Sum).Sum) / 1KB)
    Set-ItemProperty -Path $uninstallRegPath -Name "EstimatedSize" -Value ([int]$sizeKb) -Type DWord
} catch { }

Write-Host "Installation complete." -ForegroundColor Green
Write-Host "Start Menu: $shortcutDir" -ForegroundColor Green
Write-Host "Desktop shortcut created." -ForegroundColor Green
