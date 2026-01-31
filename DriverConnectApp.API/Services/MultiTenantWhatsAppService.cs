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
using System.Net.Http.Headers;

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
        private readonly IWebHostEnvironment _environment;

        public MultiTenantWhatsAppService(
            AppDbContext context,
            ILogger<MultiTenantWhatsAppService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMessageService messageService,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _messageService = messageService;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _environment = environment;
        }

        public class WhatsAppMediaService
        {
            private readonly HttpClient _httpClient;
            private readonly IWebHostEnvironment _environment;
            private readonly ILogger _logger;
            private readonly IConfiguration _configuration;
            private readonly IHttpContextAccessor _httpContextAccessor;

            public WhatsAppMediaService(
                HttpClient httpClient,
                IWebHostEnvironment environment,
                ILogger logger,
                IConfiguration configuration,
                IHttpContextAccessor httpContextAccessor)
            {
                _httpClient = httpClient;
                _environment = environment;
                _logger = logger;
                _configuration = configuration;
                _httpContextAccessor = httpContextAccessor;
                
            }

            private HttpClient CreateMediaHttpClient()
            {
                var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                return client;
            }

            public async Task<string?> UploadMediaToWhatsAppAsync(
   byte[] fileBytes,
   string fileName,
   string mimeType,
   string phoneNumberId,
   string accessToken)
            {
                try
                {
                    // ✅ FIX: Use a separate HttpClient for media operations
                    using var mediaHttpClient = CreateMediaHttpClient();
                    var uploadUrl = $"https://graph.facebook.com/v19.0/{phoneNumberId}/media";

                    using var form = new MultipartFormDataContent();
                    using var fileContent = new ByteArrayContent(fileBytes);

                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
                    form.Add(fileContent, "file", fileName);
                    form.Add(new StringContent(mimeType), "type");
                    form.Add(new StringContent("whatsapp"), "messaging_product"); // ✅ FIXED: Changed from "media" to "whatsapp"

                    mediaHttpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await mediaHttpClient.PostAsync(uploadUrl, form);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var jsonDoc = JsonDocument.Parse(responseContent);
                        if (jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                        {
                            var mediaId = idElement.GetString();
                            _logger.LogInformation("✅ Media uploaded to WhatsApp: {MediaId}", mediaId);
                            return mediaId;
                        }
                    }

                    _logger.LogError("❌ Failed to upload media to WhatsApp: {Response}", responseContent);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading media to WhatsApp");
                    return null;
                }
            }

            public async Task<(byte[]? FileBytes, string? MimeType, string? FileName)> DownloadWhatsAppMediaAsync(
        string mediaId,
        string accessToken,
        string? originalFileName = null)
            {
                try
                {
                    // ✅ FIX: Use a separate HttpClient for media operations
                    using var mediaHttpClient = CreateMediaHttpClient();

                    // Step 1: Get media URL from WhatsApp
                    var mediaUrl = $"https://graph.facebook.com/v19.0/{mediaId}";

                    // ✅ FIXED: Use HttpRequestMessage instead of shared headers
                    using var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await mediaHttpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to get media URL for {MediaId}: {StatusCode}",
                            mediaId, response.StatusCode);
                        return (null, null, null);
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseContent);

                    if (!jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                    {
                        _logger.LogError("No URL in media response for {MediaId}", mediaId);
                        return (null, null, null);
                    }

                    var downloadUrl = urlElement.GetString();
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        _logger.LogError("Empty download URL for {MediaId}", mediaId);
                        return (null, null, null);
                    }

                    // Step 2: Download the actual media file
                    using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var mediaResponse = await mediaHttpClient.SendAsync(downloadRequest);
                    if (!mediaResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to download media from {DownloadUrl}", downloadUrl);
                        return (null, null, null);
                    }

                    var fileBytes = await mediaResponse.Content.ReadAsByteArrayAsync();
                    var mimeType = mediaResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                    // ✅ ADD: Normalize WhatsApp audio MIME types
                    mimeType = NormalizeMimeType(mimeType);

                    var fileName = originalFileName ?? $"{Guid.NewGuid()}{GetFileExtension(mimeType)}";

                    _logger.LogInformation("✅ Downloaded WhatsApp media: {FileName} ({Size} bytes)",
                        fileName, fileBytes.Length);

                    return (fileBytes, mimeType, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error downloading WhatsApp media {MediaId}", mediaId);
                    return (null, null, null);
                }
            }

            private string NormalizeMimeType(string mimeType)
            {
                return mimeType?.ToLower() switch
                {
                    "audio/ogg; codecs=opus" => "audio/ogg",
                    "audio/ogg;codecs=opus" => "audio/ogg",
                    _ => mimeType
                };
            }

            private HttpClient CreateMediaHttpClientWithTimeout()
            {
                var handler = new HttpClientHandler();
                var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(60);
                return client;
            }

            public async Task<(string? LocalPath, string? FileName, long? FileSize)> DownloadAndStoreMediaAsync(
        string mediaId,
        string accessToken,
        string mediaType)
            {
                try
                {
                    _logger.LogInformation("Downloading media {MediaId} of type {MediaType}", mediaId, mediaType);

                    // ✅ FIX: Use a separate HttpClient for media operations
                    using var mediaHttpClient = CreateMediaHttpClient();

                    // Step 1: Get media URL from WhatsApp
                    var mediaUrl = $"https://graph.facebook.com/v19.0/{mediaId}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await mediaHttpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to get media URL: {StatusCode}", response.StatusCode);
                        return (null, null, null);
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(content);

                    if (!jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                    {
                        _logger.LogError("No URL in media response");
                        return (null, null, null);
                    }

                    var downloadUrl = urlElement.GetString();
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        _logger.LogError("Empty download URL");
                        return (null, null, null);
                    }

                    // Step 2: Download the actual media
                    using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var downloadResponse = await mediaHttpClient.SendAsync(downloadRequest);
                    if (!downloadResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to download media: {StatusCode}", downloadResponse.StatusCode);
                        return (null, null, null);
                    }

                    var fileBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
                    var mimeType = downloadResponse.Content.Headers.ContentType?.MediaType ??
                                  (mediaType.ToLower() switch
                                  {
                                      "image" => "image/jpeg",
                                      "video" => "video/mp4",
                                      "audio" => "audio/mpeg",
                                      "document" => "application/octet-stream",
                                      _ => "application/octet-stream"
                                  });

                    // Step 3: Save to local storage
                    var extension = GetFileExtension(mimeType);
                    var fileName = $"{Guid.NewGuid()}{extension}";

                    var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    var filePath = Path.Combine(uploadsDir, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                    // Step 4: Return the URL accessible from frontend
                    var baseUrl = _configuration["BaseUrl"] ?? $"{(_httpContextAccessor.HttpContext?.Request.Scheme ?? "https")}://{(_httpContextAccessor.HttpContext?.Request.Host.Value ?? "onestopvan.work.gd")}";
                    var localPath = $"{baseUrl}/uploads/{fileName}";

                    _logger.LogInformation("Media downloaded and saved: {LocalPath}", localPath);

                    return (localPath, fileName, fileBytes.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error downloading media {MediaId}", mediaId);
                    return (null, null, null);
                }
            }

            public async Task<string?> SaveMediaLocallyAsync(
    byte[] fileBytes,
    string fileName,
    string mimeType)
            {
                try
                {
                    var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsDir))
                        Directory.CreateDirectory(uploadsDir);

                    // ✅ ADD: Generate unique filename to prevent collisions
                    var safeFileName = Path.GetFileName(fileName);
                    var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
                    var fullPath = Path.Combine(uploadsDir, uniqueFileName);

                    await File.WriteAllBytesAsync(fullPath, fileBytes);

                    var baseUrl = _configuration["BaseUrl"] ?? "https://onestopvan.work.gd";
                    return $"{baseUrl}/uploads/{uniqueFileName}"; // ✅ Return unique filename
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving media locally");
                    return null;
                }
            }

            public string GetFileExtension(string mimeType)
            {
                return mimeType.ToLower() switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    "video/mp4" => ".mp4",
                    "video/3gpp" => ".3gp",
                    "audio/mpeg" => ".mp3",
                    "audio/aac" => ".aac",
                    "audio/ogg" => ".ogg",
                    "application/pdf" => ".pdf",
                    _ => ".bin"
                };
            }
        }


        private void ValidateMediaSize(MessageType mediaType, long fileSizeBytes)
        {
            const long MEGABYTE = 1024 * 1024;
            long maxSize = mediaType switch
            {
                MessageType.Image => 5 * MEGABYTE,     // WhatsApp limit: 5MB
                MessageType.Video => 16 * MEGABYTE,    // WhatsApp limit: 16MB
                MessageType.Audio => 16 * MEGABYTE,    // WhatsApp limit: 16MB
                MessageType.Document => 100 * MEGABYTE, // WhatsApp limit: 100MB
                _ => throw new ArgumentException($"Unsupported media type: {mediaType}")
            };

            if (fileSizeBytes > maxSize)
            {
                throw new InvalidOperationException(
                    $"WhatsApp {mediaType} size {fileSizeBytes / MEGABYTE:F2}MB exceeds limit of {maxSize / MEGABYTE}MB");
            }
        }


        public async Task<object> SendMessageAsync(SendMessageRequest request, int teamId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            _logger.LogInformation(
                "📤 Processing message for team {TeamId}: Type={MessageType}, Content={Content}",
                teamId, request.MessageType, request.Content?.Substring(0, Math.Min(50, request.Content?.Length ?? 0)));

            // ✅ Get team
            var team = await GetTeamById(teamId);
            if (team == null || !team.IsActive)
                throw new InvalidOperationException($"Team {teamId} not found or inactive");

            // ✅ Get phone number
            string phoneNumber = await GetTargetPhoneNumberAsync(request, teamId);
            if (string.IsNullOrEmpty(phoneNumber))
                throw new InvalidOperationException("Could not determine phone number");

            // ✅ For non-template messages: Check 24-hour window
            if (!request.IsTemplateMessage && !request.IsGroupMessage && request.ConversationId.HasValue)
            {
                bool canSend = await CanSendNonTemplateMessage(request.ConversationId.Value);
                if (!canSend)
                {
                    _logger.LogWarning("🚫 24-hour window expired for conversation {ConversationId}", request.ConversationId);
                    throw new InvalidOperationException(
                        "TEMPLATE_REQUIRED|Cannot send free messages outside 24-hour window. Use template messages instead.");
                }
            }

            // ✅ Send via WhatsApp API
            var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
            string? whatsAppMessageId = null;

            if (!testMode)
            {
                if (request.IsTemplateMessage)
                {
                    // Template messages use separate endpoint
                    throw new InvalidOperationException("Template messages should use SendTemplateMessageAsync");
                }
                else
                {
                    // ✅ CRITICAL FIX: Determine message type and route to correct sending method
                    if (!Enum.TryParse<MessageType>(request.MessageType, out var messageType))
                    {
                        messageType = MessageType.Text;
                    }

                    _logger.LogInformation("📤 Message routing: Type={MessageType}, HasMediaUrl={HasMediaUrl}, HasLocation={HasLocation}",
                        messageType, !string.IsNullOrEmpty(request.MediaUrl), !string.IsNullOrEmpty(request.Location));

                    // ✅ Route 1: TEXT MESSAGE (no media, no location)
                    if (messageType == MessageType.Text && string.IsNullOrEmpty(request.MediaUrl))
                    {
                        _logger.LogInformation("📝 Sending TEXT message to {Phone}", phoneNumber);
                        whatsAppMessageId = await SendWhatsAppTextMessageAndGetIdAsync(
                            phoneNumber,
                            request.Content ?? string.Empty,
                            teamId);

                        if (string.IsNullOrEmpty(whatsAppMessageId))
                        {
                            _logger.LogError("❌ Failed to send text message");
                            throw new InvalidOperationException("Failed to send text message to WhatsApp");
                        }
                        _logger.LogInformation("✅ Text message sent successfully");
                    }
                    // ✅ Route 2: MEDIA MESSAGE (image/video/audio/document)
                    else if (!string.IsNullOrEmpty(request.MediaUrl) &&
                             (messageType == MessageType.Image || messageType == MessageType.Video ||
                              messageType == MessageType.Audio || messageType == MessageType.Document))
                    {
                        _logger.LogInformation("🎯 Sending {MediaType} to {Phone} from URL: {MediaUrl}",
                            messageType, phoneNumber, request.MediaUrl);

                        bool success = await SendMediaFromUrlAsync(
                            phoneNumber,
                            request.MediaUrl,
                            messageType,
                            teamId,
                            request.Content ?? $"Sent a {messageType.ToString().ToLower()}");

                        if (success)
                        {
                            whatsAppMessageId = $"media_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}";
                            _logger.LogInformation("✅ {MediaType} sent successfully to {Phone}", messageType, phoneNumber);
                        }
                        else
                        {
                            _logger.LogError("❌ Failed to send {MediaType} to {Phone}", messageType, phoneNumber);
                            throw new InvalidOperationException($"Failed to send {messageType} media to WhatsApp");
                        }
                    }
                    // ✅ Route 3: LOCATION MESSAGE
                    else if (messageType == MessageType.Location && !string.IsNullOrEmpty(request.Location))
                    {
                        _logger.LogInformation("📍 Sending location to {Phone}: {Location}", phoneNumber, request.Location);

                        var locationParts = request.Location.Split(',');
                        if (locationParts.Length == 2 &&
                            decimal.TryParse(locationParts[0].Trim(), out var latitude) &&
                            decimal.TryParse(locationParts[1].Trim(), out var longitude))
                        {
                            bool success = await SendLocationMessageAsync(
                                phoneNumber,
                                latitude,
                                longitude,
                                teamId,
                                request.Content ?? "Location shared");

                            if (success)
                            {
                                whatsAppMessageId = $"location_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}";
                                _logger.LogInformation("✅ Location sent successfully to {Phone}", phoneNumber);
                            }
                            else
                            {
                                _logger.LogError("❌ Failed to send location to {Phone}", phoneNumber);
                                throw new InvalidOperationException("Failed to send location to WhatsApp");
                            }
                        }
                        else
                        {
                            _logger.LogError("❌ Invalid location format: {Location}. Expected: 'latitude,longitude'", request.Location);
                            throw new InvalidOperationException("Invalid location format. Expected: 'latitude,longitude'");
                        }
                    }
                    // ✅ Route 4: FALLBACK (unexpected combination)
                    else
                    {
                        _logger.LogWarning("⚠️ Unexpected message configuration - MessageType={MessageType}, HasMedia={HasMedia}, HasLocation={HasLocation}",
                            request.MessageType, !string.IsNullOrEmpty(request.MediaUrl), !string.IsNullOrEmpty(request.Location));

                        // Send as text as fallback
                        whatsAppMessageId = await SendWhatsAppTextMessageAndGetIdAsync(
                            phoneNumber,
                            request.Content ?? "Message",
                            teamId);

                        if (string.IsNullOrEmpty(whatsAppMessageId))
                        {
                            throw new InvalidOperationException("Failed to send message to WhatsApp");
                        }
                    }
                }
            }
            else
            {
                // Test mode
                whatsAppMessageId = $"test_{DateTime.UtcNow.Ticks}";
                _logger.LogInformation("🧪 TEST MODE: Would send {MessageType} to {Phone}",
                    request.MessageType, phoneNumber);
            }

            return new
            {
                Status = "SentToWhatsApp",
                WhatsAppMessageId = whatsAppMessageId,
                PhoneNumber = phoneNumber,
                Success = true,
                Timestamp = DateTime.UtcNow,
                MessageType = request.MessageType
            };
        }

        private async Task<string> SendWhatsAppTextMessageAndGetIdAsync(string to, string text, int teamId)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null || !team.IsActive)
                {
                    _logger.LogError("Team {TeamId} not found or inactive", teamId);
                    return string.Empty;
                }

                // ✅ Use PhoneNumberUtil for proper formatting
                var formattedPhone = PhoneNumberUtil.FormatForWhatsAppApi(to, team.CountryCode ?? "91");

                _logger.LogInformation("📤 Sending WhatsApp text to {FormattedPhone}", formattedPhone);

                var apiVersion = team.ApiVersion ?? "19.0";
                var url = $"https://graph.facebook.com/v{apiVersion}/{team.WhatsAppPhoneNumberId}/messages";

                var requestBody = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = formattedPhone,
                    type = "text",
                    text = new { body = text }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // ✅ Extract WhatsApp message ID from response
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    if (jsonDoc.RootElement.TryGetProperty("messages", out var messages) &&
                        messages.GetArrayLength() > 0 &&
                        messages[0].TryGetProperty("id", out var idElement))
                    {
                        var messageId = idElement.GetString();
                        _logger.LogInformation("✅ WhatsApp text sent to {Phone}, Message ID: {MessageId}",
                            formattedPhone, messageId);
                        return messageId ?? string.Empty;
                    }
                }

                _logger.LogError("❌ WhatsApp API Error: {StatusCode} - {Response}",
                    response.StatusCode, responseContent);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending WhatsApp text to {To}", to);
                return string.Empty;
            }
        }

        private async Task<string> GetTargetPhoneNumberAsync(SendMessageRequest request, int teamId)
        {
            if (!string.IsNullOrEmpty(request.PhoneNumber))
                return request.PhoneNumber;

            if (request.DriverId.HasValue)
            {
                var driver = await _context.Drivers.FindAsync(request.DriverId.Value);
                if (driver != null)
                    return driver.PhoneNumber;
            }

            if (request.ConversationId.HasValue)
            {
                var conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.TeamId == teamId);

                if (conversation?.Driver != null)
                    return conversation.Driver.PhoneNumber;
            }

            throw new InvalidOperationException("Could not determine phone number");
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
                Conversation? conversation = null;

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
                    await SendMediaFromUrlAsync(phoneNumber, request.MediaUrl, messageType, team.Id, request.Content);
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
                var currentUserId = currentUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var currentUserName = "Staff";

                if (!string.IsNullOrEmpty(currentUserId))
                {
                    var user = await _userManager.FindByIdAsync(currentUserId);
                    currentUserName = user?.FullName ?? user?.UserName ?? "Staff";
                }

                // Find or create conversation
                Conversation? conversation = null;

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

        public async Task<string?> SendTemplateMessageAsync(
            string to,
            string templateName,
            Dictionary<string, string> templateParameters,
            int teamId,
            string? languageCode = "en_US")
        {
            try
            {
                string displayContent = GenerateTemplateDisplayContent(templateName, templateParameters);
                _logger.LogInformation("📝 Template Display Content: {Content}", displayContent);
                _logger.LogInformation("🎯 Sending template: {Template} to {To}", templateName, to);

                var team = await GetTeamById(teamId);
                if (team == null)
                {
                    _logger.LogError("❌ Team {TeamId} not found", teamId);
                    return null;
                }

                if (string.IsNullOrEmpty(team.WhatsAppPhoneNumberId) ||
                    string.IsNullOrEmpty(team.WhatsAppAccessToken))
                {
                    _logger.LogError("❌ Team {TeamId} WhatsApp config missing", teamId);
                    return null;
                }

                // ✅ FIX: Use UK country code (44) for +447 numbers
                var detectedCountryCode = PhoneNumberUtil.DetectCountryCode(to);
                var countryCodeToUse = detectedCountryCode ?? team.CountryCode ?? "44"; // UK default

                _logger.LogInformation("🌍 Country code - Detected: {Detected}, Using: {Using}",
                    detectedCountryCode ?? "none", countryCodeToUse);

                var formattedPhone = PhoneNumberUtil.FormatForWhatsAppApi(to, countryCodeToUse);

                _logger.LogInformation("📱 Phone formatting: {Original} → {Formatted}", to, formattedPhone);
                _logger.LogInformation("🔑 Using Phone ID: {PhoneId}, API v{Version}",
                    team.WhatsAppPhoneNumberId, team.ApiVersion ?? "19.0");

                var apiVersion = string.IsNullOrEmpty(team.ApiVersion) ? "19.0" : team.ApiVersion;
                var url = $"https://graph.facebook.com/v{apiVersion}/{team.WhatsAppPhoneNumberId}/messages";

                // ✅ BUILD PROPER WHATSAPP TEMPLATE STRUCTURE
                var requestBody = new Dictionary<string, object>
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = formattedPhone,
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

                // Add parameters if they exist
                if (templateParameters != null && templateParameters.Any())
                {
                    var components = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "body",
                            ["parameters"] = templateParameters
                                .OrderBy(p => p.Key)
                                .Select(p => new Dictionary<string, object>
                                {
                                    ["type"] = "text",
                                    ["text"] = p.Value
                                })
                                .ToList()
                        }
                    };

                    ((Dictionary<string, object>)requestBody["template"])["components"] = components;
                }

                var json = JsonSerializer.Serialize(requestBody);
                _logger.LogInformation("📤 WhatsApp API Payload: {Json}", json);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 WhatsApp Response Status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("📥 WhatsApp Response Body: {Response}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    // ✅ EXTRACT AND RETURN THE REAL WHATSAPP ID
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    if (jsonDoc.RootElement.TryGetProperty("messages", out var messagesArray) &&
                        messagesArray.GetArrayLength() > 0 &&
                        messagesArray[0].TryGetProperty("id", out var idElement))
                    {
                        var messageId = idElement.GetString();
                        _logger.LogInformation("✅ Template '{Template}' sent to {Phone}, Message ID: {MessageId}",
                            templateName, formattedPhone, messageId);
                        return messageId; // ✅ This is the REAL WhatsApp ID
                    }
                }

                _logger.LogError("❌ WhatsApp API Error: {StatusCode} - {Response}",
                    response.StatusCode, responseContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SendTemplateMessageAsync crashed for template {Template}", templateName);
                return null;
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

                string formattedPhone;

                // Phone number formatting logic (keep your existing)
                if (to.Contains("@c.us"))
                {
                    formattedPhone = PhoneNumberUtil.ExtractPhoneFromWhatsAppId(to) ?? string.Empty;
                }
                else
                {
                    formattedPhone = PhoneNumberUtil.FormatForWhatsAppApi(to, team.CountryCode ?? "91") ?? string.Empty;
                }

                _logger.LogInformation("📤 Sending WhatsApp text: Original={Original}, Formatted={Formatted}, Team={TeamName}",
                    to, formattedPhone, team.Name);

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";

                var requestBody = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = formattedPhone,
                    type = "text",
                    text = new { body = text }
                };

                var json = JsonSerializer.Serialize(requestBody);

                // ✅ FIXED: Use a separate HttpClient for sending text messages
                using var sendClient = new HttpClient();
                sendClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await sendClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 WhatsApp Response: Status={StatusCode}, Body={Response}",
                    response.StatusCode, responseContent.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ WhatsApp text message sent to {Phone}", to);
                    return true;
                }

                _logger.LogError("❌ Failed to send WhatsApp text: {Error}", responseContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending WhatsApp text to {To}", to);
                return false;
            }
        }

        public async Task<(bool Success, string? ErrorMessage, string? MediaUrl)> SendMediaMessageAsync(
    string to,
    byte[] fileBytes,
    string fileName,
    string mimeType,
    MessageType mediaType,
    int teamId,
    string? caption = null)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null || !team.IsActive)
                    return (false, "Team not found or inactive", null);

                ValidateMediaSize(mediaType, fileBytes.Length);

                // ✅ WhatsApp size limits
                var fileSizeMB = fileBytes.Length / (1024.0 * 1024.0);

                switch (mediaType)
                {
                    case MessageType.Video when fileSizeMB > 16:
                        return (false, "Video size exceeds 16MB limit", null);
                    case MessageType.Image when fileSizeMB > 5:
                        return (false, "Image size exceeds 5MB limit", null);
                    case MessageType.Audio when fileSizeMB > 16:
                        return (false, "Audio size exceeds 16MB limit", null);
                    case MessageType.Document when fileSizeMB > 100:
                        return (false, "Document size exceeds 100MB limit", null);
                }

                // ✅ Step 1: Upload to WhatsApp Cloud API
                var mediaService = new WhatsAppMediaService(_httpClient, _environment, _logger, _configuration, _httpContextAccessor);
                var mediaId = await mediaService.UploadMediaToWhatsAppAsync(
                    fileBytes, fileName, mimeType,
                    team.WhatsAppPhoneNumberId ?? throw new InvalidOperationException("WhatsAppPhoneNumberId is null"),
                    team.WhatsAppAccessToken ?? throw new InvalidOperationException("WhatsAppAccessToken is null"));

                if (string.IsNullOrEmpty(mediaId))
                    return (false, "Failed to upload media to WhatsApp", null);

                // ✅ Step 2: Save locally for our records
                var localUrl = await mediaService.SaveMediaLocallyAsync(fileBytes, fileName, mimeType);

                // ✅ Step 3: Send message with media_id (NOT link!)
                var formattedPhone = PhoneNumberUtil.FormatForWhatsAppApi(to, team.CountryCode ?? "44") ?? string.Empty;
                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";

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
                    ["to"] = formattedPhone,
                    ["type"] = whatsappMediaType,
                    [whatsappMediaType] = new Dictionary<string, object>
                    {
                        ["id"] = mediaId
                    }
                };

                if (!string.IsNullOrEmpty(caption) && mediaType != MessageType.Audio)
                {
                    ((Dictionary<string, object>)requestBody[whatsappMediaType])["caption"] = caption;
                }

                var json = JsonSerializer.Serialize(requestBody);

                // ✅ FIX: Use a separate HttpClient for sending the media message
                using var mediaHttpClient = new HttpClient();
                mediaHttpClient.Timeout = TimeSpan.FromSeconds(30);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                mediaHttpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await mediaHttpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ WhatsApp {MediaType} sent to {To}", mediaType, to);
                    return (true, null, localUrl);
                }
                else
                {
                    var error = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var errorMessage = error.TryGetProperty("error", out var errorObj)
                        ? errorObj.GetProperty("message").GetString()
                        : "Unknown error";

                    _logger.LogError("❌ Failed to send WhatsApp media: {Error}", errorMessage);
                    return (false, errorMessage, null);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("exceeds limit"))
            {
                // Handle size limit errors
                return (false, ex.Message, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp media message");
                return (false, ex.Message, null);
            }
        }

        public async Task<bool> SendMediaFromUrlAsync(string to, string mediaUrl, MessageType mediaType, int teamId, string? caption = null)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null || !team.IsActive)
                {
                    _logger.LogError("Team {TeamId} not found or inactive", teamId);
                    return false;
                }

                // ✅ FIX: Download file from URL using a separate HttpClient
                using var downloadClient = new HttpClient();
                downloadClient.Timeout = TimeSpan.FromSeconds(30);

                var response = await downloadClient.GetAsync(mediaUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download media from {MediaUrl}", mediaUrl);
                    return false;
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = Path.GetFileName(mediaUrl) ?? $"media_{Guid.NewGuid()}";
                var mimeType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                // Use the new media upload method
                var result = await SendMediaMessageAsync(to, fileBytes, fileName, mimeType, mediaType, teamId, caption);
                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending media from URL");
                return false;
            }
        }

        [Obsolete("Use SendMediaMessageAsync with byte array instead")]
        public async Task<bool> SendMediaMessageAsync(string to, string mediaUrl, MessageType mediaType, int teamId, string? caption = null)
        {
            _logger.LogWarning("⚠️ Using deprecated SendMediaMessageAsync with URL. Convert to byte array method.");
            return await SendMediaFromUrlAsync(to, mediaUrl, mediaType, teamId, caption);
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
                else if (!string.IsNullOrEmpty(mediaUrl) && messageType != MessageType.Text)
                {
                    // ✅ FIX: Use a separate HttpClient for downloading
                    using var downloadClient = new HttpClient();
                    downloadClient.Timeout = TimeSpan.FromSeconds(30);

                    var downloadResponse = await downloadClient.GetAsync(mediaUrl);
                    if (!downloadResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to download media from {MediaUrl}", mediaUrl);
                        return false;
                    }

                    var fileBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
                    var fileName = Path.GetFileName(mediaUrl) ?? $"media_{Guid.NewGuid()}";
                    var mimeType = downloadResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                    // Upload media to WhatsApp
                    var mediaService = new WhatsAppMediaService(_httpClient, _environment, _logger, _configuration, _httpContextAccessor);
                    var mediaId = await mediaService.UploadMediaToWhatsAppAsync(
                        fileBytes, fileName, mimeType,
                        team.WhatsAppPhoneNumberId ?? throw new InvalidOperationException("WhatsAppPhoneNumberId is null"),
                        team.WhatsAppAccessToken ?? throw new InvalidOperationException("WhatsAppAccessToken is null"));

                    if (string.IsNullOrEmpty(mediaId))
                    {
                        _logger.LogError("Failed to upload media to WhatsApp for group message");
                        return false;
                    }

                    string whatsappMediaType = messageType switch
                    {
                        MessageType.Image => "image",
                        MessageType.Video => "video",
                        MessageType.Audio => "audio",
                        MessageType.Document => "document",
                        _ => "document"
                    };

                    var requestBodyDict = new Dictionary<string, object>
                    {
                        ["messaging_product"] = "whatsapp",
                        ["recipient_type"] = "group",
                        ["to"] = groupId,
                        ["type"] = whatsappMediaType,
                        [whatsappMediaType] = new Dictionary<string, object>
                        {
                            ["id"] = mediaId
                        }
                    };

                    if (!string.IsNullOrEmpty(messageText))
                    {
                        // ✅ FIX: Cast properly to access dictionary
                        var mediaDict = requestBodyDict[whatsappMediaType] as Dictionary<string, object>;
                        if (mediaDict != null)
                        {
                            mediaDict["caption"] = messageText;
                        }
                    }

                    requestBody = requestBodyDict;
                }
                else
                {
                    _logger.LogWarning("Cannot send {MessageType} without media URL for team {TeamName}", messageType, team.Name);
                    return false;
                }

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // ✅ FIX: Use a separate HttpClient for sending group messages
                using var sendClient = new HttpClient();
                sendClient.Timeout = TimeSpan.FromSeconds(30);
                sendClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await sendClient.PostAsync(url, content);

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

        public async Task<MediaUploadResult> UploadMediaForFrontend(IFormFile file, int teamId)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null)
                    return new MediaUploadResult { Success = false, Error = "Team not found" };

                // Validate file size
                var maxSize = GetMaxFileSize(Path.GetExtension(file.FileName).ToLower());
                if (file.Length > maxSize)
                    return new MediaUploadResult
                    {
                        Success = false,
                        Error = $"File exceeds {maxSize / (1024 * 1024)}MB limit"
                    };

                // Read file
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                // Upload to WhatsApp
                var mediaService = new WhatsAppMediaService(_httpClient, _environment, _logger, _configuration, _httpContextAccessor);
                var mediaId = await mediaService.UploadMediaToWhatsAppAsync(
                    fileBytes, file.FileName, file.ContentType,
                    team.WhatsAppPhoneNumberId ?? string.Empty,
                    team.WhatsAppAccessToken ?? string.Empty);

                if (string.IsNullOrEmpty(mediaId))
                    return new MediaUploadResult { Success = false, Error = "Failed to upload to WhatsApp" };

                // Save locally
                var localUrl = await mediaService.SaveMediaLocallyAsync(fileBytes, file.FileName, file.ContentType);

                return new MediaUploadResult
                {
                    Success = true,
                    MediaId = mediaId,
                    LocalUrl = localUrl,
                    FileName = file.FileName,
                    FileSize = file.Length,
                    MimeType = file.ContentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media for frontend");
                return new MediaUploadResult { Success = false, Error = ex.Message };
            }
        }

        private long GetMaxFileSize(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" => 5 * 1024 * 1024, // 5MB
                ".mp4" or ".mov" or ".avi" => 16 * 1024 * 1024, // 16MB
                ".mp3" or ".m4a" or ".ogg" => 16 * 1024 * 1024, // 16MB
                ".pdf" or ".doc" or ".docx" or ".txt" => 100 * 1024 * 1024, // 100MB
                _ => 5 * 1024 * 1024 // Default 5MB
            };
        }

        // ✅ ADD: Model for media upload result
        public class MediaUploadResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? MediaId { get; set; }
            public string? LocalUrl { get; set; }
            public string? FileName { get; set; }
            public long FileSize { get; set; }
            public string? MimeType { get; set; }
        }

        // ✅ ADD: Method to check media status
        public async Task<MediaStatusResult> CheckMediaStatus(string mediaId, int teamId)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null)
                    return new MediaStatusResult { Exists = false, Error = "Team not found" };

                var url = $"https://graph.facebook.com/v19.0/{mediaId}";
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await _httpClient.GetAsync(url);
                return new MediaStatusResult
                {
                    Exists = response.IsSuccessStatusCode,
                    Error = response.IsSuccessStatusCode ? null : "Media not found"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking media status");
                return new MediaStatusResult { Exists = false, Error = ex.Message };
            }
        }

        public class MediaStatusResult
        {
            public bool Exists { get; set; }
            public string? Error { get; set; }
        }

        public async Task<bool> SendLocationMessageAsync(string to, decimal latitude, decimal longitude, int teamId, string? name = null, string? address = null)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null || !team.IsActive)
                {
                    _logger.LogError("Team {TeamId} not found or inactive", teamId);
                    return false;
                }

                var formattedPhone = PhoneNumberUtil.FormatForWhatsAppApi(to, team.CountryCode ?? "91");
                _logger.LogInformation("📍 Sending location to {Phone}: Lat={Latitude}, Lon={Longitude}",
                    formattedPhone, latitude, longitude);

                var url = $"https://graph.facebook.com/v{team.ApiVersion}/{team.WhatsAppPhoneNumberId}/messages";

                var requestBody = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = formattedPhone,
                    type = "location",
                    location = new
                    {
                        latitude = latitude.ToString("F6"),
                        longitude = longitude.ToString("F6"),
                        name = name ?? "Location",
                        address = address ?? name ?? "Shared Location"  // ✅ NOW USES address PARAMETER
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);
                httpRequest.Content = content;

                var response = await _httpClient.SendAsync(httpRequest);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Location sent to {Phone}", formattedPhone);
                    return true;
                }
                else
                {
                    _logger.LogError("❌ Failed to send location to {Phone}: {StatusCode} - {Response}",
                        formattedPhone, response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending location message to {To}", to);
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

            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.WhatsAppPhoneNumberId == phoneNumberId && t.IsActive);
            return team;
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

            var messageData = ExtractMessageData(incomingMessage, team);

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
                // Extract phone number
                var phoneNumber = PhoneNumberUtil.ExtractPhoneFromWhatsAppId(incomingMessage.From);
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    _logger.LogWarning("⚠️ Could not extract phone number from: {From}", incomingMessage.From);
                    return;
                }

                // Normalize phone number
                var detectedCountryCode = PhoneNumberUtil.DetectCountryCode(phoneNumber);
                var countryCodeToUse = detectedCountryCode ?? team.CountryCode ?? "91";
                var normalizedPhone = PhoneNumberUtil.NormalizePhoneNumber(phoneNumber, countryCodeToUse);

                // Find or create driver
                var driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalizedPhone && d.TeamId == team.Id);

                if (driver == null)
                {
                    driver = new Driver
                    {
                        Name = normalizedPhone,
                        PhoneNumber = normalizedPhone,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        TeamId = team.Id
                    };
                    _context.Drivers.Add(driver);
                    await _context.SaveChangesAsync();
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
                }

                // ✅ CRITICAL: Check if message already exists (deduplication)
                var existingMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.WhatsAppMessageId == incomingMessage.Id);

                if (existingMessage != null)
                {
                    _logger.LogInformation("⚠️ Message already exists: {MessageId}", incomingMessage.Id);
                    return; // Skip duplicate
                }

                // Update conversation timestamps
                conversation.LastInboundMessageAt = DateTime.UtcNow;
                conversation.LastMessageAt = DateTime.UtcNow;

                // ✅ EXTRACT MESSAGE DATA WITH MEDIA DOWNLOADING
                var messageData = ExtractMessageData(incomingMessage, team);

                string? mediaUrl = messageData.MediaUrl;
                string? fileName = messageData.FileName;
                long? fileSize = messageData.FileSize;

                // Create WhatsAppMediaService instance
                var mediaService = new WhatsAppMediaService(_httpClient, _environment, _logger, _configuration, _httpContextAccessor);

                if (!string.IsNullOrEmpty(incomingMessage.Image?.Id))
                {
                    var result = await mediaService.DownloadAndStoreMediaAsync(
                        incomingMessage.Image.Id,
                        team.WhatsAppAccessToken,
                        "image");

                    if (result.LocalPath != null)
                    {
                        mediaUrl = result.LocalPath;
                        fileName = result.FileName ?? "image.jpg";
                        fileSize = result.FileSize;
                    }
                }
                else if (!string.IsNullOrEmpty(incomingMessage.Video?.Id))
                {
                    var result = await mediaService.DownloadAndStoreMediaAsync(
                        incomingMessage.Video.Id,
                        team.WhatsAppAccessToken,
                        "video");

                    if (result.LocalPath != null)
                    {
                        mediaUrl = result.LocalPath;
                        fileName = result.FileName ?? "video.mp4";
                        fileSize = result.FileSize;
                    }
                }
                else if (!string.IsNullOrEmpty(incomingMessage.Audio?.Id))
                {
                    var result = await mediaService.DownloadAndStoreMediaAsync(
                        incomingMessage.Audio.Id,
                        team.WhatsAppAccessToken,
                        "audio");

                    if (result.LocalPath != null)
                    {
                        mediaUrl = result.LocalPath;
                        fileName = result.FileName ?? "audio.mp3";
                        fileSize = result.FileSize;
                    }
                }
                else if (!string.IsNullOrEmpty(incomingMessage.Document?.Id))
                {
                    var result = await mediaService.DownloadAndStoreMediaAsync(
                        incomingMessage.Document.Id,
                        team.WhatsAppAccessToken,
                        "document");

                    if (result.LocalPath != null)
                    {
                        mediaUrl = result.LocalPath;
                        fileName = incomingMessage.Document.Filename ?? result.FileName ?? "document.pdf";
                        fileSize = result.FileSize;
                    }
                }

                // Create message record
                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Content = messageData.Content,
                    MessageType = messageData.Type,
                    MediaUrl = mediaUrl,
                    FileName = fileName,
                    FileSize = fileSize,
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
                    Context = incomingMessage.Context?.Id,
                    Status = MessageStatus.Delivered
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Inbound message saved: {MessageId}, MediaUrl: {MediaUrl}",
                    incomingMessage.Id, messageData.MediaUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing inbound message");
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

                        if (string.IsNullOrEmpty(phoneNumberIdStr))
                        {
                            _logger.LogWarning("⚠️ Phone number ID is empty");
                            continue;
                        }

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
        private Task ProcessIncomingMessagesAsync(JsonElement value, JsonElement messages)
        {
            try
            {
                // Get phone number ID from metadata
                if (!value.TryGetProperty("metadata", out var metadata) ||
                    !metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
                {
                    _logger.LogError("No phone number ID found in webhook metadata");
                    return Task.CompletedTask;
                }

                var phoneNumberIdStr = phoneNumberId.GetString();
                if (string.IsNullOrEmpty(phoneNumberIdStr))
                {
                    _logger.LogError("Phone number ID is empty");
                    return Task.CompletedTask;
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessIncomingMessagesAsync");
                return Task.CompletedTask;
            }
        }

        private Task ProcessStatusUpdatesAsync(JsonElement value, JsonElement statuses)
        {
            try
            {
                foreach (var status in statuses.EnumerateArray())
                {
                    var statusJson = status.GetRawText();
                    _logger.LogInformation("📊 Status update: {Status}", statusJson);
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing status updates");
                return Task.CompletedTask;
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
                 string? ContactPhone) ExtractMessageData(IncomingMessage incomingMessage, Team team)
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
                fileName = "image.jpg";
            }
            else if (incomingMessage.Video != null)
            {
                content = incomingMessage.Video.Caption ?? "Video";
                type = MessageType.Video;
                mediaUrl = incomingMessage.Video.Link;
                mimeType = incomingMessage.Video.MimeType;
                fileName = "video.mp4";
            }
            else if (incomingMessage.Audio != null)
            {
                content = "Audio message";
                type = MessageType.Audio;
                mediaUrl = incomingMessage.Audio.Link;
                mimeType = incomingMessage.Audio.MimeType;
                fileName = "audio.mp3";
            }
            else if (incomingMessage.Document != null)
            {
                content = incomingMessage.Document.Caption ?? "Document";
                type = MessageType.Document;
                mediaUrl = incomingMessage.Document.Link;
                fileName = incomingMessage.Document.Filename ?? "document.pdf";
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

        private async Task<string?> DownloadAndSaveWhatsAppMedia(string mediaId, Team team, string mediaType, string? originalMimeType)
        {
            try
            {
                var mediaService = new WhatsAppMediaService(_httpClient, _environment, _logger, _configuration, _httpContextAccessor);

                var result = await mediaService.DownloadWhatsAppMediaAsync(
                    mediaId,
                    team.WhatsAppAccessToken ?? throw new InvalidOperationException("WhatsAppAccessToken is null"),
                    $"{mediaType}_{mediaId}"
                );

                if (result.FileBytes == null)
                {
                    _logger.LogError("Failed to download WhatsApp media {MediaId}", mediaId);
                    return null;
                }

                var extension = mediaService.GetFileExtension(result.MimeType ?? originalMimeType ?? "application/octet-stream");
                var fileName = $"{Guid.NewGuid()}{extension}";
                var localUrl = await mediaService.SaveMediaLocallyAsync(result.FileBytes, fileName, result.MimeType ?? originalMimeType ?? "application/octet-stream");

                _logger.LogInformation("✅ Downloaded and saved WhatsApp media: {LocalUrl}", localUrl);
                return localUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading WhatsApp media {MediaId}", mediaId);
                return null;
            }
        }

        public string RenderTemplateForDisplay(string templateName, Dictionary<string, string>? parameters)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return "Template message";

            try
            {
                var orderedParams = parameters?
                    .Where(p => int.TryParse(p.Key, out _))
                    .OrderBy(p => int.Parse(p.Key))
                    .Select(p => p.Value)
                    .ToList() ?? new List<string>();

                // ✅ ACTUAL WHATSAPP TEMPLATE CONTENT
                return templateName.ToLowerInvariant() switch
                {
                    // Your actual hello_world template content
                    "hello_world" when orderedParams.Count >= 1 =>
                        $"Hello {orderedParams[0]}, welcome to our service! This is a test message from WhatsApp Business API.",
                    "hello_world" =>
                        "Hello, welcome to our service! This is a test message from WhatsApp Business API.",

                    // Add other templates with actual content
                    "booking_confirmation" when orderedParams.Count >= 3 =>
                        $"Your booking #{orderedParams[0]} has been confirmed for {orderedParams[1]} at {orderedParams[2]}. Thank you for choosing us!",

                    "delivery_update" when orderedParams.Count >= 2 =>
                        $"Your delivery #{orderedParams[0]} is on the way. Estimated arrival: {orderedParams[1]}. Track your order on our website.",

                    "welcome_message" when orderedParams.Count >= 2 =>
                        $"Welcome to {orderedParams[0]}, {orderedParams[1]}! We're glad to have you with us. Let us know if you need any assistance.",

                    "payment_reminder" when orderedParams.Count >= 3 =>
                        $"Hello {orderedParams[0]}, this is a reminder that your payment of {orderedParams[1]} is due on {orderedParams[2]}. Please make payment to avoid service disruption.",

                    "order_shipped" when orderedParams.Count >= 2 =>
                        $"Your order #{orderedParams[0]} has been shipped. Tracking number: {orderedParams[1]}. Expected delivery in 3-5 business days.",

                    "appointment_reminder" when orderedParams.Count >= 3 =>
                        $"Reminder: Your appointment with {orderedParams[0]} is on {orderedParams[1]} at {orderedParams[2]}. Please arrive 10 minutes early.",

                    // Default fallback - should match your WhatsApp templates
                    _ => orderedParams.Count > 0
                        ? $"{templateName}: {string.Join(", ", orderedParams)}"
                        : $"{templateName} message"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to render template {TemplateName}", templateName);
                return $"{templateName} message";
            }
        }

        private string GetTemplateDefaultMessage(string templateName)
        {
            return templateName.ToLower() switch
            {
                "hello_world" => "Hello! Welcome to our service.",
                "booking_confirmation" => "Your booking has been confirmed.",
                "delivery_update" => "Your delivery is on the way.",
                "welcome_message" => "Welcome to our service!",
                "payment_reminder" => "Payment reminder sent.",
                "order_shipped" => "Your order has been shipped.",
                "appointment_reminder" => "Appointment reminder sent.",
                "service_completed" => "Service completed successfully.",
                "invoice_sent" => "Invoice has been issued.",
                "feedback_request" => "We'd love your feedback!",
                _ => $"Template: {templateName}"
            };
        }

        private string GenerateTemplateDisplayContent(string templateName, Dictionary<string, string> parameters)
        {
            if (parameters == null || !parameters.Any())
                return $"Template: {templateName}";

            var paramValues = parameters
                .OrderBy(p => p.Key)
                .Select(p => p.Value)
                .ToList();

            // This should match the same logic as in MessagesController
            return templateName.ToLower() switch
            {
                "hello_world" when paramValues.Count >= 1 => $"Hello {paramValues[0]}, welcome to our service!",
                "welcome_message" when paramValues.Count >= 2 => $"Welcome {paramValues[0]} to {paramValues[1]}!",
                // Add other templates as needed
                _ => $"{templateName}: {string.Join(", ", paramValues)}"
            };
        }

        // ✅ ADDED: Missing interface method implementation
        public async Task<bool> SendWhatsAppMessageAsync(string phoneNumber, string message, bool isTemplate, MessageContext? context, int teamId)
        {
            try
            {
                if (isTemplate)
                {
                    _logger.LogWarning("SendWhatsAppMessageAsync called for template - use SendTemplateMessageAsync instead");
                    return false;
                }

                // ✅ ACTUALLY SEND THE MESSAGE
                return await SendWhatsAppTextMessageAsync(phoneNumber, message, teamId, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in SendWhatsAppMessageAsync for {Phone}", phoneNumber);
                return false;
            }
        }

        public class WindowStatusDto
        {
            public bool CanSendNonTemplateMessages { get; set; }
            public int HoursRemaining { get; set; }
            public int MinutesRemaining { get; set; }
            public DateTime? LastInboundMessageAt { get; set; }
            public DateTime? WindowExpiresAt { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}