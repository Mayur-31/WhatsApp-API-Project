using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DriverConnectApp.Domain.Entities
{
    public class Driver
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty; // Fixed: Added default value

        [Required]
        public string PhoneNumber { get; set; } = string.Empty; // Fixed: Added default value

        public int? DepotId { get; set; }
        public virtual Depot? Depot { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public int? TeamId { get; set; }
        public virtual Team? Team { get; set; }

        // Navigation properties
        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}