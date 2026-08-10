using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ClientCenter
{
    /// <summary>
    /// Simple thread-safe file logger. Prefers the application directory;
    /// falls back to %LocalAppData%\ClientCenter if the app folder is not writable.
    /// </summary>
    public static class AppLogger
    {
        static readonly object Sync = new object();
        static string _logPath;
        static bool _initialized;
        const long MaxBytes = 5 * 1024 * 1024;

        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return _logPath;
            }
        }

        public static void Initialize()
        {
            EnsureInitialized();
        }

        static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (Sync)
            {
                if (_initialized)
                    return;

                string appDir = null;
                try
                {
                    appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                }
                catch { }

                if (!string.IsNullOrEmpty(appDir) && TryUseDirectory(appDir, out _logPath))
                {
                    _initialized = true;
                }
                else
                {
                    string fallback = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ClientCenter");
                    TryUseDirectory(fallback, out _logPath);
                    _initialized = true;
                }

                try
                {
                    WriteUnlocked("INFO", "=== Client Center logging started ===");
                    WriteUnlocked("INFO", "Log file: " + _logPath);
                    WriteUnlocked("INFO", "App: " + (Assembly.GetExecutingAssembly().Location ?? ""));
                    WriteUnlocked("INFO", "Version: " + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                    WriteUnlocked("INFO", "OS: " + Environment.OSVersion);
                    WriteUnlocked("INFO", "CLR: " + Environment.Version);
                }
                catch { }
            }
        }

        static bool TryUseDirectory(string directory, out string logPath)
        {
            logPath = null;
            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                logPath = Path.Combine(directory, "ClientCenter.log");
                // Verify we can write (Program Files installs may deny this).
                File.AppendAllText(logPath, "", Encoding.UTF8);
                return true;
            }
            catch
            {
                logPath = null;
                return false;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            if (ex == null)
            {
                Write("ERROR", message);
                return;
            }

            var sb = new StringBuilder();
            sb.Append(message);
            sb.Append(": ");
            sb.Append(ex.GetType().FullName);
            sb.Append(" - ");
            sb.Append(ex.Message);
            if (ex.InnerException != null)
            {
                sb.Append(" | Inner: ");
                sb.Append(ex.InnerException.GetType().FullName);
                sb.Append(" - ");
                sb.Append(ex.InnerException.Message);
            }
            sb.AppendLine();
            sb.Append(ex.ToString());
            Write("ERROR", sb.ToString());
        }

        public static void Debug(string message)
        {
            Write("DEBUG", message);
        }

        static void Write(string level, string message)
        {
            EnsureInitialized();
            lock (Sync)
            {
                WriteUnlocked(level, message);
            }
        }

        static void WriteUnlocked(string level, string message)
        {
            if (string.IsNullOrEmpty(_logPath))
                return;

            try
            {
                RotateIfNeededUnlocked();
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + (message ?? "") + Environment.NewLine;
                File.AppendAllText(_logPath, line, Encoding.UTF8);
            }
            catch
            {
                // Never throw from the logger.
            }
        }

        static void RotateIfNeededUnlocked()
        {
            try
            {
                if (!File.Exists(_logPath))
                    return;

                var info = new FileInfo(_logPath);
                if (info.Length < MaxBytes)
                    return;

                string backup = _logPath + ".1";
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(_logPath, backup);
            }
            catch { }
        }
    }
}
