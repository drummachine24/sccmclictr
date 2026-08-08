using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    public partial class IntuneIMEGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;

        public IntuneIMEGrid()
        {
            InitializeComponent();
        }

        public SCCMAgent SCCMAgentConnection
        {
            get { return oAgent; }
            set
            {
                if (value != null && value.isConnected)
                {
                    oAgent = value;
                    LoadData();
                }
            }
        }

        private void LoadData()
        {
            IntuneMdmHelper.WithWaitCursor(() =>
            {
                var rows = IntuneMdmHelper.LoadPropertyRows(oAgent, IntuneMdmScripts.IME, new TimeSpan(0, 0, 45));
                var logTail = rows.FirstOrDefault(r =>
                    string.Equals(r.Section, "LogTail", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Name, "Content", StringComparison.OrdinalIgnoreCase));

                var display = rows.Where(r =>
                    !(string.Equals(r.Section, "LogTail", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(r.Name, "Content", StringComparison.OrdinalIgnoreCase))).ToList();

                dataGrid1.ItemsSource = display;
                tbLogTail.Text = logTail != null ? logTail.Value : "";
                tbStatus.Text = display.Count + " properties";
                if (Listener != null)
                    Listener.WriteLine("# Intune Management Extension probe");
            });
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
