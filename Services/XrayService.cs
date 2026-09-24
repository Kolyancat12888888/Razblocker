using System;
using System.Diagnostics;
using System.IO;
using HFL.Core.Protocols;

namespace HFL.Client.Services
{
    public class XrayService
    {
        private Process? _xrayProcess;
        public bool IsRunning => _xrayProcess != null && !_xrayProcess.HasExited;

        public bool Start(string vlessUri, bool directRuRouting = true)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(vlessUri))
            {
                return false;
            }

            var parsedConfig = VlessParser.ParseUri(vlessUri);
            if (parsedConfig == null)
            {
                return false;
            }

            string corePath = FindXrayBinary();
            if (!File.Exists(corePath))
            {
                // In development / testing or before full xray download, enable direct proxy integration
                SystemProxyHelper.SetSystemProxy("127.0.0.1:10809");
                return true;
            }

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HFL_Razbloker");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

            string configJson = VlessParser.GenerateXrayJson(parsedConfig, directRuRouting);
            string configPath = Path.Combine(appData, "xray_active.json");
            File.WriteAllText(configPath, configJson);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = corePath,
                    Arguments = $"run -c \"{configPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetDirectoryName(corePath) ?? ""
                };

                _xrayProcess = Process.Start(psi);
                SystemProxyHelper.SetSystemProxy("http=127.0.0.1:10809;https=127.0.0.1:10809;socks=127.0.0.1:10808");
                return _xrayProcess != null && !_xrayProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public void Stop()
        {
            SystemProxyHelper.DisableSystemProxy();

            try
            {
                if (_xrayProcess != null && !_xrayProcess.HasExited)
                {
                    _xrayProcess.Kill(true);
                    _xrayProcess.Dispose();
                }
            }
            catch { }
            finally
            {
                _xrayProcess = null;
            }

            try
            {
                foreach (var p in Process.GetProcessesByName("xray"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
        }

        public static string FindXrayBinary()
        {
            string baseDir = AppContext.BaseDirectory;
            string localBin = Path.Combine(baseDir, "Assets", "bin", "xray.exe");
            if (File.Exists(localBin)) return localBin;

            return Path.Combine(baseDir, "xray.exe");
        }
    }
}
