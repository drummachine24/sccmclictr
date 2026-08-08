#Requires -Version 5.1
<#
.SYNOPSIS
    Builds MSI (WiX) and Setup.exe (Inno Setup) installers from a published payload folder.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputDir = "",

    [switch]$SkipMsi,
    [switch]$SkipExe
)

$ErrorActionPreference = "Stop"
$toolsDir = $PSScriptRoot
$installerDir = Join-Path $toolsDir "installer"
$repoRoot = Split-Path -Parent $toolsDir

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "artifacts"
}

if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}
if (-not (Test-Path -LiteralPath (Join-Path $PublishDir "SCCMCliCtrWPF.exe"))) {
    throw "SCCMCliCtrWPF.exe not found in publish directory: $PublishDir"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# MSI ProductVersion requires up to four integer parts.
$msiVersion = $Version
if ($msiVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version '$Version' is not a valid dotted numeric version for MSI/EXE metadata."
}
if ($msiVersion -match '^\d+\.\d+\.\d+$') {
    $msiVersion = "$msiVersion.0"
}

$msiPath = Join-Path $OutputDir "ClientCenter-v$Version-win-x64.msi"
$exePath = Join-Path $OutputDir "ClientCenter-v$Version-win-x64-setup.exe"

function Install-WixCli {
    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if ($wix) { return $wix.Source }

    Write-Host "Installing WiX .NET tool..." -ForegroundColor Cyan
    dotnet tool install --global wix --version 5.0.2
    if (-not $?) {
        dotnet tool update --global wix --version 5.0.2
    }

    $candidate = Join-Path $env:USERPROFILE ".dotnet\tools\wix.exe"
    if (Test-Path $candidate) { return $candidate }
    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if ($wix) { return $wix.Source }
    throw "WiX CLI (wix.exe) is not available after install."
}

function Find-ISCC {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    return $null
}

function Install-InnoSetup {
    $existing = Find-ISCC
    if ($existing) { return $existing }

    Write-Host "Installing Inno Setup 6..." -ForegroundColor Cyan
    $installer = Join-Path $env:TEMP "innosetup-6-install.exe"
    $url = "https://jrsoftware.org/download.php/is.exe"
    Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing
    $args = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-"
    $p = Start-Process -FilePath $installer -ArgumentList $args -Wait -PassThru
    if ($p.ExitCode -ne 0) {
        throw "Inno Setup silent install failed with exit code $($p.ExitCode)."
    }

    $iscc = Find-ISCC
    if (-not $iscc) { throw "ISCC.exe not found after Inno Setup install." }
    return $iscc
}

if (-not $SkipMsi) {
    Write-Host "Building MSI (WiX)..." -ForegroundColor Cyan
    $wixExe = Install-WixCli
    if (Test-Path -LiteralPath $msiPath) { Remove-Item -LiteralPath $msiPath -Force }

    # Ensure the standard UI dialog set is available (Finish / completion page).
    Write-Host "Ensuring WixToolset.UI.wixext is installed..." -ForegroundColor Cyan
    & $wixExe extension add "WixToolset.UI.wixext/5.0.2"
    if (-not $?) {
        # Already present is fine; force-add when add fails for other reasons.
        & $wixExe extension add "WixToolset.UI.wixext/5.0.2" --force
    }

    $wxs = Join-Path $installerDir "ClientCenter.wxs"
    $bindPath = (Resolve-Path -LiteralPath $PublishDir).Path
    Push-Location $installerDir
    try {
        & $wixExe build $wxs `
            -ext WixToolset.UI.wixext `
            -bindpath "PublishDir=$bindPath" `
            -d "Version=$msiVersion" `
            -arch x64 `
            -o $msiPath
    }
    finally {
        Pop-Location
    }

    if (-not $? -or -not (Test-Path -LiteralPath $msiPath)) {
        throw "WiX MSI build failed."
    }
    Write-Host "MSI created: $msiPath" -ForegroundColor Green
}

if (-not $SkipExe) {
    Write-Host "Building Setup.exe (Inno Setup)..." -ForegroundColor Cyan
    $iscc = Install-InnoSetup
    if (Test-Path -LiteralPath $exePath) { Remove-Item -LiteralPath $exePath -Force }

    $iss = Join-Path $installerDir "ClientCenter.iss"
    $publishAbs = (Resolve-Path -LiteralPath $PublishDir).Path
    $outputAbs = (Resolve-Path -LiteralPath $OutputDir).Path

    & $iscc `
        "/DMyAppVersion=$Version" `
        "/DPublishDir=$publishAbs" `
        "/DOutputDir=$outputAbs" `
        $iss

    if (-not $? -or -not (Test-Path -LiteralPath $exePath)) {
        throw "Inno Setup build failed. Expected output: $exePath"
    }
    Write-Host "Setup.exe created: $exePath" -ForegroundColor Green
}

Write-Host "Installer build complete." -ForegroundColor Green
