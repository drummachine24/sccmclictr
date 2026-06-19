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
$shell = New-Object -ComObject WScript.Shell
$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
$desktop = [Environment]::GetFolderPath("Desktop")

if (Test-Path $shortcutDir) { Remove-Item $shortcutDir -Recurse -Force }
$desktopShortcut = Join-Path $desktop "Client Center.lnk"
if (Test-Path $desktopShortcut) { Remove-Item $desktopShortcut -Force }
if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }

Write-Host "Client Center removed." -ForegroundColor Green
