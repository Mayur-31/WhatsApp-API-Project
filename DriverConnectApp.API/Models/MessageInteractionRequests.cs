using System.Collections.Generic;

namespace DriverConnectApp.API.Models
{
    public class UpdateMessageStatusRequest
    {
        public string Status { get; set; } = "Sent"; // "Sent", "Delivered", "Read"
        public int? DriverId { get; set; }
        public int? GroupParticipantId { get; set; }
    }

    public class ReactToMessageRequest
    {
        public string Reaction { get; set; } = string.Empty;
    }

    public class ForwardMessageRequest
    {
        public List<int> ConversationIds { get; set; } = new List<int>();
        public string? CustomMessage { get; set; }
    }

    public class PinMessageRequest
    {
        public bool IsPinned { get; set; }
    }

    public class StarMessageRequest
    {
        public bool IsStarred { get; set; }
    }

    public class MessageInfoResponse
    {
        public MessageDto Message { get; set; } = new MessageDto();
        public List<MessageRecipientDto> Recipients { get; set; } = new List<MessageRecipientDto>();
        public List<MessageReactionDto> Reactions { get; set; } = new List<MessageReactionDto>();
        public int TotalRecipients { get; set; }
        public int DeliveredCount { get; set; }
        public int ReadCount { get; set; }
    }
}