#Requires -Version 5.1
<#
.SYNOPSIS
    Builds Client Center release artifacts: portable ZIP, MSI, and Setup.exe.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.1.15",
    [switch]$SkipZip,
    [switch]$SkipMsi,
    [switch]$SkipExe,
    [switch]$SkipInstallers
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "SCCMCliCtrWPF\SCCMCliCtrWPF\SCCMCliCtr.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "ClientCenter-v$Version-$Runtime"
$zipPath = Join-Path $artifactsRoot "ClientCenter-v$Version-$Runtime-selfcontained.zip"

Push-Location $repoRoot
try {
    Write-Host "Building plugins and dependencies ($Configuration)..." -ForegroundColor Cyan
    dotnet build $project -c $Configuration
    if (-not $?) { throw "Build failed." }

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Write-Host "Publishing self-contained app ($Runtime)..." -ForegroundColor Cyan
    dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true `
        -o $publishDir
    if (-not $?) { throw "Publish failed." }

    $pluginDir = Join-Path $repoRoot "SCCMCliCtrWPF\SCCMCliCtrWPF\bin\$Configuration\net10.0-windows"
    if (Test-Path $pluginDir) {
        Write-Host "Copying plugin payloads..." -ForegroundColor Cyan
        Get-ChildItem $pluginDir -Filter "Plugin_*.dll" | Copy-Item -Destination $publishDir -Force
        if (Test-Path (Join-Path $pluginDir "RuckZuck.Base.dll")) {
            Copy-Item (Join-Path $pluginDir "RuckZuck.Base.dll") $publishDir -Force
        }
        if (Test-Path (Join-Path $pluginDir "Plugin_Explorer.dll.config")) {
            Copy-Item (Join-Path $pluginDir "Plugin_Explorer.dll.config") $publishDir -Force
        }
    }

    $psScriptsSrc = Join-Path $repoRoot "Plugins\Plugin_PSScripts\PSScripts"
    if (Test-Path $psScriptsSrc) {
        $psScriptsDst = Join-Path $publishDir "PSScripts"
        New-Item -ItemType Directory -Force -Path $psScriptsDst | Out-Null
        Copy-Item (Join-Path $psScriptsSrc "*") $psScriptsDst -Recurse -Force
    }

    # Portable ZIP helpers (not used by MSI/EXE installers).
    Copy-Item (Join-Path $PSScriptRoot "Install-ClientCenter.ps1") $publishDir -Force
    Copy-Item (Join-Path $PSScriptRoot "Uninstall-ClientCenter.ps1") $publishDir -Force
    Copy-Item (Join-Path $PSScriptRoot "Install.cmd") $publishDir -Force
    Copy-Item (Join-Path $PSScriptRoot "Uninstall.cmd") $publishDir -Force

    @"
Client Center for Configuration Manager v$Version
=================================================

Self-contained build for $Runtime (includes .NET runtime — no separate .NET install needed).

Preferred installers (from GitHub Releases):
  - ClientCenter-v$Version-win-x64-setup.exe  (wizard)
  - ClientCenter-v$Version-win-x64.msi          (enterprise / silent)

Portable ZIP options:
  Install:   .\Install.cmd  (admin)
  Uninstall: .\Uninstall.cmd
  Or run:    .\SCCMCliCtrWPF.exe

Silent MSI example:
  msiexec /i ClientCenter-v$Version-win-x64.msi /qn

Requirements on THIS machine:
  - Windows 10/11 x64
  - Administrator rights for installers

Requirements on TARGET machines (SCCM troubleshooting):
  - WinRM enabled
  - ConfigMgr client agent installed
  - Appropriate admin rights on the target

Fork: https://github.com/drummachine24/sccmclictr
Original: https://github.com/rzander/sccmclictr
Copyright (C) 2023 by Roger Zander
Modernization (2026): Josh (drummachine24)
"@ | Set-Content -Path (Join-Path $publishDir "README.txt") -Encoding UTF8

    $sizeMb = [math]::Round((Get-ChildItem $publishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
    $pluginCount = (Get-ChildItem $publishDir -Filter "Plugin_*.dll").Count
    Write-Host "Publish complete: $publishDir" -ForegroundColor Green
    Write-Host "  Size: $sizeMb MB" -ForegroundColor Green
    Write-Host "  Plugins: $pluginCount" -ForegroundColor Green

    if (-not $SkipZip) {
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Write-Host "Creating portable zip: $zipPath" -ForegroundColor Cyan
        Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
        Write-Host "Zip created." -ForegroundColor Green
    }

    if (-not $SkipInstallers) {
        & (Join-Path $PSScriptRoot "Build-Installers.ps1") `
            -PublishDir $publishDir `
            -Version $Version `
            -OutputDir $artifactsRoot `
            -SkipMsi:$SkipMsi `
            -SkipExe:$SkipExe
        if (-not $?) { throw "Installer build failed." }
    }

    Write-Host "Release artifacts in: $artifactsRoot" -ForegroundColor Green
    Get-ChildItem $artifactsRoot -File | Sort-Object Name | ForEach-Object {
        Write-Host ("  {0} ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB))
    }
}
finally {
    Pop-Location
}
