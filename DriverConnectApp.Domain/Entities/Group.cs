using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverConnectApp.Domain.Entities
{
    public class Group
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string WhatsAppGroupId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastActivityAt { get; set; }

        public bool IsActive { get; set; } = true;

        // Team assignment
        public int? TeamId { get; set; }

        [ForeignKey("TeamId")]
        public virtual Team? Team { get; set; }

        // Navigation properties
        public virtual ICollection<GroupParticipant> Participants { get; set; } = new List<GroupParticipant>();

        // FIX: Update this navigation property to match the foreign key
        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}