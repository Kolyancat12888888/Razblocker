using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

namespace HFL.Client.Services
{
    public class CertificateInfoModel
    {
        public string Domain { get; set; } = string.Empty;
        public string CertificatePem { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Thumbprint { get; set; } = string.Empty;
    }

    public static class CertificateManagerService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
        private const string ServerApiUrl = "http://31.77.8.9:5000/api/v1/certificates/list";
        private const string RootCaUrl = "http://31.77.8.9:5000/api/v1/certificates/root-ca";

        /// <summary>
        /// Fetch all domain certificates and Root CA from server and install into Windows Trusted Root / Personal store
        /// </summary>
        public static async Task<int> InstallAllCertificatesAsync()
        {
            int installedCount = 0;
            try
            {
                // 1. Fetch Root CA if available and install into Root store
                try
                {
                    var rootCaBytes = await _httpClient.GetByteArrayAsync(RootCaUrl);
                    if (rootCaBytes != null && rootCaBytes.Length > 0)
                    {
                        var rootCert = new X509Certificate2(rootCaBytes);
                        InstallToStore(rootCert, StoreName.Root, StoreLocation.LocalMachine);
                        InstallToStore(rootCert, StoreName.Root, StoreLocation.CurrentUser);
                        installedCount++;
                    }
                }
                catch
                {
                    // If no dedicated Root CA, proceed to domain certs
                }

                // 2. Fetch list of domain certificates from Panel / Server API
                var certs = await _httpClient.GetFromJsonAsync<CertificateInfoModel[]>(ServerApiUrl);
                if (certs != null)
                {
                    foreach (var certInfo in certs)
                    {
                        if (string.IsNullOrWhiteSpace(certInfo.CertificatePem)) continue;

                        byte[] certRaw = System.Text.Encoding.UTF8.GetBytes(certInfo.CertificatePem);
                        var x509 = X509Certificate2.CreateFromPem(certInfo.CertificatePem);

                        // Install into Trusted Root Certification Authorities to satisfy Kaspersky & Windows
                        InstallToStore(x509, StoreName.Root, StoreLocation.LocalMachine);
                        InstallToStore(x509, StoreName.Root, StoreLocation.CurrentUser);
                        InstallToStore(x509, StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                        installedCount++;
                    }
                }

                // Update settings flag
                var settings = ConfigService.Load();
                settings.AutoInstallCerts = true;
                ConfigService.Save(settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CertManager] Error installing certificates: {ex.Message}");
            }

            return installedCount;
        }

        /// <summary>
        /// Uninstall and remove all HFL and *.local / *.internal certificates from system certificate stores
        /// </summary>
        public static int UninstallAllCertificates()
        {
            int removedCount = 0;
            StoreLocation[] locations = { StoreLocation.LocalMachine, StoreLocation.CurrentUser };
            StoreName[] storeNames = { StoreName.Root, StoreName.CertificateAuthority, StoreName.My };

            foreach (var loc in locations)
            {
                foreach (var sName in storeNames)
                {
                    try
                    {
                        using var store = new X509Store(sName, loc);
                        store.Open(OpenFlags.ReadWrite);

                        var toRemove = new X509Certificate2Collection();
                        foreach (var cert in store.Certificates)
                        {
                            string subject = cert.Subject.ToLowerInvariant();
                            string issuer = cert.Issuer.ToLowerInvariant();
                            string friendly = cert.FriendlyName.ToLowerInvariant();

                            if (subject.Contains("hfl") || subject.Contains(".local") || subject.Contains(".internal") ||
                                issuer.Contains("hfl") || issuer.Contains(".local") || issuer.Contains(".internal") ||
                                friendly.Contains("hfl"))
                            {
                                toRemove.Add(cert);
                            }
                        }

                        foreach (var cert in toRemove)
                        {
                            store.Remove(cert);
                            removedCount++;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            var settings = ConfigService.Load();
            settings.AutoInstallCerts = false;
            ConfigService.Save(settings);

            return removedCount;
        }

        private static void InstallToStore(X509Certificate2 cert, StoreName storeName, StoreLocation storeLocation)
        {
            try
            {
                using var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadWrite);

                // Check if already present by thumbprint
                var existing = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
                if (existing.Count == 0)
                {
                    store.Add(cert);
                }
            }
            catch
            {
            }
        }
    }
}
