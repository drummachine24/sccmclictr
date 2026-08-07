#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Removes Client Center for Configuration Manager from Program Files.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "${env:ProgramFiles}\Client Center for Configuration Manager"
)

$ErrorActionPreference = "Stop"

Get-Process -Name "SCCMCliCtrWPF" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping running Client Center (PID $($_.Id))..." -ForegroundColor Yellow
    $_.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 2
    if (-not $_.HasExited) { Stop-Process -Id $_.Id -Force }
}

$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
$desktop = [Environment]::GetFolderPath("Desktop")

if (Test-Path $shortcutDir) { Remove-Item $shortcutDir -Recurse -Force }
$desktopShortcut = Join-Path $desktop "Client Center.lnk"
if (Test-Path $desktopShortcut) { Remove-Item $desktopShortcut -Force }
if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }

$uninstallRegPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClientCenterForConfigMgr"
if (Test-Path $uninstallRegPath) { Remove-Item $uninstallRegPath -Recurse -Force }

Write-Host "Client Center removed." -ForegroundColor Green
