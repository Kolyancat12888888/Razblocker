using System;
using System.Text.Json.Serialization;

namespace HFL.Core.Models
{
    public class LicenseInfo
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public int DurationDays { get; set; } = 30; // -1 for lifetime
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Hwid { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Note { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public string? AppVersion { get; set; }
    }

    public class ValidateRequest
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("hwid")]
        public string Hwid { get; set; } = string.Empty;

        [JsonPropertyName("app_version")]
        public string? AppVersion { get; set; }
    }

    public class ValidateResponse
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("expires_at")]
        public string? ExpiresAt { get; set; }

        [JsonPropertyName("days_left")]
        public int DaysLeft { get; set; }

        [JsonPropertyName("server_config")]
        public ClientServerConfig? ServerConfig { get; set; }
    }

    public class ClientServerConfig
    {
        [JsonPropertyName("doh_url")]
        public string DohUrl { get; set; } = string.Empty;

        [JsonPropertyName("vless_uri")]
        public string VlessUri { get; set; } = string.Empty;

        [JsonPropertyName("strategies")]
        public string[] ZapretStrategies { get; set; } = Array.Empty<string>();
    }
}
