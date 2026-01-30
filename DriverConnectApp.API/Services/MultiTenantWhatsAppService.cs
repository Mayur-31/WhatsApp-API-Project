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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMessageService _messageService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _environment;
        private const int MAX_RETRIES = 3;

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
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _messageService = messageService;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _environment = environment;
        }

        public class WhatsAppMediaService
        {
            private readonly IHttpClientFactory _httpClientFactory;
            private readonly IWebHostEnvironment _environment;
            private readonly ILogger _logger;
            private readonly IConfiguration _configuration;
            private readonly IHttpContextAccessor _httpContextAccessor;

            public WhatsAppMediaService(
                IHttpClientFactory httpClientFactory,
                IWebHostEnvironment environment,
                ILogger logger,
                IConfiguration configuration,
                IHttpContextAccessor httpContextAccessor)
            {
                _httpClientFactory = httpClientFactory;
                _environment = environment;
                _logger = logger;
                _configuration = configuration;
                _httpContextAccessor = httpContextAccessor;
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
                    var uploadUrl = $"https://graph.facebook.com/v19.0/{phoneNumberId}/media";

                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    using var form = new MultipartFormDataContent();
                    using var fileContent = new ByteArrayContent(fileBytes);

                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
                    form.Add(fileContent, "file", fileName);
                    form.Add(new StringContent(mimeType), "type");
                    form.Add(new StringContent("whatsapp"), "messaging_product");

                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await httpClient.PostAsync(uploadUrl, form);
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
                    // Step 1: Get media URL from WhatsApp
                    var mediaUrl = $"https://graph.facebook.com/v19.0/{mediaId}";

                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    using var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await httpClient.SendAsync(request);
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

                    var mediaResponse = await httpClient.SendAsync(downloadRequest);
                    if (!mediaResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to download media from {DownloadUrl}", downloadUrl);
                        return (null, null, null);
                    }

                    var fileBytes = await mediaResponse.Content.ReadAsByteArrayAsync();
                    var mimeType = mediaResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
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

            public async Task<(string? LocalPath, string? FileName, long? FileSize)> DownloadAndStoreMediaAsync(
                string mediaId,
                string accessToken,
                string mediaType)
            {
                try
                {
                    _logger.LogInformation("Downloading media {MediaId} of type {MediaType}", mediaId, mediaType);

                    var result = await DownloadWhatsAppMediaAsync(mediaId, accessToken, $"{mediaType}_{mediaId}");

                    if (result.FileBytes == null || result.MimeType == null)
                    {
                        return (null, null, null);
                    }

                    // Step 3: Save to local storage
                    var extension = GetFileExtension(result.MimeType);
                    var fileName = $"{Guid.NewGuid()}{extension}";

                    var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    var filePath = Path.Combine(uploadsDir, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, result.FileBytes);

                    // Step 4: Return the URL accessible from frontend
                    var baseUrl = _configuration["BaseUrl"] ?? $"{(_httpContextAccessor.HttpContext?.Request.Scheme ?? "https")}://{(_httpContextAccessor.HttpContext?.Request.Host.Value ?? "onestopvan.work.gd")}";
                    var localPath = $"{baseUrl}/uploads/{fileName}";

                    _logger.LogInformation("Media downloaded and saved: {LocalPath}", localPath);

                    return (localPath, fileName, result.FileBytes.Length);
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

                    var safeFileName = Path.GetFileName(fileName);
                    var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
                    var fullPath = Path.Combine(uploadsDir, uniqueFileName);

                    await File.WriteAllBytesAsync(fullPath, fileBytes);

                    var baseUrl = _configuration["BaseUrl"] ?? "https://onestopvan.work.gd";
                    return $"{baseUrl}/uploads/{uniqueFileName}";
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
                    "audio/amr" => ".amr",
                    "application/pdf" => ".pdf",
                    "application/msword" => ".doc",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                    "text/plain" => ".txt",
                    _ => ".bin"
                };
            }
        }

        private void ValidateMediaSize(MessageType mediaType, long fileSizeBytes)
        {
            const long MEGABYTE = 1024 * 1024;
            long maxSize = mediaType switch
            {
                MessageType.Image => 5 * MEGABYTE,
                MessageType.Video => 16 * MEGABYTE,
                MessageType.Audio => 16 * MEGABYTE,
                MessageType.Document => 100 * MEGABYTE,
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
                teamId, request.MessageType, request.Content?.Substring(0, Math.Min(50, request.Content.Length)));

            var team = await GetTeamById(teamId);
            if (team == null || !team.IsActive)
                throw new InvalidOperationException($"Team {teamId} not found or inactive");

            string phoneNumber = await GetTargetPhoneNumberAsync(request, teamId);
            if (string.IsNullOrEmpty(phoneNumber))
                throw new InvalidOperationException("Could not determine phone number");

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

            var testMode = _configuration.GetValue<bool>("WhatsApp:TestMode", false);
            string? whatsAppMessageId = null;

            if (!testMode)
            {
                if (request.IsTemplateMessage)
                {
                    throw new InvalidOperationException("Template messages should use SendTemplateMessageAsync");
                }
                else
                {
                    whatsAppMessageId = await SendWhatsAppTextMessageAndGetIdAsync(
                        phoneNumber,
                        request.Content ?? string.Empty,
                        teamId);
                }
            }

            return new
            {
                Status = "SentToWhatsApp",
                WhatsAppMessageId = whatsAppMessageId,
                PhoneNumber = phoneNumber,
                Success = true,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<bool> ProcessQueuedMessageAsync(int messageId, int teamId)
        {
            try
            {
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE Messages SET Status = {0}, RetryCount = RetryCount + 1 WHERE Id = {1} AND Status = {2}",
                    (int)MessageStatus.Sent,
                    messageId,
                    (int)MessageStatus.Queued);

                if (affectedRows == 0)
                {
                    _logger.LogWarning("⚠️ Message {MsgId} already processed or not found", messageId);
                    return false;
                }

                var message = await _context.Messages
                    .Include(m => m.Conversation)
                        .ThenInclude(c => c.Driver)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                {
                    _logger.LogError("❌ Message {MsgId} not found after lock", messageId);
                    return false;
                }

                var team = await GetTeamById(teamId);
                if (team == null || string.IsNullOrEmpty(team.WhatsAppAccessToken))
                {
                    throw new InvalidOperationException("Team WhatsApp configuration missing");
                }

                var phoneNumber = message.Conversation?.Driver?.PhoneNumber;
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    throw new InvalidOperationException("No phone number available");
                }

                // FIXED: Removed HasValue check since ConversationId is not nullable in this context
                // Instead, check if it's not 0 (assuming 0 means no conversation)
                if (!message.IsTemplateMessage && message.ConversationId > 0)
                {
                    var canSend = await CanSendNonTemplateMessageAsync(message.ConversationId);
                    if (!canSend)
                    {
                        throw new InvalidOperationException("24-hour window expired");
                    }
                }

                string? whatsAppMessageId = null;

                switch (message.MessageType)
                {
                    case MessageType.Image:
                    case MessageType.Video:
                    case MessageType.Audio:
                    case MessageType.Document:
                        whatsAppMessageId = await SendMediaMessageInternalAsync(
                            phoneNumber,
                            message.MessageType,
                            message.MediaUrl!,
                            message.FileName ?? "file",
                            message.Content,
                            team);
                        break;

                    case MessageType.Location:
                        whatsAppMessageId = await SendLocationMessageInternalAsync(
                            phoneNumber,
                            message.Location!,
                            team);
                        break;

                    case MessageType.Text:
                    default:
                        if (message.IsTemplateMessage && !string.IsNullOrEmpty(message.TemplateName))
                        {
                            var parameters = !string.IsNullOrEmpty(message.TemplateParametersJson)
                                ? JsonSerializer.Deserialize<Dictionary<string, string>>(message.TemplateParametersJson)
                                : new Dictionary<string, string>();

                            whatsAppMessageId = await SendTemplateMessageInternalAsync(
                                phoneNumber,
                                message.TemplateName!,
                                parameters ?? new Dictionary<string, string>(),
                                team);
                        }
                        else
                        {
                            whatsAppMessageId = await SendTextMessageInternalAsync(
                                phoneNumber,
                                message.Content ?? "",
                                team);
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(whatsAppMessageId))
                {
                    message.Status = MessageStatus.Sent;
                    message.WhatsAppMessageId = whatsAppMessageId;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Message {MsgId} sent: {WAMsgId}",
                        messageId, whatsAppMessageId);
                    return true;
                }
                else
                {
                    throw new InvalidOperationException("No WhatsApp message ID returned");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing message {MsgId}", messageId);

                var message = await _context.Messages.FindAsync(messageId);
                if (message != null)
                {
                    if (message.RetryCount >= MAX_RETRIES)
                    {
                        message.Status = MessageStatus.Failed;
                        _logger.LogError("❌ Message {MsgId} failed after {Retries} attempts",
                            messageId, MAX_RETRIES);
                    }
                    else
                    {
                        message.Status = MessageStatus.Queued;
                        message.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.RetryCount));
                        _logger.LogWarning("⚠️ Message {MsgId} will retry at {NextRetry}",
                            messageId, message.NextRetryAt);
                    }

                    await _context.SaveChangesAsync();
                }

                return false;
            }
        }

        private async Task<string?> SendTextMessageInternalAsync(string phoneNumber, string text, Team team)
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phoneNumber,
                type = "text",
                text = new { body = text }
            };

            return await SendWhatsAppRequestInternalAsync(payload, team);
        }

        private async Task<string?> SendTemplateMessageInternalAsync(
            string phoneNumber,
            string templateName,
            Dictionary<string, string> parameters,
            Team team)
        {
            var components = new List<object>();

            if (parameters.Any())
            {
                var bodyParams = parameters
                    .OrderBy(p => p.Key)
                    .Select(p => new { type = "text", text = p.Value })
                    .ToList();

                components.Add(new { type = "body", parameters = bodyParams });
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phoneNumber,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = "en_US" },
                    components
                }
            };

            return await SendWhatsAppRequestInternalAsync(payload, team);
        }

        private async Task<string?> SendMediaMessageInternalAsync(
            string phoneNumber,
            MessageType mediaType,
            string mediaUrl,
            string fileName,
            string? caption,
            Team team)
        {
            try
            {
                _logger.LogInformation("📤 Sending {MediaType} to {Phone}", mediaType, phoneNumber);

                var (fileBytes, mimeType) = await DownloadMediaSafelyAsync(mediaUrl, fileName);

                ValidateMediaSize(mediaType, fileBytes.Length);

                var mediaId = await UploadMediaInternalAsync(
                    fileBytes,
                    fileName,
                    mimeType,
                    team.WhatsAppPhoneNumberId!,
                    team.WhatsAppAccessToken!);

                if (string.IsNullOrEmpty(mediaId))
                    throw new InvalidOperationException("Media upload failed");

                _logger.LogInformation("✅ Media uploaded: {MediaId}", mediaId);

                var payload = CreateMediaPayload(phoneNumber, mediaType, mediaId, caption, fileName);
                return await SendWhatsAppRequestInternalAsync(payload, team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending media");
                throw;
            }
        }

        private async Task<(byte[] FileBytes, string MimeType)> DownloadMediaSafelyAsync(string mediaUrl, string fileName)
        {
            const long MAX_SIZE = 100 * 1024 * 1024;

            if (Uri.IsWellFormedUriString(mediaUrl, UriKind.Absolute))
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var response = await httpClient.GetAsync(mediaUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength > MAX_SIZE)
                    throw new InvalidOperationException($"File size {contentLength} exceeds {MAX_SIZE} limit");

                using var memoryStream = new MemoryStream();
                using var stream = await response.Content.ReadAsStreamAsync();

                var buffer = new byte[8192];
                int bytesRead;
                long totalBytes = 0;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > MAX_SIZE)
                        throw new InvalidOperationException($"File size exceeds {MAX_SIZE} limit");

                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                }

                return (memoryStream.ToArray(),
                        response.Content.Headers.ContentType?.MediaType ?? GetMimeTypeInternal(fileName));
            }
            else
            {
                var uploadsDir = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads");
                var localPath = Path.Combine(uploadsDir, Path.GetFileName(mediaUrl));

                if (!File.Exists(localPath))
                    throw new FileNotFoundException($"File not found: {localPath}");

                var fileInfo = new FileInfo(localPath);
                if (fileInfo.Length > MAX_SIZE)
                    throw new InvalidOperationException($"File size {fileInfo.Length} exceeds {MAX_SIZE} limit");

                return (await File.ReadAllBytesAsync(localPath), GetMimeTypeInternal(fileName));
            }
        }

        private async Task<string?> UploadMediaInternalAsync(
            byte[] fileBytes,
            string fileName,
            string mimeType,
            string phoneNumberId,
            string accessToken)
        {
            try
            {
                var url = $"https://graph.facebook.com/v19.0/{phoneNumberId}/media";

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);

                using var form = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(fileBytes);

                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent("whatsapp"), "messaging_product");

                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("id", out var id))
                    {
                        return id.GetString();
                    }
                }

                try
                {
                    var errorDoc = JsonDocument.Parse(responseContent);
                    var error = errorDoc.RootElement.GetProperty("error");
                    var errorMsg = error.GetProperty("message").GetString();
                    _logger.LogError("❌ Upload error: {Error}", errorMsg);
                }
                catch
                {
                    _logger.LogError("❌ Upload failed: {Response}", responseContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Upload exception");
                return null;
            }
        }

        private async Task<string?> SendLocationMessageInternalAsync(string phoneNumber, string locationJson, Team team)
        {
            try
            {
                var locationData = JsonSerializer.Deserialize<Dictionary<string, object>>(locationJson);
                if (locationData == null)
                    throw new ArgumentException("Invalid location data");

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = phoneNumber,
                    type = "location",
                    location = new
                    {
                        latitude = Convert.ToDouble(locationData["latitude"]),
                        longitude = Convert.ToDouble(locationData["longitude"]),
                        name = locationData.ContainsKey("name") ? locationData["name"].ToString() : null,
                        address = locationData.ContainsKey("address") ? locationData["address"].ToString() : null
                    }
                };

                return await SendWhatsAppRequestInternalAsync(payload, team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Location send error");
                throw;
            }
        }

        private async Task<string?> SendWhatsAppRequestInternalAsync(object payload, Team team)
        {
            try
            {
                var url = $"https://graph.facebook.com/v19.0/{team.WhatsAppPhoneNumberId}/messages";
                var json = JsonSerializer.Serialize(payload);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                        messages.GetArrayLength() > 0 &&
                        messages[0].TryGetProperty("id", out var id))
                    {
                        return id.GetString();
                    }
                }

                try
                {
                    var errorDoc = JsonDocument.Parse(responseContent);
                    var error = errorDoc.RootElement.GetProperty("error");
                    var errorMsg = error.GetProperty("message").GetString();
                    var errorCode = error.TryGetProperty("code", out var code) ? code.GetInt32() : 0;
                    _logger.LogError("❌ WhatsApp error {Code}: {Message}", errorCode, errorMsg);
                }
                catch
                {
                    _logger.LogError("❌ WhatsApp error: {Response}", responseContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Request error");
                throw;
            }
        }

        private object CreateMediaPayload(
            string phoneNumber,
            MessageType mediaType,
            string mediaId,
            string? caption,
            string? fileName)
        {
            return mediaType switch
            {
                MessageType.Image => new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = phoneNumber,
                    type = "image",
                    image = new { id = mediaId, caption }
                },
                MessageType.Video => new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = phoneNumber,
                    type = "video",
                    video = new { id = mediaId, caption }
                },
                MessageType.Audio => new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = phoneNumber,
                    type = "audio",
                    audio = new { id = mediaId }
                },
                MessageType.Document => new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = phoneNumber,
                    type = "document",
                    document = new { id = mediaId, caption, filename = fileName }
                },
                _ => throw new ArgumentException($"Unsupported: {mediaType}")
            };
        }

        private string GetMimeTypeInternal(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".3gp" => "video/3gpp",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".aac" => "audio/aac",
                ".amr" => "audio/amr",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
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

        private async Task<bool> CanSendNonTemplateMessageAsync(int conversationId)
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || conversation.IsGroupConversation)
                return true;

            var lastInbound = conversation.Messages
                .Where(m => m.IsFromDriver && m.Status == MessageStatus.Sent)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            if (lastInbound == null)
                return false;

            return DateTime.UtcNow <= lastInbound.SentAt.AddHours(24);
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

                if (!conversation.LastInboundMessageAt.HasValue)
                {
                    _logger.LogWarning("🚫 Conversation {ConversationId} has NO inbound messages - CANNOT send free text", conversationId);
                    return false;
                }

                var timeSinceLastInbound = DateTime.UtcNow - conversation.LastInboundMessageAt.Value;
                var canSend = timeSinceLastInbound.TotalHours < 24.0;

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

                var detectedCountryCode = PhoneNumberUtil.DetectCountryCode(to);
                var countryCodeToUse = detectedCountryCode ?? team.CountryCode ?? "44";

                _logger.LogInformation("🌍 Country code - Detected: {Detected}, Using: {Using}",
                    detectedCountryCode ?? "none", countryCodeToUse);

                var formattedPhone = PhoneNumberUtil.FormatForWhatsAppApi(to, countryCodeToUse);

                _logger.LogInformation("📱 Phone formatting: {Original} → {Formatted}", to, formattedPhone);
                _logger.LogInformation("🔑 Using Phone ID: {PhoneId}, API v{Version}",
                    team.WhatsAppPhoneNumberId, team.ApiVersion ?? "19.0");

                var apiVersion = string.IsNullOrEmpty(team.ApiVersion) ? "19.0" : team.ApiVersion;
                var url = $"https://graph.facebook.com/v{apiVersion}/{team.WhatsAppPhoneNumberId}/messages";

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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 WhatsApp Response Status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("📥 WhatsApp Response Body: {Response}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    if (jsonDoc.RootElement.TryGetProperty("messages", out var messagesArray) &&
                        messagesArray.GetArrayLength() > 0 &&
                        messagesArray[0].TryGetProperty("id", out var idElement))
                    {
                        var messageId = idElement.GetString();
                        _logger.LogInformation("✅ Template '{Template}' sent to {Phone}, Message ID: {MessageId}",
                            templateName, formattedPhone, messageId);
                        return messageId;
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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.SendAsync(request);
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

                var mediaService = new WhatsAppMediaService(_httpClientFactory, _environment, _logger, _configuration, _httpContextAccessor);
                var mediaId = await mediaService.UploadMediaToWhatsAppAsync(
                    fileBytes, fileName, mimeType,
                    team.WhatsAppPhoneNumberId ?? throw new InvalidOperationException("WhatsAppPhoneNumberId is null"),
                    team.WhatsAppAccessToken ?? throw new InvalidOperationException("WhatsAppAccessToken is null"));
                if (string.IsNullOrEmpty(mediaId))
                    return (false, "Failed to upload media to WhatsApp", null);

                var localUrl = await mediaService.SaveMediaLocallyAsync(fileBytes, fileName, mimeType);

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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.PostAsync(url, content);
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

                var (fileBytes, mimeType) = await DownloadMediaSafelyAsync(mediaUrl, Path.GetFileName(mediaUrl) ?? $"media_{Guid.NewGuid()}");
                var fileName = Path.GetFileName(mediaUrl) ?? $"media_{Guid.NewGuid()}";

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
                    var (fileBytes, mimeType) = await DownloadMediaSafelyAsync(mediaUrl, Path.GetFileName(mediaUrl) ?? $"media_{Guid.NewGuid()}");
                    var fileName = Path.GetFileName(mediaUrl) ?? $"media_{Guid.NewGuid()}";

                    var mediaService = new WhatsAppMediaService(_httpClientFactory, _environment, _logger, _configuration, _httpContextAccessor);
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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.PostAsync(url, content);

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

                var maxSize = GetMaxFileSize(Path.GetExtension(file.FileName).ToLower());
                if (file.Length > maxSize)
                    return new MediaUploadResult
                    {
                        Success = false,
                        Error = $"File exceeds {maxSize / (1024 * 1024)}MB limit"
                    };

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                var mediaService = new WhatsAppMediaService(_httpClientFactory, _environment, _logger, _configuration, _httpContextAccessor);
                var mediaId = await mediaService.UploadMediaToWhatsAppAsync(
                    fileBytes, file.FileName, file.ContentType,
                    team.WhatsAppPhoneNumberId ?? string.Empty,
                    team.WhatsAppAccessToken ?? string.Empty);

                if (string.IsNullOrEmpty(mediaId))
                    return new MediaUploadResult { Success = false, Error = "Failed to upload to WhatsApp" };

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
                ".jpg" or ".jpeg" or ".png" or ".gif" => 5 * 1024 * 1024,
                ".mp4" or ".mov" or ".avi" => 16 * 1024 * 1024,
                ".mp3" or ".m4a" or ".ogg" => 16 * 1024 * 1024,
                ".pdf" or ".doc" or ".docx" or ".txt" => 100 * 1024 * 1024,
                _ => 5 * 1024 * 1024
            };
        }

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

        public async Task<MediaStatusResult> CheckMediaStatus(string mediaId, int teamId)
        {
            try
            {
                var team = await GetTeamById(teamId);
                if (team == null)
                    return new MediaStatusResult { Exists = false, Error = "Team not found" };

                var url = $"https://graph.facebook.com/v19.0/{mediaId}";

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.SendAsync(request);
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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", team.WhatsAppAccessToken);

                var response = await httpClient.PostAsync(url, content);

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
                ApiVersion = request.ApiVersion ?? "19.0",
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
                var phoneNumber = PhoneNumberUtil.ExtractPhoneFromWhatsAppId(incomingMessage.From);
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    _logger.LogWarning("⚠️ Could not extract phone number from: {From}", incomingMessage.From);
                    return;
                }

                var detectedCountryCode = PhoneNumberUtil.DetectCountryCode(phoneNumber);
                var countryCodeToUse = detectedCountryCode ?? team.CountryCode ?? "91";
                var normalizedPhone = PhoneNumberUtil.NormalizePhoneNumber(phoneNumber, countryCodeToUse);

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

                var existingMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.WhatsAppMessageId == incomingMessage.Id);

                if (existingMessage != null)
                {
                    _logger.LogInformation("⚠️ Message already exists: {MessageId}", incomingMessage.Id);
                    return;
                }

                conversation.LastInboundMessageAt = DateTime.UtcNow;
                conversation.LastMessageAt = DateTime.UtcNow;

                var messageData = ExtractMessageData(incomingMessage, team);

                string? mediaUrl = messageData.MediaUrl;
                string? fileName = messageData.FileName;
                long? fileSize = messageData.FileSize;

                var mediaService = new WhatsAppMediaService(_httpClientFactory, _environment, _logger, _configuration, _httpContextAccessor);

                // FIXED: Added null check for WhatsAppAccessToken
                if (!string.IsNullOrEmpty(incomingMessage.Image?.Id) && !string.IsNullOrEmpty(team.WhatsAppAccessToken))
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
                else if (!string.IsNullOrEmpty(incomingMessage.Video?.Id) && !string.IsNullOrEmpty(team.WhatsAppAccessToken))
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
                else if (!string.IsNullOrEmpty(incomingMessage.Audio?.Id) && !string.IsNullOrEmpty(team.WhatsAppAccessToken))
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
                else if (!string.IsNullOrEmpty(incomingMessage.Document?.Id) && !string.IsNullOrEmpty(team.WhatsAppAccessToken))
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

                return templateName.ToLowerInvariant() switch
                {
                    "hello_world" when orderedParams.Count >= 1 =>
                        $"Hello {orderedParams[0]}, welcome to our service! This is a test message from WhatsApp Business API.",
                    "hello_world" =>
                        "Hello, welcome to our service! This is a test message from WhatsApp Business API.",

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

            return templateName.ToLower() switch
            {
                "hello_world" when paramValues.Count >= 1 => $"Hello {paramValues[0]}, welcome to our service!",
                "welcome_message" when paramValues.Count >= 2 => $"Welcome {paramValues[0]} to {paramValues[1]}!",
                _ => $"{templateName}: {string.Join(", ", paramValues)}"
            };
        }

        public async Task<bool> SendWhatsAppMessageAsync(string phoneNumber, string message, bool isTemplate, MessageContext? context, int teamId)
        {
            try
            {
                if (isTemplate)
                {
                    _logger.LogWarning("SendWhatsAppMessageAsync called for template - use SendTemplateMessageAsync instead");
                    return false;
                }

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