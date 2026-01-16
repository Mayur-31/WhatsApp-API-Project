using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverConnectApp.API.Services
{
    public class MessageService : IMessageService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MessageService> _logger;

        public MessageService(AppDbContext context, ILogger<MessageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MessageDto?> SendMessageAsync(SendMessageRequest request)
        {
            try
            {
                _logger.LogInformation("SendMessageAsync called for DriverId: {DriverId}", request.DriverId);

                // FIXED: Validate driver exists with null check
                if (!request.DriverId.HasValue)
                {
                    _logger.LogError("DriverId is null");
                    return null;
                }

                var driver = await _context.Drivers.FindAsync(request.DriverId.Value);
                if (driver == null)
                {
                    _logger.LogError("Driver not found with ID: {DriverId}", request.DriverId);
                    return null;
                }

                var topic = request.Topic ?? $"Conversation with {driver.Name}";
                // FIXED: Use request.DriverId.Value since we've validated it's not null
                var conversation = await GetOrCreateConversationEntityAsync(request.DriverId.Value, topic);

                if (conversation == null)
                {
                    _logger.LogError("Failed to get or create conversation for DriverId: {DriverId}", request.DriverId);
                    return null;
                }

                _logger.LogInformation("Using conversation ID: {ConversationId}", conversation.Id);

                // Parse message type
                if (!Enum.TryParse<MessageType>(request.MessageType, out var messageType))
                {
                    messageType = MessageType.Text;
                }

                // Create message
                var message = await CreateMessageAsync(
                    conversation.Id,
                    request.Content,
                    request.IsFromDriver,
                    request.WhatsAppMessageId ?? Guid.NewGuid().ToString(),
                    request.Context,
                    messageType,
                    request.MediaUrl,
                    request.FileName,
                    request.FileSize,
                    request.MimeType,
                    request.Location,
                    request.ContactName,
                    request.ContactPhone
                );

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessageAsync for driver {DriverId}", request.DriverId);
                return null;
            }
        }

        public async Task<List<MessageDto>> GetMessagesByConversationIdAsync(int conversationId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .Include(m => m.ReplyToMessage) // NEW: Include reply messages
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        Content = m.Content,
                        ConversationId = m.ConversationId,
                        IsFromDriver = m.IsFromDriver,
                        SentAt = m.SentAt,
                        Context = m.Context,
                        WhatsAppMessageId = m.WhatsAppMessageId,
                        Location = m.Location,
                        JobId = m.JobId,
                        Priority = m.Priority,
                        ThreadId = m.ThreadId,
                        MessageType = m.MessageType.ToString(),
                        MediaUrl = m.MediaUrl,
                        FileName = m.FileName,
                        FileSize = m.FileSize,
                        MimeType = m.MimeType,
                        ContactName = m.ContactName,
                        ContactPhone = m.ContactPhone,
                        // NEW: Enhanced reply functionality
                        ReplyToMessageId = m.ReplyToMessageId,
                        ReplyToMessageContent = m.ReplyToMessageContent,
                        ReplyToSenderName = m.ReplyToSenderName,
                        ReplyToMessage = m.ReplyToMessage != null ? new MessageDto
                        {
                            Id = m.ReplyToMessage.Id,
                            Content = m.ReplyToMessage.Content,
                            MessageType = m.ReplyToMessage.MessageType.ToString(),
                            MediaUrl = m.ReplyToMessage.MediaUrl,
                            FileName = m.ReplyToMessage.FileName,
                            IsFromDriver = m.ReplyToMessage.IsFromDriver,
                            SentAt = m.ReplyToMessage.SentAt,
                            SenderName = m.ReplyToMessage.SenderName,
                            SenderPhoneNumber = m.ReplyToMessage.SenderPhoneNumber
                        } : null
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
                return new List<MessageDto>();
            }
        }

        public async Task<ConversationDto?> GetOrCreateConversationAsync(int driverId, string? topic = null)
        {
            try
            {
                var driver = await _context.Drivers.FindAsync(driverId);
                if (driver == null)
                {
                    _logger.LogError("Driver not found: {DriverId}", driverId);
                    return null;
                }

                var existingConversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.DriverId == driverId);

                if (existingConversation != null)
                {
                    // FIXED: Handle nullable DriverId properly
                    var dto = new ConversationDto
                    {
                        Id = existingConversation.Id,
                        DriverId = existingConversation.DriverId ?? 0, // FIXED: Use 0 if null
                        DriverName = existingConversation.Driver?.Name ?? "Unknown Driver",
                        DriverPhone = existingConversation.Driver?.PhoneNumber ?? "No phone",
                        Topic = existingConversation.Topic ?? string.Empty,
                        LastMessageAt = existingConversation.LastMessageAt,
                        CreatedAt = existingConversation.CreatedAt,
                        IsAnswered = existingConversation.IsAnswered,
                        DepartmentId = existingConversation.DepartmentId,
                        AssignedToUserId = existingConversation.AssignedToUserId
                    };

                    // Set LastMessagePreview if messages exist
                    if (existingConversation.Messages != null && existingConversation.Messages.Any())
                    {
                        var lastMessage = existingConversation.Messages.OrderByDescending(m => m.SentAt).First();
                        dto.LastMessagePreview = lastMessage.Content?.Length > 50
                            ? lastMessage.Content.Substring(0, 50) + "..."
                            : lastMessage.Content ?? "No content";
                        dto.MessageCount = existingConversation.Messages.Count;
                    }

                    return dto;
                }

                var conversation = new Conversation
                {
                    DriverId = driverId,
                    Topic = topic ?? $"Conversation with {driver.Name}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                return new ConversationDto
                {
                    Id = conversation.Id,
                    DriverId = conversation.DriverId ?? 0, // FIXED: Use 0 if null
                    DriverName = driver.Name,
                    DriverPhone = driver.PhoneNumber ?? "No phone",
                    Topic = conversation.Topic ?? string.Empty,
                    LastMessageAt = conversation.LastMessageAt,
                    CreatedAt = conversation.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating conversation for driver {DriverId}", driverId);
                return null;
            }
        }

        private async Task<Conversation?> GetOrCreateConversationEntityAsync(int driverId, string? topic = null)
        {
            try
            {
                var driver = await _context.Drivers.FindAsync(driverId);
                if (driver == null)
                {
                    _logger.LogError("Driver not found: {DriverId}", driverId);
                    return null;
                }

                var existingConversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.DriverId == driverId);

                if (existingConversation != null)
                {
                    return existingConversation;
                }

                var conversation = new Conversation
                {
                    DriverId = driverId,
                    Topic = topic ?? $"Conversation with {driver.Name}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating conversation entity for driver {DriverId}", driverId);
                return null;
            }
        }

        public async Task<List<ConversationDto>> GetAllConversationsAsync()
        {
            try
            {
                var conversations = await _context.Conversations
                    .Include(c => c.Driver)
                    .Include(c => c.Department)
                    .Include(c => c.Messages)
                    .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                    .Select(c => new ConversationDto
                    {
                        Id = c.Id,
                        // FIXED: Handle nullable DriverId properly
                        DriverId = c.DriverId ?? 0, // Use 0 for group conversations
                        DriverName = c.Driver != null ? c.Driver.Name : "Unknown Driver",
                        DriverPhone = c.Driver != null ? c.Driver.PhoneNumber : "No phone",
                        Topic = c.Topic ?? "General Conversation",
                        LastMessageAt = c.LastMessageAt,
                        CreatedAt = c.CreatedAt,
                        MessageCount = c.Messages != null ? c.Messages.Count : 0,
                        IsAnswered = c.IsAnswered,
                        DepartmentId = c.DepartmentId,
                        DepartmentName = c.Department != null ? c.Department.Name : null,
                        AssignedToUserId = c.AssignedToUserId,
                        UnreadCount = 0
                    })
                    .ToListAsync();

                // Set LastMessagePreview for each conversation
                foreach (var convo in conversations)
                {
                    var conversation = await _context.Conversations
                        .Include(c => c.Messages)
                        .FirstOrDefaultAsync(c => c.Id == convo.Id);
                    if (conversation?.Messages != null && conversation.Messages.Any())
                    {
                        var lastMessage = conversation.Messages.OrderByDescending(m => m.SentAt).First();
                        convo.LastMessagePreview = lastMessage.Content?.Length > 50
                            ? lastMessage.Content.Substring(0, 50) + "..."
                            : lastMessage.Content ?? "No content";
                    }
                }

                _logger.LogInformation("Retrieved {Count} conversations with DriverIds: {DriverIds}",
                    conversations.Count,
                    string.Join(", ", conversations.Select(c => c.DriverId)));

                return conversations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all conversations");
                return new List<ConversationDto>();
            }
        }

        public async Task<MessageDto?> CreateMessageAsync(
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
            // NEW: Enhanced reply functionality
            int? replyToMessageId = null,
            string? replyToMessageContent = null,
            string? replyToSenderName = null)
        {
            try
            {
                var conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                {
                    _logger.LogError("Conversation not found: {ConversationId}", conversationId);
                    return null;
                }

                var message = new Message
                {
                    Content = content,
                    ConversationId = conversationId,
                    IsFromDriver = isFromDriver,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = whatsAppMessageId,
                    Context = context,
                    MessageType = messageType,
                    MediaUrl = mediaUrl,
                    FileName = fileName,
                    FileSize = fileSize,
                    MimeType = mimeType,
                    Location = location,
                    ContactName = contactName,
                    ContactPhone = contactPhone,

                    // NEW: Enhanced reply functionality
                    ReplyToMessageId = replyToMessageId,
                    ReplyToMessageContent = replyToMessageContent,
                    ReplyToSenderName = replyToSenderName
                };

                _context.Messages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                await _context.SaveChangesAsync();

                // Include reply message data in response
                Message? replyToMessage = null;
                if (replyToMessageId.HasValue)
                {
                    replyToMessage = await _context.Messages
                        .FirstOrDefaultAsync(m => m.Id == replyToMessageId.Value);
                }

                var messageDto = new MessageDto
                {
                    Id = message.Id,
                    Content = message.Content,
                    ConversationId = message.ConversationId,
                    IsFromDriver = message.IsFromDriver,
                    SentAt = message.SentAt,
                    Context = message.Context,
                    WhatsAppMessageId = message.WhatsAppMessageId,
                    Location = message.Location,
                    JobId = message.JobId,
                    Priority = message.Priority,
                    ThreadId = message.ThreadId,
                    MessageType = message.MessageType.ToString(),
                    MediaUrl = message.MediaUrl,
                    FileName = message.FileName,
                    FileSize = message.FileSize,
                    MimeType = message.MimeType,
                    ContactName = message.ContactName,
                    ContactPhone = message.ContactPhone,

                    // NEW: Enhanced reply functionality
                    ReplyToMessageId = message.ReplyToMessageId,
                    ReplyToMessageContent = message.ReplyToMessageContent,
                    ReplyToSenderName = message.ReplyToSenderName
                };

                // Include full reply message if available
                if (replyToMessage != null)
                {
                    messageDto.ReplyToMessage = new MessageDto
                    {
                        Id = replyToMessage.Id,
                        Content = replyToMessage.Content,
                        MessageType = replyToMessage.MessageType.ToString(),
                        MediaUrl = replyToMessage.MediaUrl,
                        FileName = replyToMessage.FileName,
                        IsFromDriver = replyToMessage.IsFromDriver,
                        SentAt = replyToMessage.SentAt,
                        SenderName = replyToMessage.SenderName,
                        SenderPhoneNumber = replyToMessage.SenderPhoneNumber
                    };
                }

                return messageDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message in conversation {ConversationId}", conversationId);
                return null;
            }
        }
    }
}