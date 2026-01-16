using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverConnectApp.Domain.Entities
{
    public class MessageReaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        [ForeignKey("MessageId")]
        public virtual Message Message { get; set; } = null!;

        // Who reacted (staff user or driver)
        public string? UserId { get; set; } // Staff user ID (string, no navigation property)
        public int? DriverId { get; set; } // Driver ID

        [Required]
        public string Reaction { get; set; } = string.Empty; // Emoji like "👍", "❤️", "😂", etc.

        public DateTime ReactedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - Only for entities in Domain project
        [ForeignKey("DriverId")]
        public virtual Driver? Driver { get; set; }

        // Removed: ApplicationUser navigation property to avoid circular dependency
        // We'll handle this relationship in Infrastructure layer
    }
}