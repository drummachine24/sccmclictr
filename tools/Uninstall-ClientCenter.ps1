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

function Stop-ClientCenterProcesses {
    param([string]$TargetExePath)

    try {
        $targetFull = [System.IO.Path]::GetFullPath($TargetExePath)
        Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object { $_.ExecutablePath -and ([System.IO.Path]::GetFullPath($_.ExecutablePath) -ieq $targetFull) } |
            ForEach-Object {
                Write-Host "Stopping Client Center process by path (PID $($_.ProcessId))..." -ForegroundColor Yellow
                Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
            }
    } catch { }

    Get-Process -Name "SCCMCliCtrWPF" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping Client Center (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }

    # taskkill writes to stderr when the process is already gone; do not treat that as fatal.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    try {
        $null = & taskkill.exe /F /IM $exeName /T 2>&1
    } finally {
        $ErrorActionPreference = $prevEap
    }
    Start-Sleep -Seconds 1
}

$installedExe = Join-Path $InstallDir $exeName
Stop-ClientCenterProcesses -TargetExePath $installedExe

$startMenu = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $startMenu "Client Center for Configuration Manager"
$desktop = [Environment]::GetFolderPath("Desktop")

if (Test-Path $shortcutDir) { Remove-Item $shortcutDir -Recurse -Force }
$desktopShortcut = Join-Path $desktop "Client Center.lnk"
if (Test-Path $desktopShortcut) { Remove-Item $desktopShortcut -Force }

if (Test-Path $InstallDir) {
    for ($i = 1; $i -le 5; $i++) {
        try {
            Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction Stop
            break
        } catch {
            Write-Host "Retry $i/5: waiting for file locks to release..." -ForegroundColor Yellow
            Stop-ClientCenterProcesses -TargetExePath $installedExe
            Start-Sleep -Seconds (2 * $i)
            if ($i -eq 5) {
                $backup = "{0}.old.{1}" -f $InstallDir, (Get-Date -Format "yyyyMMddHHmmss")
                Write-Host "Folder still locked. Moving aside to:`n  $backup" -ForegroundColor Yellow
                Move-Item -LiteralPath $InstallDir -Destination $backup -Force
            }
        }
    }
}

$uninstallRegPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClientCenterForConfigMgr"
if (Test-Path $uninstallRegPath) { Remove-Item $uninstallRegPath -Recurse -Force }

Write-Host "Client Center removed." -ForegroundColor Green
