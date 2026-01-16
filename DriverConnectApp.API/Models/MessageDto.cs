using System;
using System.Collections.Generic;

namespace DriverConnectApp.API.Models
{
    public class MessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text";
        public string? MediaUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? Location { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public bool IsFromDriver { get; set; }
        public DateTime SentAt { get; set; }

        // FIXED: Added null check for FormattedDate
        public string FormattedDate => FormatDate(SentAt) ?? string.Empty;
        public string FormattedTime => SentAt.ToString("hh:mm tt");
        public string? WhatsAppMessageId { get; set; }
        public string? JobId { get; set; }
        public string? Context { get; set; }
        public string? Priority { get; set; }
        public int? ThreadId { get; set; }

        // GROUP MESSAGE FIELDS
        public bool IsGroupMessage { get; set; }
        public string? SenderPhoneNumber { get; set; }
        public string? SenderName { get; set; }

        // NEW: Staff user information
        public string? SentByUserId { get; set; }
        public string? SentByUserName { get; set; }

        // NEW: Reply functionality
        public int? ReplyToMessageId { get; set; }
        public string? ReplyToMessageContent { get; set; }
        public string? ReplyToSenderName { get; set; }
        public MessageDto? ReplyToMessage { get; set; }

        // NEW: WhatsApp Status & Interaction Features
        public string Status { get; set; } = "Sent"; // "Sent", "Delivered", "Read"
        public bool IsStarred { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public int ForwardCount { get; set; }
        public int? ForwardedFromMessageId { get; set; }
        public MessageDto? ForwardedFromMessage { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }

        // NEW: Template message properties
        public bool IsTemplateMessage { get; set; }
        public string? TemplateName { get; set; }

        // Reactions
        public List<MessageReactionDto> Reactions { get; set; } = new List<MessageReactionDto>();

        // Recipients (for group messages)
        public List<MessageRecipientDto> Recipients { get; set; } = new List<MessageRecipientDto>();

        // Helper properties for UI
        public bool CanDelete => !IsDeleted;
        public string StatusIcon => GetStatusIcon();
        public string? PinnedInfo => IsPinned ? $"Pinned {FormatRelativeTime(PinnedAt)}" : null;

        // FIXED: Ensure FormatDate never returns null
        private static string FormatDate(DateTime date)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            if (date.Date == today)
                return "Today";
            else if (date.Date == yesterday)
                return "Yesterday";
            else
                return date.ToString("MMMM dd, yyyy") ?? string.Empty; // Added null coalescing
        }

        private string GetStatusIcon()
        {
            return Status switch
            {
                "Sent" => "✅",
                "Delivered" => "✅✅",
                "Read" => "🔵", // Blue tick
                _ => "🕒"
            };
        }

        private string? FormatRelativeTime(DateTime? dateTime)
        {
            if (!dateTime.HasValue) return null;

            var timeSpan = DateTime.UtcNow - dateTime.Value;
            if (timeSpan.TotalMinutes < 1) return "just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";

            return dateTime.Value.ToString("MMM dd");
        }
    }

    public class MessageReactionDto
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string? UserId { get; set; }
        public int? DriverId { get; set; }
        public string Reaction { get; set; } = string.Empty;
        public DateTime ReactedAt { get; set; }
        public string? UserName { get; set; }
        public string? DriverName { get; set; }
        public string ReactorName => UserName ?? DriverName ?? "Unknown";
    }

    public class MessageRecipientDto
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int? DriverId { get; set; }
        public int? GroupParticipantId { get; set; }
        public string Status { get; set; } = "Sent";
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool HasSeen { get; set; }
        public DateTime? SeenAt { get; set; }

        // Additional info
        public string? ParticipantName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? DriverName { get; set; }
    }
}