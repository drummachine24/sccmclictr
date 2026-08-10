using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Management.Automation;
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
        /// using System.Management (avoids PowerShell Core CimCmdlets load failures).
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

            ConnectionOptions options = BuildConnectionOptions(agent);
            var failures = new List<string>();
            foreach (string siteHost in hosts)
            {
                if (TryEnrichFromHostWmi(info, siteHost, options, failures))
                    return;
            }

            if (failures.Count > 0)
                info.SiteLookupNote = string.Join(" | ", failures);
        }

        static ConnectionOptions BuildConnectionOptions(SCCMAgent agent)
        {
            var options = new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
                EnablePrivileges = true
            };

            try
            {
                PSCredential cred = agent.ConnectionInfo != null ? agent.ConnectionInfo.Credential : null;
                if (cred != null)
                {
                    options.Username = cred.UserName;
                    options.SecurePassword = cred.Password;
                }
            }
            catch { }

            return options;
        }

        static bool TryEnrichFromHostWmi(ClientInfoSnapshot info, string siteHost, ConnectionOptions options, List<string> failures)
        {
            try
            {
                string ns = @"\\" + siteHost + @"\root\SMS\site_" + info.SiteCode;
                var scope = new ManagementScope(ns, options);
                scope.Connect();

                var notes = new List<string>();
                ResolveBoundaryGroupNames(scope, info, notes);
                ResolveCollections(scope, info, notes);

                if (notes.Count > 0)
                    info.SiteLookupNote = string.Join(" | ", notes);

                return true;
            }
            catch (Exception ex)
            {
                failures.Add(siteHost + ": " + ex.Message);
                return false;
            }
        }

        static void ResolveBoundaryGroupNames(ManagementScope scope, ClientInfoSnapshot info, List<string> notes)
        {
            var ids = (info.BoundaryGroupIds ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (ids.Count == 0)
                return;

            try
            {
                var nameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT GroupID, Name FROM SMS_BoundaryGroup")))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        using (mo)
                        {
                            string id = mo["GroupID"] != null ? mo["GroupID"].ToString() : "";
                            string name = mo["Name"] != null ? mo["Name"].ToString() : "";
                            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                                nameById[id] = name;
                        }
                    }
                }

                var resolved = new List<string>();
                foreach (string id in ids)
                    resolved.Add(nameById.ContainsKey(id) ? nameById[id] : id);

                info.BoundaryGroups = string.Join(", ", resolved);
            }
            catch (Exception ex)
            {
                notes.Add("Boundary group lookup failed: " + ex.Message);
                info.BoundaryGroups = info.BoundaryGroupIds;
            }
        }

        static void ResolveCollections(ManagementScope scope, ClientInfoSnapshot info, List<string> notes)
        {
            try
            {
                var names = new List<string>();
                if (!string.IsNullOrWhiteSpace(info.ComputerName))
                {
                    names.Add(info.ComputerName.Trim());
                    if (info.ComputerName.Contains("."))
                        names.Add(info.ComputerName.Split('.')[0]);
                }
                names = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var rows = new List<ClientCollectionRow>();
                foreach (string name in names)
                {
                    string safe = EscapeWql(name);
                    string query =
                        "SELECT SMS_Collection.Name, SMS_Collection.CollectionID, SMS_Collection.Comment " +
                        "FROM SMS_FullCollectionMembership, SMS_Collection " +
                        "WHERE SMS_FullCollectionMembership.Name = '" + safe + "' " +
                        "AND SMS_FullCollectionMembership.CollectionID = SMS_Collection.CollectionID";

                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementObject mo in results)
                        {
                            using (mo)
                            {
                                string cName = mo["Name"] != null ? mo["Name"].ToString() : "";
                                string cId = mo["CollectionID"] != null ? mo["CollectionID"].ToString() : "";
                                string comment = mo["Comment"] != null ? mo["Comment"].ToString() : "";
                                if (string.IsNullOrWhiteSpace(cName) && string.IsNullOrWhiteSpace(cId))
                                    continue;
                                rows.Add(new ClientCollectionRow
                                {
                                    Name = cName ?? "",
                                    CollectionID = cId ?? "",
                                    Comment = comment ?? ""
                                });
                            }
                        }
                    }

                    if (rows.Count > 0)
                        break;
                }

                info.Collections = rows
                    .GroupBy(r => r.CollectionID + "|" + r.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                notes.Add("Collection lookup failed: " + ex.Message);
            }
        }

        static string EscapeWql(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
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
