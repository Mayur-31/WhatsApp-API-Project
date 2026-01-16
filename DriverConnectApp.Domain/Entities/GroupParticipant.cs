using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverConnectApp.Domain.Entities
{
    public class GroupParticipant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GroupId { get; set; }

        [ForeignKey("GroupId")]
        public virtual Group Group { get; set; } = null!;

        // Can be either a driver or just a phone number
        public int? DriverId { get; set; }

        [ForeignKey("DriverId")]
        public virtual Driver? Driver { get; set; }

        // Store phone number directly if not a driver
        public string? PhoneNumber { get; set; }

        public string? ParticipantName { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Role in group (admin, member, etc.)
        public string Role { get; set; } = "member";
    }
}