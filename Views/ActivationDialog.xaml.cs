using System;
using System.Windows;
using HFL.Client.Services;
using HFL.Core.Security;

namespace HFL.Client.Views
{
    public partial class ActivationDialog : Window
    {
        private readonly LicenseClientService _licenseService;
        public bool IsActivated { get; private set; } = false;

        public ActivationDialog(LicenseClientService licenseService)
        {
            InitializeComponent();
            _licenseService = licenseService;

            var settings = ConfigService.Load();
            KeyTextBox.Text = settings.LicenseKey;
            HwidText.Text = HwidGenerator.GetHwid();
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            string key = KeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(key))
            {
                ErrorText.Text = "Введите лицензионный ключ.";
                return;
            }

            ActivateButton.IsEnabled = false;
            ActivateButton.Content = "⏳ Проверка ключа...";
            ErrorText.Text = "";

            var result = await _licenseService.ValidateAsync(key);

            ActivateButton.IsEnabled = true;
            ActivateButton.Content = "⚡ Активировать лицензию";

            if (result.Valid)
            {
                var settings = ConfigService.Load();
                settings.LicenseKey = key;
                ConfigService.Save(settings);

                IsActivated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Text = $"❌ {result.Message ?? "Недействительный ключ или ошибка сервера."}";
            }
        }

        private void CopyHwid_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(HwidText.Text);
            MessageBox.Show("HWID скопирован в буфер обмена!", "HFL Razbloker", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
