using DriverConnectApp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DriverConnectApp.Domain.Entities
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public MessageType MessageType { get; set; } = MessageType.Text;

        // Media fields
        public string? MediaUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }

        // Conversation relationship
        [Required]
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        // Basic message info
        public bool IsFromDriver { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // Context fields
        public string? Context { get; set; }
        public string? Location { get; set; }
        public string? JobId { get; set; }
        public string? Priority { get; set; }

        // WhatsApp identifiers
        public string? WhatsAppMessageId { get; set; }

        // Thread relationship
        public int? ThreadId { get; set; }
        public Thread? Thread { get; set; }

        // Contact messages
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }

        // Group message fields
        public string? SenderPhoneNumber { get; set; }
        public string? SenderName { get; set; }
        public bool IsGroupMessage { get; set; } = false;

        // Reply functionality
        public int? ReplyToMessageId { get; set; }

        [ForeignKey("ReplyToMessageId")]
        public virtual Message? ReplyToMessage { get; set; }
        
        public string? ReplyToMessageContent { get; set; }
        public string? ReplyToSenderName { get; set; }

        // Staff user who sent the message
        public string? SentByUserId { get; set; }
        public string? SentByUserName { get; set; }

        // WhatsApp Message Status & Interaction Features
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public bool IsStarred { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        public DateTime? PinnedAt { get; set; }
        public int ForwardCount { get; set; } = 0;
        public int? ForwardedFromMessageId { get; set; }

        [ForeignKey("ForwardedFromMessageId")]
        public virtual Message? ForwardedFromMessage { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }

        // Navigation properties for message interactions
        public virtual ICollection<MessageRecipient> Recipients { get; set; } = new List<MessageRecipient>();
        public virtual ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();

        // ✅ Template message fields (FIXED: Keep these)
        public bool IsTemplateMessage { get; set; } = false;
        public string? TemplateName { get; set; }
        public string? TemplateParametersJson { get; set; }

        [NotMapped]
        public Dictionary<string, string> TemplateParameters
        {
            get => string.IsNullOrEmpty(TemplateParametersJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(TemplateParametersJson) ?? new Dictionary<string, string>();
            set => TemplateParametersJson = JsonSerializer.Serialize(value);
        }
    }

    public enum MessageStatus
    {
        Sent = 1,
        Delivered = 2,
        Read = 3,
        Failed = 4,
        Pending = 5
    }
}