using System;
using System.Diagnostics;
using System.IO;

namespace HFL.Client.Services
{
    public class ZapretService
    {
        private Process? _winwsProcess;
        public bool IsRunning => _winwsProcess != null && !_winwsProcess.HasExited;

        public bool Start(string? customArgs = null, int strategyIndex = 0)
        {
            Stop();

            string winwsPath = FindWinwsBinary();
            if (!File.Exists(winwsPath))
            {
                return false;
            }

            string binDir = Path.GetDirectoryName(winwsPath) ?? "";
            string listPath = Path.Combine(AppContext.BaseDirectory, "Assets", "lists", "list-general.txt");
            if (!File.Exists(listPath))
            {
                listPath = Path.Combine(binDir, "list-general.txt");
            }

            string tlsFake = Path.Combine(binDir, "tls_clienthello_www_google_com.bin");
            string quicFake = Path.Combine(binDir, "quic_initial_www_google_com.bin");

            string args;
            if (!string.IsNullOrEmpty(customArgs))
            {
                args = customArgs;
            }
            else
            {
                // Transparent packet-level filtering: includes DNS (53), Web (80,443), Discord (50000-50050)
                // Windows network adapter settings remain 100% UNTOUCHED!
                switch (strategyIndex)
                {
                    case 0:
                        // Ultimate Strategy: YouTube 4K 60FPS + Discord Gateway & Voice + In-flight DNS Protection
                        args = $"--wf-tcp=53,80,443 --wf-udp=53,443,50000-50050 " +
                               $"--filter-udp=443 {(File.Exists(listPath) ? $"--hostlist=\"{listPath}\"" : "")} --dpi-desync=fake --dpi-desync-repeats=6 {(File.Exists(quicFake) ? $"--dpi-desync-fake-quic=\"{quicFake}\"" : "")} --newfilter " +
                               $"--filter-udp=50000-50050 --dpi-desync=fake --dpi-desync-any-protocol --dpi-desync-cutoff=d3 --newfilter " +
                               $"--filter-tcp=80,443 {(File.Exists(listPath) ? $"--hostlist=\"{listPath}\"" : "")} --dpi-desync=fake,split2 --dpi-desync-autottl=2 --dpi-desync-fooling=md5sig {(File.Exists(tlsFake) ? $"--dpi-desync-fake-tls=\"{tlsFake}\"" : "")}";
                        break;

                    case 1:
                        // Disorder + BadSeq Strategy
                        args = $"--wf-tcp=53,80,443 --wf-udp=53,443,50000-50050 " +
                               $"--filter-udp=443 --dpi-desync=fake --dpi-desync-repeats=6 --newfilter " +
                               $"--filter-udp=50000-50050 --dpi-desync=fake --dpi-desync-any-protocol --dpi-desync-cutoff=d3 --newfilter " +
                               $"--filter-tcp=80,443 {(File.Exists(listPath) ? $"--hostlist=\"{listPath}\"" : "")} --dpi-desync=disorder2 --dpi-desync-split-pos=1 --dpi-desync-fooling=badseq";
                        break;

                    default:
                        // Fake + Syndata Strategy
                        args = $"--wf-tcp=53,80,443 --wf-udp=53,443,50000-50050 " +
                               $"--filter-udp=443 --dpi-desync=fake --newfilter " +
                               $"--filter-udp=50000-50050 --dpi-desync=fake --dpi-desync-cutoff=d3 --newfilter " +
                               $"--filter-tcp=80,443 --dpi-desync=fake,syndata --dpi-desync-repeats=8 --dpi-desync-fooling=badsum";
                        break;
                }
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = winwsPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = binDir
                };

                _winwsProcess = Process.Start(psi);
                return _winwsProcess != null && !_winwsProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                if (_winwsProcess != null && !_winwsProcess.HasExited)
                {
                    _winwsProcess.Kill(true);
                    _winwsProcess.Dispose();
                }
            }
            catch
            {
            }
            finally
            {
                _winwsProcess = null;
            }

            try
            {
                foreach (var p in Process.GetProcessesByName("winws"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
        }

        public static string FindWinwsBinary()
        {
            string baseDir = AppContext.BaseDirectory;
            string localBin = Path.Combine(baseDir, "Assets", "bin", "winws.exe");
            if (File.Exists(localBin)) return localBin;

            string rootBin = Path.Combine(baseDir, "winws.exe");
            return rootBin;
        }
    }
}
