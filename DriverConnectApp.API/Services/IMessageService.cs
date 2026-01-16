using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.Domain.Enums;

namespace DriverConnectApp.API.Services
{
    public interface IMessageService
    {
        Task<MessageDto?> SendMessageAsync(SendMessageRequest request);
        Task<List<MessageDto>> GetMessagesByConversationIdAsync(int conversationId);
        Task<ConversationDto?> GetOrCreateConversationAsync(int driverId, string? topic = null);
        Task<List<ConversationDto>> GetAllConversationsAsync();

        // UPDATED: Added reply functionality parameters
        Task<MessageDto?> CreateMessageAsync(
            int conversationId,
            string content,
            bool isFromDriver,
            string whatsAppMessageId,
            string? context = null,
            MessageType messageType = MessageType.Text,
            string? mediaUrl = null,
            string? fileName = null,
            long? fileSize = null,
            string? mimeType = null,
            string? location = null,
            string? contactName = null,
            string? contactPhone = null,
            int? replyToMessageId = null,
            string? replyToMessageContent = null,
            string? replyToSenderName = null);
    }
}