#Requires -Version 5.1
<#
.SYNOPSIS
    Registers or removes the Client Center right-click action in the ConfigMgr / MECM console.

.DESCRIPTION
    Writes sccmclictr.xml into each device-node Actions GUID under the Admin Console
    XmlStorage\Extensions folder. Looks up the console from 32-bit and 64-bit
    ConfigMgr10 Setup registry keys, then well-known install paths.

    Does not fail the caller when no console is present (exit 0 after a warning).
#>
[CmdletBinding()]
param(
    [string]$ExePath,
    [switch]$Unregister
)

$ErrorActionPreference = "Stop"

$actionGuids = @(
    "2b646eff-442b-410e-adf3-d4ec699e0ab4",
    "3dde85c4-ce0e-4999-ab84-698a569dfcac",
    "3fd01cd1-9e01-461e-92cd-94866b8d1f39",
    "9b73a906-6908-4316-b61e-cbab300c9791",
    "64db983c-10bc-4b47-8f2d-cfff48f34faf",
    "ed9dee86-eadd-4ac8-82a1-7234a4646e62",
    "f7cc4bbb-e70e-43e1-978c-1c263d946fff",
    "fb04b7a5-bc4c-4468-8eb8-937d8eb90efb"
)

if (-not $ExePath) {
    $ExePath = Join-Path $PSScriptRoot "SCCMCliCtrWPF.exe"
}

function Get-AdminConsoleRoots {
    $candidates = New-Object System.Collections.Generic.List[string]

    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
        try {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            $key = $base.OpenSubKey("SOFTWARE\Microsoft\ConfigMgr10\Setup")
            if ($key) {
                $ui = [string]$key.GetValue("UI Installation Directory")
                if (-not [string]::IsNullOrWhiteSpace($ui)) {
                    $candidates.Add($ui.TrimEnd("\"))
                }
            }
        } catch { }
    }

    $pf86 = ${env:ProgramFiles(x86)}
    $pf = $env:ProgramFiles
    foreach ($p in @(
            $(if ($pf86) { Join-Path $pf86 "Microsoft Endpoint Manager\AdminConsole" }),
            $(if ($pf86) { Join-Path $pf86 "Microsoft Configuration Manager\AdminConsole" }),
            $(if ($pf) { Join-Path $pf "Microsoft Endpoint Manager\AdminConsole" }),
            $(if ($pf) { Join-Path $pf "Microsoft Configuration Manager\AdminConsole" })
        )) {
        if ($p) { $candidates.Add($p.TrimEnd("\")) }
    }

    $seen = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $candidates) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        if (-not $seen.Add($root)) { continue }
        if (Test-Path -LiteralPath $root) { $root }
    }
}

function Get-ActionXml {
    param([string]$ClientCenterExe)
    $safe = [System.Security.SecurityElement]::Escape($ClientCenterExe)
    @"
<ActionDescription Class="Executable" SelectionMode="Both" DisplayName="Client Center" MnemonicDisplayName="Client Center..." Description="Open Client Center...">
	<ShowOn>
		<string>ContextMenu</string>
	</ShowOn>
	<Executable>
		<FilePath>$safe</FilePath>
		<Parameters>##SUB:Name##</Parameters>
	</Executable>
</ActionDescription>
"@
}

$roots = @(Get-AdminConsoleRoots)
if ($roots.Count -eq 0) {
    Write-Host "ConfigMgr / MECM console not found; skipping console extension."
    exit 0
}

if (-not $Unregister) {
    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "Client Center executable not found: $ExePath"
    }
    $ExePath = [System.IO.Path]::GetFullPath($ExePath)
}

$written = 0
$removed = 0
foreach ($root in $roots) {
    foreach ($guid in $actionGuids) {
        $actionDir = Join-Path $root "XmlStorage\Extensions\Actions\$guid"
        $xmlPath = Join-Path $actionDir "sccmclictr.xml"
        if ($Unregister) {
            if (Test-Path -LiteralPath $xmlPath) {
                Remove-Item -LiteralPath $xmlPath -Force -ErrorAction SilentlyContinue
                $removed++
                Write-Host "Removed $xmlPath"
            }
        } else {
            New-Item -ItemType Directory -Path $actionDir -Force | Out-Null
            # UTF-8 without BOM; ConfigMgr console XML readers are picky about encoding.
            $utf8 = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($xmlPath, (Get-ActionXml -ClientCenterExe $ExePath), $utf8)
            $written++
            Write-Host "Wrote $xmlPath"
        }
    }
}

if ($Unregister) {
    Write-Host "Console extension removed from $($roots.Count) console install(s) ($removed file(s))."
} else {
    Write-Host "Console extension registered in $($roots.Count) console install(s) ($written file(s))."
    Write-Host "Restart the ConfigMgr console if it is already open."
}
