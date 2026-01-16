namespace DriverConnectApp.API.Models
{
    public class CreateTeamRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string WhatsAppPhoneNumberId { get; set; } = string.Empty; // FIXED: Made required
        public string WhatsAppAccessToken { get; set; } = string.Empty; // FIXED: Made required
        public string WhatsAppBusinessAccountId { get; set; } = string.Empty; // FIXED: Made required
        public string? WhatsAppPhoneNumber { get; set; }
        public string? ApiVersion { get; set; } = "18.0";
    }

    public class UpdateTeamRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? WhatsAppPhoneNumberId { get; set; }
        public string? WhatsAppAccessToken { get; set; }
        public string? WhatsAppBusinessAccountId { get; set; }
        public string? WhatsAppPhoneNumber { get; set; }
        public string? ApiVersion { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? WhatsAppPhoneNumberId { get; set; }
        public string? WhatsAppAccessToken { get; set; }
        public string? WhatsAppBusinessAccountId { get; set; }
        public string? WhatsAppPhoneNumber { get; set; }
        public string? ApiVersion { get; set; }
        public int UserCount { get; set; }
        public int ContactCount { get; set; }
        public int ChatCount { get; set; }
        public int GroupCount { get; set; } // FIXED: Added missing property
    }
}