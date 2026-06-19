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

if (-not (Test-Path (Join-Path $sourceDir $exeName))) {
    throw "Could not find $exeName in $sourceDir. Run this script from the extracted release folder."
}

Write-Host "Installing to: $InstallDir" -ForegroundColor Cyan
if (Test-Path $InstallDir) {
    Write-Host "Removing previous installation..." -ForegroundColor Yellow
    Remove-Item $InstallDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item (Join-Path $sourceDir "*") $InstallDir -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
New-Item -ItemType Directory -Path $shortcutDir -Force | Out-Null

$shortcut = $shell.CreateShortcut((Join-Path $shortcutDir "Client Center.lnk"))
$shortcut.TargetPath = Join-Path $InstallDir $exeName
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "Client Center for Configuration Manager"
$shortcut.Save()

$desktop = [Environment]::GetFolderPath("Desktop")
$desktopShortcut = $shell.CreateShortcut((Join-Path $desktop "Client Center.lnk"))
$desktopShortcut.TargetPath = Join-Path $InstallDir $exeName
$desktopShortcut.WorkingDirectory = $InstallDir
$desktopShortcut.Description = "Client Center for Configuration Manager"
$desktopShortcut.Save()

Write-Host "Installation complete." -ForegroundColor Green
Write-Host "Start Menu: $shortcutDir" -ForegroundColor Green
Write-Host "Desktop shortcut created." -ForegroundColor Green
