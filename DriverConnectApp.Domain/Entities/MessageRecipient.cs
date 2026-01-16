using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverConnectApp.Domain.Entities
{
    public class MessageRecipient
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        [ForeignKey("MessageId")]
        public virtual Message Message { get; set; } = null!;

        // For individual messages
        public int? DriverId { get; set; }

        [ForeignKey("DriverId")]
        public virtual Driver? Driver { get; set; }

        // For group messages
        public int? GroupParticipantId { get; set; }

        [ForeignKey("GroupParticipantId")]
        public virtual GroupParticipant? GroupParticipant { get; set; }

        // Status tracking
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

        // For group messages, track if this recipient has seen the message
        public bool HasSeen { get; set; } = false;
        public DateTime? SeenAt { get; set; }
    }
}