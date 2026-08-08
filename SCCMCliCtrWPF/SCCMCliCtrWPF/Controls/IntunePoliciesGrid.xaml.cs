using System;
using System.Windows;
using System.Windows.Controls;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    public partial class IntunePoliciesGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;

        public IntunePoliciesGrid()
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
                var rows = IntuneMdmHelper.LoadPropertyRows(oAgent, IntuneMdmScripts.Policies, new TimeSpan(0, 1, 0));
                dataGrid1.ItemsSource = rows;
                tbStatus.Text = rows.Count + " values (read-only)";
                if (Listener != null)
                    Listener.WriteLine("# Intune MDM policies probe (read-only)");
            });
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
