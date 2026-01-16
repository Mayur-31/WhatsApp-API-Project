namespace DriverConnectApp.API.Models
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public int? DepotId { get; set; }
        public string? DepotName { get; set; }
        public int? TeamId { get; set; }
        public string? TeamName { get; set; }
        public string TeamRole { get; set; } = "TeamMember";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public List<string> Roles { get; set; } = new List<string>();

        // ADD THESE MISSING PROPERTIES:
        public bool IsSuperAdmin { get; set; }
        public bool IsAdmin { get; set; }
    }
}