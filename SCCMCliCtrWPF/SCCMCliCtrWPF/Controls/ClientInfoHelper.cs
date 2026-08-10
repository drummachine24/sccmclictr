using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    /// <summary>
    /// Snapshot of common client identity / status fields from a connected agent.
    /// </summary>
    internal class ClientInfoSnapshot
    {
        public string OSCaption { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string OSBuild { get; set; } = "";
        public string IPAddress { get; set; } = "";
        public string SiteCode { get; set; } = "";
        public string BoundaryGroups { get; set; } = "";
        public string PrimaryUser { get; set; } = "";
        public string CurrentUser { get; set; } = "";
        public string LastCheckedIn { get; set; } = "";

        public string OSVersionBuild
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OSVersion) && string.IsNullOrWhiteSpace(OSBuild))
                    return "";
                if (string.IsNullOrWhiteSpace(OSBuild))
                    return OSVersion;
                if (string.IsNullOrWhiteSpace(OSVersion))
                    return "Build " + OSBuild;
                if (OSVersion.EndsWith("." + OSBuild, StringComparison.Ordinal) || OSVersion.Contains("." + OSBuild + "."))
                    return OSVersion;
                return OSVersion + " (Build " + OSBuild + ")";
            }
        }

        public string SummaryLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(OSCaption))
                    parts.Add(OSCaption.Trim());
                if (!string.IsNullOrWhiteSpace(OSVersionBuild))
                    parts.Add(OSVersionBuild);
                if (!string.IsNullOrWhiteSpace(IPAddress))
                    parts.Add(IPAddress);
                if (!string.IsNullOrWhiteSpace(SiteCode))
                    parts.Add("Site " + SiteCode);
                if (!string.IsNullOrWhiteSpace(BoundaryGroups))
                    parts.Add("BG: " + BoundaryGroups);
                if (!string.IsNullOrWhiteSpace(PrimaryUser))
                    parts.Add("Primary: " + PrimaryUser);
                else if (!string.IsNullOrWhiteSpace(CurrentUser))
                    parts.Add("User: " + CurrentUser);
                if (!string.IsNullOrWhiteSpace(LastCheckedIn))
                    parts.Add("Last DDR: " + LastCheckedIn);
                return string.Join("  |  ", parts);
            }
        }
    }

    internal static class ClientInfoHelper
    {
        const string Script = @"
$ErrorActionPreference = 'SilentlyContinue'
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$ips = @(
    Get-CimInstance Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=True' |
    ForEach-Object { $_.IPAddress } |
    Where-Object { $_ -match '^\d{1,3}(\.\d{1,3}){3}$' -and $_ -ne '0.0.0.0' } |
    Select-Object -Unique
)
$ddr = Get-CimInstance -Namespace 'ROOT\CCM\Scheduler' -Query ""SELECT LastTriggerTime FROM CCM_Scheduler_History WHERE ScheduleID='{00000000-0000-0000-0000-000000000003}' AND UserSID='Machine'""
$affinity = @(Get-CimInstance -Namespace 'ROOT\ccm\Policy\Machine\ActualConfig' -ClassName CCM_UserAffinity | Where-Object {
    $_.IsUserAffinitySet -eq $true -or $_.IsUserAffinitySet -eq 1 -or [string]$_.IsUserAffinitySet -eq 'True'
})
$primary = @(
    foreach ($a in $affinity) {
        if ($a.ConsoleUser) { $a.ConsoleUser }
        elseif ($a.PSObject.Properties['ConsoleUser']) { [string]$a.ConsoleUser }
    }
) | Where-Object { $_ } | Select-Object -Unique
$bg = @(Get-CimInstance -Namespace 'ROOT\ccm\LocationServices' -ClassName BoundaryGroupCache)
$bgIds = @(
    foreach ($b in $bg) {
        if ($null -ne $b.BoundaryGroupIDs) { @($b.BoundaryGroupIDs) }
    }
) | Where-Object { $_ } | Select-Object -Unique
[pscustomobject]@{
    OSCaption = [string]$os.Caption
    OSVersion = [string]$os.Version
    OSBuild = [string]$os.BuildNumber
    IPAddress = ($ips -join ', ')
    CurrentUser = [string]$cs.UserName
    PrimaryUser = ($primary -join ', ')
    LastDDR = $(if ($ddr -and $ddr.LastTriggerTime) { [string]$ddr.LastTriggerTime } else { '' })
    BoundaryGroupIDs = ($bgIds -join ', ')
}
";

        public static ClientInfoSnapshot Load(SCCMAgent agent)
        {
            var info = new ClientInfoSnapshot();
            if (agent == null || !agent.isConnected)
                return info;

            try
            {
                info.SiteCode = Safe(() => agent.Client.AgentProperties.AssignedSite) ?? "";
            }
            catch { }

            try
            {
                List<PSObject> results = agent.Client.GetObjectsFromPS(Script, true);
                PSObject po = results != null ? results.FirstOrDefault() : null;
                if (po != null)
                {
                    info.OSCaption = Prop(po, "OSCaption");
                    info.OSVersion = Prop(po, "OSVersion");
                    info.OSBuild = Prop(po, "OSBuild");
                    info.IPAddress = Prop(po, "IPAddress");
                    info.CurrentUser = Prop(po, "CurrentUser");
                    info.PrimaryUser = Prop(po, "PrimaryUser");
                    info.BoundaryGroups = Prop(po, "BoundaryGroupIDs");
                    info.LastCheckedIn = FormatTimestamp(Prop(po, "LastDDR"));
                }
            }
            catch
            {
                // Fall back to typed APIs for a few fields if the bulk script fails.
                try { info.OSVersion = agent.Client.Inventory.OSVersion ?? ""; } catch { }
                try
                {
                    var ids = agent.Client.LocationServices.BoundaryGroupCacheList
                        .Where(b => b.BoundaryGroupIDs != null)
                        .SelectMany(b => b.BoundaryGroupIDs)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct()
                        .ToList();
                    info.BoundaryGroups = string.Join(", ", ids);
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(info.BoundaryGroups))
            {
                try
                {
                    var ids = agent.Client.LocationServices.BoundaryGroupCacheList
                        .Where(b => b.BoundaryGroupIDs != null)
                        .SelectMany(b => b.BoundaryGroupIDs)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct()
                        .ToList();
                    info.BoundaryGroups = string.Join(", ", ids);
                }
                catch { }
            }

            return info;
        }

        static string Prop(PSObject o, string name)
        {
            try
            {
                if (o == null || o.Properties[name] == null || o.Properties[name].Value == null)
                    return "";
                return o.Properties[name].Value.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }

        static string Safe(Func<string> f)
        {
            try { return f(); }
            catch { return ""; }
        }

        static string FormatTimestamp(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            DateTime dt;
            if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt) ||
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            {
                return dt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            }

            // CIM DMTF: yyyyMMddHHmmss.ffffff+UUU
            if (raw.Length >= 14 && raw.Take(14).All(char.IsDigit))
            {
                try
                {
                    dt = new DateTime(
                        int.Parse(raw.Substring(0, 4)),
                        int.Parse(raw.Substring(4, 2)),
                        int.Parse(raw.Substring(6, 2)),
                        int.Parse(raw.Substring(8, 2)),
                        int.Parse(raw.Substring(10, 2)),
                        int.Parse(raw.Substring(12, 2)),
                        DateTimeKind.Utc);
                    return dt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
                }
                catch { }
            }

            return raw;
        }
    }
}
