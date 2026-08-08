using System;

namespace ClientCenter.Controls
{
    /// <summary>Row used by Intune/MDM diagnostic grids.</summary>
    public class IntunePropertyRow
    {
        public string Section { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public IntunePropertyRow() { }

        public IntunePropertyRow(string section, string name, string value)
        {
            Section = section ?? "";
            Name = name ?? "";
            Value = value ?? "";
        }
    }

    /// <summary>Row used by MDM event log grid.</summary>
    public class IntuneLogRow
    {
        public string TimeCreated { get; set; }
        public string Id { get; set; }
        public string Level { get; set; }
        public string Provider { get; set; }
        public string Message { get; set; }
    }
}
