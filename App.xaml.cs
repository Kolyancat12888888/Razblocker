using System.Configuration;
using System.Data;
using System.Windows;

namespace HFL.Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args != null && e.Args.Length > 0)
        {
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--uninstall-certs", StringComparison.OrdinalIgnoreCase))
                {
                    int removed = Services.CertificateManagerService.UninstallAllCertificates();
                    MessageBox.Show($"Сертификаты HFL и .local успешно удалены из системы ({removed} шт.). Автоустановка отключена.", 
                        "HFL Razbloker - Сертификаты", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
                else if (arg.Equals("--install-certs", StringComparison.OrdinalIgnoreCase))
                {
                    int installed = await Services.CertificateManagerService.InstallAllCertificatesAsync();
                    MessageBox.Show($"Сертификаты панели и сайтов успешно установлены в доверенные ({installed} шт.). Автоустановка включена.", 
                        "HFL Razbloker - Сертификаты", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }
        }

        // Normal GUI startup: if AutoInstallCerts enabled, fetch and install in background
        var settings = Services.ConfigService.Load();
        if (settings.AutoInstallCerts)
        {
            _ = Task.Run(async () =>
            {
                await Services.CertificateManagerService.InstallAllCertificatesAsync();
            });
        }
    }
}

