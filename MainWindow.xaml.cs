using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HFL.Client.Services;
using HFL.Client.Views;

namespace HFL.Client
{
    public partial class MainWindow : Window
    {
        private readonly LicenseClientService _licenseService;
        private readonly ZapretService _zapretService;
        private readonly DnsClientService _dnsService;
        private readonly StrategyOptimizer _optimizer;
        private readonly StringBuilder _logBuffer = new();

        public MainWindow()
        {
            InitializeComponent();

            _licenseService = new LicenseClientService();
            _zapretService = new ZapretService();
            _dnsService = new DnsClientService();
            _optimizer = new StrategyOptimizer(_zapretService, _dnsService, _licenseService);

            _optimizer.OnLog += AppendLog;
            _optimizer.OnLatencyChanged += UpdateLatencyUi;
            _optimizer.OnStrategyChanged += UpdateStrategyUi;
            _optimizer.OnConnectionStateChanged += UpdateConnectionStateUi;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = ConfigService.Load();

            if (string.IsNullOrEmpty(settings.LicenseKey))
            {
                ShowActivationDialog();
                return;
            }

            AppendLog($"🔍 Проверка лицензии: {settings.LicenseKey}...");
            var res = await _licenseService.ValidateAsync(settings.LicenseKey);

            if (res.Valid)
            {
                UpdateLicenseInfoUi();
                AppendLog("✅ Лицензия подтверждена. Zapret готов к запуску.");
            }
            else
            {
                AppendLog($"⚠️ Лицензия не активна ({res.Message}). Открытие окна активации...");
                ShowActivationDialog();
            }
        }

        private void ShowActivationDialog()
        {
            var dlg = new ActivationDialog(_licenseService) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.IsActivated)
            {
                UpdateLicenseInfoUi();
                AppendLog("✅ Лицензия успешно активирована!");
            }
            else if (_licenseService.CurrentLicense == null || !_licenseService.CurrentLicense.Valid)
            {
                LicenseText.Text = "🔴 Лицензия не активирована";
                LicenseBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                LicenseBadge.Background = new SolidColorBrush(Color.FromRgb(39, 19, 19));
            }
        }

        private void UpdateLicenseInfoUi()
        {
            var lic = _licenseService.CurrentLicense;
            if (lic != null && lic.Valid)
            {
                string days = lic.DaysLeft >= 0 ? $"{lic.DaysLeft} дн." : "Бессрочно";
                LicenseText.Text = $"🟢 Лицензия: {days}";
                LicenseBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                LicenseBadge.Background = new SolidColorBrush(Color.FromRgb(19, 39, 31));
            }
        }

        private async void TogglePower_Click(object sender, RoutedEventArgs e)
        {
            if (_optimizer.IsActive)
            {
                _optimizer.Stop();
            }
            else
            {
                var settings = ConfigService.Load();
                await _optimizer.StartAutonomousAsync(settings);
            }
        }

        private void UpdateConnectionStateUi(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                if (isConnected)
                {
                    PowerButtonText.Text = "ОТКЛЮЧИТЬ";
                    PowerButtonText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                    PowerIcon.Text = "🛡️";
                    ConnectionStatusText.Text = "🟢 HFL DNS Перехват Активен";
                    ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                }
                else
                {
                    PowerButtonText.Text = "ВКЛЮЧИТЬ";
                    PowerButtonText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    PowerIcon.Text = "⚡";
                    ConnectionStatusText.Text = "Отключено";
                    ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                }
            });
        }

        private void UpdateLatencyUi(int ms)
        {
            Dispatcher.Invoke(() =>
            {
                if (ms <= 0)
                {
                    PingText.Text = "— ms";
                    PingText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                }
                else
                {
                    PingText.Text = $"{ms} ms";
                    if (ms <= 80)
                    {
                        PingText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    }
                    else if (ms <= 150)
                    {
                        PingText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    }
                    else
                    {
                        PingText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                }
            });
        }

        private void UpdateStrategyUi(string strategy)
        {
            Dispatcher.Invoke(() =>
            {
                StrategyText.Text = strategy;
            });
        }

        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _logBuffer.AppendLine(message);
                LogTextBox.Text = _logBuffer.ToString();
                LogScrollViewer.ScrollToEnd();
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowActivationDialog();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _optimizer.Stop();
        }
    }
}