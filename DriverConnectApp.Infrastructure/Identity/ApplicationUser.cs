using DriverConnectApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DriverConnectApp.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Foreign keys for department and depot
        public int? DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        public int? DepotId { get; set; }
        public virtual Depot? Depot { get; set; }

        // Team assignment
        public int? TeamId { get; set; }
        public virtual Team? Team { get; set; }
        public string? TeamRole { get; set; } // FIXED: Made properly nullable

        // Relationship with Driver
        public int? DriverId { get; set; }
        public virtual Driver? Driver { get; set; }

        // Navigation properties
        public virtual ICollection<MessageReaction> MessageReactions { get; set; } = new List<MessageReaction>();

        // Add this missing navigation property
        public virtual ICollection<Conversation> AssignedConversations { get; set; } = new List<Conversation>();
    }
}