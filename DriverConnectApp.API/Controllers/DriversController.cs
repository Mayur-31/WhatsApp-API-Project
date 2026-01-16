using DriverConnectApp.API.Models;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DriverConnectApp.API.Services;

namespace DriverConnectApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DriversController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DriversController> _logger;

        public DriversController(AppDbContext context, ILogger<DriversController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Drivers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Driver>>> GetDrivers()
        {
            try
            {
                _logger.LogInformation("🚀 Getting all drivers");

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                var userTeamId = await GetCurrentUserTeamId();

                _logger.LogInformation("👤 User {UserId} - Admin: {IsAdmin}, UserTeamId: {UserTeamId}",
                    currentUserId, isAdmin, userTeamId);

                var query = _context.Drivers.AsNoTracking();

                // Apply team filtering
                if (!isAdmin && userTeamId.HasValue)
                {
                    query = query.Where(d => d.TeamId == userTeamId.Value);
                    _logger.LogInformation("🔧 Filtering drivers for team: {TeamId}", userTeamId.Value);
                }
                else if (!isAdmin)
                {
                    _logger.LogWarning("⚠️ User {UserId} has no team assigned, returning empty drivers list", currentUserId);
                    return Ok(new List<Driver>());
                }

                var drivers = await query.ToListAsync();
                _logger.LogInformation("✅ Successfully retrieved {Count} drivers", drivers.Count);
                return Ok(drivers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR retrieving drivers: {ErrorMessage}", ex.Message);
                return StatusCode(500, new
                {
                    message = "Failed to retrieve drivers",
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        // GET: api/Drivers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Driver>> GetDriver(int id)
        {
            try
            {
                _logger.LogInformation("🔍 Getting driver {DriverId}", id);

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                var userTeamId = await GetCurrentUserTeamId();

                var query = _context.Drivers.AsNoTracking();

                if (!isAdmin && userTeamId.HasValue)
                {
                    query = query.Where(d => d.TeamId == userTeamId.Value);
                }

                var driver = await query.FirstOrDefaultAsync(d => d.Id == id);

                if (driver == null)
                {
                    _logger.LogWarning("⚠️ Driver {DriverId} not found", id);
                    return NotFound(new { message = $"Driver with ID {id} not found" });
                }

                _logger.LogInformation("✅ Retrieved driver {DriverId}: {DriverName}", id, driver.Name);
                return Ok(driver);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving driver {DriverId}", id);
                return StatusCode(500, new
                {
                    message = "Failed to retrieve driver",
                    error = ex.Message
                });
            }
        }

        private async Task<int?> GetCurrentUserTeamId()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("⚠️ No user ID found in claims");
                    return null;
                }

                var user = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.TeamId, u.TeamRole })
                    .FirstOrDefaultAsync();

                _logger.LogInformation("👤 User {UserId} - TeamId: {TeamId}, TeamRole: {TeamRole}",
                    userId, user?.TeamId, user?.TeamRole);

                return user?.TeamId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting current user team ID");
                return null;
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDriver(int id)
        {
            try
            {
                _logger.LogInformation("🚀 Starting comprehensive deletion process for driver {DriverId}", id);

                // Get current user's team for authorization
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin");
                var userTeamId = await GetCurrentUserTeamId();

                // Step 1: Find the driver with all related data
                var driverQuery = _context.Drivers
                    .Include(d => d.Conversations!)
                        .ThenInclude(c => c.Messages!)
                            .ThenInclude(m => m.Recipients)
                    .Include(d => d.Conversations!)
                        .ThenInclude(c => c.Messages!)
                            .ThenInclude(m => m.Reactions)
                    .Include(d => d.Conversations!)
                        .ThenInclude(c => c.Messages!)
                            .ThenInclude(m => m.ReplyToMessage)
                    .Include(d => d.Conversations!)
                        .ThenInclude(c => c.Messages!)
                            .ThenInclude(m => m.ForwardedFromMessage)
                    .AsQueryable();

                // Apply team filter for non-admin users
                if (!isAdmin && userTeamId.HasValue)
                {
                    driverQuery = driverQuery.Where(d => d.TeamId == userTeamId.Value);
                }

                var driver = await driverQuery.FirstOrDefaultAsync(d => d.Id == id);

                if (driver == null)
                {
                    _logger.LogWarning("Driver with ID {DriverId} not found", id);
                    return NotFound(new { message = $"Driver with ID {id} not found" });
                }

                // Additional authorization check
                if (!isAdmin && driver.TeamId != userTeamId)
                {
                    return Forbid("You do not have permission to delete this contact.");
                }

                _logger.LogInformation("✅ Found driver: {DriverName} ({DriverPhone}) with {ConversationCount} conversations",
                    driver.Name, driver.PhoneNumber, driver.Conversations?.Count ?? 0);

                // Step 2: Collect all message IDs from this driver's conversations
                var allMessageIds = new List<int>();
                var allConversationIds = new List<int>();

                // FIXED: Added null check for Conversations
                if (driver.Conversations != null)
                {
                    foreach (var conversation in driver.Conversations)
                    {
                        allConversationIds.Add(conversation.Id);
                        if (conversation.Messages != null)
                        {
                            allMessageIds.AddRange(conversation.Messages.Select(m => m.Id));
                        }
                    }
                }

                _logger.LogInformation("📋 Found {MessageCount} messages and {ConversationCount} conversations to delete",
                    allMessageIds.Count, allConversationIds.Count);

                if (allMessageIds.Any())
                {
                    // Step 3: Remove message recipients for these messages
                    var recipients = await _context.MessageRecipients
                        .Where(mr => allMessageIds.Contains(mr.MessageId))
                        .ToListAsync();

                    if (recipients.Any())
                    {
                        _logger.LogInformation("🗑️ Deleting {RecipientCount} message recipients", recipients.Count);
                        _context.MessageRecipients.RemoveRange(recipients);
                    }

                    // Step 4: Remove message reactions for these messages
                    var reactions = await _context.MessageReactions
                        .Where(mr => allMessageIds.Contains(mr.MessageId))
                        .ToListAsync();

                    if (reactions.Any())
                    {
                        _logger.LogInformation("🗑️ Deleting {ReactionCount} message reactions", reactions.Count);
                        _context.MessageReactions.RemoveRange(reactions);
                    }

                    // Step 5: Handle self-referencing foreign keys in messages
                    var messagesWithReplies = await _context.Messages
                        .Where(m => allMessageIds.Contains(m.ReplyToMessageId ?? 0))
                        .ToListAsync();

                    foreach (var message in messagesWithReplies)
                    {
                        message.ReplyToMessageId = null;
                        message.ReplyToMessageContent = null;
                        message.ReplyToSenderName = null;
                    }

                    var messagesWithForwards = await _context.Messages
                        .Where(m => allMessageIds.Contains(m.ForwardedFromMessageId ?? 0))
                        .ToListAsync();

                    foreach (var message in messagesWithForwards)
                    {
                        message.ForwardedFromMessageId = null;
                        message.ForwardCount = 0;
                    }

                    // Step 6: Delete all messages from conversations
                    // FIXED: Added null check for Conversations
                    if (driver.Conversations != null)
                    {
                        foreach (var conversation in driver.Conversations)
                        {
                            if (conversation.Messages != null && conversation.Messages.Any())
                            {
                                _logger.LogInformation("🗑️ Deleting {MessageCount} messages from conversation {ConversationId}",
                                    conversation.Messages.Count, conversation.Id);
                                _context.Messages.RemoveRange(conversation.Messages);
                            }
                        }
                    }
                }

                // Step 7: Remove driver from group participants
                var groupParticipants = await _context.GroupParticipants
                    .Where(gp => gp.DriverId == id)
                    .ToListAsync();

                if (groupParticipants.Any())
                {
                    _logger.LogInformation("👥 Removing driver from {ParticipantCount} group participants", groupParticipants.Count);
                    foreach (var participant in groupParticipants)
                    {
                        participant.DriverId = null;
                        participant.Driver = null;
                        // Keep the participant record but remove driver association
                    }
                }

                // Step 8: Delete all conversations
                // FIXED: Added null check and use Count property safely
                if (driver.Conversations?.Any() == true)
                {
                    _logger.LogInformation("🗑️ Deleting {Count} conversations", driver.Conversations.Count);
                    _context.Conversations.RemoveRange(driver.Conversations);
                }

                // Step 9: Delete the driver
                _logger.LogInformation("🗑️ Deleting driver {DriverId}", id);
                _context.Drivers.Remove(driver);

                // Step 10: Save all changes
                await _context.SaveChangesAsync();

                _logger.LogInformation("🎉 Successfully deleted driver {DriverId} and all associated data", id);
                return Ok(new { message = "Contact deleted successfully" });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting driver {DriverId}. Inner exception: {InnerException}",
                    id, dbEx.InnerException?.Message);
                return StatusCode(500, new
                {
                    message = "Database constraint error while deleting contact",
                    error = dbEx.InnerException?.Message,
                    details = "This usually happens when there are complex message relationships. Please try again."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting driver {DriverId}", id);
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred while deleting the contact",
                    error = ex.Message,
                    details = ex.StackTrace
                });
            }
        }

        // POST: api/Drivers - FIXED VERSION
        [HttpPost]
        public async Task<ActionResult<Driver>> CreateDriver([FromBody] DriverCreateRequest request)
        {
            try
            {
                _logger.LogInformation("🚀 Creating new driver: {Name}, Phone: {Phone}", request.Name, request.PhoneNumber);

                if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.PhoneNumber))
                {
                    _logger.LogWarning("❌ Driver creation failed: Name and PhoneNumber are required");
                    return BadRequest(new { message = "Name and PhoneNumber are required" });
                }

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                var userTeamId = await GetCurrentUserTeamId();

                int targetTeamId;
                if (isAdmin && request.TeamId.HasValue)
                {
                    targetTeamId = request.TeamId.Value;
                }
                else if (userTeamId.HasValue)
                {
                    targetTeamId = userTeamId.Value;
                }
                else
                {
                    _logger.LogWarning("❌ No team context available for driver creation");
                    return BadRequest(new { message = "No team context available for driver creation" });
                }

                // ✅ Get team to use correct country code
                var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == targetTeamId && t.IsActive);
                if (team == null)
                {
                    return BadRequest(new { message = "Team not found" });
                }

                // ✅ Normalize phone number using team's country code
                var normalizedPhone = PhoneNumberUtil.NormalizePhoneNumber(request.PhoneNumber, team.CountryCode ?? "91");

                // ✅ FIXED: Fetch all drivers first, then compare in memory to avoid LINQ translation error
                var allDriversInTeam = await _context.Drivers
                    .Where(d => d.TeamId == targetTeamId)
                    .ToListAsync();

                // Compare in memory using the utility method
                var existingDriver = allDriversInTeam
                    .FirstOrDefault(d => PhoneNumberUtil.ArePhoneNumbersEqual(d.PhoneNumber, normalizedPhone, team.CountryCode ?? "91"));

                if (existingDriver != null)
                {
                    _logger.LogWarning("❌ Driver with normalized phone {Phone} already exists in team {TeamId}",
                        normalizedPhone, targetTeamId);
                    return Conflict(new { message = "Driver with this phone number already exists in your team" });
                }

                // Create driver with normalized phone
                var driver = new Driver
                {
                    Name = request.Name,
                    PhoneNumber = normalizedPhone, // ✅ Store normalized
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    TeamId = targetTeamId
                };

                _context.Drivers.Add(driver);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created driver {DriverId}: {DriverName} with normalized phone {Phone}",
                    driver.Id, driver.Name, normalizedPhone);

                // Create conversation
                var conversation = new Conversation
                {
                    DriverId = driver.Id,
                    Topic = $"Conversation with {driver.Name}",
                    CreatedAt = DateTime.UtcNow,
                    IsAnswered = false,
                    TeamId = targetTeamId,
                    IsActive = true,
                    IsGroupConversation = false
                };

                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("🎉 SUCCESS: Created driver {DriverId} and conversation {ConversationId} for team {TeamId}",
                    driver.Id, conversation.Id, targetTeamId);

                return CreatedAtAction(nameof(GetDriver), new { id = driver.Id }, driver);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR creating driver: {ErrorMessage}. InnerException: {InnerException}",
                    ex.Message, ex.InnerException?.Message);
                return StatusCode(500, new
                {
                    message = "Failed to create driver",
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        public class DriverCreateRequest
        {
            public required string PhoneNumber { get; set; }
            public required string Name { get; set; }
            public bool IsActive { get; set; } = true;
            public int? TeamId { get; set; } // Optional for admin users to specify team
        }
    }
}