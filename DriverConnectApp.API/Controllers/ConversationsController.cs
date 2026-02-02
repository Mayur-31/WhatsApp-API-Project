using DriverConnectApp.API.Models;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DriverConnectApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ConversationsController> _logger;

        public ConversationsController(AppDbContext context, ILogger<ConversationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task<int?> GetCurrentUserTeamId()
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return null;

            var user = await _context.Users
                .Where(u => u.Id == currentUserId && u.IsActive)
                .Select(u => new { u.TeamId })
                .FirstOrDefaultAsync();

            return user?.TeamId;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetConversations(
            bool? unanswered = null,
            bool? groupsOnly = null,
            bool? individualsOnly = null,
            int? teamId = null)
        {
            try
            {
                _logger.LogInformation("🚀 GetConversations called with filters - unanswered: {Unanswered}, groupsOnly: {GroupsOnly}, individualsOnly: {IndividualsOnly}, teamId: {TeamId}",
                    unanswered, groupsOnly, individualsOnly, teamId);

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                var userTeamId = await GetCurrentUserTeamId();

                _logger.LogInformation("👤 User {UserId} - Admin: {IsAdmin}, UserTeamId: {UserTeamId}",
                    currentUserId, isAdmin, userTeamId);

                // Start with base query
                var query = _context.Conversations
                    .Where(c => c.IsActive)
                    .AsQueryable();

                // Apply team filtering logic
                if (isAdmin)
                {
                    if (teamId.HasValue && teamId > 0)
                    {
                        query = query.Where(c => c.TeamId == teamId.Value);
                        _logger.LogInformation("🔧 Admin filtering by team: {TeamId}", teamId);
                    }
                    else if (teamId.HasValue && teamId == 0)
                    {
                        _logger.LogInformation("🔧 Admin selected 'All Teams' - no team filter applied");
                    }
                    else
                    {
                        _logger.LogInformation("🔧 Admin default - showing all teams");
                    }
                }
                else
                {
                    if (userTeamId.HasValue)
                    {
                        query = query.Where(c => c.TeamId == userTeamId.Value);
                        _logger.LogInformation("🔧 Non-admin user filtered to team: {TeamId}", userTeamId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ User {UserId} has no team assigned, returning empty conversations", currentUserId);
                        return Ok(new List<ConversationDto>());
                    }
                }

                // Apply existing filters
                if (unanswered.HasValue && unanswered.Value)
                {
                    query = query.Where(c => !c.IsAnswered);
                }

                if (groupsOnly.HasValue && groupsOnly.Value)
                {
                    query = query.Where(c => c.IsGroupConversation);
                }

                if (individualsOnly.HasValue && individualsOnly.Value)
                {
                    query = query.Where(c => !c.IsGroupConversation);
                }

                // Get conversations with proper null handling - FIXED: Use direct counts instead of navigation properties
                var conversations = await query
                    .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                    .ThenByDescending(c => c.Id)
                    .Select(c => new ConversationDto
                    {
                        Id = c.Id,
                        DriverId = c.DriverId ?? 0, // FIXED: Handle NULL for group conversations
                        DriverName = c.IsGroupConversation
                            ? (c.GroupName ?? "Unknown Group")
                            : (c.Driver != null ? c.Driver.Name : "Unknown Driver"),
                        DriverPhone = c.IsGroupConversation
                            ? (c.WhatsAppGroupId ?? "No ID")
                            : (c.Driver != null ? c.Driver.PhoneNumber : "No phone"),
                        Topic = c.Topic ?? (c.IsGroupConversation ? "Group Conversation" : "General Conversation"),
                        LastMessageAt = c.LastMessageAt,
                        CreatedAt = c.CreatedAt,
                        MessageCount = _context.Messages.Count(m => m.ConversationId == c.Id), // FIXED: Use direct count
                        IsAnswered = c.IsAnswered,
                        DepartmentId = c.DepartmentId,
                        DepartmentName = c.Department != null ? c.Department.Name : null,
                        AssignedToUserId = c.AssignedToUserId,
                        UnreadCount = 0,
                        LastMessagePreview = _context.Messages
                            .Where(m => m.ConversationId == c.Id)
                            .OrderByDescending(m => m.SentAt)
                            .Select(m => m.Content ?? "Loading...")
                            .FirstOrDefault() ?? "No messages", // FIXED: Handle null properly
                        IsGroupConversation = c.IsGroupConversation,
                        GroupName = c.GroupName,
                        WhatsAppGroupId = c.WhatsAppGroupId,
                        GroupId = c.GroupId,
                        GroupMemberCount = c.Group != null ?
                            _context.GroupParticipants.Count(gp => gp.GroupId == c.GroupId && gp.IsActive) : 0, // FIXED: Use direct count
                        TeamId = c.TeamId ?? 0
                    })
                    .ToListAsync();

                _logger.LogInformation("✅ Successfully loaded {Count} conversations for user {UserId}",
                    conversations.Count, currentUserId);

                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR in GetConversations: {ErrorMessage}. StackTrace: {StackTrace}",
                    ex.Message, ex.StackTrace);
                return StatusCode(500, new
                {
                    message = "Failed to load conversations",
                    error = ex.Message,
                    details = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("groups/{groupId}")]
        public async Task<ActionResult<GroupDto>> GetGroup(int groupId)
        {
            try
            {
                _logger.LogInformation("🔍 Getting group {GroupId}", groupId);

                var group = await _context.Groups
                    .Include(g => g.Participants)
                        .ThenInclude(p => p.Driver)
                    .Include(g => g.Conversations)
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    _logger.LogWarning("⚠️ Group {GroupId} not found", groupId);
                    return NotFound(new { message = "Group not found" });
                }

                var groupDto = new GroupDto
                {
                    Id = group.Id,
                    WhatsAppGroupId = group.WhatsAppGroupId ?? string.Empty, // FIXED: Handle null
                    Name = group.Name ?? string.Empty, // FIXED: Handle null
                    Description = group.Description ?? string.Empty, // FIXED: Handle null
                    CreatedAt = group.CreatedAt,
                    LastActivityAt = group.LastActivityAt,
                    IsActive = group.IsActive,
                    ConversationCount = group.Conversations.Count,
                    ParticipantCount = group.Participants.Count(p => p.IsActive),
                    Participants = group.Participants
                        .Where(p => p.IsActive)
                        .Select(p => new GroupParticipantDto
                        {
                            Id = p.Id,
                            GroupId = p.GroupId, // FIXED: This should be int, not int?
                            DriverId = p.DriverId,
                            PhoneNumber = p.PhoneNumber ?? p.Driver?.PhoneNumber,
                            ParticipantName = p.ParticipantName ?? p.Driver?.Name,
                            DriverName = p.Driver?.Name,
                            DriverPhone = p.Driver?.PhoneNumber,
                            JoinedAt = p.JoinedAt,
                            IsActive = p.IsActive,
                            Role = p.Role ?? "member" // FIXED: Handle null
                        }).ToList()
                };

                _logger.LogInformation("✅ Retrieved group {GroupId} with {ParticipantCount} participants",
                    groupId, groupDto.ParticipantCount);

                return Ok(groupDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching group {GroupId}", groupId);
                return StatusCode(500, new
                {
                    message = "Failed to fetch group",
                    error = ex.Message
                });
            }
        }

        [HttpPut("groups/{groupId}")]
        public async Task<ActionResult<GroupDto>> UpdateGroup(int groupId, [FromBody] UpdateGroupRequest request)
        {
            try
            {
                var group = await _context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                if (!string.IsNullOrEmpty(request.Name))
                {
                    group.Name = request.Name;
                }

                if (request.Description != null)
                {
                    group.Description = request.Description;
                }

                if (request.IsActive.HasValue)
                {
                    group.IsActive = request.IsActive.Value;
                }

                group.LastActivityAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return await GetGroup(groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {GroupId}", groupId);
                return StatusCode(500, new { message = "An error occurred while updating the group" });
            }
        }

        [HttpDelete("groups/{groupId}")]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            try
            {
                var group = await _context.Groups
                    .Include(g => g.Conversations)
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                // Soft delete the group
                group.IsActive = false;
                group.LastActivityAt = DateTime.UtcNow;

                // Soft delete all conversations for this group
                var conversations = await _context.Conversations
                    .Where(c => c.GroupId == groupId)
                    .ToListAsync();

                foreach (var conversation in conversations)
                {
                    conversation.IsActive = false;
                }

                // Soft delete all participants
                var participants = await _context.GroupParticipants
                    .Where(p => p.GroupId == groupId && p.IsActive)
                    .ToListAsync();

                foreach (var participant in participants)
                {
                    participant.IsActive = false;
                }

                // Save all changes
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Group {GroupId} deleted successfully", groupId);

                return Ok(new
                {
                    success = true,
                    message = "Group deleted successfully",
                    groupId = groupId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {GroupId}", groupId);
                return StatusCode(500, new { message = "An error occurred while deleting the group", error = ex.Message });
            }
        }

        [HttpPost("groups/{groupId}/participants")]
        public async Task<ActionResult<GroupDto>> AddParticipants(int groupId, [FromBody] AddParticipantsRequest request)
        {
            try
            {
                _logger.LogInformation("Starting to add {Count} participants to group {GroupId}",
                    request.Participants.Count, groupId);

                var group = await _context.Groups
                    .Include(g => g.Participants)
                        .ThenInclude(p => p.Driver)
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    _logger.LogWarning("Group {GroupId} not found when adding participants", groupId);
                    return NotFound(new { message = "Group not found" });
                }

                var addedParticipants = new List<GroupParticipant>();

                foreach (var participantRequest in request.Participants)
                {
                    _logger.LogInformation("Processing participant: Phone={PhoneNumber}, Name={Name}, DriverId={DriverId}",
                        participantRequest.PhoneNumber, participantRequest.ParticipantName, participantRequest.DriverId);

                    if (!participantRequest.DriverId.HasValue && string.IsNullOrEmpty(participantRequest.PhoneNumber))
                    {
                        return BadRequest(new { message = "Each participant must have either DriverId or PhoneNumber" });
                    }

                    var existingParticipant = group.Participants
                        .FirstOrDefault(p => p.IsActive &&
                            (p.PhoneNumber == participantRequest.PhoneNumber ||
                             (p.DriverId.HasValue && p.DriverId == participantRequest.DriverId)));

                    if (existingParticipant != null)
                    {
                        _logger.LogInformation("Participant already exists in group {GroupId}: {PhoneNumber}", groupId, participantRequest.PhoneNumber);
                        continue;
                    }

                    var participant = new GroupParticipant
                    {
                        GroupId = groupId,
                        DriverId = participantRequest.DriverId,
                        PhoneNumber = participantRequest.PhoneNumber,
                        ParticipantName = participantRequest.ParticipantName,
                        Role = participantRequest.Role ?? "member",
                        JoinedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.GroupParticipants.Add(participant);
                    addedParticipants.Add(participant);
                    _logger.LogInformation("Added participant: {PhoneNumber} to group {GroupId}", participantRequest.PhoneNumber, groupId);
                }

                group.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully added {Count} participants to group {GroupId}. Total active participants: {TotalParticipants}",
                    addedParticipants.Count, groupId, group.Participants.Count(p => p.IsActive));

                return await GetGroup(groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding participants to group {GroupId}", groupId);
                return StatusCode(500, new { message = "An error occurred while adding participants" });
            }
        }

        [HttpDelete("groups/{groupId}/participants")]
        public async Task<ActionResult<GroupDto>> RemoveParticipants(int groupId, [FromBody] RemoveParticipantsRequest request)
        {
            try
            {
                var group = await _context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                var participants = await _context.GroupParticipants
                    .Where(p => p.GroupId == groupId && request.ParticipantIds.Contains(p.Id))
                    .ToListAsync();

                foreach (var participant in participants)
                {
                    participant.IsActive = false;
                    _logger.LogInformation("Removed participant {ParticipantId} from group {GroupId}", participant.Id, groupId);
                }

                group.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return await GetGroup(groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing participants from group {GroupId}", groupId);
                return StatusCode(500, new { message = "An error occurred while removing participants" });
            }
        }

        [HttpPost("groups")]
        public async Task<ActionResult<GroupDto>> CreateGroup([FromBody] CreateGroupRequest request)
        {
            try
            {
                _logger.LogInformation("🚀 Starting group creation: {GroupName}, WhatsAppID: {WhatsAppId}",
                    request.Name, request.WhatsAppGroupId);

                if (string.IsNullOrEmpty(request.WhatsAppGroupId) || string.IsNullOrEmpty(request.Name))
                {
                    _logger.LogWarning("❌ Group creation failed: WhatsAppGroupId and Name are required");
                    return BadRequest(new { message = "WhatsAppGroupId and Name are required" });
                }

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                var userTeamId = await GetCurrentUserTeamId();

                _logger.LogInformation("👤 Group Creation - User {UserId}, Admin: {IsAdmin}, UserTeamId: {UserTeamId}, RequestTeamId: {RequestTeamId}",
                    currentUserId, isAdmin, userTeamId, request.TeamId);

                if (!isAdmin && !userTeamId.HasValue)
                {
                    _logger.LogWarning("❌ User {UserId} has no team assigned for group creation", currentUserId);
                    return BadRequest(new { message = "User is not assigned to a team. Please contact administrator." });
                }

                int targetTeamId = isAdmin && request.TeamId.HasValue ? request.TeamId.Value : (userTeamId ?? 0);

                if (targetTeamId == 0)
                {
                    _logger.LogWarning("❌ No team context available for group creation");
                    return BadRequest(new { message = "No team context available for group creation" });
                }

                // ✅ FIX: Check for existing group with same WhatsApp ID in the same team
                var existingGroup = await _context.Groups
                    .FirstOrDefaultAsync(g => g.WhatsAppGroupId == request.WhatsAppGroupId && g.TeamId == targetTeamId);

                if (existingGroup != null)
                {
                    _logger.LogWarning("❌ Group with WhatsApp ID {WhatsAppId} already exists in team {TeamId}",
                        request.WhatsAppGroupId, targetTeamId);

                    // ✅ RETURN EXISTING GROUP INSTEAD OF ERROR - User-friendly approach
                    _logger.LogInformation("✅ Returning existing group {GroupId}: {GroupName}",
                        existingGroup.Id, existingGroup.Name);

                    return await GetGroup(existingGroup.Id);
                }

                // Create the group
                var group = new Group
                {
                    WhatsAppGroupId = request.WhatsAppGroupId,
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    TeamId = targetTeamId
                };

                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created group {GroupId}: {GroupName}", group.Id, group.Name);

                // Add participants if provided
                if (request.Participants != null && request.Participants.Any())
                {
                    _logger.LogInformation("👥 Adding {Count} participants to group {GroupId}",
                        request.Participants.Count, group.Id);

                    foreach (var participantRequest in request.Participants)
                    {
                        var participant = new GroupParticipant
                        {
                            GroupId = group.Id,
                            DriverId = participantRequest.DriverId,
                            PhoneNumber = participantRequest.PhoneNumber,
                            ParticipantName = participantRequest.ParticipantName,
                            Role = participantRequest.Role ?? "member",
                            JoinedAt = DateTime.UtcNow,
                            IsActive = true
                        };

                        _context.GroupParticipants.Add(participant);
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Added {Count} participants to group {GroupId}",
                        request.Participants.Count, group.Id);
                }

                // ✅ Check if conversation already exists for this group
                var existingConversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.GroupId == group.Id && c.IsGroupConversation && c.TeamId == targetTeamId);

                if (existingConversation == null)
                {
                    // Create conversation for the group
                    var conversation = new Conversation
                    {
                        WhatsAppGroupId = group.WhatsAppGroupId,
                        GroupName = group.Name,
                        GroupId = group.Id,
                        IsGroupConversation = true,
                        Topic = $"Group: {group.Name}",
                        CreatedAt = DateTime.UtcNow,
                        IsAnswered = false,
                        TeamId = targetTeamId,
                        IsActive = true
                    };

                    _context.Conversations.Add(conversation);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("🎉 SUCCESS: Created group {GroupId} with conversation {ConversationId} for team {TeamId}",
                        group.Id, conversation.Id, targetTeamId);
                }
                else
                {
                    _logger.LogInformation("✅ Group conversation already exists: {ConversationId}", existingConversation.Id);
                }

                return await GetGroup(group.Id);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ DATABASE ERROR creating group: {ErrorMessage}. InnerException: {InnerException}",
                    dbEx.Message, dbEx.InnerException?.Message);

                // Check if it's a duplicate key error
                if (dbEx.InnerException?.Message.Contains("UNIQUE constraint") == true)
                {
                    return Conflict(new
                    {
                        message = "A group with this WhatsApp ID already exists",
                        error = "Duplicate group",
                        details = "Please check if the group was already created"
                    });
                }

                return StatusCode(500, new
                {
                    message = "Database error while creating group",
                    error = dbEx.InnerException?.Message ?? dbEx.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR creating group: {ErrorMessage}. InnerException: {InnerException}",
                    ex.Message, ex.InnerException?.Message);
                return StatusCode(500, new
                {
                    message = "Failed to create group",
                    error = ex.Message,
                    details = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("{id}/latest")]
        public async Task<ActionResult<object>> GetLatestMessageInfo(int id)
        {
            try
            {
                _logger.LogInformation("🔍 GetLatestMessageInfo called for conversation {Id}", id);

                if (!await HasAccessToConversation(id))
                {
                    return Forbid("You do not have access to this conversation.");
                }

                var conversation = await _context.Conversations
                    .Where(c => c.Id == id && c.IsActive)
                    .FirstOrDefaultAsync();

                if (conversation == null)
                {
                    return NotFound(new { message = "Conversation not found" });
                }

                var latestMessage = await _context.Messages
                    .Where(m => m.ConversationId == id && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new { m.SentAt, m.Id })
                    .FirstOrDefaultAsync();

                var latestTimestamp = latestMessage != null ? latestMessage.SentAt.ToString("o") : (conversation.LastMessageAt?.ToString("o") ?? null);
                var messageCount = await _context.Messages.CountAsync(m => m.ConversationId == id && !m.IsDeleted);

                return Ok(new
                {
                    latestMessageTimestamp = latestTimestamp,
                    lastMessageId = latestMessage?.Id ?? 0,
                    messageCount = messageCount,
                    hasNewMessages = latestMessage != null,
                    conversationId = id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetLatestMessageInfo for conversation {Id}", id);
                return StatusCode(500, new { message = "An error occurred while checking for new messages" });
            }
        }

        [HttpGet("{id}/messages/since/{lastMessageId}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessagesSince(int id, int lastMessageId)
        {
            try
            {
                _logger.LogInformation("📥 GetMessagesSince called for conversation {ConversationId}, lastMessageId: {LastMessageId}",
                    id, lastMessageId);

                if (!await HasAccessToConversation(id))
                {
                    return Forbid("You do not have access to this conversation.");
                }

                var lastMessage = await _context.Messages
                    .Where(m => m.Id == lastMessageId)
                    .Select(m => m.SentAt)
                    .FirstOrDefaultAsync();

                DateTime cutoffTime;
                if (lastMessage != default(DateTime))
                {
                    cutoffTime = lastMessage;
                }
                else
                {
                    cutoffTime = DateTime.UtcNow.AddHours(-1);
                    _logger.LogWarning("⚠️ Last message {LastMessageId} not found, using 1-hour cutoff", lastMessageId);
                }

                var newMessages = await _context.Messages
                    .Where(m => m.ConversationId == id &&
                               !m.IsDeleted &&
                               m.SentAt > cutoffTime &&
                               m.Id > lastMessageId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        ConversationId = m.ConversationId,
                        Content = m.Content ?? string.Empty,
                        MessageType = m.MessageType.ToString(),
                        IsFromDriver = m.IsFromDriver,
                        // FIXED: Remove .ToString("o") - assign DateTime directly
                        SentAt = m.SentAt,
                        // FIXED: Convert enum to string and handle null
                        Status = m.Status.ToString(),
                        WhatsAppMessageId = m.WhatsAppMessageId,
                        MediaUrl = m.MediaUrl,
                        FileName = m.FileName,
                        FileSize = m.FileSize,
                        MimeType = m.MimeType,
                        Location = m.Location,
                        ContactName = m.ContactName,
                        ContactPhone = m.ContactPhone,
                        IsGroupMessage = m.IsGroupMessage,
                        SenderPhoneNumber = m.SenderPhoneNumber,
                        SenderName = m.SenderName,
                        Context = m.Context,
                        ReplyToMessageId = m.ReplyToMessageId,
                        ReplyToMessageContent = m.ReplyToMessageContent,
                        ReplyToSenderName = m.ReplyToSenderName
                    })
                    .ToListAsync();

                _logger.LogInformation("✅ Returning {Count} new messages for conversation {ConversationId}",
                    newMessages.Count, id);

                return Ok(newMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetMessagesSince for conversation {ConversationId}", id);
                return StatusCode(500, new { message = "An error occurred while fetching new messages" });
            }
        }

        [HttpGet("{id}/summary")]
        public async Task<ActionResult<object>> GetConversationSummary(int id)
        {
            try
            {
                _logger.LogInformation("📋 GetConversationSummary called for conversation {Id}", id);

                if (!await HasAccessToConversation(id))
                {
                    return Forbid("You do not have access to this conversation.");
                }

                var conversation = await _context.Conversations
                    .Where(c => c.Id == id && c.IsActive)
                    .Select(c => new
                    {
                        Id = c.Id,
                        LastMessageAt = c.LastMessageAt,
                        LastMessagePreview = _context.Messages
                            .Where(m => m.ConversationId == c.Id && !m.IsDeleted)
                            .OrderByDescending(m => m.SentAt)
                            .Select(m => m.Content ?? "No content")
                            .FirstOrDefault() ?? "No messages",
                        MessageCount = _context.Messages.Count(m => m.ConversationId == c.Id && !m.IsDeleted),
                        IsAnswered = c.IsAnswered,
                        // FIXED: Compare to enum value MessageStatus.Read instead of string "Read"
                        UnreadCount = _context.Messages.Count(m =>
                            m.ConversationId == c.Id &&
                            !m.IsDeleted &&
                            m.IsFromDriver &&
                            m.Status != MessageStatus.Read)
                    })
                    .FirstOrDefaultAsync();

                if (conversation == null)
                {
                    return NotFound(new { message = "Conversation not found" });
                }

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetConversationSummary for conversation {Id}", id);
                return StatusCode(500, new { message = "An error occurred while fetching conversation summary" });
            }
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ConversationDetailDto>> GetConversation(int id)
        {
            _logger.LogInformation("GetConversation called for ID: {Id}", id);

            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
                var userTeamId = await GetCurrentUserTeamId();

                // First, get the conversation without the problematic column
                var conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .Include(c => c.Department)
                    .Include(c => c.Group)
                        .ThenInclude(g => g!.Participants)
                            .ThenInclude(p => p.Driver)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (conversation == null)
                {
                    _logger.LogWarning("Conversation {Id} not found", id);
                    return NotFound(new { message = "Conversation not found" });
                }

                // Then get messages separately to avoid complex query issues
                var messages = await _context.Messages
                    .Include(m => m.ReplyToMessage)
                    .Where(m => m.ConversationId == id && !m.IsDeleted)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                // Calculate window status - handle null safely
                bool canSendNonTemplate = false;
                TimeSpan? remainingTime = null;
                double? hoursRemaining = null;
                double? minutesRemaining = null;
                DateTime? windowExpiresAt = null;

                if (conversation.LastInboundMessageAt.HasValue)
                {
                    var timeSinceLastMessage = DateTime.UtcNow - conversation.LastInboundMessageAt.Value;
                    canSendNonTemplate = timeSinceLastMessage.TotalHours <= 24;

                    if (canSendNonTemplate)
                    {
                        remainingTime = TimeSpan.FromHours(24) - timeSinceLastMessage;
                        hoursRemaining = remainingTime?.TotalHours;
                        minutesRemaining = remainingTime?.TotalMinutes % 60;
                        windowExpiresAt = conversation.LastInboundMessageAt.Value.AddHours(24);
                    }
                }

                // Check team access
                if (!isAdmin)
                {
                    if (conversation.TeamId != userTeamId)
                    {
                        _logger.LogWarning("User {UserId} attempted to access conversation {ConversationId} from team {ConversationTeamId} but belongs to team {UserTeamId}",
                            currentUserId, id, conversation.TeamId, userTeamId);
                        return Forbid("You do not have access to this conversation.");
                    }
                }

                // Validate conversation data
                if (conversation.IsGroupConversation && string.IsNullOrEmpty(conversation.WhatsAppGroupId))
                {
                    _logger.LogWarning("Group conversation {Id} has no WhatsAppGroupId", id);
                    return BadRequest(new { message = "Conversation has invalid group data" });
                }

                if (!conversation.IsGroupConversation && conversation.Driver == null)
                {
                    _logger.LogWarning("Conversation {Id} has no driver data", id);
                    return BadRequest(new { message = "Conversation has invalid driver data" });
                }

                // Build DTO
                var dto = new ConversationDetailDto
                {
                    Id = conversation.Id,
                    DriverId = conversation.DriverId,
                    DriverName = conversation.IsGroupConversation
                        ? (conversation.GroupName ?? "Unknown Group")
                        : (conversation.Driver?.Name ?? "Unknown Driver"),
                    TeamId = conversation.TeamId,
                    DriverPhone = conversation.IsGroupConversation
                        ? (conversation.WhatsAppGroupId ?? "No ID")
                        : (conversation.Driver?.PhoneNumber ?? "No phone"),
                    Topic = conversation.Topic ?? (conversation.IsGroupConversation ? "Group Conversation" : "General Conversation"),
                    LastMessageAt = conversation.LastMessageAt,
                    CreatedAt = conversation.CreatedAt,
                    IsAnswered = conversation.IsAnswered,
                    DepartmentId = conversation.DepartmentId,
                    DepartmentName = conversation.Department?.Name,
                    AssignedToUserId = conversation.AssignedToUserId,
                    IsGroupConversation = conversation.IsGroupConversation,
                    GroupName = conversation.GroupName,
                    WhatsAppGroupId = conversation.WhatsAppGroupId,
                    GroupId = conversation.GroupId,

                    // 24-hour rule status
                    LastInboundMessageAt = conversation.LastInboundMessageAt,
                    CanSendNonTemplateMessages = canSendNonTemplate,
                    HoursRemaining = hoursRemaining,
                    MinutesRemaining = minutesRemaining,
                    WindowExpiresAt = windowExpiresAt,
                    NonTemplateMessageStatus = canSendNonTemplate
                        ? $"Free messaging available ({Math.Floor(hoursRemaining ?? 0)}h {Math.Floor(minutesRemaining ?? 0)}m remaining)"
                        : conversation.LastInboundMessageAt.HasValue
                            ? "Template messages only (no inbound message in last 24 hours)"
                            : "No inbound messages yet",

                    Messages = messages.Select(m => new MessageDto
                    {
                        Id = m.Id,
                        Content = m.Content ?? string.Empty,
                        IsFromDriver = m.IsFromDriver,
                        SentAt = m.SentAt,
                        ConversationId = m.ConversationId,
                        WhatsAppMessageId = m.WhatsAppMessageId,
                        Context = m.Context,
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
                            Content = m.ReplyToMessage.Content ?? string.Empty,
                            MessageType = m.ReplyToMessage.MessageType.ToString(),
                            MediaUrl = m.ReplyToMessage.MediaUrl,
                            FileName = m.ReplyToMessage.FileName,
                            IsFromDriver = m.ReplyToMessage.IsFromDriver,
                            SentAt = m.ReplyToMessage.SentAt,
                            SenderName = m.ReplyToMessage.SenderName,
                            SenderPhoneNumber = m.ReplyToMessage.SenderPhoneNumber
                        } : null
                    }).ToList(),

                    Participants = new List<GroupParticipantDto>()
                };

                // Load group participants
                if (conversation.IsGroupConversation && conversation.GroupId.HasValue)
                {
                    var participants = await _context.GroupParticipants
                        .Include(p => p.Driver)
                        .Where(p => p.GroupId == conversation.GroupId.Value && p.IsActive)
                        .ToListAsync();

                    dto.Participants = participants.Select(p => new GroupParticipantDto
                    {
                        Id = p.Id,
                        GroupId = p.GroupId,
                        DriverId = p.DriverId,
                        PhoneNumber = p.PhoneNumber ?? p.Driver?.PhoneNumber,
                        ParticipantName = p.ParticipantName ?? p.Driver?.Name ?? "Unknown",
                        DriverName = p.Driver?.Name,
                        DriverPhone = p.Driver?.PhoneNumber,
                        JoinedAt = p.JoinedAt,
                        IsActive = p.IsActive,
                        Role = p.Role ?? "member"
                    }).ToList();

                    _logger.LogInformation("Loaded {Count} active participants for group {GroupId}",
                        dto.Participants.Count, conversation.GroupId);
                }

                _logger.LogInformation("Returning conversation {Id} with {MessageCount} messages and {ParticipantCount} participants",
                    id, dto.Messages.Count, dto.Participants.Count);

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching conversation {Id}", id);
                return StatusCode(500, new
                {
                    message = "An error occurred while fetching the conversation",
                    details = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("{id}/window-status")]
        public async Task<IActionResult> GetWindowStatus(int id)
        {
            try
            {
                _logger.LogInformation("📊 Getting window status for conversation {ConversationId}", id);

                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                if (conversation == null)
                    return NotFound(new { message = "Conversation not found" });

                // Use the entity method for consistent calculation
                var status = conversation.GetWindowStatus();

                _logger.LogInformation(
                    "✅ Window Status - Conv {ConvId}: CanSend={CanSend}, LastInbound={LastInbound}, Status={Status}",
                    id, status.CanSendNonTemplateMessages, status.LastInboundMessageAt, status.Status);

                return Ok(new
                {
                    canSendNonTemplateMessages = status.CanSendNonTemplateMessages,
                    hoursRemaining = status.HoursRemaining,
                    minutesRemaining = status.MinutesRemaining,
                    lastInboundMessageAt = status.LastInboundMessageAt,
                    windowExpiresAt = status.WindowExpiresAt,
                    message = status.Message,
                    status = status.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting window status for conversation {ConversationId}", id);
                return StatusCode(500, new { message = "Failed to get window status", error = ex.Message });
            }
        }

        [HttpPost("{id}/test-inbound")]
        public async Task<IActionResult> SimulateInboundMessage(int id)
        {
            try
            {
                var conversation = await _context.Conversations.FindAsync(id);
                if (conversation == null)
                    return NotFound(new { message = "Conversation not found" });

                // Simulate inbound message
                conversation.LastInboundMessageAt = DateTime.UtcNow;
                conversation.LastMessageAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Create a test inbound message
                var testMessage = new Message
                {
                    ConversationId = conversation.Id,
                    Content = "[TEST] Inbound message from driver",
                    MessageType = MessageType.Text,
                    IsFromDriver = true,
                    SenderPhoneNumber = conversation.Driver?.PhoneNumber ?? "919876543210",
                    SenderName = conversation.Driver?.Name ?? "Test Driver",
                    SentAt = DateTime.UtcNow,
                    WhatsAppMessageId = $"test_inbound_{DateTime.UtcNow.Ticks}"
                };

                _context.Messages.Add(testMessage);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Simulated inbound message",
                    conversationId = id,
                    lastInboundMessageAt = conversation.LastInboundMessageAt,
                    windowStatus = conversation.GetWindowStatus()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating inbound message");
                return StatusCode(500, new { message = "Failed to simulate", error = ex.Message });
            }
        }

        [HttpGet("{conversationId}/media")]
        public async Task<ActionResult<ConversationMediaResponse>> GetConversationMedia(int conversationId)
        {
            try
            {
                _logger.LogInformation("Getting media for conversation {ConversationId}", conversationId);

                var conversation = await _context.Conversations
                    .Include(c => c.Driver)
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                {
                    return NotFound(new { message = "Conversation not found" });
                }

                var messages = await _context.Messages
                    .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .ToListAsync();

                var response = new ConversationMediaResponse
                {
                    ConversationName = conversation.IsGroupConversation
                        ? conversation.GroupName ?? "Group"
                        : conversation.Driver?.Name ?? "Contact",
                    IsGroupConversation = conversation.IsGroupConversation
                };

                foreach (var message in messages)
                {
                    if (!string.IsNullOrEmpty(message.MediaUrl))
                    {
                        var mediaItem = new MediaItemDto
                        {
                            Id = message.Id,
                            MessageId = message.Id,
                            ConversationId = conversationId,
                            Url = message.MediaUrl,
                            FileName = message.FileName,
                            FileSize = message.FileSize,
                            MimeType = message.MimeType,
                            SentAt = message.SentAt,
                            SenderName = message.SenderName ?? (message.IsFromDriver ? "Driver" : "You"),
                            IsFromDriver = message.IsFromDriver,
                            Description = message.Content
                        };

                        switch (message.MessageType)
                        {
                            case MessageType.Image:
                                mediaItem.Type = "image";
                                mediaItem.ThumbnailUrl = message.MediaUrl;
                                response.Images.Add(mediaItem);
                                break;
                            case MessageType.Video:
                                mediaItem.Type = "video";
                                mediaItem.ThumbnailUrl = message.MediaUrl;
                                response.Videos.Add(mediaItem);
                                break;
                            case MessageType.Document:
                                mediaItem.Type = "document";
                                response.Documents.Add(mediaItem);
                                break;
                            case MessageType.Audio:
                                mediaItem.Type = "audio";
                                response.Documents.Add(mediaItem);
                                break;
                        }
                    }

                    if (message.MessageType == MessageType.Text && !string.IsNullOrEmpty(message.Content))
                    {
                        var links = ExtractLinksFromText(message.Content);
                        foreach (var link in links)
                        {
                            response.Links.Add(new MediaItemDto
                            {
                                Id = message.Id,
                                MessageId = message.Id,
                                ConversationId = conversationId,
                                Type = "link",
                                Url = link,
                                Title = GetDomainFromUrl(link),
                                Description = message.Content.Length > 100
                                    ? message.Content.Substring(0, 100) + "..."
                                    : message.Content,
                                SentAt = message.SentAt,
                                SenderName = message.SenderName ?? (message.IsFromDriver ? "Driver" : "You"),
                                IsFromDriver = message.IsFromDriver
                            });
                        }
                    }
                }

                response.TotalItems = response.Images.Count + response.Videos.Count +
                                     response.Documents.Count + response.Links.Count;

                _logger.LogInformation("Found {TotalItems} media items for conversation {ConversationId}",
                    response.TotalItems, conversationId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media for conversation {ConversationId}", conversationId);
                return StatusCode(500, new { message = "An error occurred while fetching media" });
            }
        }

        // Helper methods for link extraction
        private List<string> ExtractLinksFromText(string text)
        {
            var links = new List<string>();
            var urlRegex = new System.Text.RegularExpressions.Regex(
                @"https?://[^\s]+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var matches = urlRegex.Matches(text);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                links.Add(match.Value);
            }

            return links;
        }

        private string GetDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "Link";
            }
        }

        [HttpGet("groups")]
        public async Task<ActionResult<IEnumerable<GroupDto>>> GetGroups(bool? activeOnly = null)
        {
            try
            {
                var query = _context.Groups
                    .Include(g => g.Conversations)
                    .AsQueryable();

                if (activeOnly.HasValue && activeOnly.Value)
                {
                    query = query.Where(g => g.IsActive);
                }

                var groups = await query
                    .OrderByDescending(g => g.LastActivityAt ?? g.CreatedAt)
                    .Select(g => new GroupDto
                    {
                        Id = g.Id,
                        WhatsAppGroupId = g.WhatsAppGroupId ?? string.Empty, // FIXED: Handle null
                        Name = g.Name ?? string.Empty, // FIXED: Handle null
                        Description = g.Description ?? string.Empty, // FIXED: Handle null
                        CreatedAt = g.CreatedAt,
                        LastActivityAt = g.LastActivityAt,
                        IsActive = g.IsActive,
                        ConversationCount = g.Conversations.Count,
                        ParticipantCount = g.Participants.Count(p => p.IsActive)
                    })
                    .ToListAsync();

                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching groups");
                return StatusCode(500, new { message = "An error occurred while fetching groups" });
            }
        }

        [HttpGet("unanswered/count")]
        public async Task<ActionResult<object>> GetUnansweredCount()
        {
            try
            {
                var count = await _context.Conversations
                    .Where(c => !c.IsAnswered)
                    .CountAsync();

                _logger.LogInformation("Unanswered conversations count: {Count}", count);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unanswered count");
                return StatusCode(500, new { message = "An error occurred while fetching unanswered count" });
            }
        }

        [HttpPut("{id}/mark-answered")]
        public async Task<IActionResult> MarkAsAnswered(int id)
        {
            _logger.LogInformation("MarkAsAnswered called for conversation {Id}", id);

            try
            {
                var conversation = await _context.Conversations.FindAsync(id);
                if (conversation == null)
                {
                    _logger.LogWarning("Conversation {Id} not found for mark-answered", id);
                    return NotFound(new { message = "Conversation not found" });
                }

                if (!await HasAccessToConversation(id))
                {
                    return Forbid("You do not have access to this conversation.");
                }

                conversation.IsAnswered = true;
                conversation.LastMessageAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Conversation {Id} marked as answered", id);
                return Ok(new { message = "Conversation marked as answered" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking conversation {Id} as answered", id);
                return StatusCode(500, new { message = "An error occurred while updating the conversation" });
            }
        }

        private async Task<bool> HasAccessToConversation(int conversationId)
        {
            var isAdmin = User.IsInRole("Admin");
            if (isAdmin) return true;

            var userTeamId = await GetCurrentUserTeamId();
            var conversationTeamId = await _context.Conversations
                .Where(c => c.Id == conversationId)
                .Select(c => c.TeamId)
                .FirstOrDefaultAsync();

            return conversationTeamId == userTeamId;
        }

        [HttpPut("{id}/mark-unanswered")]
        public async Task<IActionResult> MarkAsUnanswered(int id)
        {
            _logger.LogInformation("MarkAsUnanswered called for conversation {Id}", id);

            try
            {
                var conversation = await _context.Conversations.FindAsync(id);
                if (conversation == null)
                {
                    _logger.LogWarning("Conversation {Id} not found for mark-unanswered", id);
                    return NotFound(new { message = "Conversation not found" });
                }

                conversation.IsAnswered = false;
                conversation.LastMessageAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Conversation {Id} marked as unanswered", id);
                return Ok(new { message = "Conversation marked as unanswered" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking conversation {Id} as unanswered", id);
                return StatusCode(500, new { message = "An error occurred while updating the conversation" });
            }
        }

        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignConversation(int id, [FromBody] AssignRequest request)
        {
            _logger.LogInformation("AssignConversation called for conversation {Id} with data: {@Request}", id, request);

            try
            {
                var conversation = await _context.Conversations.FindAsync(id);
                if (conversation == null)
                {
                    _logger.LogWarning("Conversation {Id} not found for assignment", id);
                    return NotFound(new { message = "Conversation not found" });
                }

                conversation.DepartmentId = request.DepartmentId;
                conversation.AssignedToUserId = request.AssignedToUserId;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Conversation {Id} assigned successfully", id);
                return Ok(new { message = "Conversation assigned successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning conversation {Id}", id);
                return StatusCode(500, new { message = "An error occurred while assigning the conversation" });
            }
        }
    }

    // Request models
    public class AssignRequest
    {
        public int? DepartmentId { get; set; }
        public string? AssignedToUserId { get; set; }
    }

    public class CreateGroupRequest
    {
        public string WhatsAppGroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<GroupParticipantRequest> Participants { get; set; } = new List<GroupParticipantRequest>();
        public int? TeamId { get; set; }
    }

    public class UpdateGroupRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }

    public class GroupParticipantRequest
    {
        public int? DriverId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ParticipantName { get; set; }
        public string? Role { get; set; }
    }

    public class AddParticipantsRequest
    {
        public List<GroupParticipantRequest> Participants { get; set; } = new List<GroupParticipantRequest>();
    }

    public class RemoveParticipantsRequest
    {
        public List<int> ParticipantIds { get; set; } = new List<int>();
    }
}