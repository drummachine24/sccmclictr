using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ClientCenter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppLogger.Initialize();
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += App_UnhandledException;
            TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            AppLogger.Info("Application exiting (code " + e.ApplicationExitCode + ")");
        }

        static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                AppLogger.Error("Dispatcher unhandled exception", e.Exception);
                MessageBox.Show(
                    "An unexpected error occurred.\n\n" + e.Exception.Message +
                    "\n\nDetails were written to:\n" + AppLogger.LogPath,
                    "Client Center Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            }
            catch
            {
                e.Handled = true;
            }
        }

        static void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                if (ex != null)
                    AppLogger.Error("Domain unhandled exception (IsTerminating=" + e.IsTerminating + ")", ex);
                else
                    AppLogger.Error("Domain unhandled exception (IsTerminating=" + e.IsTerminating + "): " + e.ExceptionObject);
            }
            catch { }
        }

        static void App_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                AppLogger.Error("Unobserved task exception", e.Exception);
                e.SetObserved();
            }
            catch { }
        }
    }
}
