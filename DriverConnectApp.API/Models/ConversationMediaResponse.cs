namespace DriverConnectApp.API.Models
{
    public class ConversationMediaResponse
    {
        public List<MediaItemDto> Images { get; set; } = new List<MediaItemDto>();
        public List<MediaItemDto> Videos { get; set; } = new List<MediaItemDto>();
        public List<MediaItemDto> Documents { get; set; } = new List<MediaItemDto>();
        public List<MediaItemDto> Links { get; set; } = new List<MediaItemDto>();
        public int TotalItems { get; set; }
        public string ConversationName { get; set; } = string.Empty;
        public bool IsGroupConversation { get; set; }
    }
}
