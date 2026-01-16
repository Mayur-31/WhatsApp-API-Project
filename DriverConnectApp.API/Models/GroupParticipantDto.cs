namespace DriverConnectApp.API.Models
{
    public class GroupParticipantDto
    {
        public int Id { get; set; }
        public int GroupId { get; set; }  // ✅ This is correct - int (non-nullable)
        public int? DriverId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ParticipantName { get; set; }
        public string? DriverName { get; set; }
        public string? DriverPhone { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}