using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Windows.Input;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    internal static class IntuneMdmHelper
    {
        public static List<IntunePropertyRow> LoadPropertyRows(SCCMAgent agent, string script, TimeSpan? timeout = null)
        {
            var rows = new List<IntunePropertyRow>();
            if (agent == null || !agent.isConnected)
            {
                rows.Add(new IntunePropertyRow("Connection", "Status", "Not connected"));
                return rows;
            }

            try
            {
                List<PSObject> results = timeout.HasValue
                    ? agent.Client.GetObjectsFromPS(script, false, timeout.Value)
                    : agent.Client.GetObjectsFromPS(script, false);

                if (results == null || results.Count == 0)
                {
                    rows.Add(new IntunePropertyRow("Result", "Status", "No data returned from target"));
                    return rows;
                }

                foreach (PSObject po in results)
                {
                    if (po == null)
                        continue;

                    string section = GetProp(po, "Section");
                    string name = GetProp(po, "Name");
                    string value = GetProp(po, "Value");

                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(value) && string.IsNullOrEmpty(section))
                    {
                        // Fallback: treat whole object as string
                        value = po.ToString();
                        if (string.IsNullOrWhiteSpace(value))
                            continue;
                        name = "Output";
                        section = "Result";
                    }

                    rows.Add(new IntunePropertyRow(section, name, value));
                }
            }
            catch (Exception ex)
            {
                rows.Add(new IntunePropertyRow("Error", "Exception", ex.Message));
            }

            if (rows.Count == 0)
                rows.Add(new IntunePropertyRow("Result", "Status", "No data returned from target"));

            return rows;
        }

        public static List<IntuneLogRow> LoadLogRows(SCCMAgent agent, string script, TimeSpan? timeout = null)
        {
            var rows = new List<IntuneLogRow>();
            if (agent == null || !agent.isConnected)
            {
                rows.Add(new IntuneLogRow { Level = "Info", Message = "Not connected", Provider = "Connection" });
                return rows;
            }

            try
            {
                List<PSObject> results = timeout.HasValue
                    ? agent.Client.GetObjectsFromPS(script, false, timeout.Value)
                    : agent.Client.GetObjectsFromPS(script, false);

                if (results == null || results.Count == 0)
                {
                    rows.Add(new IntuneLogRow { Level = "Info", Message = "No log data returned", Provider = "Result" });
                    return rows;
                }

                foreach (PSObject po in results)
                {
                    if (po == null)
                        continue;
                    rows.Add(new IntuneLogRow
                    {
                        TimeCreated = GetProp(po, "TimeCreated"),
                        Id = GetProp(po, "Id"),
                        Level = GetProp(po, "Level"),
                        Provider = GetProp(po, "Provider"),
                        Message = GetProp(po, "Message")
                    });
                }
            }
            catch (Exception ex)
            {
                rows.Add(new IntuneLogRow { Level = "Error", Message = ex.Message, Provider = "Exception" });
            }

            return rows;
        }

        public static string RunAction(SCCMAgent agent, string script)
        {
            if (agent == null || !agent.isConnected)
                return "Not connected";
            try
            {
                string result = agent.Client.GetStringFromPS(script, false);
                return string.IsNullOrWhiteSpace(result) ? "Completed (no output)." : result.Trim();
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        public static void WithWaitCursor(Action action)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                action();
            }
            finally
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
        }

        private static string GetProp(PSObject po, string name)
        {
            try
            {
                var prop = po.Properties[name];
                if (prop == null || prop.Value == null)
                    return "";
                return Convert.ToString(prop.Value) ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
