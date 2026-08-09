#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs Client Center for Configuration Manager to Program Files.

.NOTES
    Upgrades in place instead of deleting the install folder. Locked binaries are
    renamed aside (classic Windows replace technique) and removed now or on reboot.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "${env:ProgramFiles}\Client Center for Configuration Manager"
)

$ErrorActionPreference = "Stop"
$sourceDir = $PSScriptRoot
$exeName = "SCCMCliCtrWPF.exe"
$exePath = Join-Path $sourceDir $exeName

if (-not ("ClientCenterNative" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class ClientCenterNative {
    public const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);
}
"@
}

function Stop-ClientCenterProcesses {
    param([string]$TargetExePath)

    $stopped = $false
    $installRoot = Split-Path -Parent $TargetExePath

    try {
        $targetFull = [System.IO.Path]::GetFullPath($TargetExePath)
        Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object {
                $_.ExecutablePath -and (
                    ([System.IO.Path]::GetFullPath($_.ExecutablePath) -ieq $targetFull) -or
                    ($installRoot -and $_.ExecutablePath.StartsWith($installRoot, [System.StringComparison]::OrdinalIgnoreCase))
                )
            } |
            ForEach-Object {
                Write-Host "Stopping process from install folder (PID $($_.ProcessId): $($_.Name))..." -ForegroundColor Yellow
                Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
                $stopped = $true
            }
    } catch { }

    Get-Process -Name "SCCMCliCtrWPF" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping Client Center (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        $stopped = $true
    }

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    try {
        $null = & taskkill.exe /F /IM $exeName /T 2>&1
        if ($LASTEXITCODE -eq 0) { $stopped = $true }
    } finally {
        $ErrorActionPreference = $prevEap
    }

    if ($stopped) { Start-Sleep -Seconds 2 }
}

function Register-DeleteOnReboot {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    [void][ClientCenterNative]::MoveFileEx($Path, $null, [ClientCenterNative]::MOVEFILE_DELAY_UNTIL_REBOOT)
}

function Remove-FileBestEffort {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $true }
    try {
        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
        return $true
    } catch {
        try {
            $pending = "$Path.pendingdelete"
            Move-Item -LiteralPath $Path -Destination $pending -Force -ErrorAction Stop
            Register-DeleteOnReboot -Path $pending
            return $true
        } catch {
            Register-DeleteOnReboot -Path $Path
            return $false
        }
    }
}

function Copy-FileReplaceLocked {
    param(
        [string]$Source,
        [string]$Destination
    )

    $destDir = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    try {
        Copy-Item -LiteralPath $Source -Destination $Destination -Force -ErrorAction Stop
        return
    } catch {
        # Windows often allows renaming an in-use binary even when overwrite/delete fails.
        $stamp = Get-Date -Format "yyyyMMddHHmmssfff"
        $aside = "$Destination.replaced.$stamp"
        try {
            Move-Item -LiteralPath $Destination -Destination $aside -Force -ErrorAction Stop
            Copy-Item -LiteralPath $Source -Destination $Destination -Force -ErrorAction Stop
            if (-not (Remove-FileBestEffort -Path $aside)) {
                Write-Host "Queued for delete on reboot: $aside" -ForegroundColor Yellow
            }
            return
        } catch {
            throw "Could not replace locked file '$Destination'. Close Client Center / Explorer windows on the install folder and retry. Details: $($_.Exception.Message)"
        }
    }
}

function Install-PayloadInPlace {
    param(
        [string]$SourceRoot,
        [string]$DestinationRoot
    )

    Write-Host "Updating files in place..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null

    $sourceFiles = Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Force | Where-Object {
        $_.FullName -notmatch '\\artifacts\\' -and $_.Extension -ne '.zip' -and $_.Name -notlike '*.replaced.*' -and $_.Name -notlike '*.pendingdelete'
    }

    foreach ($file in $sourceFiles) {
        $relative = $file.FullName.Substring($SourceRoot.Length).TrimStart('\')
        $dest = Join-Path $DestinationRoot $relative
        Copy-FileReplaceLocked -Source $file.FullName -Destination $dest
    }

    # Remove stale payload files that are no longer in the package (keep pending/replaced leftovers).
    $sourceRelative = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $sourceFiles) {
        [void]$sourceRelative.Add($file.FullName.Substring($SourceRoot.Length).TrimStart('\'))
    }

    Get-ChildItem -LiteralPath $DestinationRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -notlike '*.replaced.*' -and
            $_.Name -notlike '*.pendingdelete' -and
            $_.Name -ne 'ClientCenterInstall.log'
        } |
        ForEach-Object {
            $rel = $_.FullName.Substring($DestinationRoot.Length).TrimStart('\')
            if (-not $sourceRelative.Contains($rel)) {
                [void](Remove-FileBestEffort -Path $_.FullName)
            }
        }

    # Clean leftover replaced/pending files if unlocked now.
    Get-ChildItem -LiteralPath $DestinationRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like '*.replaced.*' -or $_.Name -like '*.pendingdelete' } |
        ForEach-Object { [void](Remove-FileBestEffort -Path $_.FullName) }
}

if (-not (Test-Path $exePath)) {
    throw "Could not find $exeName in $sourceDir. Run this script from the extracted release folder."
}

$productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
Write-Host "Installing Client Center v$productVersion" -ForegroundColor Cyan
Write-Host "Destination: $InstallDir" -ForegroundColor Cyan

$installedExe = Join-Path $InstallDir $exeName
Stop-ClientCenterProcesses -TargetExePath $installedExe
Install-PayloadInPlace -SourceRoot $sourceDir -DestinationRoot $InstallDir

$shell = New-Object -ComObject WScript.Shell
$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
New-Item -ItemType Directory -Path $shortcutDir -Force | Out-Null

$shortcut = $shell.CreateShortcut((Join-Path $shortcutDir "Client Center for Configuration Manager.lnk"))
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "Client Center for Configuration Manager v$productVersion"
$shortcut.IconLocation = "$installedExe,0"
$shortcut.Save()

$uninstallShortcut = $shell.CreateShortcut((Join-Path $shortcutDir "Uninstall Client Center for Configuration Manager.lnk"))
$uninstallShortcut.TargetPath = "powershell.exe"
$uninstallShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'Uninstall-ClientCenter.ps1')`""
$uninstallShortcut.WorkingDirectory = $InstallDir
$uninstallShortcut.Description = "Uninstall Client Center for Configuration Manager"
$uninstallShortcut.Save()

$desktop = [Environment]::GetFolderPath("Desktop")
$desktopShortcut = $shell.CreateShortcut((Join-Path $desktop "Client Center for Configuration Manager.lnk"))
$desktopShortcut.TargetPath = $installedExe
$desktopShortcut.WorkingDirectory = $InstallDir
$desktopShortcut.Description = "Client Center for Configuration Manager v$productVersion"
$desktopShortcut.IconLocation = "$installedExe,0"
$desktopShortcut.Save()

# Remove legacy short-name shortcuts from older installs.
$legacyStartMenu = Join-Path $shortcutDir "Client Center.lnk"
$legacyUninstall = Join-Path $shortcutDir "Uninstall Client Center.lnk"
$legacyDesktop = Join-Path $desktop "Client Center.lnk"
foreach ($legacy in @($legacyStartMenu, $legacyUninstall, $legacyDesktop)) {
    if (Test-Path -LiteralPath $legacy) {
        Remove-Item -LiteralPath $legacy -Force -ErrorAction SilentlyContinue
    }
}

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
Write-Host "Tip: if an older .exe.replaced.* file remains, it will be removed on next reboot." -ForegroundColor DarkGray
