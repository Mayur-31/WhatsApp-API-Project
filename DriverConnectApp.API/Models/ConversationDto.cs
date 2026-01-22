using System;

namespace DriverConnectApp.API.Models
{
    public class ConversationDto
    {
        public int Id { get; set; }
        public int DriverId { get; set; } // Use 0 for group conversations
        public string DriverName { get; set; } = string.Empty;
        public string DriverPhone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; }
        public bool IsAnswered { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? AssignedToUserId { get; set; }
        public int UnreadCount { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
        public bool IsGroupConversation { get; set; }
        public string? GroupName { get; set; }
        public string? WhatsAppGroupId { get; set; }
        public int? GroupId { get; set; }
        public int GroupMemberCount { get; set; }
        public int TeamId { get; set; }
    }
}