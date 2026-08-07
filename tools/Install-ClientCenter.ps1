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

function Stop-ClientCenterProcesses {
    param([string]$TargetExePath)

    $stopped = $false

    # Prefer path-based match so we catch renamed/locked installs.
    try {
        $targetFull = [System.IO.Path]::GetFullPath($TargetExePath)
        Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object { $_.ExecutablePath -and ([System.IO.Path]::GetFullPath($_.ExecutablePath) -ieq $targetFull) } |
            ForEach-Object {
                Write-Host "Stopping Client Center process by path (PID $($_.ProcessId))..." -ForegroundColor Yellow
                Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
                $stopped = $true
            }
    } catch { }

    Get-Process -Name "SCCMCliCtrWPF" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping Client Center (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        $stopped = $true
    }

    & taskkill.exe /F /IM $exeName /T 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { $stopped = $true }

    if ($stopped) {
        Start-Sleep -Seconds 2
    }
}

function Remove-InstallDirectory {
    param([string]$Path)

    if (-not (Test-Path $Path)) { return }

    Write-Host "Removing previous installation..." -ForegroundColor Yellow

    $installedExe = Join-Path $Path $exeName
    Stop-ClientCenterProcesses -TargetExePath $installedExe

    for ($i = 1; $i -le 5; $i++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        } catch {
            Write-Host "Retry $i/5: waiting for file locks to release..." -ForegroundColor Yellow
            Stop-ClientCenterProcesses -TargetExePath $installedExe
            Start-Sleep -Seconds (2 * $i)
        }
    }

    # Last resort: move the locked folder aside and continue installing.
    $backup = "{0}.old.{1}" -f $Path, (Get-Date -Format "yyyyMMddHHmmss")
    Write-Host "Install folder is still locked. Moving it aside to:`n  $backup" -ForegroundColor Yellow
    try {
        Move-Item -LiteralPath $Path -Destination $backup -Force -ErrorAction Stop
        Write-Host "Previous files were moved aside. You can delete that folder after reboot if needed." -ForegroundColor Yellow
    } catch {
        throw "Unable to replace the existing installation because '$exeName' is locked. Close Client Center (and any Explorer windows on that folder), then run Install.cmd again. Details: $($_.Exception.Message)"
    }
}

if (-not (Test-Path $exePath)) {
    throw "Could not find $exeName in $sourceDir. Run this script from the extracted release folder."
}

$productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
Write-Host "Installing Client Center v$productVersion" -ForegroundColor Cyan
Write-Host "Destination: $InstallDir" -ForegroundColor Cyan

Remove-InstallDirectory -Path $InstallDir
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
