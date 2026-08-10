using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using sccmclictr.automation;

namespace ClientCenter.Controls
{
    /// <summary>
    /// Client identity / status: OS, site, boundary groups, collections.
    /// </summary>
    public partial class ClientInformationGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;
        public string ClientInfoSummary { get; private set; } = "";
        public event Action ClientInfoChanged;

        public ClientInformationGrid()
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
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        oAgent = value;
                        LoadClientInformation();
                    }
                    catch (Exception ex)
                    {
                        if (Listener != null)
                            Listener.WriteError(ex.Message);
                    }
                    Mouse.OverrideCursor = Cursors.Arrow;
                }
            }
        }

        void LoadClientInformation()
        {
            if (oAgent == null || !oAgent.isConnected)
                return;

            try
            {
                ClientInfoSnapshot info = ClientInfoHelper.Load(oAgent);
                tbOSCaption.Text = info.OSCaption;
                tbOSVersionBuild.Text = info.OSVersionBuild;
                tbIPAddress.Text = info.IPAddress;
                tbClientSite.Text = info.SiteCode;
                tbBoundaryGroups.Text = info.BoundaryGroups;
                tbPrimaryUser.Text = info.PrimaryUser;
                tbLastCheckedIn.Text = info.LastCheckedIn;
                tbSiteLookupNote.Text = info.SiteLookupNote ?? "";
                dgCollections.ItemsSource = info.Collections;
                ClientInfoSummary = info.SummaryLine;
                if (ClientInfoChanged != null)
                    ClientInfoChanged();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError("Unable to load client information: " + ex.Message);
            }
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            if (oAgent == null || !oAgent.isConnected)
                return;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                LoadClientInformation();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }
    }
}
