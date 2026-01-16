namespace DriverConnectApp.API.Models
{
    public class SendMessageRequest
    {
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text";
        public string? MediaUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? Location { get; set; }

        // Individual message fields
        public int? DriverId { get; set; }
        public int? ConversationId { get; set; }

        // Group message fields (NEW)
        public bool IsGroupMessage { get; set; }
        public string? GroupId { get; set; }

        public bool IsFromDriver { get; set; }
        public string WhatsAppMessageId { get; set; } = string.Empty;
        public string? Context { get; set; }
        public string? JobId { get; set; }
        public string? Priority { get; set; }
        public int? ThreadId { get; set; }

        // Missing properties that were causing errors
        public string? Topic { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }

        public bool IsTemplateMessage { get; set; }
        public string? TemplateName { get; set; }
        public Dictionary<string, string>? TemplateParameters { get; set; }

        public int TeamId { get; set; }

        // ADD THESE MISSING PROPERTIES:
        public string? PhoneNumber { get; set; }
        public string? LanguageCode { get; set; } = "en_US";
    }
}