using System;
using System.Text.Json.Serialization;

namespace HFL.Core.Models
{
    public class DnsRecord
    {
        public int Id { get; set; }
        public string Domain { get; set; } = string.Empty; // e.g. "panel.local", "*.internal"
        public string IpAddress { get; set; } = string.Empty; // e.g. "127.0.0.1" or server IP
        public string RecordType { get; set; } = "A";
        public int Ttl { get; set; } = 60;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; }
    }
}
