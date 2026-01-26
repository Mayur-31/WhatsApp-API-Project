using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.API.Services;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace DriverConnectApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly ILogger<MessagesController> _logger;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMultiTenantWhatsAppService _whatsAppService;
        private readonly IServiceProvider _serviceProvider;

        private const long MAX_IMAGE_UPLOAD = 100L * 1024 * 1024;
        private const long MAX_VIDEO_UPLOAD = 500L * 1024 * 1024;
        private const long MAX_AUDIO_UPLOAD = 100L * 1024 * 1024;
        private const long MAX_DOCUMENT_SIZE = 100L * 1024 * 1024;

        private const long WHATSAPP_IMAGE_LIMIT = 5L * 1024 * 1024;
        private const long WHATSAPP_VIDEO_LIMIT = 16L * 1024 * 1024;
        private const long WHATSAPP_AUDIO_LIMIT = 16L * 1024 * 1024;
        private const long WHATSAPP_DOCUMENT_LIMIT = 100L * 1024 * 1024;

        public MessagesController(
            ILogger<MessagesController> logger,
            AppDbContext context,
            IWebHostEnvironment environment,
            UserManager<ApplicationUser> userManager,
            IMultiTenantWhatsAppService whatsAppService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _context = context;
            _environment = environment;
            _userManager = userManager;
            _whatsAppService = whatsAppService;
            _serviceProvider = serviceProvider;
        }



        [HttpPost]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> SendMessage([FromBody] MessageRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "📤 Received message request: Type={MessageType}, IsTemplate={IsTemplate}, Conversation={ConversationId}",
                    request?.MessageType, request?.IsTemplateMessage, request?.ConversationId);

                if (request == null)
                    return BadRequest(new { message = "Invalid request body" });

                // ✅ FIX: Better deduplication check
                if (!string.IsNullOrEmpty(request.WhatsAppMessageId))
                {
                    var existingMessage = await _context.Messages
                        .FirstOrDefaultAsync(m => m.WhatsAppMessageId == request.WhatsAppMessageId);

                    if (existingMessage != null)
                    {
                        _logger.LogWarning("⚠️ Message already exists: {WhatsAppMessageId}", request.WhatsAppMessageId);
                        return Ok(await MapMessageToDto(existingMessage));
                    }
                }

                // ✅ FIX: Validate template messages
                if (request.IsTemplateMessage)
                {
                    if (string.IsNullOrEmpty(request.TemplateName))
                    {
                        return BadRequest(new { message = "TemplateName is required for template messages" });
                    }

                    // Validate 24-hour window is NOT required for templates
                    _logger.LogInformation("✅ Template message - bypassing 24-hour window check");
                }
                else if (string.IsNullOrWhiteSpace(request.Content) && request.MessageType == "Text")
                {
                    return BadRequest(new { message = "Message content is required for non-template text messages" });
                }

                // Get current user info
                ApplicationUser? currentUser = null;
                string currentUserName = "Staff";
                string? currentUserId = null;

                if (!request.IsFromDriver)
                {
                    currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        currentUserName = currentUser.FullName ?? currentUser.UserName ?? "Staff";
                        currentUserId = currentUser.Id;
                    }
                }

                if (request.IsGroupMessage && !string.IsNullOrEmpty(request.GroupId))
                {
                    return await HandleGroupMessage(request, currentUser, currentUserName, currentUserId);
                }

                return await HandleIndividualMessage(request, currentUser, currentUserName, currentUserId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while sending message");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending message");
                return StatusCode(500, new { message = "Failed to send message", error = ex.Message });
            }
        }


        private async Task<MessageDto> MapMessageToDto(Message message)
        {
            var dto = new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                Content = message.Content,
                MessageType = message.MessageType.ToString(),
                MediaUrl = message.MediaUrl,
                FileName = message.FileName,
                FileSize = message.FileSize,
                MimeType = message.MimeType,
                Location = message.Location,
                ContactName = message.ContactName,
                ContactPhone = message.ContactPhone,
                IsFromDriver = message.IsFromDriver,
                SentAt = message.SentAt,
                WhatsAppMessageId = message.WhatsAppMessageId,
                SentByUserId = message.SentByUserId,
                SentByUserName = message.SentByUserName,
                IsTemplateMessage = message.IsTemplateMessage,
                TemplateName = message.TemplateName,
                Status = message.Status.ToString(),
                SenderName = message.SenderName,
                SenderPhoneNumber = message.SenderPhoneNumber
            };

            // Load reply message if exists
            if (message.ReplyToMessageId.HasValue)
            {
                var replyMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == message.ReplyToMessageId.Value);

                if (replyMessage != null)
                {
                    dto.ReplyToMessage = new MessageDto
                    {
                        Id = replyMessage.Id,
                        Content = replyMessage.Content,
                        MessageType = replyMessage.MessageType.ToString(),
                        IsFromDriver = replyMessage.IsFromDriver,
                        SentAt = replyMessage.SentAt,
                        SenderName = replyMessage.SenderName
                    };
                }
            }

            return dto;
        }
        private async Task<IActionResult> HandleIndividualMessage(
    MessageRequest request,
    ApplicationUser? currentUser,
    string currentUserName,
    string? currentUserId)
        {
            // Validate required fields for individual messages
            if (!request.DriverId.HasValue && !request.ConversationId.HasValue && string.IsNullOrEmpty(request.PhoneNumber))
            {
                return BadRequest(new { message = "DriverId, ConversationId, or PhoneNumber is required for individual messages" });
            }

            // Get or create conversation
            Conversation? conversation = null;
            Driver? driver = null;

            if (request.ConversationId.HasValue)
            {
                conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value);
            }
            else if (request.DriverId.HasValue)
            {
                driver = await _context.Drivers.FindAsync(request.DriverId.Value);
                if (driver == null)
                    return BadRequest(new { message = "Driver not found" });

                conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.DriverId == driver.Id);
            }
            else if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                // Find or create driver by phone number
                driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == request.PhoneNumber);

                if (driver == null)
                {
                    // Auto-create driver
                    driver = new Driver
                    {
                        Name = $"Driver {request.PhoneNumber}",
                        PhoneNumber = request.PhoneNumber,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        TeamId = request.TeamId ?? 1
                    };
                    _context.Drivers.Add(driver);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("👤 Auto-created driver: {DriverId} for phone {Phone}", driver.Id, request.PhoneNumber);
                }

                conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.DriverId == driver.Id);
            }

            // Create conversation if doesn't exist
            if (conversation == null)
            {
                if (driver == null && request.DriverId.HasValue)
                {
                    driver = await _context.Drivers.FindAsync(request.DriverId.Value);
                }

                if (driver == null)
                    return BadRequest(new { message = "Could not find or create conversation - driver not found" });

                conversation = new Conversation
                {
                    DriverId = driver.Id,
                    Topic = request.Topic ?? $"Conversation with {driver.Name}",
                    CreatedAt = DateTime.UtcNow,
                    IsAnswered = false,
                    TeamId = driver.TeamId
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created conversation {ConversationId} for driver {DriverId}", conversation.Id, driver.Id);
            }

            if (conversation == null)
                return BadRequest(new { message = "Could not find or create conversation" });

            // ✅ FIXED: Generate WhatsApp Message ID if not provided
            var whatsAppMessageId = request.WhatsAppMessageId;
            if (string.IsNullOrEmpty(whatsAppMessageId))
            {
                var prefix = request.IsTemplateMessage ? "template" : "web";
                whatsAppMessageId = $"{prefix}_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}";
            }

            // Handle reply functionality
            Message? replyToMessage = null;
            if (request.ReplyToMessageId.HasValue)
            {
                replyToMessage = await _context.Messages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.Id == request.ReplyToMessageId.Value && m.ConversationId == conversation.Id);

                if (replyToMessage == null)
                {
                    _logger.LogWarning("ReplyTo message {ReplyToMessageId} not found in conversation {ConversationId}",
                        request.ReplyToMessageId, conversation.Id);
                }
            }

            // Normalize MediaUrl to absolute URL
            string? mediaUrl = request.MediaUrl;
            if (!string.IsNullOrEmpty(mediaUrl) && !mediaUrl.StartsWith("http"))
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                mediaUrl = mediaUrl.StartsWith("/") ? $"{baseUrl}{mediaUrl}" : $"{baseUrl}/{mediaUrl}";
            }

            // ✅ FIXED: Create proper message content for template messages
            string messageContent;
            MessageType messageTypeEnum;

            if (request.IsTemplateMessage)
            {
                // ✅ Get RENDERED template content (actual message text)
                messageContent = _whatsAppService.RenderTemplateForDisplay(
                    request.TemplateName ?? string.Empty,
                    request.TemplateParameters);

                messageTypeEnum = MessageType.Template;

                _logger.LogInformation("📋 Template rendered: {TemplateName} -> {Content}",
                    request.TemplateName, messageContent);
            }
            else
            {
                messageContent = request.Content ?? string.Empty;
                if (!Enum.TryParse<MessageType>(request.MessageType, out messageTypeEnum))
                {
                    messageTypeEnum = MessageType.Text;
                }
            }

            // Create message with all details
            var message = new Message
            {
                ConversationId = conversation.Id,
                Content = messageContent,
                MessageType = messageTypeEnum, // ✅ Use the correct variable
                MediaUrl = request.MediaUrl,
                FileName = request.FileName,
                FileSize = request.FileSize,
                MimeType = request.MimeType,
                Location = request.Location,
                ContactName = request.ContactName,
                ContactPhone = request.ContactPhone,
                IsFromDriver = request.IsFromDriver,
                IsGroupMessage = false,
                SenderPhoneNumber = request.SenderPhoneNumber ?? (request.IsFromDriver ? conversation.Driver?.PhoneNumber : "System"),
                SenderName = request.SenderName ?? (request.IsFromDriver ? conversation.Driver?.Name : currentUserName),
                SentAt = DateTime.UtcNow,
                WhatsAppMessageId = whatsAppMessageId,
                SentByUserId = request.IsFromDriver ? null : currentUserId,
                SentByUserName = request.IsFromDriver ? null : currentUserName,
                IsTemplateMessage = request.IsTemplateMessage,
                TemplateName = request.TemplateName,
                TemplateParametersJson = request.TemplateParameters != null
                    ? JsonSerializer.Serialize(request.TemplateParameters)
                    : null,
                Status = MessageStatus.Sent
            };

            _context.Messages.Add(message);
            conversation.LastMessageAt = message.SentAt;

            // ✅ CRITICAL: Save IMMEDIATELY so frontend gets the message
            await _context.SaveChangesAsync();

            // Send to WhatsApp service if not from driver
            if (!request.IsFromDriver && !request.IsTemplateMessage) // ✅ FIX: Don't send templates here
            {
                var teamId = conversation.TeamId ?? request.TeamId ?? 1;

                var sendRequest = new SendMessageRequest
                {
                    Content = messageContent,
                    MessageType = messageTypeEnum.ToString(),
                    MediaUrl = request.MediaUrl,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    MimeType = request.MimeType,
                    Location = request.Location,
                    DriverId = conversation.DriverId,
                    ConversationId = conversation.Id,
                    IsFromDriver = false,
                    IsTemplateMessage = request.IsTemplateMessage,
                    TemplateName = request.TemplateName,
                    TemplateParameters = request.TemplateParameters,
                    TeamId = teamId,
                    Topic = request.Topic,
                    PhoneNumber = conversation.Driver?.PhoneNumber ?? request.PhoneNumber,
                    LanguageCode = request.LanguageCode ?? "en_US",
                    WhatsAppMessageId = whatsAppMessageId
                };

                // ✅ Run in background for regular messages ONLY
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var scopedWhatsAppService = scope.ServiceProvider
                                .GetRequiredService<IMultiTenantWhatsAppService>();

                            await scopedWhatsAppService.SendMessageAsync(sendRequest, teamId);
                            _logger.LogInformation("✅ WhatsApp message sent: {MessageId}", message.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Failed to send via WhatsApp: {MessageId}", message.Id);
                    }
                });
            }
            else if (!request.IsFromDriver && request.IsTemplateMessage)
            {
                // ✅ FIX: Send template IMMEDIATELY and check success BEFORE returning
                var teamId = conversation.TeamId ?? request.TeamId ?? 1;
                var targetPhone = conversation.Driver?.PhoneNumber ?? request.PhoneNumber;

                if (!string.IsNullOrEmpty(targetPhone))
                {
                    _logger.LogInformation("🚀 Sending template via WhatsApp: {Template}", request.TemplateName);

                    var templateSuccess = await _whatsAppService.SendTemplateMessageAsync(
                        targetPhone,
                        request.TemplateName!,
                        request.TemplateParameters ?? new Dictionary<string, string>(),
                        teamId,
                        request.LanguageCode ?? "en_US"
                    );

                    if (string.IsNullOrEmpty(templateSuccess))
                    {
                        // ❌ WhatsApp failed - update message status
                        message.Status = MessageStatus.Failed;
                        await _context.SaveChangesAsync();

                        _logger.LogError("❌ WhatsApp template sending failed: {Template}", request.TemplateName);

                        // Return error to frontend
                        return StatusCode(500, new
                        {
                            message = "Failed to send template via WhatsApp. Check logs.",
                            templateName = request.TemplateName,
                            success = false
                        });
                    }

                    _logger.LogInformation("✅ Template sent successfully: {Template}", request.TemplateName);
                }
            }

            // Return DTO
            var messageDto = await MapMessageToDto(message);
            return Ok(messageDto);
        }

        private async Task<IActionResult> HandleGroupMessage(
            MessageRequest request,
            ApplicationUser? currentUser,
            string currentUserName,
            string? currentUserId)
        {
            try
            {
                _logger.LogInformation("Processing group message request for group: {GroupId}", request.GroupId);

                if (string.IsNullOrEmpty(request.GroupId))
                    return BadRequest(new { message = "GroupId is required for group messages" });

                var conversation = await _context.Conversations
                    .Include(c => c.Group)
                        .ThenInclude(g => g!.Participants)
                    .FirstOrDefaultAsync(c => c.WhatsAppGroupId == request.GroupId);

                if (conversation == null)
                {
                    _logger.LogWarning("Group conversation not found for WhatsAppGroupId: {GroupId}", request.GroupId);
                    return NotFound(new { message = "Group conversation not found" });
                }

                if (!Enum.TryParse<MessageType>(request.MessageType, out var messageType))
                    messageType = MessageType.Text;

                Message? replyToMessage = null;
                if (request.ReplyToMessageId.HasValue)
                {
                    replyToMessage = await _context.Messages
                        .FirstOrDefaultAsync(m => m.Id == request.ReplyToMessageId.Value && m.ConversationId == conversation.Id);
                }

                string? mediaUrl = request.MediaUrl;
                if (!string.IsNullOrEmpty(mediaUrl) && !mediaUrl.StartsWith("http"))
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    mediaUrl = mediaUrl.StartsWith("/") ? $"{baseUrl}{mediaUrl}" : $"{baseUrl}/{mediaUrl}";
                }

                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Content = request.Content,
                    MessageType = messageType,
                    MediaUrl = mediaUrl,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    MimeType = request.MimeType,
                    Location = request.Location,
                    ContactName = request.ContactName,
                    ContactPhone = request.ContactPhone,
                    IsFromDriver = false,
                    IsGroupMessage = true,
                    SenderPhoneNumber = "System",
                    SenderName = currentUserName,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = $"web_group_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}",
                    SentByUserId = currentUserId,
                    SentByUserName = currentUserName,
                    ReplyToMessageId = request.ReplyToMessageId,
                    ReplyToMessageContent = request.ReplyToMessageContent,
                    ReplyToSenderName = request.ReplyToSenderName,
                    IsTemplateMessage = request.IsTemplateMessage,
                    TemplateName = request.TemplateName,
                    TemplateParametersJson = request.TemplateParameters != null
                        ? JsonSerializer.Serialize(request.TemplateParameters)
                        : null
                };

                _context.Messages.Add(message);
                conversation.LastMessageAt = message.SentAt;

                var teamId = conversation.TeamId ?? 1;

                var sendRequest = new SendMessageRequest
                {
                    Content = request.Content,
                    MessageType = request.MessageType,
                    MediaUrl = mediaUrl,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    MimeType = request.MimeType,
                    Location = request.Location,
                    ConversationId = conversation.Id,
                    IsGroupMessage = true,
                    GroupId = request.GroupId,
                    IsFromDriver = false,
                    IsTemplateMessage = request.IsTemplateMessage,
                    TemplateName = request.TemplateName,
                    TemplateParameters = request.TemplateParameters,
                    TeamId = teamId,
                    Topic = request.Topic
                };

                await _whatsAppService.SendMessageAsync(sendRequest, teamId);

                await _context.SaveChangesAsync();

                var messageDto = new MessageDto
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType.ToString(),
                    MediaUrl = message.MediaUrl,
                    FileName = message.FileName,
                    FileSize = message.FileSize,
                    MimeType = message.MimeType,
                    Location = message.Location,
                    ContactName = message.ContactName,
                    ContactPhone = message.ContactPhone,
                    IsFromDriver = message.IsFromDriver,
                    IsGroupMessage = message.IsGroupMessage,
                    SenderPhoneNumber = message.SenderPhoneNumber,
                    SenderName = message.SenderName,
                    SentAt = message.SentAt,
                    WhatsAppMessageId = message.WhatsAppMessageId,
                    SentByUserId = message.SentByUserId,
                    SentByUserName = message.SentByUserName,
                    ReplyToMessageId = message.ReplyToMessageId,
                    ReplyToMessageContent = message.ReplyToMessageContent,
                    ReplyToSenderName = message.ReplyToSenderName,
                    IsTemplateMessage = message.IsTemplateMessage,
                    TemplateName = message.TemplateName
                };

                _logger.LogInformation("Group message sent successfully to group: {GroupId}", request.GroupId);

                return Ok(messageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling group message");
                return StatusCode(500, new { message = "Failed to send group message", error = ex.Message });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateMessageStatus(int id, [FromBody] UpdateMessageStatusRequest request)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.Recipients)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (message == null)
                    return NotFound(new { message = "Message not found" });

                // Update message status
                if (Enum.TryParse<MessageStatus>(request.Status, out var status))
                {
                    message.Status = status;
                }

                // Update recipient status for group messages
                if (message.IsGroupMessage && (request.DriverId.HasValue || request.GroupParticipantId.HasValue))
                {
                    var recipient = message.Recipients.FirstOrDefault(r =>
                        r.DriverId == request.DriverId || r.GroupParticipantId == request.GroupParticipantId);

                    if (recipient != null)
                    {
                        recipient.Status = message.Status;

                        if (message.Status == MessageStatus.Delivered)
                            recipient.DeliveredAt = DateTime.UtcNow;
                        else if (message.Status == MessageStatus.Read)
                            recipient.ReadAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Message status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message status for message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to update message status", error = ex.Message });
            }
        }

        [HttpPost("{id}/react")]
        public async Task<IActionResult> ReactToMessage(int id, [FromBody] ReactToMessageRequest request)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                    return NotFound(new { message = "Message not found" });

                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserId = currentUser?.Id;

                // Check if user already reacted
                var existingReaction = await _context.MessageReactions
                    .FirstOrDefaultAsync(r => r.MessageId == id && r.UserId == currentUserId);

                if (existingReaction != null)
                {
                    if (existingReaction.Reaction == request.Reaction)
                    {
                        // Remove reaction if same emoji
                        _context.MessageReactions.Remove(existingReaction);
                    }
                    else
                    {
                        // Update reaction
                        existingReaction.Reaction = request.Reaction;
                        existingReaction.ReactedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Add new reaction
                    var reaction = new MessageReaction
                    {
                        MessageId = id,
                        UserId = currentUserId,
                        Reaction = request.Reaction,
                        ReactedAt = DateTime.UtcNow
                    };
                    _context.MessageReactions.Add(reaction);
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Reaction updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reacting to message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to react to message", error = ex.Message });
            }
        }

        [HttpPost("{id}/forward")]
        public async Task<IActionResult> ForwardMessage(int id, [FromBody] ForwardMessageRequest request)
        {
            try
            {
                _logger.LogInformation("🚀 Starting to forward message {MessageId} to {Count} conversations: {ConversationIds}",
                    id, request.ConversationIds?.Count ?? 0, string.Join(", ", request.ConversationIds ?? new List<int>()));

                // Validate request
                if (request.ConversationIds == null || !request.ConversationIds.Any())
                {
                    _logger.LogWarning("❌ No conversation IDs provided for forwarding message {MessageId}", id);
                    return BadRequest(new
                    {
                        message = "Please select at least one conversation",
                        error = "No conversations selected"
                    });
                }

                // Find the original message
                var originalMessage = await _context.Messages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (originalMessage == null)
                {
                    _logger.LogWarning("❌ Message {MessageId} not found for forwarding", id);
                    return NotFound(new { message = "Message not found" });
                }

                _logger.LogInformation("✅ Found original message: {Content} (Type: {MessageType})",
                    originalMessage.Content, originalMessage.MessageType);

                var currentUser = await _userManager.GetUserAsync(User);
                var forwardedMessages = new List<MessageDto>();
                var validConversationIds = new List<int>();

                // Validate all conversation IDs first
                foreach (var conversationId in request.ConversationIds)
                {
                    var conversationExists = await _context.Conversations
                        .AnyAsync(c => c.Id == conversationId);

                    if (conversationExists)
                    {
                        validConversationIds.Add(conversationId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Conversation {ConversationId} not found, skipping", conversationId);
                    }
                }

                if (validConversationIds.Count == 0)
                {
                    _logger.LogWarning("❌ No valid conversations found for forwarding");
                    return BadRequest(new
                    {
                        message = "No valid conversations found for forwarding",
                        error = "All selected conversations were invalid"
                    });
                }

                _logger.LogInformation("✅ Processing forward to {Count} valid conversations", validConversationIds.Count);

                // Process each valid conversation
                foreach (var conversationId in validConversationIds)
                {
                    _logger.LogInformation("📨 Processing forward to conversation {ConversationId}", conversationId);

                    var conversation = await _context.Conversations
                        .Include(c => c.Driver)
                        .FirstOrDefaultAsync(c => c.Id == conversationId);

                    if (conversation == null)
                    {
                        _logger.LogWarning("⚠️ Conversation {ConversationId} not found, skipping", conversationId);
                        continue;
                    }

                    _logger.LogInformation("✅ Found conversation: {ConversationName} (ID: {ConversationId})",
                        conversation.IsGroupConversation ? conversation.GroupName : conversation.Driver?.Name,
                        conversationId);

                    // Create forwarded message
                    var forwardedMessage = new Message
                    {
                        ConversationId = conversationId,
                        Content = !string.IsNullOrEmpty(request.CustomMessage)
                            ? request.CustomMessage
                            : originalMessage.Content,
                        MessageType = originalMessage.MessageType,
                        MediaUrl = originalMessage.MediaUrl,
                        FileName = originalMessage.FileName,
                        FileSize = originalMessage.FileSize,
                        MimeType = originalMessage.MimeType,
                        IsFromDriver = false, // Forwarded messages are always from staff
                        SentAt = DateTime.UtcNow,
                        WhatsAppMessageId = $"forwarded_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}",
                        ForwardCount = 0,
                        ForwardedFromMessageId = originalMessage.Id,
                        IsGroupMessage = conversation.IsGroupConversation,
                        SenderName = currentUser?.FullName ?? "Staff",
                        SenderPhoneNumber = "System",
                        SentByUserId = currentUser?.Id,
                        SentByUserName = currentUser?.FullName ?? "Staff",
                        Status = MessageStatus.Sent
                    };

                    _context.Messages.Add(forwardedMessage);

                    // Update conversation last message time
                    conversation.LastMessageAt = DateTime.UtcNow;

                    // Update original message forward count
                    originalMessage.ForwardCount += 1;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Successfully forwarded message to conversation {ConversationId} as message {ForwardedMessageId}",
                        conversationId, forwardedMessage.Id);

                    // Convert to DTO for response
                    var messageDto = new MessageDto
                    {
                        Id = forwardedMessage.Id,
                        ConversationId = forwardedMessage.ConversationId,
                        Content = forwardedMessage.Content,
                        MessageType = forwardedMessage.MessageType.ToString(),
                        MediaUrl = forwardedMessage.MediaUrl,
                        FileName = forwardedMessage.FileName,
                        FileSize = forwardedMessage.FileSize,
                        IsFromDriver = forwardedMessage.IsFromDriver,
                        SentAt = forwardedMessage.SentAt,
                        WhatsAppMessageId = forwardedMessage.WhatsAppMessageId,
                        ForwardCount = forwardedMessage.ForwardCount,
                        ForwardedFromMessageId = forwardedMessage.ForwardedFromMessageId,
                        IsGroupMessage = forwardedMessage.IsGroupMessage,
                        SenderName = forwardedMessage.SenderName,
                        SentByUserId = forwardedMessage.SentByUserId,
                        SentByUserName = forwardedMessage.SentByUserName,
                        Status = forwardedMessage.Status.ToString()
                    };

                    forwardedMessages.Add(messageDto);
                }

                if (forwardedMessages.Count == 0)
                {
                    _logger.LogWarning("❌ No messages were forwarded - all target conversations were invalid");
                    return BadRequest(new
                    {
                        message = "No valid conversations found for forwarding",
                        error = "All target conversations were invalid"
                    });
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("🎉 Successfully forwarded message {MessageId} to {Count} conversations",
                    id, forwardedMessages.Count);

                return Ok(new
                {
                    message = $"Message forwarded successfully to {forwardedMessages.Count} conversation(s)",
                    forwardedMessages = forwardedMessages,
                    originalMessageId = id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error forwarding message {MessageId}", id);
                return StatusCode(500, new
                {
                    message = "Failed to forward message",
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        [HttpPut("{id}/pin")]
        public async Task<IActionResult> PinMessage(int id, [FromBody] PinMessageRequest request)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                    return NotFound(new { message = "Message not found" });

                message.IsPinned = request.IsPinned;
                message.PinnedAt = request.IsPinned ? DateTime.UtcNow : null;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Message {(request.IsPinned ? "pinned" : "unpinned")} successfully",
                    isPinned = message.IsPinned,
                    pinnedAt = message.PinnedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to pin message", error = ex.Message });
            }
        }

        [HttpPut("{id}/star")]
        public async Task<IActionResult> StarMessage(int id, [FromBody] StarMessageRequest request)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                    return NotFound(new { message = "Message not found" });

                message.IsStarred = request.IsStarred;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Message {(request.IsStarred ? "starred" : "unstarred")} successfully",
                    isStarred = message.IsStarred
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starring message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to star message", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                    return NotFound(new { message = "Message not found" });

                var currentUser = await _userManager.GetUserAsync(User);

                // Soft delete
                message.IsDeleted = true;
                message.DeletedAt = DateTime.UtcNow;
                message.DeletedByUserId = currentUser?.Id;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Message deleted successfully",
                    messageId = id,
                    deletedAt = message.DeletedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to delete message", error = ex.Message });
            }
        }

        [HttpGet("{id}/info")]
        public async Task<IActionResult> GetMessageInfo(int id)
        {
            try
            {
                _logger.LogInformation("Getting message info for message {MessageId}", id);

                var message = await _context.Messages
                    .Include(m => m.Recipients)
                        .ThenInclude(r => r.Driver)
                    .Include(m => m.Recipients)
                        .ThenInclude(r => r.GroupParticipant)
                    .Include(m => m.Reactions)
                        .ThenInclude(r => r.Driver)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (message == null)
                {
                    _logger.LogWarning("Message {MessageId} not found for info", id);
                    return NotFound(new { message = "Message not found" });
                }

                // Get user information for reactions
                var reactionsWithUserInfo = new List<MessageReactionDto>();
                foreach (var reaction in message.Reactions)
                {
                    var reactionDto = new MessageReactionDto
                    {
                        Id = reaction.Id,
                        MessageId = reaction.MessageId,
                        UserId = reaction.UserId,
                        DriverId = reaction.DriverId,
                        Reaction = reaction.Reaction,
                        ReactedAt = reaction.ReactedAt,
                        DriverName = reaction.Driver?.Name
                    };

                    // Get user name if UserId is present
                    if (!string.IsNullOrEmpty(reaction.UserId))
                    {
                        var user = await _userManager.FindByIdAsync(reaction.UserId);
                        reactionDto.UserName = user?.FullName ?? user?.UserName ?? "Unknown User";
                    }

                    reactionsWithUserInfo.Add(reactionDto);
                }

                var response = new MessageInfoResponse
                {
                    Message = new MessageDto
                    {
                        Id = message.Id,
                        Content = message.Content,
                        MessageType = message.MessageType.ToString(),
                        SentAt = message.SentAt,
                        Status = message.Status.ToString(),
                        IsStarred = message.IsStarred,
                        IsPinned = message.IsPinned,
                        ForwardCount = message.ForwardCount,
                        IsDeleted = message.IsDeleted,
                        IsFromDriver = message.IsFromDriver,
                        SenderName = message.SenderName,
                        SentByUserName = message.SentByUserName,
                        IsTemplateMessage = message.IsTemplateMessage,
                        TemplateName = message.TemplateName
                    },
                    Recipients = message.Recipients.Select(r => new MessageRecipientDto
                    {
                        Id = r.Id,
                        MessageId = r.MessageId,
                        DriverId = r.DriverId,
                        GroupParticipantId = r.GroupParticipantId,
                        Status = r.Status.ToString(),
                        DeliveredAt = r.DeliveredAt,
                        ReadAt = r.ReadAt,
                        HasSeen = r.HasSeen,
                        SeenAt = r.SeenAt,
                        ParticipantName = r.Driver?.Name ?? r.GroupParticipant?.ParticipantName,
                        PhoneNumber = r.Driver?.PhoneNumber ?? r.GroupParticipant?.PhoneNumber,
                        DriverName = r.Driver?.Name
                    }).ToList(),
                    Reactions = reactionsWithUserInfo,
                    TotalRecipients = message.Recipients.Count,
                    DeliveredCount = message.Recipients.Count(r => r.Status >= MessageStatus.Delivered),
                    ReadCount = message.Recipients.Count(r => r.Status >= MessageStatus.Read)
                };

                _logger.LogInformation("Successfully retrieved message info for message {MessageId}", id);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message info for message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to get message info", error = ex.Message });
            }
        }

        [HttpGet("{id}/copy")]
        public async Task<IActionResult> CopyMessage(int id)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                    return NotFound(new { message = "Message not found" });

                return Ok(new
                {
                    content = message.Content,
                    messageType = message.MessageType.ToString(),
                    canCopy = !string.IsNullOrEmpty(message.Content)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to copy message", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MessageDto>> GetMessage(int id)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.ReplyToMessage)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (message == null)
                    return NotFound(new { message = "Message not found" });

                var messageDto = new MessageDto
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType.ToString(),
                    MediaUrl = message.MediaUrl,
                    FileName = message.FileName,
                    FileSize = message.FileSize,
                    MimeType = message.MimeType,
                    Location = message.Location,
                    ContactName = message.ContactName,
                    ContactPhone = message.ContactPhone,
                    IsFromDriver = message.IsFromDriver,
                    SentAt = message.SentAt,
                    WhatsAppMessageId = message.WhatsAppMessageId,
                    JobId = message.JobId,
                    Context = message.Context,
                    Priority = message.Priority,
                    ThreadId = message.ThreadId,
                    IsGroupMessage = message.IsGroupMessage,
                    SenderPhoneNumber = message.SenderPhoneNumber,
                    SenderName = message.SenderName,
                    SentByUserId = message.SentByUserId,
                    SentByUserName = message.SentByUserName,
                    ReplyToMessageId = message.ReplyToMessageId,
                    ReplyToMessageContent = message.ReplyToMessageContent,
                    ReplyToSenderName = message.ReplyToSenderName,
                    IsTemplateMessage = message.IsTemplateMessage,
                    TemplateName = message.TemplateName
                };

                // Include full reply message if available
                if (message.ReplyToMessage != null)
                {
                    messageDto.ReplyToMessage = new MessageDto
                    {
                        Id = message.ReplyToMessage.Id,
                        Content = message.ReplyToMessage.Content,
                        MessageType = message.ReplyToMessage.MessageType.ToString(),
                        MediaUrl = message.ReplyToMessage.MediaUrl,
                        FileName = message.ReplyToMessage.FileName,
                        IsFromDriver = message.ReplyToMessage.IsFromDriver,
                        SentAt = message.ReplyToMessage.SentAt,
                        SenderName = message.ReplyToMessage.SenderName,
                        SenderPhoneNumber = message.ReplyToMessage.SenderPhoneNumber,
                        SentByUserId = message.ReplyToMessage.SentByUserId,
                        SentByUserName = message.ReplyToMessage.SentByUserName
                    };
                }

                return Ok(messageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId}", id);
                return StatusCode(500, new { message = "Failed to retrieve message", error = ex.Message });
            }
        }

        [HttpPost("upload-media")]
        [RequestSizeLimit(524288000)] // 500MB
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> UploadMedia(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file uploaded" });

                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileSizeMB = file.Length / (1024.0 * 1024.0);

                // File type detection
                var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".3gp" };
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var audioExtensions = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac" };
                var documentExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar", ".7z", ".tar", ".gz" };

                string messageType;
                bool needsCompression = false;
                string processingInfo = "";

                // Determine file type and processing strategy
                if (imageExtensions.Contains(fileExtension))
                {
                    messageType = "Image";

                    // Check upload limit
                    if (file.Length > MAX_IMAGE_UPLOAD)
                    {
                        return BadRequest(new
                        {
                            message = $"Image files cannot exceed {MAX_IMAGE_UPLOAD / (1024.0 * 1024.0):F0}MB.",
                            details = $"Your file: {fileSizeMB:F1}MB",
                            suggestion = "Please reduce the image size before uploading.",
                            originalSize = fileSizeMB,
                            maxSize = MAX_IMAGE_UPLOAD / (1024.0 * 1024.0)
                        });
                    }

                    // Flag for compression if over WhatsApp limit
                    needsCompression = file.Length > WHATSAPP_IMAGE_LIMIT;
                }
                else if (videoExtensions.Contains(fileExtension))
                {
                    if (file.Length > MAX_VIDEO_UPLOAD)
                    {
                        return BadRequest(new
                        {
                            message = $"Video files cannot exceed {MAX_VIDEO_UPLOAD / (1024.0 * 1024.0):F0}MB.",
                            details = $"Your file: {fileSizeMB:F1}MB",
                            suggestion = "Please compress the video using external software before uploading.",
                            originalSize = fileSizeMB,
                            maxSize = MAX_VIDEO_UPLOAD / (1024.0 * 1024.0)
                        });
                    }

                    // Smart handling: Convert large videos to documents
                    if (file.Length > WHATSAPP_VIDEO_LIMIT)
                    {
                        messageType = "Document";
                        processingInfo = $"Video ({fileSizeMB:F1}MB) automatically converted to document attachment (WhatsApp video limit: 16MB)";
                        _logger.LogInformation("Converting large video to document: {FileName}, Size: {Size}MB", file.FileName, fileSizeMB);
                    }
                    else
                    {
                        messageType = "Video";
                    }
                }
                else if (audioExtensions.Contains(fileExtension))
                {
                    if (file.Length > MAX_AUDIO_UPLOAD)
                    {
                        return BadRequest(new
                        {
                            message = $"Audio files cannot exceed {MAX_AUDIO_UPLOAD / (1024.0 * 1024.0):F0}MB.",
                            details = $"Your file: {fileSizeMB:F1}MB",
                            suggestion = "Please compress the audio file before uploading.",
                            originalSize = fileSizeMB,
                            maxSize = MAX_AUDIO_UPLOAD / (1024.0 * 1024.0)
                        });
                    }

                    // Convert large audio to documents
                    if (file.Length > WHATSAPP_AUDIO_LIMIT)
                    {
                        messageType = "Document";
                        processingInfo = $"Audio ({fileSizeMB:F1}MB) automatically converted to document attachment (WhatsApp audio limit: 16MB)";
                    }
                    else
                    {
                        messageType = "Audio";
                    }
                }
                else
                {
                    messageType = "Document";

                    if (file.Length > MAX_DOCUMENT_SIZE)
                    {
                        return BadRequest(new
                        {
                            message = $"Document files cannot exceed {MAX_DOCUMENT_SIZE / (1024.0 * 1024.0):F0}MB.",
                            details = $"Your file: {fileSizeMB:F1}MB",
                            suggestion = "Please split the document or compress it before uploading.",
                            originalSize = fileSizeMB,
                            maxSize = MAX_DOCUMENT_SIZE / (1024.0 * 1024.0)
                        });
                    }
                }

                // Create uploads directory
                var uploadsDir = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                long finalFileSize = file.Length;
                bool wasCompressed = false;
                string? compressionInfo = null;

                // Apply compression for images if needed
                if (needsCompression)
                {
                    _logger.LogInformation("Compressing image from {OriginalSizeMB:F1}MB to fit {MaxSizeMB:F0}MB limit",
                        fileSizeMB, WHATSAPP_IMAGE_LIMIT / (1024.0 * 1024.0));

                    var compressionResult = await CompressImageAsync(file, filePath, WHATSAPP_IMAGE_LIMIT);
                    finalFileSize = compressionResult.FileSize;
                    wasCompressed = compressionResult.WasCompressed;
                    compressionInfo = compressionResult.Info;

                    _logger.LogInformation("Image compressed: {CompressionInfo}", compressionInfo);
                }
                else
                {
                    // Save file directly
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await file.CopyToAsync(stream);
                }

                // Create absolute URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var mediaUrl = $"{baseUrl}/uploads/{fileName}";

                var response = new FileUploadResponse
                {
                    FileName = file.FileName,
                    StoredFileName = fileName,
                    FileSize = finalFileSize,
                    MimeType = file.ContentType,
                    MediaUrl = mediaUrl,
                    MessageType = messageType,
                    WasCompressed = wasCompressed,
                    CompressionInfo = compressionInfo ?? processingInfo,
                    OriginalSize = file.Length
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { message = "Failed to upload file", error = ex.Message });
            }
        }

        private string GenerateTemplateContent(string templateName, Dictionary<string, string> parameters)
        {
            if (parameters == null || !parameters.Any())
                return $"Template: {templateName}";

            // Get parameter values in order
            var paramValues = parameters
                .OrderBy(p => p.Key)
                .Select(p => p.Value)
                .ToList();

            // Map template names to actual content
            return templateName.ToLower() switch
            {
                "hello_world" when paramValues.Count >= 1 => $"Hello {paramValues[0]}, welcome to our service!",
                "welcome_message" when paramValues.Count >= 2 => $"Welcome {paramValues[0]} to {paramValues[1]}!",
                "booking_confirmation" when paramValues.Count >= 3 => $"Your booking #{paramValues[0]} is confirmed for {paramValues[1]} at {paramValues[2]}.",
                "payment_reminder" when paramValues.Count >= 3 => $"Hello {paramValues[0]}, please pay {paramValues[1]} by {paramValues[2]}.",
                "order_shipped" when paramValues.Count >= 2 => $"Your order #{paramValues[0]} has been shipped. Tracking: {paramValues[1]}",
                "delivery_update" when paramValues.Count >= 2 => $"Your delivery #{paramValues[0]} is on the way. ETA: {paramValues[1]}",
                "appointment_reminder" when paramValues.Count >= 3 => $"Reminder: Your appointment with {paramValues[0]} is on {paramValues[1]} at {paramValues[2]}.",
                "service_completed" when paramValues.Count >= 2 => $"Service #{paramValues[0]} has been completed. {paramValues[1]}",
                "invoice_sent" when paramValues.Count >= 3 => $"Invoice #{paramValues[0]} for {paramValues[1]} has been sent. Amount: {paramValues[2]}",
                "feedback_request" when paramValues.Count >= 1 => $"Hello {paramValues[0]}, we'd love your feedback on our service!",

                // Add more templates as needed

                _ => $"{templateName}: {string.Join(", ", paramValues)}"
            };
        }

        private async Task<CompressionResult> CompressImageAsync(IFormFile file, string outputPath, long targetSize)
        {
            using var inputStream = file.OpenReadStream();
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputStream);

            var originalSize = file.Length;
            var originalDimensions = $"{image.Width}x{image.Height}";

            int quality = 85;
            int maxWidth = image.Width;
            int maxHeight = image.Height;

            MemoryStream? bestStream = null;
            int bestQuality = quality;
            (int, int) bestDimensions = (maxWidth, maxHeight);

            // Progressive compression algorithm
            while (quality >= 40)
            {
                using var inputStreamForIteration = file.OpenReadStream();
                using var testImage = await SixLabors.ImageSharp.Image.LoadAsync(inputStreamForIteration);

                // Resize if dimensions are too large
                if (maxWidth > 1920 || maxHeight > 1080)
                {
                    var resizeOptions = new ResizeOptions
                    {
                        Size = new Size(maxWidth, maxHeight),
                        Mode = ResizeMode.Max,
                        Compand = true
                    };
                    testImage.Mutate(x => x.Resize(resizeOptions));
                }

                using var outputStream = new MemoryStream();

                // Use appropriate encoder based on file type
                if (file.ContentType.Contains("png") || file.FileName.ToLower().EndsWith(".png"))
                {
                    var encoder = new JpegEncoder { Quality = quality };
                    await testImage.SaveAsync(outputStream, encoder);
                }
                else
                {
                    var encoder = new JpegEncoder { Quality = quality };
                    await testImage.SaveAsync(outputStream, encoder);
                }

                var compressedSize = outputStream.Length;

                // Check if we've reached target size or minimum quality
                if (compressedSize <= targetSize || quality == 40)
                {
                    bestStream = new MemoryStream(outputStream.ToArray());
                    bestQuality = quality;
                    bestDimensions = (testImage.Width, testImage.Height);

                    if (compressedSize <= targetSize)
                        break;
                }

                // Reduce quality and dimensions for next iteration
                quality -= 15;
                maxWidth = (int)(maxWidth * 0.8);
                maxHeight = (int)(maxHeight * 0.8);

                // Set minimum dimensions
                if (maxWidth < 400) maxWidth = 400;
                if (maxHeight < 400) maxHeight = 400;
            }

            if (bestStream != null)
            {
                bestStream.Position = 0;
                using var fileStream = new FileStream(outputPath, FileMode.Create);
                await bestStream.CopyToAsync(fileStream);
                bestStream.Dispose();

                var finalSize = new FileInfo(outputPath).Length;
                var sizeReduction = ((originalSize - finalSize) * 100.0 / originalSize);

                return new CompressionResult
                {
                    FileSize = finalSize,
                    WasCompressed = true,
                    Info = $"Compressed from {originalSize / (1024.0 * 1024.0):F1}MB to {finalSize / (1024.0 * 1024.0):F1}MB ({sizeReduction:F1}% reduction). " +
                           $"Dimensions: {originalDimensions} → {bestDimensions.Item1}x{bestDimensions.Item2}. Quality: {bestQuality}%"
                };
            }

            // Fallback compression with minimum settings
            using var fallbackStream = new FileStream(outputPath, FileMode.Create);
            var fallbackEncoder = new JpegEncoder { Quality = 40 };
            await image.SaveAsync(fallbackStream, fallbackEncoder);

            var fallbackSize = new FileInfo(outputPath).Length;

            return new CompressionResult
            {
                FileSize = fallbackSize,
                WasCompressed = true,
                Info = $"Compressed from {originalSize / (1024.0 * 1024.0):F1}MB to {fallbackSize / (1024.0 * 1024.0):F1}MB using fallback settings."
            };
        }

        [HttpGet("conversation/{conversationId}")]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            try
            {
                if (conversationId <= 0)
                    return BadRequest(new { message = "Valid conversation ID is required" });

                var messages = await _context.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .Include(m => m.ReplyToMessage)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        ConversationId = m.ConversationId,
                        Content = m.Content,
                        MessageType = m.MessageType.ToString(),
                        MediaUrl = m.MediaUrl,
                        FileName = m.FileName,
                        FileSize = m.FileSize,
                        MimeType = m.MimeType,
                        Location = m.Location,
                        ContactName = m.ContactName,
                        ContactPhone = m.ContactPhone,
                        IsFromDriver = m.IsFromDriver,
                        SentAt = m.SentAt,
                        WhatsAppMessageId = m.WhatsAppMessageId,
                        JobId = m.JobId,
                        Context = m.Context,
                        Priority = m.Priority,
                        ThreadId = m.ThreadId,
                        SentByUserId = m.SentByUserId,
                        SentByUserName = m.SentByUserName,
                        IsGroupMessage = m.IsGroupMessage,
                        SenderPhoneNumber = m.SenderPhoneNumber,
                        SenderName = m.SenderName,
                        ReplyToMessageId = m.ReplyToMessageId,
                        ReplyToMessageContent = m.ReplyToMessageContent,
                        ReplyToSenderName = m.ReplyToSenderName,
                        IsTemplateMessage = m.IsTemplateMessage,
                        TemplateName = m.TemplateName,
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
                            SenderPhoneNumber = m.ReplyToMessage.SenderPhoneNumber,
                            SentByUserId = m.ReplyToMessage.SentByUserId,
                            SentByUserName = m.ReplyToMessage.SentByUserName
                        } : null
                    })
                    .ToListAsync();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
                return StatusCode(500, new { message = "Failed to retrieve messages", error = ex.Message });
            }
        }

        // NEW: Check 24-hour window API endpoint
        [HttpGet("conversation/{conversationId}/can-send")]
        public async Task<IActionResult> CheckCanSendMessage(int conversationId)
        {
            try
            {
                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                    return NotFound(new { message = "Conversation not found" });

                bool canSendNonTemplate = conversation.CanSendNonTemplateMessages();

                return Ok(new
                {
                    conversationId,
                    canSendNonTemplateMessages = canSendNonTemplate,
                    lastInboundMessageAt = conversation.LastInboundMessageAt,
                    requiresTemplate = !canSendNonTemplate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking 24-hour window for conversation {ConversationId}", conversationId);
                return StatusCode(500, new { message = "Failed to check message sending capability", error = ex.Message });
            }
        }


        private async Task UpdateMessageWithWhatsAppId(
    string phoneNumber,
    string templateName,
    string whatsAppMessageId)
        {
            // Find recent template message without WhatsApp ID
            var message = await _context.Messages
                .Where(m => m.SenderPhoneNumber == phoneNumber)
                .Where(m => m.TemplateName == templateName)
                .Where(m => m.WhatsAppMessageId == null)
                .Where(m => m.SentAt > DateTime.UtcNow.AddMinutes(-5)) // Recent messages only
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            if (message != null)
            {
                message.WhatsAppMessageId = whatsAppMessageId;
                message.Status = MessageStatus.Sent;
                await _context.SaveChangesAsync();
            }
        }
        // NEW: Send template message endpoint - USE EXISTING MODEL FROM WHATSAPP CONTROLLER
        [HttpPost("send-template")]
        public async Task<IActionResult> SendTemplateMessage([FromBody] SendTemplateByDriverIdRequest request)
        {
            try
            {
                _logger.LogInformation("🚀 Sending template message to driver {DriverId} with template {TemplateName}",
                    request.DriverId, request.TemplateName);

                if (request == null || string.IsNullOrEmpty(request.TemplateName))
                    return BadRequest(new { message = "Template name is required" });

                var driver = await _context.Drivers.FindAsync(request.DriverId);
                if (driver == null)
                    return NotFound(new { message = "Driver not found" });

                if (!driver.TeamId.HasValue)
                    return BadRequest(new { message = "Driver is not assigned to a team" });

                // Get or create conversation
                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.DriverId == request.DriverId);

                if (conversation == null)
                {
                    conversation = new Conversation
                    {
                        DriverId = driver.Id,
                        Topic = "Template Message",
                        CreatedAt = DateTime.UtcNow,
                        IsAnswered = false,
                        TeamId = driver.TeamId
                    };
                    _context.Conversations.Add(conversation);
                    await _context.SaveChangesAsync();
                }

                // Get current user
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.UserName ?? "Staff";
                var currentUserId = currentUser?.Id;

                // ✅ Send template via WhatsApp API and get the message ID
                var whatsAppMessageId = await _whatsAppService.SendTemplateMessageAsync(
                    driver.PhoneNumber,
                    request.TemplateName,
                    request.TemplateParameters ?? new Dictionary<string, string>(),
                    driver.TeamId.Value,
                    request.LanguageCode
                );

                if (string.IsNullOrEmpty(whatsAppMessageId))
                {
                    return StatusCode(500, new { message = "Failed to send template message via WhatsApp API" });
                }

                // ✅ Create message with WhatsApp ID
                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Content = $"📋 Template: {request.TemplateName}",
                    MessageType = MessageType.Template,
                    IsFromDriver = false,
                    IsGroupMessage = false,
                    SenderPhoneNumber = "System",
                    SenderName = currentUserName,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = whatsAppMessageId,
                    SentByUserId = currentUserId,
                    SentByUserName = currentUserName,
                    IsTemplateMessage = true,
                    TemplateName = request.TemplateName,
                    TemplateParametersJson = request.TemplateParameters != null
                        ? JsonSerializer.Serialize(request.TemplateParameters)
                        : null,
                    Status = MessageStatus.Sent
                };

                _context.Messages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Template message sent successfully to driver {DriverId}", driver.Id);

                return Ok(new
                {
                    message = "Template message sent successfully",
                    messageId = message.Id,
                    conversationId = conversation.Id,
                    isTemplate = true,
                    whatsAppMessageId = message.WhatsAppMessageId,
                    displayContent = message.Content,
                    templateName = request.TemplateName,
                    templateParameters = request.TemplateParameters
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending template message");
                return StatusCode(500, new { message = "Failed to send template message", error = ex.Message });
            }
        }
    }


    public class CompressionResult
    {
        public long FileSize { get; set; }
        public bool WasCompressed { get; set; }
        public string? Info { get; set; }
    }

    // REQUEST MODEL FOR TEMPLATE MESSAGES BY DRIVER ID
    public class SendTemplateByDriverIdRequest
    {
        public int DriverId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public Dictionary<string, string>? TemplateParameters { get; set; }
        public string? LanguageCode { get; set; } = "en_US";
    }
}
