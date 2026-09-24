using System;

namespace HFL.Core.Models
{
    public class PanelUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Client"; // "SuperAdmin", "Admin", "Client"
        public string DiskQuota { get; set; } = "Unlimited";
        public int MaxSites { get; set; } = 10;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }
}
