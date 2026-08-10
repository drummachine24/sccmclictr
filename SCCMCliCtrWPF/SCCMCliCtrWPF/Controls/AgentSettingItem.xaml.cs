using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using sccmclictr.automation;

namespace ClientCenter
{
    /// <summary>
    /// Interaction logic for AgentSettingItem.xaml
    /// </summary>
    public partial class AgentSettingItem : UserControl
    {
        private SCCMAgent oAgent;

        public MyTraceListener Listener;

        public AgentSettingItem()
        {
            InitializeComponent();
        }

        public SCCMAgent SCCMAgentConnection
        {
            get
            {
                return oAgent;
            }
            set
            {
                if (value == null)
                {
                    oAgent = null;
                    return;
                }

                if (value.isConnected)
                {
                    if (oAgent != value)
                    {
                        oAgent = value;
                        try
                        {
                            Mouse.OverrideCursor = Cursors.Wait;

                            spAgentSettings.IsEnabled = true;
                            spHTTPPort.IsEnabled = true;
                            spHTTPSPort.IsEnabled = true;

                            ClearFields();
                            AppLogger.Info("Loading agent settings for " + (oAgent.TargetHostname ?? ""));
                            LoadAllAgentSettings();
                            AppLogger.Info("Agent settings load complete");

                            Mouse.OverrideCursor = Cursors.Arrow;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Error("AgentSettingItem.SCCMAgentConnection", ex);
                            if (Listener != null)
                                Listener.WriteError(ex.Message);
                            Mouse.OverrideCursor = Cursors.Arrow;
                        }
                    }
                }
            }
        }

        void ClearFields()
        {
            tbAgentVersion.Text = "";
            tbCachePath.Text = "";
            tbDNSSuffix.Text = "";
            tbGUID.Text = "";
            tbInetMP.Text = "";
            tbLogPath.Text = "";
            tbMP.Text = "";
            tbProxyMP.Text = "";
            tbSiteCode.Text = "";
            tbSetupPath.Text = "";
            tbSLP.Text = "";
            tbHTTPPort.Text = "";
            tbHTTPSPort.Text = "";
            cbAutoSite.IsChecked = false;
        }

        void LoadAllAgentSettings()
        {
            try { tbSiteCode.Text = oAgent.Client.AgentProperties.AssignedSite; } catch (Exception ex) { Log(ex); }
            try { tbHTTPPort.Text = oAgent.Client.AgentProperties.HTTPPort.ToString(); } catch (Exception ex) { Log(ex); tbHTTPPort.Text = ""; }
            try { tbHTTPSPort.Text = oAgent.Client.AgentProperties.HTTPSPort.ToString(); } catch (Exception ex) { Log(ex); tbHTTPSPort.Text = ""; }
            try { tbAgentVersion.Text = oAgent.Client.AgentProperties.ClientVersion; } catch (Exception ex) { Log(ex); }
            try { tbMP.Text = oAgent.Client.AgentProperties.ManagementPoint; } catch (Exception ex) { Log(ex); }
            try { tbInetMP.Text = oAgent.Client.AgentProperties.ManagementPointInternet; } catch (Exception ex) { Log(ex); }
            try { tbProxyMP.Text = oAgent.Client.AgentProperties.ManagementPointProxy; } catch (Exception ex) { Log(ex); }
            try { tbDNSSuffix.Text = oAgent.Client.AgentProperties.DNSSuffix; } catch (Exception ex) { Log(ex); }
            try { tbSLP.Text = oAgent.Client.AgentProperties.ServerLocatorPoint; } catch (Exception ex) { Log(ex); }
            try { tbGUID.Text = oAgent.Client.AgentProperties.ClientId; } catch (Exception ex) { Log(ex); }
            try { tbLogPath.Text = oAgent.Client.AgentProperties.LocalSCCMAgentLogPath; } catch (Exception ex) { Log(ex); }
            try { tbCachePath.Text = oAgent.Client.SWCache.CachePath; } catch (Exception ex) { Log(ex); }
            try { cbAutoSite.IsChecked = oAgent.Client.AgentProperties.EnableAutoAssignment; } catch (Exception ex) { Log(ex); }
            try { tbSetupPath.Text = @"C:\Windows\ccmsetup"; } catch { }
        }

        void Log(Exception ex)
        {
            if (Listener != null && ex != null)
                Listener.WriteError(ex.Message);
        }

        private void imgSiteCode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbSiteCode.Text = oAgent.Client.AgentProperties.AssignedSite;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgAgentVersion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbAgentVersion.Text = oAgent.Client.AgentProperties.ClientVersion;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgMP_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbMP.Text = oAgent.Client.AgentProperties.ManagementPoint;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgProxyMP_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbProxyMP.Text = oAgent.Client.AgentProperties.ManagementPointProxy;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgINetMP_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbInetMP.Text = oAgent.Client.AgentProperties.ManagementPointInternet;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgDNSSuffix_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbDNSSuffix.Text = oAgent.Client.AgentProperties.DNSSuffix;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSaveDNSSuffix_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            oAgent.Client.AgentProperties.DNSSuffix = tbDNSSuffix.Text;
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSaveSiteCode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (tbSiteCode.Text.Length == 3)
                oAgent.Client.AgentProperties.AssignedSite = tbSiteCode.Text;
            else
            {
                Listener.WriteError("Error: Site Code must have 3 characters.");
                MessageBox.Show("Site Code must have 3 characters!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgGetHTTPPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbHTTPPort.Text = oAgent.Client.AgentProperties.HTTPPort.ToString();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                {
                    Listener.WriteError("Error: Unable to get the HTTP Port.");
                    Listener.WriteError(ex.Message);
                }
                tbHTTPPort.Text = "";
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSetHTTPPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (int.Parse(tbHTTPPort.Text) > 0)
                    oAgent.Client.AgentProperties.HTTPPort = int.Parse(tbHTTPPort.Text);
            }
            catch (Exception ex)
            {
                if (Listener != null)
                {
                    Listener.WriteError("Error: Unable to set the HTTP Port.");
                    Listener.WriteError(ex.Message);
                }
                MessageBox.Show("Unable to set the HTTP Port.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgGetHTTPSPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbHTTPSPort.Text = oAgent.Client.AgentProperties.HTTPSPort.ToString();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                {
                    Listener.WriteError("Error: Unable to get the HTTPS Port.");
                    Listener.WriteError(ex.Message);
                }
                tbHTTPSPort.Text = "";
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSetHTTPSPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (int.Parse(tbHTTPSPort.Text) > 0)
                    oAgent.Client.AgentProperties.HTTPSPort = int.Parse(tbHTTPSPort.Text);
            }
            catch (Exception ex)
            {
                if (Listener != null)
                {
                    Listener.WriteError("Error: Unable to set the HTTPS Port.");
                    Listener.WriteError(ex.Message);
                }
                MessageBox.Show("Unable to set the HTTPS Port.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgGetSLP_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbSLP.Text = oAgent.Client.AgentProperties.ServerLocatorPoint;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSaveSLP_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                oAgent.Client.AgentProperties.ServerLocatorPoint = tbSLP.Text;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
        }

        private void imgGetGUID_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbGUID.Text = oAgent.Client.AgentProperties.ClientId;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgGetLogPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbLogPath.Text = oAgent.Client.AgentProperties.LocalSCCMAgentLogPath;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgOpenLogPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                Process Explorer = new Process();
                Explorer.StartInfo.FileName = "Explorer.exe";

                if (!oAgent.ConnectIPC_)
                    oAgent.ConnectIPC_ = true;

                string sLogPath = "";
                try
                {
                    sLogPath = oAgent.Client.AgentProperties.LocalSCCMAgentLogPath.Replace(':', '$');
                }
                catch { }
                if (!string.IsNullOrEmpty(sLogPath))
                {
                    Explorer.StartInfo.Arguments = @"\\" + oAgent.TargetHostname + @"\" + sLogPath;
                }
                else
                {
                    Explorer.StartInfo.Arguments = @"\\" + oAgent.TargetHostname + @"\admin$\CCM\Logs";
                }

                Explorer.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                Explorer.Start();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgOpenSetupPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                Process Explorer = new Process();
                Explorer.StartInfo.FileName = "Explorer.exe";

                if (!oAgent.ConnectIPC_)
                    oAgent.ConnectIPC_ = true;

                Explorer.StartInfo.Arguments = @"\\" + oAgent.TargetHostname + @"\admin$\ccmsetup";
                Explorer.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                Explorer.Start();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgGetCachePath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbCachePath.Text = oAgent.Client.SWCache.CachePath;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSaveCachepath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                oAgent.Client.SWCache.CachePath = tbCachePath.Text;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
        }

        private void imgOpenCachePath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                Process Explorer = new Process();
                Explorer.StartInfo.FileName = "Explorer.exe";

                if (!oAgent.ConnectIPC_)
                    oAgent.ConnectIPC_ = true;

                string sCachePath = "";
                try
                {
                    sCachePath = oAgent.Client.SWCache.CachePath.Replace(':', '$');
                }
                catch { }
                if (!string.IsNullOrEmpty(sCachePath))
                {
                    Explorer.StartInfo.Arguments = @"\\" + oAgent.TargetHostname + @"\" + sCachePath;
                }
                else
                {
                    Explorer.StartInfo.Arguments = @"\\" + oAgent.TargetHostname + @"\admin$\CCM\Cache";
                }

                Explorer.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                Explorer.Start();
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgGetAutoSite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                cbAutoSite.IsChecked = oAgent.Client.AgentProperties.EnableAutoAssignment;
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void imgSaveAutoSite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                string message = "Do you want to restart the SCCM Agent?";
                string caption = "SCCM Agent must be restarted!";
                MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.DefaultDesktopOnly);

                if (result == MessageBoxResult.Yes)
                {
                    oAgent.Client.AgentProperties.EnableAutoAssignment = ((bool)cbAutoSite.IsChecked);
                    oAgent.Client.Services.GetService("CcmExec").RestartService();
                }
                else
                {
                    oAgent.Client.AgentProperties.EnableAutoAssignment = ((bool)cbAutoSite.IsChecked);
                }
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
