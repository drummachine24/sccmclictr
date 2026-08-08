using System;
using System.Windows;
using System.Windows.Controls;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    public partial class IntuneCoMgmtGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;

        public IntuneCoMgmtGrid()
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
                var rows = IntuneMdmHelper.LoadPropertyRows(oAgent, IntuneMdmScripts.CoManagement, new TimeSpan(0, 0, 45));
                dataGrid1.ItemsSource = rows;
                tbStatus.Text = rows.Count + " properties";
                if (Listener != null)
                    Listener.WriteLine("# Intune co-management probe");
            });
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
