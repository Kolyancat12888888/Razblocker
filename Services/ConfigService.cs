using System;
using System.IO;
using System.Text.Json;

namespace HFL.Client.Services
{
    public class AppSettings
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string SelectedMode { get; set; } = "Auto"; // "Auto", "Zapret", "3XUI", "DNS"
        public bool DirectRuRouting { get; set; } = true;
        public string CustomDohUrl { get; set; } = "";
        public bool AutoStartWithWindows { get; set; } = false;
        public bool AutoInstallCerts { get; set; } = true;
    }

    public static class ConfigService
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HFL_Razbloker"
        );

        private static readonly string FilePath = Path.Combine(FolderPath, "config.json");
        private static AppSettings? _cached;

        public static AppSettings Load()
        {
            if (_cached != null) return _cached;

            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);

                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    _cached = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    return _cached;
                }
            }
            catch
            {
            }

            _cached = new AppSettings();
            Save(_cached);
            return _cached;
        }

        public static void Save(AppSettings settings)
        {
            _cached = settings;
            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
            }
        }
    }
}
