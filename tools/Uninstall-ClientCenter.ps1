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
$exeName = "SCCMCliCtrWPF.exe"

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
            }
    } catch { }

    Get-Process -Name "SCCMCliCtrWPF" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping Client Center (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    try {
        $null = & taskkill.exe /F /IM $exeName /T 2>&1
    } finally {
        $ErrorActionPreference = $prevEap
    }
    Start-Sleep -Seconds 1
}

function Register-DeleteOnReboot {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    [void][ClientCenterNative]::MoveFileEx($Path, $null, [ClientCenterNative]::MOVEFILE_DELAY_UNTIL_REBOOT)
}

$installedExe = Join-Path $InstallDir $exeName
Stop-ClientCenterProcesses -TargetExePath $installedExe

$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
$desktop = [Environment]::GetFolderPath("Desktop")

if (Test-Path $shortcutDir) { Remove-Item $shortcutDir -Recurse -Force -ErrorAction SilentlyContinue }
foreach ($desktopName in @(
        "Client Center for Configuration Manager.lnk",
        "Client Center.lnk"
    )) {
    $desktopShortcut = Join-Path $desktop $desktopName
    if (Test-Path $desktopShortcut) { Remove-Item $desktopShortcut -Force -ErrorAction SilentlyContinue }
}

if (Test-Path $InstallDir) {
    Write-Host "Removing installation files..." -ForegroundColor Cyan
    # Delete files individually so a single locked binary does not block the rest.
    Get-ChildItem -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Force -Recurse -ErrorAction Stop
            } catch {
                try {
                    if (-not $_.PSIsContainer) {
                        $pending = "$($_.FullName).pendingdelete"
                        Move-Item -LiteralPath $_.FullName -Destination $pending -Force -ErrorAction Stop
                        Register-DeleteOnReboot -Path $pending
                        Write-Host "Queued for delete on reboot: $pending" -ForegroundColor Yellow
                    } else {
                        Register-DeleteOnReboot -Path $_.FullName
                    }
                } catch {
                    Register-DeleteOnReboot -Path $_.FullName
                    Write-Host "Queued for delete on reboot: $($_.FullName)" -ForegroundColor Yellow
                }
            }
        }

    if (Test-Path -LiteralPath $InstallDir) {
        try {
            Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction Stop
        } catch {
            Register-DeleteOnReboot -Path $InstallDir
            Write-Host "Install folder queued for delete on reboot." -ForegroundColor Yellow
        }
    }
}

$uninstallRegPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClientCenterForConfigMgr"
if (Test-Path $uninstallRegPath) { Remove-Item $uninstallRegPath -Recurse -Force }

Write-Host "Client Center removed." -ForegroundColor Green
