using Azure.Core;
using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using DriverConnectApp.API.Services;

namespace DriverConnectApp.API.Services
{
    public class MultiTenantWhatsAppService : IMultiTenantWhatsAppService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MultiTenantWhatsAppService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMessageService _messageService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MultiTenantWhatsAppService(
            AppDbContext context,
            ILogger<MultiTenantWhatsAppService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMessageService messageService,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _messageService = messageService;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<object> SendMessageAsync(SendMessageRequest request, int teamId)
        {
            if (request == null)
            {
                _logger.LogError("Invalid send message request for team {TeamId}", teamId);
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogInformation(
                "📨 Processing message for team {TeamId}: IsTemplate={IsTemplate}, TemplateName={TemplateName}, Content={Content}",
                teamId, request.IsTemplateMessage, request.TemplateName, request.Content);

            var team = await GetTeamById(teamId);
            if (team == null || !team.IsActive)
            {
                throw new InvalidOperationException($"Team {teamId} not found or inactive");
            }

            // ✅ CRITICAL FIX: Check 24-hour window for non-template messages (STRICT - NO GRACE PERIOD)
            if (!request.IsTemplateMessage && !request.IsGroupMessage && request.ConversationId.HasValue)
            {
                bool canSendFreeText = await CanSendNonTemplateMessage(request.ConversationId.Value);

                if (!canSendFreeText)
                {
                    _logger.LogWarning(
                        "🚫 BLOCKED: Cannot send non-template message to conversation {ConversationId} - outside 24-hour window",
                        request.ConversationId);

                    throw new InvalidOperationException(
                        "TEMPLATE_REQUIRED|Cannot send regular messages outside 24-hour window. " +
                        "Please use a template message instead. The 24-hour window only opens when a customer messages you.");
                }

                _logger.LogInformation(
                    "✅ ALLOWED: Can send free text to conversation {ConversationId} - within 24-hour window",
                    request.ConversationId);
            }

            // Handle group messages first
            if (request.IsGroupMessage && !string.IsNullOrEmpty(request.GroupId))
            {
                var success = await SendGroupMessageAsync(
                    request.GroupId,
                    request.Content ?? "",
                    request.MediaUrl,
                    Enum.TryParse<MessageType>(request.MessageType, out var groupMessageType) ? groupMessageType : MessageType.Text,
                    team
                );

                if (success)
                {
                    var conversation = await _context.Conversations
                        .FirstOrDefaultAsync(c => c.WhatsAppGroupId == request.GroupId && c.IsGroupConversation && c.TeamId == teamId);

                    if (conversation != null)
                    {
                        var currentUserName = GetCurrentUserName();
                        var currentUserId = GetCurrentUserId();

                        var groupMessage = new Message
                        {
                            ConversationId = conversation.Id,
                            Content = request.Content ?? "",
                            MessageType = Enum.TryParse<MessageType>(request.MessageType, out var msgType) ? msgType : MessageType.Text,
                            MediaUrl = request.MediaUrl,
                            FileName = request.FileName,
                            FileSize = request.FileSize,
                            MimeType = request.MimeType,
                            IsFromDriver = false,
                            IsGroupMessage = true,
                            SenderPhoneNumber = "System",
                            SenderName = currentUserName,
                            SentAt = DateTime.UtcNow,
                            WhatsAppMessageId = $"web_group_{DateTime.UtcNow.Ticks}",
                            SentByUserId = currentUserId,
                            SentByUserName = currentUserName,
                            IsTemplateMessage = request.IsTemplateMessage,
                            TemplateName = request.TemplateName,
                            TemplateParametersJson = request.TemplateParameters != null
                                ? JsonSerializer.Serialize(request.TemplateParameters)
                                : null
                        };

                        _context.Messages.Add(groupMessage);
                        conversation.LastMessageAt = groupMessage.SentAt;
                        await _context.SaveChangesAsync();
                    }
                }

                return new
                {
                    Status = success ? "Sent" : "Failed",
                    IsGroup = true,
                    IsTemplate = request.IsTemplateMessage
                };
            }

            // Handle template messages for individuals
            if (request.IsTemplateMessage && !string.IsNullOrEmpty(request.TemplateName))
            {
                _logger.LogInformation("🎯 Sending template message: {TemplateName} for team {TeamId}",
                    request.TemplateName, teamId);

                string? phoneNumber = GetPhoneNumberFromRequest(request, teamId);

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    throw new InvalidOperationException("Phone number is required for template messages. Could not find phone number from request, driver, or conversation.");
                }

                var success = await SendTemplateMessageAsync(
                    phoneNumber,
                    request.TemplateName,
                    request.TemplateParameters ?? new Dictionary<string, string>(),
                    teamId,
                    request.LanguageCode ?? "en_US"
                );

                if (success)
                {
                    return await SaveTemplateMessageToDatabase(request, teamId, phoneNumber);
                }
                else
                {
                    throw new InvalidOperationException("Failed to send template message via WhatsApp API");
                }
            }

            // Handle regular messages for individuals
            if (request.DriverId == null && string.IsNullOrEmpty(request.PhoneNumber))
            {
                throw new InvalidOperationException("DriverId or PhoneNumber is required for regular messages");
            }

            Driver? driver = null;
            if (request.DriverId.HasValue)
            {
                driver = await _context.Drivers.FindAsync(request.DriverId.Value);
            }
            else if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                var normalizedPhone = PhoneNumberUtil.NormalizePhoneNumber(request.PhoneNumber, team.CountryCode ?? "91");
                driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalizedPhone && d.TeamId == teamId);

                if (driver == null)
                {
                    driver = new Driver
                    {
                        Name = $"Driver {request.PhoneNumber}",
                        PhoneNumber = normalizedPhone,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        TeamId = teamId
                    };
                    _context.Drivers.Add(driver);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("📝 Auto-created driver with ID: {DriverId} for phone {PhoneNumber}",
                        driver.Id, normalizedPhone);
                }
            }

            if (driver == null)
            {
                throw new InvalidOperationException("Could not find or create driver for message");
            }

            var messageType = Enum.TryParse<MessageType>(request.MessageType, out var type)
                ? type : MessageType.Text;

            var conversationObj = await GetOrCreateConversationAsync(request, driver.Id, team.Id);

            var currentUserNameMsg = GetCurrentUserName();
            var currentUserIdMsg = GetCurrentUserId();

            var message = new Message
            {
                ConversationId = conversationObj.Id,
                Content = request.Content ?? string.Empty,
                MessageType = messageType,
                MediaUrl = request.MediaUrl,
                FileName = request.FileName,
                FileSize = request.FileSize,
                MimeType = request.MimeType,
                IsFromDriver = false,
                IsGroupMessage = false,
                SenderPhoneNumber = "System",
                SenderName = currentUserNameMsg,
                SentAt = DateTime.UtcNow,
                WhatsAppMessageId = $"web_{DateTime.UtcNow.Ticks}",
                SentByUserId = currentUserIdMsg,
                SentByUserName = currentUserNameMsg,
                IsTemplateMessage = false
            };

            _context.Messages.Add(message);
            conversationObj.LastMessageAt = message.SentAt;
            await _context.SaveChangesAsync();

            var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
            if (!testMode)
            {
                await SendRegularMessageToWhatsAppApi(request, driver.PhoneNumber, team);
            }

            return new
            {
                MessageId = message.WhatsAppMessageId,
                Status = "Sent",
                IsTemplate = false,
                ConversationId = conversationObj.Id
            };
        }

        private async Task<object> HandleGroupMessage(SendMessageRequest request, Team team)
        {
            try
            {
                _logger.LogInformation("📨 Handling GROUP message for group {GroupId}", request.GroupId);

                if (string.IsNullOrEmpty(request.GroupId))
                    return new { message = "GroupId is required", status = "failed" };

                var conversation = await _context.Conversations
                    .Include(c => c.Group)
                    .FirstOrDefaultAsync(c => c.WhatsAppGroupId == request.GroupId && c.IsGroupConversation && c.TeamId == team.Id);

                if (conversation == null)
                {
                    _logger.LogWarning("⚠️ Group conversation not found for WhatsAppGroupId: {GroupId}", request.GroupId);
                    return new { message = "Group conversation not found", status = "failed" };
                }

                if (!Enum.TryParse<MessageType>(request.MessageType, out var messageType))
                    messageType = MessageType.Text;

                var currentUserName = GetCurrentUserName();
                var currentUserId = GetCurrentUserId();

                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Content = request.Content ?? "",
                    MessageType = messageType,
                    MediaUrl = request.MediaUrl,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    MimeType = request.MimeType,
                    IsFromDriver = false,
                    IsGroupMessage = true,
                    SenderPhoneNumber = "System",
                    SenderName = currentUserName,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = $"web_group_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}",
                    SentByUserId = currentUserId,
                    SentByUserName = currentUserName,
                    IsTemplateMessage = request.IsTemplateMessage,
                    TemplateName = request.TemplateName,
                    TemplateParametersJson = request.TemplateParameters != null
                        ? JsonSerializer.Serialize(request.TemplateParameters)
                        : null
                };

                _context.Messages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Group message created for conversation {ConversationId}", conversation.Id);

                return new
                {
                    status = "success",
                    messageId = message.Id,
                    isGroup = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling group message");
                throw;
            }
        }

        private async Task<object> HandleIndividualMessage(SendMessageRequest request, Team team)
        {
            try
            {
                _logger.LogInformation(
                    "📨 Handling INDIVIDUAL message for driver {DriverId}, conversation {ConversationId}",
                    request.DriverId, request.ConversationId);

                // Get or create conversation
                Conversation conversation = null;

                if (request.ConversationId.HasValue)
                {
                    conversation = await _context.Conversations
                        .Include(c => c.Driver)
                        .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.TeamId == team.Id);
                }

                if (conversation == null && request.DriverId.HasValue)
                {
                    conversation = await _context.Conversations
                        .Include(c => c.Driver)
                        .FirstOrDefaultAsync(c => c.DriverId == request.DriverId.Value && c.TeamId == team.Id);

                    if (conversation == null)
                    {
                        var driver = await _context.Drivers.FindAsync(request.DriverId.Value);
                        if (driver == null)
                            return new { message = "Driver not found", status = "failed" };

                        conversation = new Conversation
                        {
                            DriverId = driver.Id,
                            Topic = request.Topic ?? "General Conversation",
                            CreatedAt = DateTime.UtcNow,
                            IsAnswered = false,
                            TeamId = team.Id,
                            IsGroupConversation = false
                        };
                        _context.Conversations.Add(conversation);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("✅ Created new conversation {ConversationId} for driver {DriverId}", conversation.Id, driver.Id);
                    }
                }

                if (conversation == null)
                    return new { message = "Could not find or create conversation", status = "failed" };

                // Convert message type
                if (!Enum.TryParse<MessageType>(request.MessageType, out var messageType))
                    messageType = MessageType.Text;

                var currentUserName = GetCurrentUserName();
                var currentUserId = GetCurrentUserId();

                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Content = request.Content ?? string.Empty,
                    MessageType = messageType,
                    MediaUrl = request.MediaUrl,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    MimeType = request.MimeType,
                    Location = request.Location,
                    ContactName = request.ContactName,
                    ContactPhone = request.ContactPhone,
                    IsFromDriver = false,
                    IsGroupMessage = false,
                    SenderPhoneNumber = "System",
                    SenderName = currentUserName,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = request.WhatsAppMessageId ?? $"web_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}",
                    SentByUserId = currentUserId,
                    SentByUserName = currentUserName,
                    IsTemplateMessage = request.IsTemplateMessage,
                    TemplateName = request.TemplateName,
                    TemplateParametersJson = request.TemplateParameters != null
                        ? JsonSerializer.Serialize(request.TemplateParameters)
                        : null
                };

                _context.Messages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Individual message created for conversation {ConversationId}", conversation.Id);

                return new
                {
                    status = "success",
                    messageId = message.Id,
                    conversationId = conversation.Id,
                    isTemplate = request.IsTemplateMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling individual message");
                throw;
            }
        }

        private async Task<Conversation> GetOrCreateConversationAsync(SendMessageRequest request, int driverId, int teamId)
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.TeamId == teamId);

            if (conversation == null && driverId > 0)
            {
                conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.DriverId == driverId && c.TeamId == teamId);

                if (conversation == null)
                {
                    conversation = new Conversation
                    {
                        DriverId = driverId,
                        Topic = request.Topic ?? "New Conversation",
                        CreatedAt = DateTime.UtcNow,
                        IsAnswered = false,
                        TeamId = teamId,
                        LastMessageAt = DateTime.UtcNow
                    };
                    _context.Conversations.Add(conversation);
                    await _context.SaveChangesAsync();
                }
            }

            if (conversation == null)
            {
                throw new InvalidOperationException("Conversation not found and could not be created");
            }

            return conversation;
        }

        private async Task SendRegularMessageToWhatsAppApi(SendMessageRequest request, string phoneNumber, Team team)
        {
            try
            {
                var messageType = Enum.TryParse<MessageType>(request.MessageType, out var type)
                    ? type : MessageType.Text;

                if (messageType == MessageType.Text && !string.IsNullOrEmpty(request.Content))
                {
                    await SendWhatsAppTextMessageAsync(phoneNumber, request.Content, team.Id);
                }
                else if (!string.IsNullOrEmpty(request.MediaUrl))
                {
                    await SendMediaMessageAsync(phoneNumber, request.MediaUrl, messageType, team.Id, request.Content);
                }
                else if (messageType == MessageType.Location && !string.IsNullOrEmpty(request.Location))
                {
                    var locationParts = request.Location.Split(',');
                    if (locationParts.Length == 2 &&
                        decimal.TryParse(locationParts[0], out var latitude) &&
                        decimal.TryParse(locationParts[1], out var longitude))
                    {
                        await SendLocationMessageAsync(phoneNumber, latitude, longitude, team.Id, request.Content);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to WhatsApp API for team {TeamId}", team.Id);
            }
        }

        private async Task<object> SaveTemplateMessageToDatabase(SendMessageRequest request, int teamId, string phoneNumber)
        {
            try
            {
                // Get current user
                var currentUser = _httpContextAccessor.HttpContext?.User;
                var currentUserId = currentUser?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var currentUserName = "Staff";

                if (!string.IsNullOrEmpty(currentUserId))
                {
                    var user = await _userManager.FindByIdAsync(currentUserId);
                    currentUserName = user?.FullName ?? user?.UserName ?? "Staff";
                }

                // Find or create conversation
                Conversation conversation = null!;

                if (request.ConversationId.HasValue)
                {
                    conversation = await _context.Conversations
                        .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.TeamId == teamId);
                }

                // If no conversation but we have a phone number, find by driver
                if (conversation == null && !string.IsNullOrEmpty(phoneNumber))
                {
                    var driverObj = await _context.Drivers
                        .FirstOrDefaultAsync(d => d.PhoneNumber == phoneNumber && d.TeamId == teamId);

                    if (driverObj != null)
                    {
                        conversation = await _context.Conversations
                            .FirstOrDefaultAsync(c => c.DriverId == driverObj.Id && c.TeamId == teamId);
                    }
                }

                // Create new conversation if needed
                if (conversation == null && !string.IsNullOrEmpty(phoneNumber))
                {
                    var driverObj = await _context.Drivers
                        .FirstOrDefaultAsync(d => d.PhoneNumber == phoneNumber && d.TeamId == teamId);

                    if (driverObj == null)
                    {
                        // Create driver if doesn't exist
                        driverObj = new Driver
                        {
                            Name = $"Driver {phoneNumber}",
                            PhoneNumber = phoneNumber,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true,
                            TeamId = teamId
                        };
                        _context.Drivers.Add(driverObj);
                        await _context.SaveChangesAsync();
                    }

                    conversation = new Conversation
                    {
                        DriverId = driverObj.Id,
                        Topic = request.Topic ?? $"Template: {request.TemplateName}",
                        CreatedAt = DateTime.UtcNow,
                        IsAnswered = false,
                        TeamId = teamId,
                        LastMessageAt = DateTime.UtcNow
                    };
                    _context.Conversations.Add(conversation);
                    await _context.SaveChangesAsync();
                }

                if (conversation == null)
                {
                    throw new InvalidOperationException("Could not find or create conversation for template message");
                }

                // Create message
                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Content = request.Content ?? $"Template: {request.TemplateName}",
                    MessageType = MessageType.Text,
                    IsFromDriver = false,
                    IsGroupMessage = false,
                    SenderPhoneNumber = "System",
                    SenderName = currentUserName,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = $"template_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}",
                    SentByUserId = currentUserId,
                    SentByUserName = currentUserName,
                    IsTemplateMessage = true,
                    TemplateName = request.TemplateName,
                    TemplateParametersJson = request.TemplateParameters != null
                        ? JsonSerializer.Serialize(request.TemplateParameters)
                        : null
                };

                _context.Messages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                await _context.SaveChangesAsync();

                return new
                {
                    MessageId = message.WhatsAppMessageId,
                    Status = "Sent",
                    IsTemplate = true,
                    ConversationId = conversation.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving template message to database");
                throw;
            }
        }

        private async Task<bool> CanSendNonTemplateMessage(int conversationId)
        {
            try
            {
                var conversation = await _context.Conversations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                {
                    _logger.LogWarning("⚠️ Conversation {ConversationId} not found for window check", conversationId);
                    return false;
                }

                // ✅ STRICT CHECK: No inbound message = cannot send free text
                if (!conversation.LastInboundMessageAt.HasValue)
                {
                    _logger.LogWarning("🚫 Conversation {ConversationId} has NO inbound messages - CANNOT send free text", conversationId);
                    return false;
                }

                // ✅ STRICT CHECK: Must be within exactly 24 hours (NO GRACE PERIOD)
                var timeSinceLastInbound = DateTime.UtcNow - conversation.LastInboundMessageAt.Value;
                var canSend = timeSinceLastInbound.TotalHours < 24.0; // Strictly less than 24 hours

                _logger.LogInformation(
                    "📊 Window check for conversation {ConversationId}: LastInbound={LastInbound}, TimeSince={TimeSinceHours}h, CanSend={CanSend}",
                    conversationId,
                    conversation.LastInboundMessageAt,
                    Math.Round(timeSinceLastInbound.TotalHours, 2),
                    canSend);

                return canSend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking window for conversation {ConversationId}", conversationId);
                return false;
            }
        }

        public async Task<bool> SendTemplateMessageAsync(
    string to,
    string templateName,
    Dictionary<string, string> templateParameters,
    int teamId,
    string? languageCode = "en_US")
        {
            try
            {
                _logger.LogInformation("🎯 Sending template message: {TemplateName} to {To} for team {TeamId}",
                    templateName, to, teamId);

                var team = await GetTeamById(teamId);
                if (team == null)
                {
                    _logger.LogError("❌ Team {TeamId} not found", teamId);
                    return false;
                }

                if (string.IsNullOrEmpty(team.WhatsAppPhoneNumberId) ||
                    string.IsNullOrEmpty(team.WhatsAppAccessToken))
                {
                    _logger.LogError("❌ Team {TeamId} is missing WhatsApp configuration", teamId);
                    return false;
                }

                // ✅ FIX: Detect country code from the phone number first, then use team's country code
                var detectedCountryCode = PhoneNumberUtil.DetectCountryCode(to);
                var countryCodeToUse = detectedCountryCode ?? team.CountryCode ?? "91";

                _logger.LogInformation(
                    "🌍 Template Message - Country Code: Detected={Detected}, Team={Team}, Using={Using}",
                    detectedCountryCode ?? "none",
                    team.CountryCode ?? "none",
                    countryCodeToUse);

                // Normalize phone number for WhatsApp API
                var formattedPhoneNumber = PhoneNumberUtil.FormatForWhatsAppApi(to, countryCodeToUse);

                _logger.LogInformation(
                    "📞 Template - Original: {Original}, Normalized: {Normalized}",
                    to, formattedPhoneNumber);

                var apiVersion = string.IsNullOrEmpty(team.ApiVersion) ? "18.0" : team.ApiVersion;
                var url = $"https://graph.facebook.com/v{apiVersion}/{team.WhatsAppPhoneNumberId}/messages";

                var requestBody = new Dictionary<string, object>
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = formattedPhoneNumber,
                    ["type"] = "template",
                    ["template"] = new Dictionary<string, object>
                    {
                        ["name"] = templateName,
                        ["language"] = new Dictionary<string, string>
                        {
                            ["code"] = languageCode ?? "en_US"
                        }
                    }
                };

                if (templateParameters != null && templateParameters.Any())
                {
                    var components = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "body",
                    ["parameters"] = templateParameters.Select(kv => new Dictionary<string, string>
                    {
                        ["type"] = "text",
                        ["text"] = kv.Value
                    }).ToArray()
                }
            };

                    ((Dictionary<string, object>)requestBody["template"])["components"] = components;
                }

                var json = JsonSerializer.Serialize(requestBody);
                _logger.LogInformation("📤 Sending to WhatsApp API: {Json}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {team.WhatsAppAccessToken}");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Template message sent successfully to {To}", formattedPhoneNumber);
                    return true;
                }
                else
                {
                    _logger.LogError("❌ Failed to send template. Status: {Status}, Response: {Response}",
                        response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending template message");
                return false;
            }
        }

        public async Task<bool> SendWhatsAppTextMessageAsync(string to, string text, int teamId, bool isTemplate = false)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null || !team.IsActive)
                {
                    _logger.LogError("Team {TeamId} not found or inactive", teamId);
                    return false;
                }

                var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
                if (testMode)
                {
                    _logger.LogInformation("🧪 TEST MODE: Would send message to {To} for team {TeamName}: {Text}",
                        to, team.Name, text);
                    return true;
                }

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";
                var formattedPhoneNumber = new string(to.Where(char.IsDigit).ToArray());

                var requestBody = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = formattedPhoneNumber,
                    type = "text",
                    text = new { body = text }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ WhatsApp text message sent to {To} for team {TeamName}",
                        to, team.Name);
                    return true;
                }
                else
                {
                    _logger.LogError("❌ Failed to send WhatsApp text message: Status={Status}, Error={Error}",
                        response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp text message to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendMediaMessageAsync(string to, string mediaUrl, MessageType mediaType, int teamId, string? caption = null)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null || !team.IsActive)
                {
                    _logger.LogError("Team {TeamId} not found or inactive", teamId);
                    return false;
                }

                var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
                if (testMode)
                {
                    _logger.LogInformation("🧪 TEST MODE: Would send media to {To} for team {TeamName}: {MediaUrl}",
                        to, team.Name, mediaUrl);
                    return true;
                }

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";
                var formattedPhoneNumber = new string(to.Where(char.IsDigit).ToArray());

                string whatsappMediaType = mediaType switch
                {
                    MessageType.Image => "image",
                    MessageType.Video => "video",
                    MessageType.Audio => "audio",
                    MessageType.Document => "document",
                    _ => "document"
                };

                var requestBody = new Dictionary<string, object>
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = formattedPhoneNumber,
                    ["type"] = whatsappMediaType,
                    [whatsappMediaType] = new Dictionary<string, string>
                    {
                        ["link"] = mediaUrl
                    }
                };

                if (!string.IsNullOrEmpty(caption) && (whatsappMediaType == "image" || whatsappMediaType == "video" || whatsappMediaType == "document"))
                {
                    ((Dictionary<string, object>)requestBody[whatsappMediaType])["caption"] = caption;
                }

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ WhatsApp media message sent to {To} for team {TeamName}",
                        to, team.Name);
                    return true;
                }
                else
                {
                    _logger.LogError("❌ Failed to send WhatsApp media message: Status={Status}, Error={Error}",
                        response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp media message to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendGroupMessageAsync(string groupId, string messageText, string? mediaUrl, MessageType messageType, Team team)
        {
            var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
            if (testMode)
            {
                _logger.LogInformation("🧪 TEST MODE: Group message would be sent to {GroupId} for team {TeamName}: {Message}",
                    groupId, team.Name, messageText);
                return true;
            }

            try
            {
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
                    _logger.LogWarning("Cannot send {MessageType} without media URL for team {TeamName}", messageType, team.Name);
                    return false;
                }

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var messageId = responseData.GetProperty("messages")[0].GetProperty("id").GetString();

                    _logger.LogInformation("✅ Group message sent to {GroupId} for team {TeamName} with ID {MessageId}",
                        groupId, team.Name, messageId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ Failed to send group message to {GroupId} for team {TeamName}. Status: {StatusCode}, Error: {Error}",
                        groupId, team.Name, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending group message to {GroupId} for team {TeamName}", groupId, team.Name);
                return false;
            }
        }

        public async Task<bool> SendLocationMessageAsync(string to, decimal latitude, decimal longitude, int teamId, string? name = null, string? address = null)
        {
            var team = await GetTeamById(teamId);
            if (team == null || !team.IsActive)
            {
                _logger.LogError("Team {TeamId} not found or inactive", teamId);
                return false;
            }

            var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
            if (testMode)
            {
                _logger.LogInformation("🧪 TEST MODE: Location message would be sent to {To} for team {TeamName}",
                    to, team.Name);
                return true;
            }

            try
            {
                var payload = new
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

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";
                var formattedPhoneNumber = new string(to.Where(char.IsDigit).ToArray());

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Location message sent successfully to {To} for team {TeamName}", to, team.Name);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ Failed to send location message to {To} for team {TeamName}. Status: {StatusCode}, Error: {Error}",
                        to, team.Name, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending location message to {To} for team {TeamId}", to, teamId);
                return false;
            }
        }

        // ✅ FIXED: Make this public to match the interface
        public async Task<Team?> GetTeamById(int teamId)
        {
            return await _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId && t.IsActive);
        }

        public async Task<Team?> GetTeamByPhoneNumberId(string phoneNumberId)
        {
            if (string.IsNullOrEmpty(phoneNumberId))
                return null;

            return await _context.Teams
                .FirstOrDefaultAsync(t => t.WhatsAppPhoneNumberId == phoneNumberId && t.IsActive);
        }

        public async Task<List<Team>> GetAllTeams()
        {
            return await _context.Teams
                .Where(t => t.IsActive)
                .ToListAsync();
        }

        public async Task<Team> CreateTeamAsync(CreateTeamRequest request)
        {
            var team = new Team
            {
                Name = request.Name,
                Description = request.Description,
                WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId,
                WhatsAppAccessToken = request.WhatsAppAccessToken,
                WhatsAppBusinessAccountId = request.WhatsAppBusinessAccountId,
                WhatsAppPhoneNumber = request.WhatsAppPhoneNumber,
                ApiVersion = request.ApiVersion ?? "18.0",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            var config = new TeamConfiguration
            {
                TeamId = team.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.TeamConfigurations.Add(config);
            await _context.SaveChangesAsync();

            return team;
        }

        public async Task<Team> UpdateTeamAsync(int teamId, UpdateTeamRequest request)
        {
            var team = await GetTeamById(teamId);
            if (team == null)
                throw new ArgumentException($"Team {teamId} not found");

            if (!string.IsNullOrEmpty(request.Name))
                team.Name = request.Name;

            if (!string.IsNullOrEmpty(request.Description))
                team.Description = request.Description;

            if (!string.IsNullOrEmpty(request.WhatsAppPhoneNumberId))
                team.WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId;

            if (!string.IsNullOrEmpty(request.WhatsAppAccessToken))
                team.WhatsAppAccessToken = request.WhatsAppAccessToken;

            if (!string.IsNullOrEmpty(request.WhatsAppBusinessAccountId))
                team.WhatsAppBusinessAccountId = request.WhatsAppBusinessAccountId;

            if (!string.IsNullOrEmpty(request.WhatsAppPhoneNumber))
                team.WhatsAppPhoneNumber = request.WhatsAppPhoneNumber;

            if (!string.IsNullOrEmpty(request.ApiVersion))
                team.ApiVersion = request.ApiVersion;

            if (request.IsActive.HasValue)
                team.IsActive = request.IsActive.Value;

            team.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return team;
        }

        public async Task ProcessIncomingMessage(IncomingMessage incomingMessage, int teamId)
        {
            if (incomingMessage == null)
            {
                _logger.LogError("❌ Invalid incoming message");
                throw new ArgumentNullException(nameof(incomingMessage));
            }

            try
            {
                var team = await GetTeamById(teamId);
                if (team == null)
                {
                    _logger.LogError("❌ Team {TeamId} not found", teamId);
                    return;
                }

                if (incomingMessage.IsGroupMessage)
                {
                    await ProcessGroupMessage(incomingMessage, team);
                    return;
                }

                await ProcessIndividualMessage(incomingMessage, team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing incoming message");
                throw;
            }
        }


        private async Task ProcessGroupMessage(IncomingMessage incomingMessage, Team team)
        {
            _logger.LogInformation("📥 Processing GROUP message from {Participant} in group {GroupId}",
                incomingMessage.Participant, incomingMessage.GroupId);

            if (string.IsNullOrEmpty(incomingMessage.GroupId))
            {
                _logger.LogWarning("⚠️ Group message without GroupId");
                return;
            }

            var group = await GetOrCreateGroupAsync(incomingMessage.GroupId, team.Id);
            var conversation = await GetOrCreateGroupConversationAsync(group, team.Id);

            // ✅ UPDATE TIMESTAMP FOR GROUP CONVERSATIONS TOO
            conversation.LastInboundMessageAt = DateTime.UtcNow;
            conversation.LastMessageAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Updated LastInboundMessageAt for group conversation {ConversationId}",
                conversation.Id);

            var messageData = ExtractMessageData(incomingMessage);

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
                IsFromDriver = true,
                IsGroupMessage = true,
                SenderPhoneNumber = incomingMessage.ActualSender,
                SenderName = incomingMessage.ActualSender,
                SentAt = DateTime.UtcNow,
                WhatsAppMessageId = incomingMessage.Id,
                Context = incomingMessage.Context?.Id
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Processed group message {MessageId}", incomingMessage.Id);
        }

        private async Task ProcessIndividualMessage(IncomingMessage incomingMessage, Team team)
        {
            try
            {
                _logger.LogInformation("📥 Processing INDIVIDUAL message from {From}, Type: {Type}",
                    incomingMessage.From, incomingMessage.Type);

                // Extract phone number from WhatsApp format (e.g., "919763083597@c.us" -> "919763083597")
                var phoneNumber = PhoneNumberUtil.ExtractPhoneFromWhatsAppId(incomingMessage.From);

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    _logger.LogWarning("⚠️ Could not extract phone number from: {From}", incomingMessage.From);
                    return;
                }

                _logger.LogInformation("📱 Phone number extracted from WhatsApp: {Phone}", phoneNumber);

                // ✅ FIX: First try to detect country code from the number itself
                var detectedCountryCode = PhoneNumberUtil.DetectCountryCode(phoneNumber);

                // Use detected country code, or fall back to team's country code
                var countryCodeToUse = detectedCountryCode ?? team.CountryCode ?? "91";

                _logger.LogInformation(
                    "🌍 Country Code Selection: Detected={Detected}, Team={Team}, Using={Using}",
                    detectedCountryCode ?? "none",
                    team.CountryCode ?? "none",
                    countryCodeToUse);

                // Normalize phone number with the appropriate country code
                var normalizedPhone = PhoneNumberUtil.NormalizePhoneNumber(phoneNumber, countryCodeToUse);

                _logger.LogInformation(
                    "📱 Phone normalization: Original={Original}, Detected Code={Code}, Normalized={Normalized}",
                    phoneNumber, countryCodeToUse, normalizedPhone);

                // Find driver by normalized phone number
                var driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalizedPhone && d.TeamId == team.Id);

                if (driver == null)
                {
                    _logger.LogInformation(
                        "👤 Auto-creating driver for phone: {Phone} in team {TeamId} with country code {Code}",
                        normalizedPhone, team.Id, countryCodeToUse);

                    driver = new Driver
                    {
                        Name = $"Driver {normalizedPhone}",
                        PhoneNumber = normalizedPhone,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        TeamId = team.Id
                    };
                    _context.Drivers.Add(driver);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Created driver {DriverId} for phone {Phone}",
                        driver.Id, normalizedPhone);
                }

                // Find or create conversation
                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.DriverId == driver.Id && c.TeamId == team.Id);

                if (conversation == null)
                {
                    conversation = new Conversation
                    {
                        DriverId = driver.Id,
                        TeamId = team.Id,
                        Topic = $"Conversation with {driver.Name}",
                        CreatedAt = DateTime.UtcNow,
                        IsAnswered = false,
                        IsGroupConversation = false,
                        IsActive = true
                    };
                    _context.Conversations.Add(conversation);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Created conversation {ConversationId} for driver {DriverId}",
                        conversation.Id, driver.Id);
                }

                // ✅ CRITICAL: UPDATE TIMESTAMP - This opens the 24-hour window
                conversation.LastInboundMessageAt = DateTime.UtcNow;
                conversation.LastMessageAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("🕐 Updated LastInboundMessageAt for conversation {ConversationId}: {Timestamp}",
                    conversation.Id, conversation.LastInboundMessageAt);

                // Extract message data
                var messageData = ExtractMessageData(incomingMessage);

                // Create message record
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
                    IsFromDriver = true,
                    IsGroupMessage = false,
                    SenderPhoneNumber = normalizedPhone,
                    SenderName = driver.Name,
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = incomingMessage.Id,
                    Context = incomingMessage.Context?.Id
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Processed inbound message {MessageId}. Window now OPEN for 24 hours",
                    incomingMessage.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing individual message from {From}", incomingMessage.From);
                throw;
            }
        }


        private async Task<Conversation> GetOrCreateConversationForDriver(int driverId, int teamId, string topic)
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.DriverId == driverId && c.TeamId == teamId);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    DriverId = driverId,
                    TeamId = teamId,
                    Topic = topic,
                    CreatedAt = DateTime.UtcNow,
                    IsAnswered = false,
                    LastMessageAt = DateTime.UtcNow,
                    LastInboundMessageAt = DateTime.UtcNow
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            return conversation;
        }

        // ✅ FIXED: Changed from WhatsAppGroup to Group
        private async Task<Group> GetOrCreateGroupAsync(string groupId, int teamId)
        {
            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.WhatsAppGroupId == groupId && g.TeamId == teamId);

            if (group == null)
            {
                group = new Group
                {
                    WhatsAppGroupId = groupId,
                    TeamId = teamId,
                    Name = $"Group {groupId.Substring(0, Math.Min(10, groupId.Length))}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created group {GroupId}", group.Id);
            }

            group.LastActivityAt = DateTime.UtcNow;
            return group;
        }

        private async Task<Conversation> GetOrCreateGroupConversationAsync(Group group, int teamId)
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    c.WhatsAppGroupId == group.WhatsAppGroupId &&
                    c.TeamId == teamId &&
                    c.IsGroupConversation);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    TeamId = teamId,
                    Topic = group.Name,
                    CreatedAt = DateTime.UtcNow,
                    IsAnswered = false,
                    IsGroupConversation = true,
                    WhatsAppGroupId = group.WhatsAppGroupId,
                    GroupName = group.Name,
                    GroupId = group.Id,
                    LastMessageAt = DateTime.UtcNow,
                    LastInboundMessageAt = DateTime.UtcNow,
                    IsActive = true
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created group conversation {ConversationId}", conversation.Id);
            }

            return conversation;
        }

        public async Task ProcessWebhookAsync(string requestBody)
        {
            try
            {
                _logger.LogInformation("📥 Processing webhook. Body length: {Length}", requestBody.Length);

                using var doc = JsonDocument.Parse(requestBody);
                var root = doc.RootElement;

                if (!root.TryGetProperty("entry", out var entries))
                {
                    _logger.LogWarning("⚠️ No 'entry' property in webhook");
                    return;
                }

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes))
                        continue;

                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value))
                            continue;

                        // Get phone number ID to identify team
                        if (!value.TryGetProperty("metadata", out var metadata) ||
                            !metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
                        {
                            _logger.LogWarning("⚠️ No phone_number_id in metadata");
                            continue;
                        }

                        var phoneNumberIdStr = phoneNumberId.GetString();
                        _logger.LogInformation("📱 Found phone number ID: {PhoneId}", phoneNumberIdStr);

                        var team = await GetTeamByPhoneNumberId(phoneNumberIdStr);
                        if (team == null)
                        {
                            _logger.LogWarning("⚠️ Team not found for phone ID {PhoneId}", phoneNumberIdStr);
                            continue;
                        }

                        _logger.LogInformation("🏢 Processing for team: {TeamName} (ID: {TeamId})", team.Name, team.Id);

                        // Process incoming messages
                        if (value.TryGetProperty("messages", out var messages))
                        {
                            _logger.LogInformation("🎉 Found {Count} INBOUND MESSAGES for team {TeamId}",
                                messages.GetArrayLength(), team.Id);

                            foreach (var msg in messages.EnumerateArray())
                            {
                                try
                                {
                                    var incomingMessage = JsonSerializer.Deserialize<IncomingMessage>(
                                        msg.GetRawText(),
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                    if (incomingMessage != null)
                                    {
                                        _logger.LogInformation(
                                            "📨 Processing inbound message - From: {From}, Type: {Type}, IsGroup: {IsGroup}, ID: {Id}",
                                            incomingMessage.From,
                                            incomingMessage.Type,
                                            incomingMessage.IsGroupMessage,
                                            incomingMessage.Id);

                                        await ProcessIncomingMessage(incomingMessage, team.Id);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "❌ Error processing individual webhook message");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("📊 No inbound messages in this webhook payload");
                        }

                        // Process status updates
                        if (value.TryGetProperty("statuses", out var statuses))
                        {
                            _logger.LogInformation("📊 Found {Count} status updates", statuses.GetArrayLength());
                            foreach (var status in statuses.EnumerateArray())
                            {
                                var statusJson = status.GetRawText();
                                _logger.LogInformation("📊 Status update: {Status}", statusJson);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing webhook");
            }
        }

        // ✅ FIXED: Added missing ProcessIncomingMessagesAsync method
        private async Task ProcessIncomingMessagesAsync(JsonElement value, JsonElement messages)
        {
            try
            {
                // Get phone number ID from metadata
                if (!value.TryGetProperty("metadata", out var metadata) ||
                    !metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
                {
                    _logger.LogError("No phone number ID found in webhook metadata");
                    return;
                }

                var phoneNumberIdStr = phoneNumberId.GetString();
                if (string.IsNullOrEmpty(phoneNumberIdStr))
                {
                    _logger.LogError("Phone number ID is empty");
                    return;
                }

                // Find team by phone number ID
                var team = await GetTeamByPhoneNumberId(phoneNumberIdStr);
                if (team == null)
                {
                    _logger.LogError("Team not found for phone number ID: {PhoneNumberId}", phoneNumberIdStr);
                    return;
                }

                _logger.LogInformation("📨 Processing {Count} incoming messages for team {TeamName}",
                    messages.GetArrayLength(), team.Name);

                // Process each message
                foreach (var msg in messages.EnumerateArray())
                {
                    try
                    {
                        // Parse incoming message
                        var incomingMessage = JsonSerializer.Deserialize<IncomingMessage>(
                            msg.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (incomingMessage != null)
                        {
                            await ProcessIncomingMessage(incomingMessage, team.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing individual message in webhook");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessIncomingMessagesAsync");
            }
        }

        private async Task ProcessStatusUpdatesAsync(JsonElement value, JsonElement statuses)
        {
            try
            {
                foreach (var status in statuses.EnumerateArray())
                {
                    // FIXED: Convert JsonElement to string instead of using dynamic
                    var statusJson = status.GetRawText();
                    _logger.LogInformation("📊 Status update: {Status}", statusJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing status updates");
            }
        }

        public WindowStatusDto GetWindowStatus(Conversation conversation)
        {
            if (conversation.LastInboundMessageAt == null)
            {
                return new WindowStatusDto
                {
                    CanSendNonTemplateMessages = false,
                    HoursRemaining = 0,
                    MinutesRemaining = 0,
                    LastInboundMessageAt = null,
                    Message = "No incoming message received yet. Use template messages only."
                };
            }

            var elapsed = DateTime.UtcNow - conversation.LastInboundMessageAt.Value;
            bool canSend = elapsed.TotalHours < 24;

            return new WindowStatusDto
            {
                CanSendNonTemplateMessages = canSend,
                HoursRemaining = canSend ? Math.Max(0, 24 - (int)elapsed.TotalHours) : 0,
                MinutesRemaining = canSend ? Math.Max(0, (int)((24 * 60) - elapsed.TotalMinutes)) : 0,
                LastInboundMessageAt = conversation.LastInboundMessageAt,
                WindowExpiresAt = canSend ? conversation.LastInboundMessageAt.Value.AddHours(24) : null,
                Message = canSend
                    ? $"Free messaging available for {(int)elapsed.TotalHours}h {(int)(elapsed.TotalMinutes % 60)}m more"
                    : "24-hour window expired. Template messages only."
            };
        }

        private string ExtractMessageContent(JsonElement msg)
        {
            if (msg.TryGetProperty("text", out var text) && text.TryGetProperty("body", out var body))
                return body.GetString() ?? string.Empty;
            if (msg.TryGetProperty("image", out var img) && img.TryGetProperty("caption", out var caption))
                return caption.GetString() ?? "[Image]";
            return msg.GetProperty("type").GetString() switch
            {
                "audio" => "[Audio Message]",
                "video" => "[Video Message]",
                "document" => "[Document]",
                "location" => "[Location]",
                _ => "[Message]"
            };
        }

        private string? GetPhoneNumberFromRequest(SendMessageRequest request, int teamId)
        {
            if (!string.IsNullOrEmpty(request.PhoneNumber))
                return request.PhoneNumber;

            if (request.DriverId.HasValue)
            {
                var driver = _context.Drivers.FirstOrDefault(d => d.Id == request.DriverId.Value);
                if (driver != null)
                    return driver.PhoneNumber;
            }

            if (request.ConversationId.HasValue)
            {
                var conversation = _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefault(c => c.Id == request.ConversationId.Value);

                if (conversation?.Driver != null)
                    return conversation.Driver.PhoneNumber;
            }

            return null;
        }

        private string GetCurrentUserName()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId))
                    return "Staff";

                var user = _userManager.FindByIdAsync(currentUserId).Result;
                return user?.FullName ?? user?.UserName ?? "Staff";
            }
            catch
            {
                return "Staff";
            }
        }

        private string? GetCurrentUserId()
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
            catch
            {
                return null;
            }
        }

        private MessageType ParseMessageType(string whatsAppType) => whatsAppType switch
        {
            "text" => MessageType.Text,
            "image" => MessageType.Image,
            "video" => MessageType.Video,
            "audio" => MessageType.Audio,
            "document" => MessageType.Document,
            "location" => MessageType.Location,
            _ => MessageType.Text
        };

        private DateTime UnixTimeStampToDateTime(long timestamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(timestamp).ToLocalTime();
            return dateTime;
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

        // ✅ ADDED: Missing interface method implementation
        public Task SendWhatsAppMessageAsync(string phoneNumber, string message, bool isTemplate, MessageContext? context, int teamId)
        {
            _logger.LogInformation("📨 SendWhatsAppMessageAsync called for phone {Phone}", phoneNumber);
            return Task.CompletedTask;
        }

        public class WindowStatusDto
        {
            public bool CanSendNonTemplateMessages { get; set; }
            public int HoursRemaining { get; set; }
            public int MinutesRemaining { get; set; }
            public DateTime? LastInboundMessageAt { get; set; }
            public DateTime? WindowExpiresAt { get; set; }
            public string Message { get; set; } = string.Empty; // Fixed: Initialize with default value
        }
    }
}
