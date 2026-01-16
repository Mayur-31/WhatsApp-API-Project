using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverConnectApp.Domain.Entities
{
    public class Team
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // WhatsApp Business Configuration
        [MaxLength(100)]
        public string? WhatsAppPhoneNumberId { get; set; }

        [MaxLength(500)]
        public string? WhatsAppAccessToken { get; set; }

        [MaxLength(100)]
        public string? WhatsAppBusinessAccountId { get; set; }

        [MaxLength(20)]
        public string? WhatsAppPhoneNumber { get; set; }

        [MaxLength(10)]
        public string ApiVersion { get; set; } = "18. 0";

        // ✅ NEW: Country code for phone normalization (e.g., "44" for UK, "91" for India)
        [MaxLength(3)]
        public string CountryCode { get; set; } = "44"; // Default to UK

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();
        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
        public virtual ICollection<Group> Groups { get; set; } = new List<Group>();
    }
}