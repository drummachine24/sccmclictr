using System;
using System.Windows;
using System.Windows.Controls;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    public partial class IntuneMdmLogsGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;

        public IntuneMdmLogsGrid()
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
                var rows = IntuneMdmHelper.LoadLogRows(oAgent, IntuneMdmScripts.MdmLogs, new TimeSpan(0, 0, 60));
                dataGrid1.ItemsSource = rows;
                tbStatus.Text = rows.Count + " entries";
                if (Listener != null)
                    Listener.WriteLine("# Intune MDM logs probe");
            });
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
