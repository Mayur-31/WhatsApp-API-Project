using System.Text.Json.Serialization;

namespace DriverConnectApp.API.Models.WhatsApp
{
    public class OutgoingMessage
    {
        [JsonPropertyName("messaging_product")]
        public string MessagingProduct { get; set; } = "whatsapp";

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public OutgoingTextContent Text { get; set; } = new OutgoingTextContent();
    }

    public class OutgoingTextContent
    {
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }

    public class MessageResponse
    {
        [JsonPropertyName("messaging_product")]
        public string MessagingProduct { get; set; } = string.Empty;

        [JsonPropertyName("contacts")]
        public ContactResponse[] Contacts { get; set; } = Array.Empty<ContactResponse>();

        [JsonPropertyName("messages")]
        public MessageResponseItem[] Messages { get; set; } = Array.Empty<MessageResponseItem>();
    }

    public class ContactResponse
    {
        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("wa_id")]
        public string WaId { get; set; } = string.Empty;
    }

    public class MessageResponseItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }
}