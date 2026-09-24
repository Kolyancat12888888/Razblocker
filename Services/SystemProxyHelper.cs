using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HFL.Client.Services
{
    public static class SystemProxyHelper
    {
        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public static void SetSystemProxy(string proxyServer, string bypassList = "localhost;127.*;10.*;192.168.*;*.ru;*.su;*.xn--p1ai;*.vk.com;*.yandex.ru")
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                if (key != null)
                {
                    key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
                    key.SetValue("ProxyOverride", bypassList, RegistryValueKind.String);
                }

                NotifySettingsChanged();
            }
            catch { }
        }

        public static void DisableSystemProxy()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                if (key != null)
                {
                    key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                }

                NotifySettingsChanged();
            }
            catch { }
        }

        private static void NotifySettingsChanged()
        {
            try
            {
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
            catch { }
        }
    }
}
