using System;
using System.Collections.Generic;

namespace DriverConnectApp.API.Models
{
    public class ConversationDetailDto
    {
        public int Id { get; set; }
        public int? DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string DriverPhone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsAnswered { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? AssignedToUserId { get; set; }
        public bool IsGroupConversation { get; set; }
        public string? GroupName { get; set; }
        public string? WhatsAppGroupId { get; set; }
        public int? GroupId { get; set; } // FIXED: Changed from int to int?
        public int? TeamId { get; set; }
        public List<MessageDto> Messages { get; set; } = new List<MessageDto>();
        public List<GroupParticipantDto> Participants { get; set; } = new List<GroupParticipantDto>();

        public bool CanSendNonTemplateMessages { get; set; }
        public DateTime? LastInboundMessageAt { get; set; }
        public string? NonTemplateMessageStatus { get; set; }
        public double? HoursRemaining { get; set; }
        public double? MinutesRemaining { get; set; }
        public DateTime? WindowExpiresAt { get; set; }
    }
}