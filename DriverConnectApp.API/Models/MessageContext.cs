namespace DriverConnectApp.API.Models
{
    public class MessageContext
    {
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public int? ThreadId { get; set; }
    }
}