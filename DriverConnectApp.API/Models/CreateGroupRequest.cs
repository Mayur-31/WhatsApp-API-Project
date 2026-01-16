using System.Collections.Generic;

namespace DriverConnectApp.API.Models
{
    public class CreateGroupRequest
    {
        public string WhatsAppGroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // NEW: Participants for the group
        public List<GroupParticipantRequest> Participants { get; set; } = new List<GroupParticipantRequest>();

        public int? TeamId { get; set; }
    }

    public class GroupParticipantRequest
    {
        public int? DriverId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ParticipantName { get; set; }
        public string Role { get; set; } = "member";
    }
}