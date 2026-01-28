using System.Text.Json.Serialization;

namespace DriverConnectApp.API.Models.WhatsApp
{
    public class IncomingMessage
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty; // Can be individual or group ID

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public MessageText? Text { get; set; }

        [JsonPropertyName("video")]
        public Video? Video { get; set; } // ✅ ADDED

        [JsonPropertyName("audio")]
        public Audio? Audio { get; set; } // ✅ ADDED

        [JsonPropertyName("image")]
        public Image? Image { get; set; }

        [JsonPropertyName("document")]
        public Document? Document { get; set; }

        [JsonPropertyName("location")]
        public Location? Location { get; set; }

        [JsonPropertyName("contacts")]
        public List<Contact>? Contacts { get; set; }

        [JsonPropertyName("context")]
        public WhatsAppContext? Context { get; set; }

        // GROUP MESSAGE FIELDS (NEW)
        [JsonPropertyName("group_id")]
        public string? GroupId { get; set; } // Null for individual messages

        [JsonPropertyName("participant")]
        public string? Participant { get; set; } // Actual sender in group messages

        // Helper properties
        public bool IsGroupMessage => !string.IsNullOrEmpty(GroupId);
        public string ActualSender => IsGroupMessage ? (Participant ?? From) : From;
    }

    public class MessageText
    {
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }

    public class Image
    {
        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        public string? Link { get; set; }
    }

    public class Document
    {
        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        public string? Link { get; set; }
    }

    public class Location
    {
        [JsonPropertyName("latitude")]
        public decimal Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public decimal Longitude { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }
    }

    public class Contact
    {
        [JsonPropertyName("name")]
        public ContactName? Name { get; set; }

        [JsonPropertyName("phones")]
        public List<ContactPhone>? Phones { get; set; }
    }

    public class ContactName
    {
        [JsonPropertyName("formatted_name")]
        public string? FormattedName { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    public class ContactPhone
    {
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class WhatsAppContext
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    public class Video
    {
        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        public string? Link { get; set; }
    }

    public class Audio
    {
        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        public string? Link { get; set; }
    }
}