namespace DriverConnectApp.API.Models
{
    public class MediaItemDto
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public string Type { get; set; } = string.Empty; // "image", "video", "document", "link"
        public string? Url { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime SentAt { get; set; }
        public string? SenderName { get; set; }
        public bool IsFromDriver { get; set; }
        public string? Duration { get; set; } // For videos/audio
        public string? Dimensions { get; set; } // For images
    }

    
}