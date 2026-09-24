using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace HFL.Core.Security
{
    public static class HwidGenerator
    {
        private static string? _cachedHwid;

        public static string GetHwid()
        {
            if (!string.IsNullOrEmpty(_cachedHwid))
                return _cachedHwid;

            var sb = new StringBuilder();

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // 1. Processor Id
                    sb.Append(GetWmiProperty("Win32_Processor", "ProcessorId"));
                    // 2. BaseBoard SerialNumber
                    sb.Append(GetWmiProperty("Win32_BaseBoard", "SerialNumber"));
                    // 3. BIOS SerialNumber
                    sb.Append(GetWmiProperty("Win32_BIOS", "SerialNumber"));
                    // 4. Windows Cryptography MachineGuid
                    sb.Append(GetMachineGuid());
                }
            }
            catch
            {
                // Fallback
            }

            if (sb.Length == 0)
            {
                // Fallback environment info
                sb.Append(Environment.MachineName);
                sb.Append(Environment.UserName);
                sb.Append(Environment.ProcessorCount);
            }

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            
            // Format: HFL-HWID-XXXX-XXXX-XXXX-XXXX
            string hex = Convert.ToHexString(hash);
            _cachedHwid = $"HWID-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
            return _cachedHwid;
        }

        private static string GetWmiProperty(string table, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {table}");
                foreach (var item in searcher.Get())
                {
                    var val = item[property]?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val.Trim();
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private static string GetMachineGuid()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key != null)
                {
                    var val = key.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }
            }
            catch
            {
            }
            return string.Empty;
        }
    }
}
