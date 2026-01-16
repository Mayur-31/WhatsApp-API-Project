namespace DriverConnectApp.API.Models
{
    public class FileUploadResponse
    {
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string MediaUrl { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;

        // Compression information
        public bool WasCompressed { get; set; }
        public string? CompressionInfo { get; set; }
        public long OriginalSize { get; set; }
        public bool WhatsAppCompliant { get; set; }
    }
}