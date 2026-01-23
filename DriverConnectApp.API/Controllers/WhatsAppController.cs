using DriverConnectApp.API.Models;
using DriverConnectApp.API.Services;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriverConnectApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        private readonly IMultiTenantWhatsAppService _whatsAppService;
        private readonly ILogger<WhatsAppController> _logger;
        private readonly AppDbContext _context;
        private readonly IMessageService _messageService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string? _appSecret;
        private readonly bool _skipSignatureVerification;

        public WhatsAppController(
            IMultiTenantWhatsAppService whatsAppService,
            ILogger<WhatsAppController> logger,
            IConfiguration configuration,
            AppDbContext context,
            IMessageService messageService,
            UserManager<ApplicationUser> userManager)
        {
            _whatsAppService = whatsAppService ?? throw new ArgumentNullException(nameof(whatsAppService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _userManager = userManager;
            _appSecret = configuration["WhatsApp:AppSecret"];
            _skipSignatureVerification = configuration.GetValue<bool>("WhatsApp:SkipSignatureVerification", false);
        }

        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            _logger.LogInformation("Webhook verification request: Mode={Mode}, Token={Token}, Challenge={Challenge}",
                mode, token, challenge);

            var verifyToken = "MySecureToken123";

            if (mode == "subscribe" && token == verifyToken)
            {
                _logger.LogInformation("Webhook verified successfully with challenge: {Challenge}", challenge);
                return Content(challenge ?? string.Empty, "text/plain");
            }

            _logger.LogWarning("Webhook verification failed. Mode={Mode}, Token={Token}", mode, token);
            return Forbid("Verification failed");
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ProcessWebhook()
        {
            try
            {
                Request.EnableBuffering();

                _logger.LogInformation("Received multi-tenant webhook");

                string requestBody;
                using (var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8, true, 1024, true))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                Request.Body.Position = 0;

                bool isTestRequest = IsTestRequest(Request);

                if (!_skipSignatureVerification && !isTestRequest)
                {
                    var signature256 = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                    if (string.IsNullOrEmpty(signature256))
                    {
                        _logger.LogError("Missing X-Hub-Signature-256 header");
                        return BadRequest("Missing signature header");
                    }
                    if (!VerifySignature(requestBody, signature256))
                    {
                        _logger.LogError("Invalid webhook signature");
                        return BadRequest("Invalid signature");
                    }
                }

                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Webhook data is null or empty");
                    return Ok(new { status = "ok", message = "Empty payload accepted" });
                }

                await _whatsAppService.ProcessWebhookAsync(requestBody);
                return Ok(new { status = "ok", message = "Processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multi-tenant webhook");
                return Ok(new { status = "error", message = "Internal error occurred" });
            }
        }

        [HttpPost("send-message/{teamId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMessage(int teamId, [FromBody] FrontendSendRequest request)
        {
            if (string.IsNullOrEmpty(request?.PhoneNumber) || string.IsNullOrEmpty(request?.Message))
            {
                return BadRequest(new { success = false, error = "Phone number and message are required" });
            }

            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

                if (!isAdmin)
                {
                    var userTeamId = await _context.Users
                        .Where(u => u.Id == currentUserId)
                        .Select(u => u.TeamId)
                        .FirstOrDefaultAsync();

                    if (userTeamId != teamId)
                    {
                        return Forbid("You do not have access to send messages for this team.");
                    }
                }

                var driver = await _context.Drivers.FirstOrDefaultAsync(d =>
                    d.PhoneNumber == request.PhoneNumber.Trim() && d.TeamId == teamId);

                if (driver == null)
                {
                    _logger.LogInformation("Auto-creating driver for phone number: {PhoneNumber} in team {TeamId}", request.PhoneNumber, teamId);
                    driver = new Driver
                    {
                        Name = $"Driver {request.PhoneNumber}",
                        PhoneNumber = request.PhoneNumber.Trim(),
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        TeamId = teamId
                    };
                    _context.Drivers.Add(driver);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Auto-created driver with ID: {DriverId} in team {TeamId}", driver.Id, teamId);
                }

                var sendRequest = new SendMessageRequest
                {
                    Content = request.Message,
                    MessageType = "Text",
                    DriverId = driver.Id,
                    IsFromDriver = false,
                    WhatsAppMessageId = $"web_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}",
                    TeamId = teamId
                };

                await _whatsAppService.SendMessageAsync(sendRequest, teamId);

                var messageId = Guid.NewGuid().ToString();
                _logger.LogInformation("Message sent successfully to {PhoneNumber} in team {TeamId} with ID {MessageId}",
                    request.PhoneNumber, teamId, messageId);

                return Ok(new
                {
                    success = true,
                    messageId = messageId,
                    timestamp = DateTime.UtcNow,
                    phoneNumber = request.PhoneNumber,
                    driverId = driver.Id,
                    teamId = teamId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to {PhoneNumber} in team {TeamId}", request.PhoneNumber, teamId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("send-template/{teamId}")]
        [Authorize]
        public async Task<IActionResult> SendTemplateMessage(int teamId, [FromBody] SendTemplateByPhoneRequest request)
        {
            try
            {
                _logger.LogInformation("🎯 Sending template message for team {TeamId}: {TemplateName} to {PhoneNumber}",
                    teamId, request.TemplateName, request.PhoneNumber);

                var currentUser = await _userManager.GetUserAsync(User);
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

                // Check permissions
                if (!isAdmin)
                {
                    if (currentUser?.TeamId != teamId)
                    {
                        return Forbid("You do not have access to send messages for this team.");
                    }
                }

                // Validate team
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.Id == teamId && t.IsActive);

                if (team == null)
                {
                    return BadRequest(new { success = false, error = "Team not found or inactive" });
                }

                // Validate team has WhatsApp configuration
                if (string.IsNullOrEmpty(team.WhatsAppPhoneNumberId) ||
                    string.IsNullOrEmpty(team.WhatsAppAccessToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Team is missing WhatsApp configuration. Please update team settings."
                    });
                }

                // Send template message using the service - NOW RETURNS string?
                var whatsAppMessageId = await _whatsAppService.SendTemplateMessageAsync(
                    request.PhoneNumber,
                    request.TemplateName,
                    request.TemplateParameters ?? new Dictionary<string, string>(),
                    teamId,
                    request.LanguageCode
                );

                // Check if we got a valid WhatsApp message ID
                if (!string.IsNullOrEmpty(whatsAppMessageId))
                {
                    // Find or create driver
                    var driver = await _context.Drivers.FirstOrDefaultAsync(d =>
                        d.PhoneNumber == request.PhoneNumber.Trim() && d.TeamId == teamId);

                    if (driver == null)
                    {
                        driver = new Driver
                        {
                            Name = $"Driver {request.PhoneNumber}",
                            PhoneNumber = request.PhoneNumber.Trim(),
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true,
                            TeamId = teamId
                        };
                        _context.Drivers.Add(driver);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("👤 Created new driver: {DriverId} for team {TeamId}", driver.Id, teamId);
                    }

                    // Create conversation if it doesn't exist
                    var conversation = await _context.Conversations
                        .FirstOrDefaultAsync(c => c.DriverId == driver.Id && c.TeamId == teamId);

                    if (conversation == null)
                    {
                        conversation = new Conversation
                        {
                            DriverId = driver.Id,
                            Topic = $"Template: {request.TemplateName}",
                            CreatedAt = DateTime.UtcNow,
                            IsAnswered = false,
                            TeamId = teamId,
                            LastMessageAt = DateTime.UtcNow
                        };
                        _context.Conversations.Add(conversation);
                        await _context.SaveChangesAsync();
                    }

                    // Save message to database
                    var currentUserName = currentUser?.FullName ?? currentUser?.UserName ?? "Staff";

                    var message = new Message
                    {
                        ConversationId = conversation.Id,
                        Content = $"Template: {request.TemplateName}",
                        MessageType = MessageType.Text,
                        IsFromDriver = false,
                        SentAt = DateTime.UtcNow,
                        WhatsAppMessageId = whatsAppMessageId, // Use the real WhatsApp ID
                        SenderName = currentUserName,
                        SentByUserId = currentUser?.Id,
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

                    _logger.LogInformation("✅ Template message sent successfully and saved to database");

                    return Ok(new
                    {
                        success = true,
                        message = "Template message sent successfully",
                        driverId = driver.Id,
                        conversationId = conversation.Id,
                        messageId = message.Id,
                        whatsAppMessageId = whatsAppMessageId,
                        teamId = teamId
                    });
                }
                else
                {
                    _logger.LogError("❌ Failed to send template message via WhatsApp API");
                    return BadRequest(new
                    {
                        success = false,
                        error = "Failed to send template message. Please check team WhatsApp configuration."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending template message");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        [HttpPost("webhook-debug")]
        public async Task<IActionResult> ProcessWebhookDebug()
        {
            try
            {
                Request.EnableBuffering();

                _logger.LogInformation("🔍 WEBHOOK DEBUG: Received webhook request");

                // Log headers
                foreach (var header in Request.Headers)
                {
                    _logger.LogInformation("Header: {Key} = {Value}", header.Key, header.Value);
                }

                // Read body
                string requestBody;
                using (var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8, true, 1024, true))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                Request.Body.Position = 0;

                _logger.LogInformation("📝 WEBHOOK BODY: {RequestBody}", requestBody);

                // Parse and log structure
                try
                {
                    using var doc = JsonDocument.Parse(requestBody);
                    var root = doc.RootElement;
                    _logger.LogInformation("📦 Webhook JSON structure: {Structure}", root.ToString());

                    // Check for messages
                    if (root.TryGetProperty("entry", out var entries))
                    {
                        foreach (var entry in entries.EnumerateArray())
                        {
                            _logger.LogInformation("📨 Entry found");

                            if (entry.TryGetProperty("changes", out var changes))
                            {
                                foreach (var change in changes.EnumerateArray())
                                {
                                    if (change.TryGetProperty("value", out var value))
                                    {
                                        // Check for messages
                                        if (value.TryGetProperty("messages", out var messages))
                                        {
                                            _logger.LogInformation("🎉 INBOUND MESSAGES FOUND: {Count}", messages.GetArrayLength());
                                            foreach (var msg in messages.EnumerateArray())
                                            {
                                                _logger.LogInformation("📨 Message: {Message}", msg.ToString());
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogInformation("📊 No 'messages' property found");
                                        }

                                        // Check for statuses
                                        if (value.TryGetProperty("statuses", out var statuses))
                                        {
                                            _logger.LogInformation("📊 Statuses found: {Count}", statuses.GetArrayLength());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "❌ JSON parsing error");
                }

                // Process normally
                await _whatsAppService.ProcessWebhookAsync(requestBody);

                return Ok(new
                {
                    status = "ok",
                    message = "Webhook processed (debug mode)",
                    bodyLength = requestBody.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in webhook debug");
                return StatusCode(500, new { message = "Debug error", error = ex.Message });
            }
        }

        [HttpPost("test-inbound/{conversationId}")]
        [Authorize]
        public async Task<IActionResult> SimulateInboundMessage(int conversationId)
        {
            try
            {
                var conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                    return NotFound(new { message = "Conversation not found" });

                // SIMULATE INBOUND MESSAGE
                conversation.LastInboundMessageAt = DateTime.UtcNow;
                conversation.LastMessageAt = DateTime.UtcNow;

                // Create a test inbound message
                var testMessage = new Message
                {
                    ConversationId = conversation.Id,
                    Content = "[TEST] Inbound message from driver to open 24-hour window",
                    MessageType = MessageType.Text,
                    IsFromDriver = true,
                    SenderPhoneNumber = conversation.Driver?.PhoneNumber ?? "919876543210",
                    SenderName = conversation.Driver?.Name ?? "Test Driver",
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = $"test_inbound_{DateTime.UtcNow.Ticks}"
                };

                _context.Messages.Add(testMessage);
                await _context.SaveChangesAsync();

                // Get updated window status
                var status = conversation.GetWindowStatus();

                return Ok(new
                {
                    message = "✅ SIMULATED: Inbound message received. 24-hour window NOW OPEN!",
                    conversationId,
                    lastInboundMessageAt = conversation.LastInboundMessageAt,
                    canSendNonTemplateMessages = status.CanSendNonTemplateMessages,
                    hoursRemaining = status.HoursRemaining,
                    minutesRemaining = status.MinutesRemaining,
                    windowStatus = status.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating inbound message");
                return StatusCode(500, new { message = "Failed to simulate", error = ex.Message });
            }
        }


        [HttpGet("teams")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetTeams()
        {
            try
            {
                var teams = await _context.Teams
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Name)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        description = t.Description,
                        whatsAppPhoneNumber = t.WhatsAppPhoneNumber,
                        isActive = t.IsActive,
                        createdAt = t.CreatedAt
                    })
                    .ToListAsync();

                return Ok(teams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading teams");
                return StatusCode(500, new { message = "Failed to load teams", error = ex.Message });
            }
        }

        private bool VerifySignature(string payload, string signature)
        {
            if (string.IsNullOrEmpty(_appSecret) || string.IsNullOrEmpty(signature))
                return false;

            try
            {
                var key = Encoding.UTF8.GetBytes(_appSecret);
                using var hmac = new HMACSHA256(key);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var computedSignature = "sha256=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

                return computedSignature == signature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying webhook signature");
                return false;
            }
        }

        private bool IsTestRequest(HttpRequest request)
        {
            return request.Headers.Any(h => h.Key == "X-Test-Mode" && h.Value == "true") ||
                   request.Query.ContainsKey("test") ||
                   (request.Body != null && request.ContentLength.HasValue && request.ContentLength < 1024);
        }
    }

    public class FrontendSendRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // RENAMED: Changed from SendTemplateRequest to SendTemplateByPhoneRequest
    public class SendTemplateByPhoneRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public Dictionary<string, string> TemplateParameters { get; set; } = new();
        public string LanguageCode { get; set; } = "en_US";
    }
}