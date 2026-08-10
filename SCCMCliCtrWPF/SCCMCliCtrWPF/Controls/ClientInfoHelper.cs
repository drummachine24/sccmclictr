using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    internal class ClientCollectionRow
    {
        public string Name { get; set; } = "";
        public string CollectionID { get; set; } = "";
        public string Comment { get; set; } = "";
    }

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
        public string ManagementPoint { get; set; } = "";
        public string SiteServer { get; set; } = "";
        public string BoundaryGroups { get; set; } = "";
        public string BoundaryGroupIds { get; set; } = "";
        public string PrimaryUser { get; set; } = "";
        public string CurrentUser { get; set; } = "";
        public string LastCheckedIn { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public List<ClientCollectionRow> Collections { get; set; } = new List<ClientCollectionRow>();
        public string SiteLookupNote { get; set; } = "";

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
                if (Collections != null && Collections.Count > 0)
                    parts.Add("Collections: " + Collections.Count);
                return string.Join("  |  ", parts);
            }
        }
    }

    internal static class ClientInfoHelper
    {
        const string ClientScript = @"
$ErrorActionPreference = 'SilentlyContinue'
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$auth = Get-CimInstance -Namespace root\ccm -ClassName SMS_Authority
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
    }
) | Where-Object { $_ } | Select-Object -Unique
$bg = @(Get-CimInstance -Namespace 'ROOT\ccm\LocationServices' -ClassName BoundaryGroupCache)
$bgIds = @(
    foreach ($b in $bg) {
        if ($null -ne $b.BoundaryGroupIDs) { @($b.BoundaryGroupIDs) }
    }
) | Where-Object { $_ } | Select-Object -Unique
$siteCode = ''
if ($auth -and $auth.Name -and $auth.Name -match ':') {
    $siteCode = ($auth.Name -split ':')[-1]
}
$siteServer = ''
try {
    $ident = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\SMS\Identification' -ErrorAction SilentlyContinue
    if ($ident -and $ident.'Site Server Name') { $siteServer = [string]$ident.'Site Server Name' }
    elseif ($ident -and $ident.SiteServer) { $siteServer = [string]$ident.SiteServer }
} catch {}
[pscustomobject]@{
    OSCaption = [string]$os.Caption
    OSVersion = [string]$os.Version
    OSBuild = [string]$os.BuildNumber
    IPAddress = ($ips -join ', ')
    CurrentUser = [string]$cs.UserName
    ComputerName = [string]$cs.Name
    PrimaryUser = ($primary -join ', ')
    LastDDR = $(if ($ddr -and $ddr.LastTriggerTime) { [string]$ddr.LastTriggerTime } else { '' })
    BoundaryGroupIDs = ($bgIds -join ', ')
    SiteCode = [string]$siteCode
    ManagementPoint = $(if ($auth) { [string]$auth.CurrentManagementPoint } else { '' })
    SiteServer = [string]$siteServer
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
                info.ComputerName = agent.TargetHostname ?? "";
                if (info.ComputerName.Contains("."))
                    info.ComputerName = info.ComputerName.Split('.')[0];
            }
            catch { }

            try
            {
                List<PSObject> results = agent.Client.GetObjectsFromPS(ClientScript, true);
                PSObject po = results != null ? results.FirstOrDefault() : null;
                if (po != null)
                {
                    info.OSCaption = Prop(po, "OSCaption");
                    info.OSVersion = Prop(po, "OSVersion");
                    info.OSBuild = Prop(po, "OSBuild");
                    info.IPAddress = Prop(po, "IPAddress");
                    info.CurrentUser = Prop(po, "CurrentUser");
                    info.PrimaryUser = Prop(po, "PrimaryUser");
                    info.BoundaryGroupIds = Prop(po, "BoundaryGroupIDs");
                    info.BoundaryGroups = info.BoundaryGroupIds;
                    info.LastCheckedIn = FormatTimestamp(Prop(po, "LastDDR"));
                    string siteFromAuth = Prop(po, "SiteCode");
                    if (!string.IsNullOrWhiteSpace(siteFromAuth))
                        info.SiteCode = siteFromAuth;
                    info.ManagementPoint = Prop(po, "ManagementPoint");
                    info.SiteServer = Prop(po, "SiteServer");
                    string cn = Prop(po, "ComputerName");
                    if (!string.IsNullOrWhiteSpace(cn))
                        info.ComputerName = cn;
                }
            }
            catch
            {
                try { info.OSVersion = agent.Client.Inventory.OSVersion ?? ""; } catch { }
                try
                {
                    info.BoundaryGroupIds = string.Join(", ",
                        agent.Client.LocationServices.BoundaryGroupCacheList
                            .Where(b => b.BoundaryGroupIDs != null)
                            .SelectMany(b => b.BoundaryGroupIDs)
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct());
                    info.BoundaryGroups = info.BoundaryGroupIds;
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(info.BoundaryGroupIds))
            {
                try
                {
                    info.BoundaryGroupIds = string.Join(", ",
                        agent.Client.LocationServices.BoundaryGroupCacheList
                            .Where(b => b.BoundaryGroupIDs != null)
                            .SelectMany(b => b.BoundaryGroupIDs)
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct());
                    info.BoundaryGroups = info.BoundaryGroupIds;
                }
                catch { }
            }

            EnrichFromSite(agent, info);
            return info;
        }

        /// <summary>
        /// Resolve boundary group names and collection membership from the SMS Provider
        /// (usually reachable via the client's management point / site server) using the
        /// same credentials as the Client Center connection — from the local process to
        /// avoid WinRM double-hop.
        /// </summary>
        static void EnrichFromSite(SCCMAgent agent, ClientInfoSnapshot info)
        {
            if (string.IsNullOrWhiteSpace(info.SiteCode))
                return;

            var hosts = new List<string>();
            foreach (string h in new[] { info.SiteServer, info.ManagementPoint, TryGetManagementPoint(agent) })
            {
                if (string.IsNullOrWhiteSpace(h))
                    continue;
                string trimmed = h.Trim();
                if (!hosts.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
                    hosts.Add(trimmed);
            }
            if (hosts.Count == 0)
            {
                info.SiteLookupNote = "No site server / management point available to resolve boundary names / collections.";
                return;
            }

            PSCredential cred = null;
            try
            {
                if (agent.ConnectionInfo != null)
                    cred = agent.ConnectionInfo.Credential;
            }
            catch { }

            string bgIdsCsv = (info.BoundaryGroupIds ?? "").Replace("'", "''");
            string computer = (info.ComputerName ?? "").Replace("'", "''");
            string siteCode = info.SiteCode.Replace("'", "''");

            var failures = new List<string>();
            foreach (string siteHost in hosts)
            {
                string siteHostEsc = siteHost.Replace("'", "''");
                string script = BuildSiteScript(siteHostEsc, siteCode, computer, bgIdsCsv);
                if (TryEnrichFromHost(info, script, cred, siteHost, failures))
                    return;
            }

            if (failures.Count > 0)
                info.SiteLookupNote = string.Join(" | ", failures);
        }

        static bool TryEnrichFromHost(ClientInfoSnapshot info, string script, PSCredential cred, string siteHost, List<string> failures)
        {
            try
            {
                using (var runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();
                    if (cred != null)
                        runspace.SessionStateProxy.SetVariable("cred", cred);
                    else
                        runspace.SessionStateProxy.SetVariable("cred", null);

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;
                        ps.AddScript(script);

                        Collection<PSObject> results = ps.Invoke();
                        if (ps.HadErrors && (results == null || results.Count == 0))
                        {
                            var errs = ps.Streams.Error.Select(e => e.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).Take(1);
                            failures.Add(siteHost + ": " + string.Join("; ", errs));
                            return false;
                        }

                        PSObject po = results != null ? results.FirstOrDefault() : null;
                        if (po == null)
                        {
                            failures.Add(siteHost + ": no data returned");
                            return false;
                        }

                        string note = Prop(po, "Note");
                        // Treat total failure of both lookups as retry-next-host
                        string bgNames = Prop(po, "BoundaryGroupNames");
                        object collObj = null;
                        try { collObj = po.Properties["Collections"] != null ? po.Properties["Collections"].Value : null; } catch { }

                        bool hasCollections = false;
                        var rows = new List<ClientCollectionRow>();
                        if (collObj is System.Collections.IEnumerable enumerable && !(collObj is string))
                        {
                            foreach (object item in enumerable)
                            {
                                var cpo = item as PSObject;
                                if (cpo == null && item != null)
                                    cpo = PSObject.AsPSObject(item);
                                if (cpo == null)
                                    continue;
                                string name = Prop(cpo, "Name");
                                string id = Prop(cpo, "CollectionID");
                                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(id))
                                    continue;
                                hasCollections = true;
                                rows.Add(new ClientCollectionRow
                                {
                                    Name = name,
                                    CollectionID = id,
                                    Comment = Prop(cpo, "Comment")
                                });
                            }
                        }

                        bool resolvedNames = !string.IsNullOrWhiteSpace(bgNames) &&
                            (string.IsNullOrWhiteSpace(info.BoundaryGroupIds) ||
                             !string.Equals(bgNames, info.BoundaryGroupIds, StringComparison.OrdinalIgnoreCase) ||
                             bgNames.IndexOf(',') >= 0 ||
                             !bgNames.All(char.IsDigit));

                        // If both lookups failed hard, try next host
                        if (!string.IsNullOrWhiteSpace(note) && note.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 && !hasCollections && !resolvedNames)
                        {
                            failures.Add(siteHost + ": " + note);
                            return false;
                        }

                        if (!string.IsNullOrWhiteSpace(bgNames))
                            info.BoundaryGroups = bgNames;
                        else if (!string.IsNullOrWhiteSpace(info.BoundaryGroupIds))
                            info.SiteLookupNote = "Boundary group names unavailable; showing IDs.";

                        info.Collections = rows
                            .GroupBy(r => r.CollectionID + "|" + r.Name, StringComparer.OrdinalIgnoreCase)
                            .Select(g => g.First())
                            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                            .ToList();

                        if (!string.IsNullOrWhiteSpace(note))
                            info.SiteLookupNote = note;
                        else if (info.Collections.Count == 0)
                            info.SiteLookupNote = "Queried " + siteHost + " for collections (none returned).";

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add(siteHost + ": " + ex.Message);
                return false;
            }
        }

        static string BuildSiteScript(string siteHost, string siteCode, string computer, string bgIdsCsv)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$siteHost = '" + siteHost + "'");
            sb.AppendLine("$siteCode = '" + siteCode + "'");
            sb.AppendLine("$computer = '" + computer + "'");
            sb.AppendLine("$bgIdsCsv = '" + bgIdsCsv + "'");
            sb.AppendLine(@"
$cimParams = @{ ComputerName = $siteHost; Namespace = ""root\SMS\site_$siteCode"" }
if ($cred) { $cimParams['Credential'] = $cred }

$note = ''
$bgNames = @()
try {
    $ids = @($bgIdsCsv -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($ids.Count -gt 0) {
        $groups = @(Get-CimInstance @cimParams -ClassName SMS_BoundaryGroup -ErrorAction Stop)
        foreach ($id in $ids) {
            $match = $groups | Where-Object { [string]$_.GroupID -eq $id } | Select-Object -First 1
            if ($match -and $match.Name) { $bgNames += [string]$match.Name }
            else { $bgNames += $id }
        }
    }
} catch {
    $note = ""Boundary group lookup failed: $($_.Exception.Message)""
    $bgNames = @($bgIdsCsv -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

$collections = @()
try {
    $names = @($computer)
    if ($computer -match '\.') { $names += ($computer -split '\.')[0] }
    $names = $names | Select-Object -Unique
    foreach ($n in $names) {
        $query = ""SELECT SMS_Collection.Name, SMS_Collection.CollectionID, SMS_Collection.Comment FROM SMS_FullCollectionMembership, SMS_Collection WHERE SMS_FullCollectionMembership.Name = '$n' AND SMS_FullCollectionMembership.CollectionID = SMS_Collection.CollectionID""
        $found = @(Get-CimInstance @cimParams -Query $query -ErrorAction Stop)
        foreach ($c in $found) {
            $collections += [pscustomobject]@{
                Name = [string]$c.Name
                CollectionID = [string]$c.CollectionID
                Comment = [string]$c.Comment
            }
        }
        if ($collections.Count -gt 0) { break }
    }
    $collections = @($collections | Sort-Object -Property Name)
} catch {
    if (-not $note) { $note = ""Collection lookup failed: $($_.Exception.Message)"" }
    else { $note = ""$note | Collection lookup failed: $($_.Exception.Message)"" }
}

[pscustomobject]@{
    BoundaryGroupNames = ($bgNames -join ', ')
    Collections = @($collections)
    Note = $note
}
");
            return sb.ToString();
        }

        static string TryGetManagementPoint(SCCMAgent agent)
        {
            try
            {
                return agent.Client.AgentProperties.ManagementPoint ?? "";
            }
            catch
            {
                return "";
            }
        }

        static string FirstNonEmpty(params string[] values)
        {
            foreach (string v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return "";
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
