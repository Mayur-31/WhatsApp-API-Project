using System.ComponentModel.DataAnnotations;

namespace DriverConnectApp.Domain.Entities
{
    public class Depot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Location { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    }
}