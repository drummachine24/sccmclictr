using System;
using System.Windows;
using System.Windows.Controls;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    public partial class IntuneEnrollmentGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;

        public IntuneEnrollmentGrid()
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
                var rows = IntuneMdmHelper.LoadPropertyRows(oAgent, IntuneMdmScripts.Enrollment, new TimeSpan(0, 0, 45));
                dataGrid1.ItemsSource = rows;
                tbStatus.Text = rows.Count + " properties";
                if (Listener != null)
                    Listener.WriteLine("# Intune enrollment / dsregcmd probe");
            });
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
