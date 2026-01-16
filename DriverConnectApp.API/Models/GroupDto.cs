using System;
using System.Collections.Generic;

namespace DriverConnectApp.API.Models
{
    public class GroupDto
    {
        public int Id { get; set; }
        public string WhatsAppGroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public bool IsActive { get; set; }
        public int ConversationCount { get; set; }
        public int ParticipantCount { get; set; }
        public List<GroupParticipantDto> Participants { get; set; } = new List<GroupParticipantDto>();
    }
}