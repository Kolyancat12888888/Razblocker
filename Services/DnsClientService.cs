using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace HFL.Client.Services
{
    public class DnsClientService
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern int DnsFlushResolverCache();

        public bool IsActive { get; private set; }

        public void EnableDns(string serverIp = "31.77.8.9")
        {
            SetDnsServers(serverIp, "1.1.1.1");
            FlushMemoryCache();
            IsActive = true;
        }

        public void DisableDns()
        {
            RestoreDnsDhcp();
            FlushMemoryCache();
            IsActive = false;
        }

        public static void SetDnsServers(string primaryIp, string secondaryIp)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                                (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                                 i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    .ToList();

                foreach (var ni in interfaces)
                {
                    string name = ni.Name;
                    RunNetsh($"interface ip set dns name=\"{name}\" static {primaryIp} validate=no");
                    RunNetsh($"interface ip add dns name=\"{name}\" {secondaryIp} index=2 validate=no");
                }
            }
            catch { }
        }

        public static void RestoreDnsDhcp()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                                (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                                 i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    .ToList();

                foreach (var ni in interfaces)
                {
                    string name = ni.Name;
                    RunNetsh($"interface ip set dns name=\"{name}\" dhcp");
                }
            }
            catch { }
        }

        private static void RunNetsh(string arguments)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(1500);
            }
            catch { }
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
