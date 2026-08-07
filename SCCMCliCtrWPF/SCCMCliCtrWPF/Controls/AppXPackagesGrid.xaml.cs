using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using sccmclictr.automation;
using sccmclictr.automation.functions;

namespace ClientCenter.Controls
{
    /// <summary>
    /// Interaction logic for AppXPackagesGrid.xaml
    /// </summary>
    public partial class AppXPackagesGrid : UserControl
    {
        private SCCMAgent oAgent;
        public MyTraceListener Listener;
        internal List<inventory.AppXPackage> iPackages;

        public AppXPackagesGrid()
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
                        LoadPackages(true);
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

        private void LoadPackages(bool force)
        {
            if (oAgent == null || !oAgent.isConnected)
                return;

            iPackages = oAgent.Client.Inventory.GetAppXPackages(force).OrderBy(t => t.Name).ThenBy(t => t.Version).ToList();
            dataGrid1.BeginInit();
            dataGrid1.ItemsSource = null;
            dataGrid1.ItemsSource = iPackages;
            dataGrid1.EndInit();
        }

        private void bt_Reload_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                LoadPackages(true);
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void miRemoveAppX_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = dataGrid1.SelectedItems.Cast<inventory.AppXPackage>().ToList();
                if (selected.Count == 0)
                    return;

                string msg = selected.Count == 1
                    ? "Remove AppX package '" + selected[0].Name + "' for all users?"
                    : "Remove " + selected.Count + " AppX packages for all users?";

                if (MessageBox.Show(msg, "Remove AppX Package", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                Mouse.OverrideCursor = Cursors.Wait;
                foreach (inventory.AppXPackage pkg in selected)
                {
                    try
                    {
                        string sResult = pkg.Remove();
                        if (Listener != null)
                            Listener.WriteLine("Removed AppX: " + pkg.PackageFullName + (string.IsNullOrEmpty(sResult) ? "" : " (" + sResult + ")"));
                    }
                    catch (Exception ex)
                    {
                        if (Listener != null)
                            Listener.WriteError("Failed to remove " + pkg.PackageFullName + ": " + ex.Message);
                    }
                }

                LoadPackages(true);
            }
            catch (Exception ex)
            {
                if (Listener != null)
                    Listener.WriteError(ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
        }
    }
}
