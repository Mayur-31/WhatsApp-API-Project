using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverConnectApp.Domain.Entities
{
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Topic { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastMessageAt { get; set; }

        // ✅ ADDED: Track last inbound message timestamp for 24-hour window
        public DateTime? LastInboundMessageAt { get; set; }

        [Required]
        public bool IsAnswered { get; set; } = false;

        // ✅ UPDATED: Driver relationship - make it nullable for group conversations
        public int? DriverId { get; set; }
        public virtual Driver? Driver { get; set; }

        // ✅ UPDATED: Team relationship - make it nullable
        public int? TeamId { get; set; }
        public virtual Team? Team { get; set; }

        // ✅ ADDED: Department relationship
        public int? DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        // ✅ ADDED: Group relationship
        public int? GroupId { get; set; }
        public virtual Group? Group { get; set; }

        // ✅ ADDED: WhatsApp Group ID
        public string? WhatsAppGroupId { get; set; }

        // ✅ ADDED: Group conversation fields
        public bool IsGroupConversation { get; set; } = false;
        public string? GroupName { get; set; }

        // ✅ ADDED: Assigned to user
        public string? AssignedToUserId { get; set; }

        // ✅ ADDED: Active status
        public bool IsActive { get; set; } = true;

        // Status tracking
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

        // Navigation properties
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<Thread> Threads { get; set; } = new List<Thread>();

        // Custom methods
        public void UpdateInboundMessageTimestamp()
        {
            LastInboundMessageAt = DateTime.UtcNow;
            LastMessageAt = DateTime.UtcNow;
        }

        public bool CanSendNonTemplateMessages()
        {
            // NO inbound message = CANNOT send free text (STRICT)
            if (!LastInboundMessageAt.HasValue)
                return false;

            // Check if within exactly 24 hours (strict)
            var timeSinceLastInbound = DateTime.UtcNow - LastInboundMessageAt.Value;
            return timeSinceLastInbound.TotalHours < 24.0; // Strictly less than 24 hours
        }

        public class WindowStatusDto
        {
            public bool CanSendNonTemplateMessages { get; set; }
            public int HoursRemaining { get; set; }
            public int MinutesRemaining { get; set; }
            public DateTime? LastInboundMessageAt { get; set; }
            public DateTime? WindowExpiresAt { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public WindowStatusDto GetWindowStatus()
        {
            if (!LastInboundMessageAt.HasValue)
            {
                return new WindowStatusDto
                {
                    CanSendNonTemplateMessages = false,
                    HoursRemaining = 0,
                    MinutesRemaining = 0,
                    LastInboundMessageAt = null,
                    WindowExpiresAt = null,
                    Message = "No incoming message received. Use template messages to start conversation.",
                    Status = "NO_WINDOW"
                };
            }

            var elapsed = DateTime.UtcNow - LastInboundMessageAt.Value;
            bool canSend = elapsed.TotalHours < 24.0;

            var hoursRemaining = canSend ? Math.Max(0, 24 - (int)elapsed.TotalHours) : 0;
            var minutesRemaining = canSend ? Math.Max(0, (int)((24 * 60) - elapsed.TotalMinutes) % 60) : 0;
            var windowExpiresAt = canSend ? LastInboundMessageAt.Value.AddHours(24) : (DateTime?)null;

            return new WindowStatusDto
            {
                CanSendNonTemplateMessages = canSend,
                HoursRemaining = hoursRemaining,
                MinutesRemaining = minutesRemaining,
                LastInboundMessageAt = LastInboundMessageAt,
                WindowExpiresAt = windowExpiresAt,
                Message = canSend
                    ? $"Free messaging available for {hoursRemaining}h {minutesRemaining}m"
                    : "24-hour window expired. Template messages only.",
                Status = canSend ? "WINDOW_OPEN" : "WINDOW_CLOSED"
            };
        }
    }

}

    