using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HFL.Core.Models;

namespace HFL.Client.Services
{
    public class StrategyOptimizer
    {
        private readonly ZapretService _zapret;
        private readonly DnsClientService _dns;
        private readonly TransparentDnsRedirector _transparentDns;
        private readonly LicenseClientService _license;
        private readonly HttpClient _probeHttp;

        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public event Action<string>? OnLog;
        public event Action<int>? OnLatencyChanged;
        public event Action<string>? OnStrategyChanged;
        public event Action<bool>? OnConnectionStateChanged;

        public string ActiveStrategyName { get; private set; } = "Отключено";
        public int CurrentLatencyMs { get; private set; } = 0;
        public bool IsActive => _isRunning;

        public StrategyOptimizer(ZapretService zapret, DnsClientService dns, LicenseClientService license)
        {
            _zapret = zapret;
            _dns = dns;
            _transparentDns = new TransparentDnsRedirector();
            _license = license;
            _probeHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        }

        public async Task StartAutonomousAsync(AppSettings settings, int manualStrategyIndex = -1)
        {
            Stop();
            _isRunning = true;
            _cts = new CancellationTokenSource();
            OnConnectionStateChanged?.Invoke(true);

            Log("🚀 Запуск HFL Transparent DNS Resolver (Режим Чистого DNS)...");

            var token = _cts.Token;

            // Start Transparent DNS Redirection to our server DoH (without touching Windows network adapters!)
            string dohUrl = !string.IsNullOrEmpty(settings.CustomDohUrl)
                ? settings.CustomDohUrl
                : _license.CurrentLicense?.ServerConfig?.DohUrl ?? $"{LicenseClientService.ServerEndpoint}/dns-query";

            if (!string.IsNullOrEmpty(dohUrl))
            {
                Log($"🔒 Включение прозрачного перехвата DNS (Свой сервер: {dohUrl}). Домены .local/.internal активны.");
                _transparentDns.Start(dohUrl);
                _dns.EnableTransparentDns();
            }

            SetStrategy("HFL Native DNS Mode");
            Log("✅ HFL DNS активен. Запросы .local и .internal прозрачно направляются на сервер 31.77.8.9.");

            _ = MonitorHealthLoopAsync(token);
        }

        private async Task RunAutonomousFallbackLoopAsync(CancellationToken token)
        {
            Log("⚙️ Проверка Стратегии 1: YouTube 4K + Discord Voice (Fake TLS / QUIC)...");
            SetStrategy("Zapret: Стратегия 1 (YouTube + Discord)");
            _zapret.Start(null, 0);

            await Task.Delay(1500, token);
            int ping = await ProbeTargetAsync("https://www.youtube.com");

            if (ping > 0 && ping <= 120)
            {
                CurrentLatencyMs = ping;
                OnLatencyChanged?.Invoke(ping);
                Log($"✅ Стратегия 1 успешно зафиксирована! Задержка: {ping} мс (Швейцарская точность)");
                return;
            }

            Log("⚠️ Провайдер фильтрует профиль 1. Переключение на Стратегию 2 (Disorder2)...");
            SetStrategy("Zapret: Стратегия 2 (Disorder)");
            _zapret.Start(null, 1);

            await Task.Delay(1500, token);
            ping = await ProbeTargetAsync("https://www.youtube.com");

            if (ping > 0)
            {
                CurrentLatencyMs = ping;
                OnLatencyChanged?.Invoke(ping);
                Log($"✅ Стратегия 2 зафиксирована! Задержка: {ping} мс");
                return;
            }

            Log("⚡ Активация универсальной Стратегии 3 (Fake Repeats + AutoTTL)...");
            SetStrategy("Zapret: Стратегия 3 (Fake Repeats)");
            _zapret.Start(null, 2);
        }

        private async Task MonitorHealthLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(4000, token);
                    int ping = await ProbeTargetAsync("https://www.youtube.com");
                    if (ping <= 0)
                    {
                        ping = await ProbeTargetAsync("https://discord.com");
                    }

                    if (ping > 0)
                    {
                        CurrentLatencyMs = ping;
                        OnLatencyChanged?.Invoke(ping);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _transparentDns.Stop();
            _zapret.Stop();
            _dns.DisableTransparentDns();

            _isRunning = false;
            ActiveStrategyName = "Отключено";
            CurrentLatencyMs = 0;
            OnLatencyChanged?.Invoke(0);
            OnStrategyChanged?.Invoke("Отключено");
            OnConnectionStateChanged?.Invoke(false);
            Log("⏹️ Zapret и DNS-перехват остановлены. Стандартная сеть восстановлена.");
        }

        private async Task<int> ProbeTargetAsync(string url)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                var res = await _probeHttp.SendAsync(req);
                sw.Stop();
                if (res.IsSuccessStatusCode || (int)res.StatusCode < 500)
                {
                    return (int)sw.ElapsedMilliseconds;
                }
            }
            catch
            {
            }
            return -1;
        }

        private void SetStrategy(string name)
        {
            ActiveStrategyName = name;
            OnStrategyChanged?.Invoke(name);
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
