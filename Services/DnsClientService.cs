using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HFL.Client.Services
{
    /// <summary>
    /// Manages non-invasive DNS operations without modifying Windows network adapters.
    /// DNS redirection is handled transparently in-flight via WinDivert packet filtering.
    /// </summary>
    public class DnsClientService
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern int DnsFlushResolverCache();

        public bool IsActive { get; private set; }

        public void EnableTransparentDns()
        {
            FlushMemoryCache();
            IsActive = true;
        }

        public void DisableTransparentDns()
        {
            FlushMemoryCache();
            IsActive = false;
        }

        public static void FlushMemoryCache()
        {
            try
            {
                DnsFlushResolverCache();
            }
            catch { }

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(1000);
            }
            catch { }
        }
    }
}
