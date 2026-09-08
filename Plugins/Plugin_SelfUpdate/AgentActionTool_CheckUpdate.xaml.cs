using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AgentActionTools
{
    public partial class CustomTools_SelfUpdate : System.Windows.Controls.UserControl
    {
        const string ReleasesUrl = "https://github.com/drummachine24/sccmclictr/releases";
        const string LatestApiUrl = "https://api.github.com/repos/drummachine24/sccmclictr/releases/latest";
        static readonly HttpClient Http = CreateClient();
        bool _suppressToggle;

        public CustomTools_SelfUpdate()
        {
            InitializeComponent();
            _suppressToggle = true;
            cbAutoCheck.IsChecked = Properties.Settings.Default.AutoUpdateEnabled;
            _suppressToggle = false;

            // MainPage extracts the RibbonGroup from this UserControl, so UserControl.Loaded
            // never fires. Queue the opt-in check after the UI is idle. No network in the ctor.
            Dispatcher.BeginInvoke(new Action(() => { _ = MaybeAutoCheckAsync(); }), DispatcherPriority.ApplicationIdle);
        }

        static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ClientCenter-SelfUpdate");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return http;
        }

        async Task MaybeAutoCheckAsync()
        {
            if (!Properties.Settings.Default.AutoUpdateEnabled)
                return;
            if ((DateTime.Now - Properties.Settings.Default.LastUpdateCheck) < TimeSpan.FromDays(2))
                return;
            await CheckForUpdateAsync(false);
        }

        void cbAutoCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle)
                return;
            Properties.Settings.Default.AutoUpdateEnabled = cbAutoCheck.IsChecked == true;
            Properties.Settings.Default.Save();
        }

        async void btCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await CheckForUpdateAsync(true);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        async Task CheckForUpdateAsync(bool interactive)
        {
            try
            {
                Assembly entry = Assembly.GetEntryAssembly();
                if (entry == null)
                {
                    if (interactive)
                        MessageBox.Show("Could not determine the installed version.", "Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string current = FileVersionInfo.GetVersionInfo(entry.Location).FileVersion;
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                using (HttpResponseMessage response = await Http.GetAsync(LatestApiUrl, cts.Token))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException("GitHub returned " + (int)response.StatusCode + " " + response.ReasonPhrase);

                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        string tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
                        string htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : ReleasesUrl;
                        if (string.IsNullOrWhiteSpace(htmlUrl))
                            htmlUrl = ReleasesUrl;

                        Properties.Settings.Default.LastUpdateCheck = DateTime.Now;
                        Properties.Settings.Default.Save();

                        if (string.IsNullOrWhiteSpace(tag) || !IsNewer(tag, current))
                        {
                            if (interactive)
                                MessageBox.Show("No update available.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        string msg = "A newer version is available: " + tag + " (you have " + current + ")."
                            + Environment.NewLine + Environment.NewLine
                            + "Open the GitHub releases page?";
                        if (MessageBox.Show(msg, "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            Process.Start(new ProcessStartInfo(htmlUrl) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                if (interactive)
                    MessageBox.Show("Could not check for updates: " + ex.Message, "Update", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        internal static bool IsNewer(string latestTag, string currentFileVersion)
        {
            Version latest;
            Version current;
            if (!TryParseVersion(latestTag, out latest) || !TryParseVersion(currentFileVersion, out current))
                return false;
            return Normalize(latest) > Normalize(current);
        }

        static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string s = value.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(1);
            int cut = s.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0)
                s = s.Substring(0, cut);
            return Version.TryParse(s, out version);
        }

        static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build, v.Revision < 0 ? 0 : v.Revision);
        }
    }
}
