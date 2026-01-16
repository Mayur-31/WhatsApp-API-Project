using System.ComponentModel.DataAnnotations;

namespace DriverConnectApp.Domain.Entities
{
    public class TeamConfiguration
    {
        public int Id { get; set; }

        [Required]
        public int TeamId { get; set; }
        public virtual Team Team { get; set; } = null!;

        // Team-specific settings
        public string? BrandColor { get; set; } = "#10B981"; // Default green
        public string? LogoUrl { get; set; }
        public string? TimeZone { get; set; } = "GMT";
        public string? Language { get; set; } = "en";

        // Business hours
        public TimeSpan? BusinessStartTime { get; set; }
        public TimeSpan? BusinessEndTime { get; set; }
        public bool Is24Hours { get; set; } = true;

        // WhatsApp rate limiting
        public int MaxMessagesPerMinute { get; set; } = 60;
        public int MaxMessagesPerDay { get; set; } = 1000;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}