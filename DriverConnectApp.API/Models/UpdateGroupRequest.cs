using System.Collections.Generic;

namespace DriverConnectApp.API.Models
{
    public class UpdateGroupRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AddParticipantsRequest
    {
        public List<GroupParticipantRequest> Participants { get; set; } = new List<GroupParticipantRequest>();
    }

    public class RemoveParticipantsRequest
    {
        public List<int> ParticipantIds { get; set; } = new List<int>();
    }
}