namespace ClientCenter.Controls
{
    /// <summary>Remote PowerShell probes for device-local Intune/MDM diagnostics.</summary>
    internal static class IntuneMdmScripts
    {
        public const string Enrollment = @"
$ErrorActionPreference = 'SilentlyContinue'
$rows = New-Object System.Collections.Generic.List[object]
function Add-Row([string]$Section,[string]$Name,[string]$Value) {
  $rows.Add([pscustomobject]@{ Section = $Section; Name = $Name; Value = [string]$Value })
}

$dsreg = & ""$env:SystemRoot\System32\dsregcmd.exe"" /status 2>&1
$section = 'dsregcmd'
foreach ($line in $dsreg) {
  $text = [string]$line
  if ($text -match '^\|\s*(.+?)\s*\|$' -and $text -notmatch '^\|\s*-+') {
    $section = $Matches[1].Trim()
  }
  elseif ($text -match '^\s*(.+?)\s*:\s*(.*)\s*$') {
    Add-Row $section $Matches[1].Trim() $Matches[2].Trim()
  }
}

$enrollRoot = 'HKLM:\SOFTWARE\Microsoft\Enrollments'
$foundEnrollment = $false
Get-ChildItem $enrollRoot -ErrorAction SilentlyContinue | ForEach-Object {
  $guid = $_.PSChildName
  if ($guid -notmatch '^[0-9a-fA-F-]{36}$') { return }
  $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
  if (-not $p) { return }
  $provider = [string]$p.ProviderID
  $upn = [string]$p.UPN
  $discovery = [string]$p.DiscoveryServiceFullURL
  if ([string]::IsNullOrWhiteSpace($provider) -and [string]::IsNullOrWhiteSpace($upn) -and [string]::IsNullOrWhiteSpace($discovery)) { return }
  $foundEnrollment = $true
  $sec = ""Enrollment $guid""
  Add-Row $sec 'ProviderID' $provider
  Add-Row $sec 'UPN' $upn
  Add-Row $sec 'EnrollmentState' ([string]$p.EnrollmentState)
  Add-Row $sec 'EnrollmentType' ([string]$p.EnrollmentType)
  Add-Row $sec 'AADResourceID' ([string]$p.AADResourceID)
  Add-Row $sec 'DiscoveryServiceFullURL' $discovery
  Add-Row $sec 'IsFederated' ([string]$p.IsFederated)
}

if (-not $foundEnrollment) {
  Add-Row 'Enrollment' 'Status' 'No MDM enrollment entries found under HKLM\SOFTWARE\Microsoft\Enrollments'
}

try {
  $ns = Get-CimInstance -Namespace root\cimv2\mdm\dmmap -ClassName MDM_DevDetail_Ext01 -ErrorAction Stop
  foreach ($o in @($ns)) {
    Add-Row 'MDM_DevDetail_Ext01' 'DeviceName' ([string]$o.DeviceName)
    Add-Row 'MDM_DevDetail_Ext01' 'DeviceID' ([string]$o.DeviceID)
  }
} catch {
  Add-Row 'MDM WMI' 'root\cimv2\mdm\dmmap' 'Namespace or class not available'
}

$rows
";

        public const string CoManagement = @"
$ErrorActionPreference = 'SilentlyContinue'
$rows = New-Object System.Collections.Generic.List[object]
function Add-Row([string]$Section,[string]$Name,[string]$Value) {
  $rows.Add([pscustomobject]@{ Section = $Section; Name = $Name; Value = [string]$Value })
}

$workloadNames = @{
  1 = 'Compliance policies'
  2 = 'Resource access policies'
  4 = 'Device Configuration'
  8 = 'Windows Update policies'
  16 = 'Endpoint Protection'
  32 = 'Client apps'
  64 = 'Office Click-to-Run apps'
  128 = 'Computer restart'
}

$found = $false

foreach ($path in @(
  'HKLM:\SOFTWARE\Microsoft\CCM\CoManagement',
  'HKLM:\SOFTWARE\Microsoft\CCM\CoManagementHandler'
)) {
  if (Test-Path $path) {
    $found = $true
    $p = Get-ItemProperty $path -ErrorAction SilentlyContinue
    foreach ($name in $p.PSObject.Properties.Name) {
      if ($name -in @('PSPath','PSParentPath','PSChildName','PSDrive','PSProvider')) { continue }
      Add-Row $path $name ([string]$p.$name)
    }
  }
}

$caps = 'HKLM:\SOFTWARE\Microsoft\DeviceManageabilityCSP'
if (Test-Path $caps) {
  Get-ChildItem $caps -Recurse -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
    $found = $true
    Add-Row 'DeviceManageabilityCSP' $_.Name ([string](Get-ItemPropertyValue -LiteralPath $_.PSPath -Name '(default)' -ErrorAction SilentlyContinue))
  }
  Get-ChildItem $caps -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    $props = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if (-not $props) { return }
    foreach ($name in $props.PSObject.Properties.Name) {
      if ($name -in @('PSPath','PSParentPath','PSChildName','PSDrive','PSProvider')) { continue }
      $found = $true
      Add-Row (""CSP $($_.Name)"") $name ([string]$props.$name)
    }
  }
}

foreach ($nsClass in @(
  @{ Ns = 'root\ccm\ClientSDK'; Class = 'CCM_CoManagementConfiguration' },
  @{ Ns = 'root\ccm\Policy\Machine\ActualConfig'; Class = 'CCM_CoManagementPolicy' },
  @{ Ns = 'root\ccm'; Class = 'CCM_CoManagementConfiguration' }
)) {
  try {
    $objs = Get-CimInstance -Namespace $nsClass.Ns -ClassName $nsClass.Class -ErrorAction Stop
    foreach ($o in @($objs)) {
      $found = $true
      $sec = ""$($nsClass.Ns):$($nsClass.Class)""
      foreach ($prop in $o.CimInstanceProperties) {
        Add-Row $sec $prop.Name ([string]$prop.Value)
      }
      if ($o.PSObject.Properties.Name -contains 'WorkloadFlags' -or $o.PSObject.Properties.Name -contains 'Workloads') {
        $flags = 0
        if ($o.WorkloadFlags) { $flags = [int]$o.WorkloadFlags }
        elseif ($o.Workloads) { $flags = [int]$o.Workloads }
        foreach ($bit in $workloadNames.Keys | Sort-Object) {
          $owner = if (($flags -band $bit) -ne 0) { 'Intune' } else { 'ConfigMgr' }
          Add-Row 'Workload map' $workloadNames[$bit] $owner
        }
      }
    }
  } catch { }
}

if (-not $found) {
  Add-Row 'Co-management' 'Status' 'No co-management configuration detected (device may be ConfigMgr-only or Intune-only)'
}

$rows
";

        public const string IME = @"
$ErrorActionPreference = 'SilentlyContinue'
$rows = New-Object System.Collections.Generic.List[object]
function Add-Row([string]$Section,[string]$Name,[string]$Value) {
  $rows.Add([pscustomobject]@{ Section = $Section; Name = $Name; Value = [string]$Value })
}

$svc = Get-Service -Name 'IntuneManagementExtension' -ErrorAction SilentlyContinue
if ($svc) {
  Add-Row 'Service' 'Name' $svc.Name
  Add-Row 'Service' 'Status' $svc.Status
  Add-Row 'Service' 'StartType' $svc.StartType
} else {
  Add-Row 'Service' 'IntuneManagementExtension' 'Not installed'
}

$imeRoots = @(
  'C:\Program Files (x86)\Microsoft Intune Management Extension',
  'C:\Program Files\Microsoft Intune Management Extension'
)
foreach ($root in $imeRoots) {
  if (Test-Path $root) {
    Add-Row 'Install' 'Path' $root
    $dll = Get-ChildItem $root -Filter 'AgentExecutor.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dll) { $dll = Get-ChildItem $root -Filter 'IntuneManagementExtension.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 }
    if ($dll) {
      $vi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName)
      Add-Row 'Install' 'File' $dll.FullName
      Add-Row 'Install' 'FileVersion' $vi.FileVersion
      Add-Row 'Install' 'ProductVersion' $vi.ProductVersion
    }
  }
}

$logDir = 'C:\ProgramData\Microsoft\IntuneManagementExtension\Logs'
Add-Row 'Logs' 'Directory' $logDir
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir -Filter 'IntuneManagementExtension*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Add-Row 'Logs' 'LatestLog' $latest.FullName
    Add-Row 'Logs' 'LastWriteTime' $latest.LastWriteTime.ToString('o')
    $tail = Get-Content -LiteralPath $latest.FullName -Tail 40 -ErrorAction SilentlyContinue
    if ($tail) {
      Add-Row 'LogTail' 'Content' (($tail | ForEach-Object { $_ }) -join ""`n"")
    }
  } else {
    Add-Row 'Logs' 'LatestLog' 'No IntuneManagementExtension*.log files found'
  }
} else {
  Add-Row 'Logs' 'Directory' 'Not present'
}

$rows
";

        public const string MdmLogs = @"
$ErrorActionPreference = 'SilentlyContinue'
$rows = New-Object System.Collections.Generic.List[object]

$logNames = @(
  'Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin',
  'Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Operational'
)

foreach ($logName in $logNames) {
  try {
    $events = Get-WinEvent -LogName $logName -MaxEvents 50 -ErrorAction Stop
    foreach ($e in $events) {
      $rows.Add([pscustomobject]@{
        TimeCreated = $e.TimeCreated.ToString('o')
        Id = [string]$e.Id
        Level = [string]$e.LevelDisplayName
        Provider = $logName
        Message = ([string]$e.Message)
      })
    }
  } catch {
    $rows.Add([pscustomobject]@{
      TimeCreated = ''
      Id = ''
      Level = 'Info'
      Provider = $logName
      Message = ""Unable to read log: $($_.Exception.Message)""
    })
  }
}

$paths = @(
  'C:\ProgramData\Microsoft\IntuneManagementExtension\Logs',
  ""$env:windir\Logs\MDM""
)
foreach ($p in $paths) {
  if (Test-Path $p) {
    Get-ChildItem $p -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 15 | ForEach-Object {
      $rows.Add([pscustomobject]@{
        TimeCreated = $_.LastWriteTime.ToString('o')
        Id = ''
        Level = 'File'
        Provider = $p
        Message = $_.FullName
      })
    }
  } else {
    $rows.Add([pscustomobject]@{
      TimeCreated = ''
      Id = ''
      Level = 'Info'
      Provider = $p
      Message = 'Path not present'
    })
  }
}

$rows
";

        public const string Policies = @"
$ErrorActionPreference = 'SilentlyContinue'
$rows = New-Object System.Collections.Generic.List[object]
function Add-Row([string]$Section,[string]$Name,[string]$Value) {
  $rows.Add([pscustomobject]@{ Section = $Section; Name = $Name; Value = [string]$Value })
}

$enrollRoot = 'HKLM:\SOFTWARE\Microsoft\Enrollments'
$accounts = @()
Get-ChildItem $enrollRoot -ErrorAction SilentlyContinue | ForEach-Object {
  $guid = $_.PSChildName
  if ($guid -notmatch '^[0-9a-fA-F-]{36}$') { return }
  $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
  if ($p -and (-not [string]::IsNullOrWhiteSpace([string]$p.ProviderID) -or -not [string]::IsNullOrWhiteSpace([string]$p.UPN))) {
    $accounts += $guid
  }
}

if ($accounts.Count -eq 0) {
  Add-Row 'Policies' 'Status' 'No enrolled MDM accounts found'
} else {
  foreach ($guid in $accounts) {
    $base = Join-Path $enrollRoot $guid
    Get-ChildItem $base -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
      $rel = $_.PSPath -replace [regex]::Escape((Resolve-Path $base).Path), ''
      $rel = $rel.TrimStart('\')
      if ($_.PSIsContainer) {
        $props = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
        if (-not $props) { return }
        foreach ($name in $props.PSObject.Properties.Name) {
          if ($name -in @('PSPath','PSParentPath','PSChildName','PSDrive','PSProvider')) { continue }
          $val = [string]$props.$name
          if ($val.Length -gt 500) { $val = $val.Substring(0,500) + '...' }
          Add-Row $guid (""$rel\$name"".TrimStart('\')) $val
        }
      }
    }
    # Also include well-known policy store under Provisioning\OMADM
    $omadm = ""HKLM:\SOFTWARE\Microsoft\Provisioning\OMADM\Accounts\$guid""
    if (Test-Path $omadm) {
      Get-ChildItem $omadm -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $props = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
        if (-not $props) { return }
        foreach ($name in $props.PSObject.Properties.Name) {
          if ($name -in @('PSPath','PSParentPath','PSChildName','PSDrive','PSProvider')) { continue }
          $val = [string]$props.$name
          if ($val.Length -gt 500) { $val = $val.Substring(0,500) + '...' }
          Add-Row ""OMADM $guid"" (""$($_.Name)\$name"") $val
        }
      }
    }
  }
}

if ($rows.Count -eq 0) {
  Add-Row 'Policies' 'Status' 'No policy registry values found under enrollments'
}

$rows
";

        public const string Sync = @"
$ErrorActionPreference = 'SilentlyContinue'
$results = New-Object System.Collections.Generic.List[string]
$guids = @()
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Enrollments' -ErrorAction SilentlyContinue | ForEach-Object {
  $guid = $_.PSChildName
  if ($guid -notmatch '^[0-9a-fA-F-]{36}$') { return }
  $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
  if ($p -and (-not [string]::IsNullOrWhiteSpace([string]$p.ProviderID) -or -not [string]::IsNullOrWhiteSpace([string]$p.UPN))) {
    $guids += $guid
  }
}

if ($guids.Count -eq 0) {
  'No MDM enrollment found; cannot trigger sync.'
  return
}

foreach ($guid in $guids) {
  $path = ""\Microsoft\Windows\EnterpriseMgmt\$guid\""
  foreach ($taskName in @('PushLaunch','Schedule to run','Schedule #1 to run','Schedule #2 to run','Schedule #3 to run')) {
    try {
      $task = Get-ScheduledTask -TaskPath $path -TaskName $taskName -ErrorAction Stop
      Start-ScheduledTask -InputObject $task -ErrorAction Stop
      $results.Add(""Started task: $path$taskName"")
    } catch { }
  }
}

if ($results.Count -eq 0) {
  try {
    $session = New-Object -ComObject 'WbemScripting.SWbemLocator'
    # Fallback: request refresh via deviceenroller if present
    $de = Join-Path $env:SystemRoot 'system32\deviceenroller.exe'
    if (Test-Path $de) {
      Start-Process -FilePath $de -ArgumentList '/c','/Online' -WindowStyle Hidden
      $results.Add('Started deviceenroller.exe /c /Online')
    }
  } catch { }
}

if ($results.Count -eq 0) {
  'Enrollment found but no EnterpriseMgmt scheduled tasks could be started.'
} else {
  $results -join ""`n""
}
";

        public const string CollectDiag = @"
$ErrorActionPreference = 'SilentlyContinue'
$tool = Join-Path $env:SystemRoot 'system32\mdmdiagnosticstool.exe'
if (-not (Test-Path $tool)) {
  'mdmdiagnosticstool.exe not found on target.'
  return
}
$cab = Join-Path $env:TEMP (""MDMDiag-{0}.cab"" -f (Get-Date -Format 'yyyyMMddHHmmss'))
& $tool -area 'Autopilot;DeviceEnrollment;DeviceProvisioning;TPM' -cab $cab | Out-String | Out-Null
if (Test-Path $cab) {
  ""MDM diagnostics cab created: $cab""
} else {
  ""mdmdiagnosticstool.exe ran but cab was not found at $cab""
}
";
    }
}
