namespace DriverConnectApp.Domain.Entities
{
    public class Thread
    {
        public int Id { get; set; }
        public string Topic { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}