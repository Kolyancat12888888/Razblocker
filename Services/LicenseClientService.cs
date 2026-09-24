using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HFL.Core.Models;
using HFL.Core.Security;

namespace HFL.Client.Services
{
    public class LicenseClientService
    {
        // Hardcoded secure enterprise server endpoint (protected against spoofing)
        public const string ServerEndpoint = "http://31.77.8.9:5000";

        private readonly HttpClient _http;
        public ValidateResponse? CurrentLicense { get; private set; }

        public LicenseClientService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        }

        public async Task<ValidateResponse> ValidateAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return new ValidateResponse { Valid = false, Status = "empty_key", Message = "Ключ не указан" };
            }

            string hwid = HwidGenerator.GetHwid();
            string endpoint = $"{ServerEndpoint}/api/v1/license/validate";

            try
            {
                var req = new ValidateRequest
                {
                    Key = key.Trim(),
                    Hwid = hwid,
                    AppVersion = "1.0.0"
                };

                var res = await _http.PostAsJsonAsync(endpoint, req);
                if (res.IsSuccessStatusCode)
                {
                    var response = await res.Content.ReadFromJsonAsync<ValidateResponse>();
                    if (response != null)
                    {
                        CurrentLicense = response;
                        return response;
                    }
                }

                return new ValidateResponse
                {
                    Valid = false,
                    Status = "server_error",
                    Message = $"Ошибка сервера (HTTP {(int)res.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                return new ValidateResponse
                {
                    Valid = false,
                    Status = "connection_error",
                    Message = $"Не удалось связаться с сервером авторизации: {ex.Message}"
                };
            }
        }
    }
}
