using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriverConnectApp.API.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMessageService _messageService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly bool _testMode;

        public WhatsAppService(AppDbContext context, ILogger<WhatsAppService> logger,
            IHttpClientFactory httpClientFactory, IConfiguration configuration,
            IMessageService messageService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _messageService = messageService;
            _userManager = userManager;
            _testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", true);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task ProcessIncomingMessage(IncomingMessage incomingMessage)
        {
            if (incomingMessage == null)
            {
                _logger.LogError("Invalid incoming message data");
                throw new ArgumentNullException(nameof(incomingMessage));
            }

            try
            {
                // Handle group messages
                if (incomingMessage.IsGroupMessage)
                {
                    await ProcessGroupMessage(incomingMessage);
                    return;
                }

                // Handle individual messages (existing logic)
                await ProcessIndividualMessage(incomingMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing incoming message {MessageId}", incomingMessage.Id);
                throw;
            }
        }

        private async Task ProcessGroupMessage(IncomingMessage incomingMessage)
        {
            _logger.LogInformation("Processing GROUP message from {Participant} in group {GroupId}",
                incomingMessage.Participant, incomingMessage.GroupId);

            if (string.IsNullOrEmpty(incomingMessage.GroupId))
            {
                _logger.LogWarning("Group message received without GroupId");
                return;
            }

            // Get or create group
            var group = await GetOrCreateGroupAsync(incomingMessage.GroupId);

            // Get or create conversation for this group
            var conversation = await GetOrCreateGroupConversationAsync(group);

            // Find driver if participant is a known driver
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d => d.PhoneNumber.Trim() == incomingMessage.ActualSender.Trim());

            // Extract message data
            var messageData = ExtractMessageData(incomingMessage);

            // NEW: Handle reply context for incoming messages
            int? replyToMessageId = null;
            string? replyToMessageContent = null;
            string? replyToSenderName = null;

            if (incomingMessage.Context?.Id != null)
            {
                // Find the message being replied to in our database
                var repliedMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.WhatsAppMessageId == incomingMessage.Context.Id);

                if (repliedMessage != null)
                {
                    replyToMessageId = repliedMessage.Id;
                    replyToMessageContent = repliedMessage.Content?.Length > 100
                        ? repliedMessage.Content.Substring(0, 100) + "..."
                        : repliedMessage.Content;

                    // Determine sender name for the replied message
                    replyToSenderName = repliedMessage.IsGroupMessage
                        ? (repliedMessage.SenderName ?? "Unknown")
                        : (repliedMessage.IsFromDriver ? "Driver" : "You");
                }
            }

            // Create group message
            var message = new Message
            {
                ConversationId = conversation.Id,
                Content = messageData.Content,
                MessageType = messageData.Type,
                MediaUrl = messageData.MediaUrl,
                FileName = messageData.FileName,
                FileSize = messageData.FileSize,
                MimeType = messageData.MimeType,
                Location = messageData.Location,
                ContactName = messageData.ContactName,
                ContactPhone = messageData.ContactPhone,
                IsFromDriver = driver != null, // True if sender is a known driver
                IsGroupMessage = true,
                SenderPhoneNumber = incomingMessage.ActualSender,
                SenderName = driver?.Name ?? incomingMessage.ActualSender,
                SentAt = DateTime.UtcNow,
                WhatsAppMessageId = incomingMessage.Id,
                Context = incomingMessage.Context?.Id,

                // NEW: Reply functionality for incoming messages
                ReplyToMessageId = replyToMessageId,
                ReplyToMessageContent = replyToMessageContent,
                ReplyToSenderName = replyToSenderName
            };

            _context.Messages.Add(message);
            conversation.LastMessageAt = message.SentAt;
            conversation.UpdateInboundMessageTimestamp();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Processed group message from {Sender} in group {GroupName} with reply context: {HasReply}",
                message.SenderName, group.Name, replyToMessageId.HasValue);
        }

        private async Task ProcessIndividualMessage(IncomingMessage incomingMessage)
        {
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d => d.PhoneNumber.Trim() == incomingMessage.From.Trim());

            if (driver == null)
            {
                _logger.LogWarning("Driver not found for phone number: {PhoneNumber}", incomingMessage.From);
                return;
            }

            var conversation = await _messageService.GetOrCreateConversationAsync(driver.Id, "Incoming Message");
            if (conversation == null) return;

            var messageData = ExtractMessageData(incomingMessage);

            // NEW: Handle reply context for incoming individual messages
            int? replyToMessageId = null;
            string? replyToMessageContent = null;
            string? replyToSenderName = null;

            if (incomingMessage.Context?.Id != null)
            {
                // Find the message being replied to in our database
                var repliedMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.WhatsAppMessageId == incomingMessage.Context.Id);

                if (repliedMessage != null)
                {
                    replyToMessageId = repliedMessage.Id;
                    replyToMessageContent = repliedMessage.Content?.Length > 100
                        ? repliedMessage.Content.Substring(0, 100) + "..."
                        : repliedMessage.Content;

                    replyToSenderName = repliedMessage.IsFromDriver ? "Driver" : "You";
                }
            }

            await _messageService.CreateMessageAsync(
                conversation.Id,
                messageData.Content,
                true,
                incomingMessage.Id,
                incomingMessage.Context?.Id,
                messageData.Type,
                messageData.MediaUrl,
                messageData.FileName,
                messageData.FileSize,
                messageData.MimeType,
                messageData.Location,
                messageData.ContactName,
                messageData.ContactPhone,

                // NEW: Reply functionality for incoming messages
                replyToMessageId,
                replyToMessageContent,
                replyToSenderName
            );

            _logger.LogInformation("Processed individual {MessageType} message {MessageId} with reply context: {HasReply}",
                messageData.Type, incomingMessage.Id, replyToMessageId.HasValue);
        }

        private async Task<Group> GetOrCreateGroupAsync(string whatsAppGroupId)
        {
            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.WhatsAppGroupId == whatsAppGroupId);

            if (group == null)
            {
                group = new Group
                {
                    WhatsAppGroupId = whatsAppGroupId,
                    Name = $"Group {whatsAppGroupId.Substring(0, Math.Min(10, whatsAppGroupId.Length))}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new group: {GroupName} ({GroupId})",
                    group.Name, group.WhatsAppGroupId);
            }

            group.LastActivityAt = DateTime.UtcNow;
            return group;
        }

        private async Task<Conversation> GetOrCreateGroupConversationAsync(Group group)
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.WhatsAppGroupId == group.WhatsAppGroupId && c.IsGroupConversation);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    WhatsAppGroupId = group.WhatsAppGroupId,
                    GroupName = group.Name,
                    IsGroupConversation = true,
                    Topic = $"Group: {group.Name}",
                    CreatedAt = DateTime.UtcNow,
                    IsAnswered = false
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new group conversation for {GroupName}", group.Name);
            }

            return conversation;
        }

        private (string Content, MessageType Type, string? MediaUrl, string? FileName,
                 long? FileSize, string? MimeType, string? Location, string? ContactName,
                 string? ContactPhone) ExtractMessageData(IncomingMessage incomingMessage)
        {
            string content = string.Empty;
            MessageType type = MessageType.Text;
            string? mediaUrl = null;
            string? fileName = null;
            long? fileSize = null;
            string? mimeType = null;
            string? location = null;
            string? contactName = null;
            string? contactPhone = null;

            if (incomingMessage.Text?.Body != null)
            {
                content = incomingMessage.Text.Body;
                type = MessageType.Text;
            }
            else if (incomingMessage.Image != null)
            {
                content = incomingMessage.Image.Caption ?? "Image";
                type = MessageType.Image;
                mediaUrl = incomingMessage.Image.Link;
                mimeType = incomingMessage.Image.MimeType;
            }
            else if (incomingMessage.Document != null)
            {
                content = incomingMessage.Document.Caption ?? "Document";
                type = MessageType.Document;
                mediaUrl = incomingMessage.Document.Link;
                fileName = incomingMessage.Document.Filename;
                mimeType = incomingMessage.Document.MimeType;
            }
            else if (incomingMessage.Location != null)
            {
                content = incomingMessage.Location.Name ?? "Location shared";
                type = MessageType.Location;
                location = $"{incomingMessage.Location.Latitude},{incomingMessage.Location.Longitude}";
            }
            else if (incomingMessage.Contacts != null && incomingMessage.Contacts.Any())
            {
                var contact = incomingMessage.Contacts.First();
                content = "Contact shared";
                type = MessageType.Contact;
                contactName = contact.Name?.FormattedName ?? "Unknown";
                contactPhone = contact.Phones?.FirstOrDefault()?.Phone ?? "Unknown";
            }

            return (content, type, mediaUrl, fileName, fileSize, mimeType, location, contactName, contactPhone);
        }

        // NEW: Send message to WhatsApp group
        public async Task<bool> SendGroupMessageAsync(string groupId, string messageText, string? mediaUrl = null, MessageType messageType = MessageType.Text)
        {
            try
            {
                if (_testMode)
                {
                    _logger.LogInformation("TEST MODE: Group message would be sent to {GroupId}: {Message}", groupId, messageText);
                    return true;
                }

                // Verify this is a valid group conversation in our system
                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.WhatsAppGroupId == groupId && c.IsGroupConversation);

                if (conversation == null)
                {
                    _logger.LogWarning("Cannot send to group {GroupId} - no conversation found", groupId);
                    return false;
                }

                var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
                var accessToken = _configuration["WhatsApp:AccessToken"];
                var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "18.0";

                if (string.IsNullOrEmpty(phoneNumberId) || string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("WhatsApp configuration missing");
                    return false;
                }

                var url = $"https://graph.facebook.com/v{apiVersion}/{phoneNumberId}/messages";

                object requestBody;

                if (messageType == MessageType.Text)
                {
                    requestBody = new
                    {
                        messaging_product = "whatsapp",
                        recipient_type = "group",
                        to = groupId,
                        type = "text",
                        text = new { body = messageText }
                    };
                }
                else if (!string.IsNullOrEmpty(mediaUrl))
                {
                    string mediaType = messageType switch
                    {
                        MessageType.Image => "image",
                        MessageType.Video => "video",
                        MessageType.Audio => "audio",
                        MessageType.Document => "document",
                        _ => "document"
                    };

                    // Create a dictionary for dynamic property names
                    var payload = new Dictionary<string, object>
                    {
                        ["messaging_product"] = "whatsapp",
                        ["recipient_type"] = "group",
                        ["to"] = groupId,
                        ["type"] = mediaType
                    };

                    var mediaObject = new Dictionary<string, object>
                    {
                        ["link"] = mediaUrl
                    };

                    if (messageType != MessageType.Text)
                    {
                        mediaObject["caption"] = messageText;
                    }

                    payload[mediaType] = mediaObject;
                    requestBody = payload;
                }
                else
                {
                    _logger.LogWarning("Cannot send {MessageType} without media URL", messageType);
                    return false;
                }

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var messageId = responseData.GetProperty("messages")[0].GetProperty("id").GetString();

                    _logger.LogInformation("Group message sent to {GroupId} with ID {MessageId}", groupId, messageId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send group message to {GroupId}. Status: {StatusCode}, Error: {Error}",
                        groupId, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending group message to {GroupId}", groupId);
                return false;
            }
        }

        // Update existing SendMessageAsync to handle groups
        public async Task<object> SendMessageAsync(SendMessageRequest request)
        {
            if (request == null || (string.IsNullOrEmpty(request.Content) && string.IsNullOrEmpty(request.MediaUrl)))
            {
                _logger.LogError("Invalid send message request");
                throw new ArgumentNullException(nameof(request));
            }

            // Handle group messages
            if (request.IsGroupMessage && !string.IsNullOrEmpty(request.GroupId))
            {
                var messageType = Enum.TryParse<MessageType>(request.MessageType, out var type)
                    ? type : MessageType.Text;

                var success = await SendGroupMessageAsync(
                    request.GroupId,
                    request.Content,
                    request.MediaUrl,
                    messageType
                );

                if (success)
                {
                    // Save to our database with COMPLETE staff context
                    var conversation = await _context.Conversations
                        .FirstOrDefaultAsync(c => c.WhatsAppGroupId == request.GroupId && c.IsGroupConversation);

                    if (conversation != null)
                    {
                        // ENHANCED: Get current staff user for proper attribution
                        var currentUser = await _userManager.GetUserAsync(null);
                        var currentUserName = currentUser?.FullName ?? "Staff";

                        var groupMessage = new Message
                        {
                            ConversationId = conversation.Id,
                            Content = request.Content,
                            MessageType = messageType,
                            MediaUrl = request.MediaUrl,
                            FileName = request.FileName,
                            FileSize = request.FileSize,
                            MimeType = request.MimeType,
                            IsFromDriver = false, // Staff sent this
                            IsGroupMessage = true,
                            SenderPhoneNumber = "System",
                            SenderName = currentUserName, // Use actual staff name
                            SentAt = DateTime.UtcNow,
                            WhatsAppMessageId = $"web_group_{DateTime.UtcNow.Ticks}",
                            // ENHANCED: Add staff user tracking
                            SentByUserId = currentUser?.Id,
                            SentByUserName = currentUserName
                        };

                        _context.Messages.Add(groupMessage);
                        conversation.LastMessageAt = groupMessage.SentAt;
                        await _context.SaveChangesAsync();
                    }
                }

                return new { Status = success ? "Sent" : "Failed", IsGroup = true };
            }

            // Handle individual messages
            var message = await _messageService.SendMessageAsync(request);
            if (message == null) return new { Status = "Failed" };

            if (!_testMode)
            {
                var driver = await _context.Drivers.FindAsync(request.DriverId);
                if (driver != null && !string.IsNullOrEmpty(driver.PhoneNumber))
                {
                    if (!Enum.TryParse<MessageType>(request.MessageType, out var messageType))
                    {
                        messageType = MessageType.Text;
                    }

                    if (messageType == MessageType.Text)
                    {
                        await SendWhatsAppMessageAsync(driver.PhoneNumber, request.Content, false, null); // FIXED: Use null-forgiving operator
                    }
                    else if (messageType == MessageType.Location && !string.IsNullOrEmpty(request.Location))
                    {
                        var locationParts = request.Location.Split(',');
                        if (locationParts.Length == 2 &&
                            decimal.TryParse(locationParts[0], out var latitude) &&
                            decimal.TryParse(locationParts[1], out var longitude))
                        {
                            await SendLocationMessageAsync(driver.PhoneNumber, latitude, longitude, request.Content);
                        }
                    }
                    else if (!string.IsNullOrEmpty(request.MediaUrl))
                    {
                        await SendMediaMessageAsync(driver.PhoneNumber, request.MediaUrl, messageType, request.Content);
                    }
                }
            }

            return new { MessageId = message.WhatsAppMessageId, Status = "Sent" };
        }

        public async Task SendWhatsAppMessageAsync(string phoneNumber, string message, bool isTemplate, MessageContext? context)
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(message))
            {
                _logger.LogError("Invalid phone number or message");
                throw new ArgumentNullException(nameof(phoneNumber), "Invalid input");
            }

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.PhoneNumber.Trim() == phoneNumber.Trim());
            if (driver == null) throw new InvalidOperationException($"Driver with phone number {phoneNumber} not found");

            var conversation = await _messageService.GetOrCreateConversationAsync(driver.Id, isTemplate ? "Template Message" : "Direct Message");
            if (conversation == null) return;

            var whatsAppMessageId = "wamid.mock_" + Guid.NewGuid().ToString();
            await _messageService.CreateMessageAsync(
                conversation.Id,
                message,
                false,
                whatsAppMessageId,
                context?.Type,
                MessageType.Text
            );

            if (_testMode) return;

            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            var accessToken = _configuration["WhatsApp:AccessToken"];
            var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "18.0";

            if (string.IsNullOrEmpty(phoneNumberId) || string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("WhatsApp configuration missing, falling back to test mode");
                return;
            }

            var url = $"https://graph.facebook.com/v{apiVersion}/{phoneNumberId}/messages";
            var formattedPhoneNumber = new string(phoneNumber.Where(char.IsDigit).ToArray());
            var requestBody = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = formattedPhoneNumber,
                type = "text",
                text = new { body = message }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadFromJsonAsync<dynamic>();
                whatsAppMessageId = responseData?.messages?[0]?.id ?? whatsAppMessageId;
                _logger.LogInformation("WhatsApp message sent to {PhoneNumber}", phoneNumber);
            }
            else
            {
                _logger.LogWarning("Failed to send WhatsApp message: {StatusCode}", response.StatusCode);
            }
        }

        public async Task<bool> SendMediaMessageAsync(string to, string mediaUrl, MessageType messageType, string? caption = null)
        {
            try
            {
                if (_testMode)
                {
                    _logger.LogInformation("Test mode: Media message would be sent to {To}", to);
                    return true;
                }

                string mediaType = messageType switch
                {
                    MessageType.Image => "image",
                    MessageType.Video => "video",
                    MessageType.Audio => "audio",
                    MessageType.Document => "document",
                    _ => "document"
                };

                // Create dynamic payload
                var payload = new Dictionary<string, object>
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = to,
                    ["type"] = mediaType
                };

                var mediaObject = new Dictionary<string, object>
                {
                    ["link"] = mediaUrl
                };

                if (!string.IsNullOrEmpty(caption))
                {
                    mediaObject["caption"] = caption;
                }

                payload[mediaType] = mediaObject;

                var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
                var accessToken = _configuration["WhatsApp:AccessToken"];
                var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "18.0";

                var url = $"https://graph.facebook.com/v{apiVersion}/{phoneNumberId}/messages";
                var formattedPhoneNumber = new string(to.Where(char.IsDigit).ToArray());

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Media message sent successfully to {To}", to);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send media message to {To}. Status: {StatusCode}, Error: {Error}",
                        to, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending media message to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendLocationMessageAsync(string to, decimal latitude, decimal longitude, string? name = null, string? address = null)
        {
            try
            {
                if (_testMode)
                {
                    _logger.LogInformation("Test mode: Location message would be sent to {To}", to);
                    return true;
                }

                var messagePayload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = to,
                    type = "location",
                    location = new
                    {
                        latitude = latitude.ToString(),
                        longitude = longitude.ToString(),
                        name = name,
                        address = address
                    }
                };

                var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
                var accessToken = _configuration["WhatsApp:AccessToken"];
                var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "18.0";

                var url = $"https://graph.facebook.com/v{apiVersion}/{phoneNumberId}/messages";
                var formattedPhoneNumber = new string(to.Where(char.IsDigit).ToArray());

                var json = JsonSerializer.Serialize(messagePayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Location message sent successfully to {To}", to);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send location message to {To}. Status: {StatusCode}, Error: {Error}",
                        to, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending location message to {To}", to);
                return false;
            }
        }

        public async Task ProcessWebhookAsync(string webhookData)
        {
            if (string.IsNullOrEmpty(webhookData))
            {
                _logger.LogError("Webhook data is null or empty");
                throw new ArgumentNullException(nameof(webhookData));
            }

            _logger.LogInformation("Processing webhook data");
            var message = JsonSerializer.Deserialize<IncomingMessage>(webhookData);
            if (message != null)
            {
                await ProcessIncomingMessage(message);
            }
            await Task.CompletedTask;
        }
    }
}
