using DriverConnectApp.API.Models;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DriverConnectApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TeamsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public TeamsController(
            AppDbContext context,
            ILogger<TeamsController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // GET: api/teams
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TeamDto>>> GetTeams()
        {
            try
            {
                _logger.LogInformation("📋 Loading all teams");

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var isAdmin = User.IsInRole("Admin");

                _logger.LogInformation("👤 User {UserId} - SuperAdmin: {IsSuperAdmin}, Admin: {IsAdmin}",
                    currentUserId, isSuperAdmin, isAdmin);

                IQueryable<Team> teamsQuery = _context.Teams.Where(t => t.IsActive);

                // If not SuperAdmin or Admin, only show the user's team
                if (!isSuperAdmin && !isAdmin)
                {
                    var currentUser = await _context.Users
                        .Where(u => u.Id == currentUserId)
                        .Select(u => new { u.TeamId })
                        .FirstOrDefaultAsync();

                    if (currentUser?.TeamId != null)
                    {
                        teamsQuery = teamsQuery.Where(t => t.Id == currentUser.TeamId);
                        _logger.LogInformation("🔒 User restricted to team {TeamId}", currentUser.TeamId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ User {UserId} has no team assigned", currentUserId);
                        return Ok(Enumerable.Empty<TeamDto>());
                    }
                }

                var teams = await teamsQuery
                    .OrderBy(t => t.Name)
                    .Select(t => new TeamDto
                    {
                        Id = t.Id,
                        Name = t.Name ?? "Unnamed Team",
                        Description = t.Description ?? "No description",
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        WhatsAppPhoneNumberId = t.WhatsAppPhoneNumberId,
                        WhatsAppAccessToken = t.WhatsAppAccessToken,
                        WhatsAppBusinessAccountId = t.WhatsAppBusinessAccountId,
                        WhatsAppPhoneNumber = t.WhatsAppPhoneNumber ?? "Not set",
                        ApiVersion = t.ApiVersion ?? "18.0",
                        UserCount = _context.Users.Count(u => u.TeamId == t.Id && u.IsActive),
                        ContactCount = _context.Drivers.Count(d => d.TeamId == t.Id && d.IsActive),
                        ChatCount = _context.Conversations.Count(c => c.TeamId == t.Id),
                        GroupCount = _context.Groups.Count(g => g.TeamId == t.Id && g.IsActive)
                    })
                    .ToListAsync();

                _logger.LogInformation("✅ Loaded {TeamCount} teams", teams.Count);
                return Ok(teams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading teams");
                return StatusCode(500, new { message = "Failed to load teams", error = ex.Message });
            }
        }

        // GET: api/teams/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<TeamDto>> GetTeam(int id)
        {
            try
            {
                _logger.LogInformation("🔍 Loading team {TeamId}", id);

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var isAdmin = User.IsInRole("Admin");

                var team = await _context.Teams
                    .Where(t => t.Id == id && t.IsActive)
                    .Select(t => new TeamDto
                    {
                        Id = t.Id,
                        Name = t.Name ?? "Team " + t.Id,
                        Description = t.Description ?? "No description provided",
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        WhatsAppPhoneNumberId = t.WhatsAppPhoneNumberId,
                        WhatsAppAccessToken = t.WhatsAppAccessToken,
                        WhatsAppBusinessAccountId = t.WhatsAppBusinessAccountId,
                        WhatsAppPhoneNumber = t.WhatsAppPhoneNumber ?? "Not configured",
                        ApiVersion = t.ApiVersion ?? "18.0",
                        UserCount = _context.Users.Count(u => u.TeamId == t.Id && u.IsActive),
                        ContactCount = _context.Drivers.Count(d => d.TeamId == t.Id && d.IsActive),
                        ChatCount = _context.Conversations.Count(c => c.TeamId == t.Id),
                        GroupCount = _context.Groups.Count(g => g.TeamId == t.Id && g.IsActive)
                    })
                    .FirstOrDefaultAsync();

                if (team == null)
                {
                    _logger.LogWarning("⚠️ Team {TeamId} not found", id);
                    return NotFound(new { message = "Team not found" });
                }

                // Check if current user has access to this team
                if (!isSuperAdmin && !isAdmin)
                {
                    var currentUser = await _context.Users
                        .Where(u => u.Id == currentUserId)
                        .Select(u => new { u.TeamId })
                        .FirstOrDefaultAsync();

                    if (currentUser?.TeamId != id)
                    {
                        _logger.LogWarning("⛔ User {UserId} attempted to access team {TeamId} without permission", currentUserId, id);
                        return Forbid();
                    }
                }

                _logger.LogInformation("✅ Loaded team {TeamId} successfully", id);
                return Ok(team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading team {TeamId}", id);
                return StatusCode(500, new { message = "Failed to load team", error = ex.Message });
            }
        }

        // POST: api/teams
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<ActionResult<TeamDto>> CreateTeam([FromBody] CreateTeamRequest request)
        {
            try
            {
                _logger.LogInformation("🆕 Creating new team: {TeamName}", request.Name);

                // Enhanced validation with detailed error messages
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Team name is required" });
                }

                // Check for duplicate team name (case insensitive, trim spaces)
                var normalizedName = request.Name.Trim().ToLower();
                var existingTeam = await _context.Teams
                    .AnyAsync(t => t.Name != null && t.Name.Trim().ToLower() == normalizedName && t.IsActive);

                if (existingTeam)
                {
                    return BadRequest(new { message = "A team with this name already exists" });
                }

                // Create the team with proper validation
                var team = new Team
                {
                    Name = request.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumberId) ? "default_phone_id_" + Guid.NewGuid().ToString("N")[..8] : request.WhatsAppPhoneNumberId.Trim(),
                    WhatsAppAccessToken = string.IsNullOrWhiteSpace(request.WhatsAppAccessToken) ? "default_token_" + Guid.NewGuid().ToString("N")[..8] : request.WhatsAppAccessToken.Trim(),
                    WhatsAppBusinessAccountId = string.IsNullOrWhiteSpace(request.WhatsAppBusinessAccountId) ? "default_account_" + Guid.NewGuid().ToString("N")[..8] : request.WhatsAppBusinessAccountId.Trim(),
                    WhatsAppPhoneNumber = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumber) ? "+1234567890" : request.WhatsAppPhoneNumber.Trim(),
                    ApiVersion = string.IsNullOrWhiteSpace(request.ApiVersion) ? "18.0" : request.ApiVersion.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Teams.Add(team);
                await _context.SaveChangesAsync();

                // Create default team configuration
                var teamConfig = new TeamConfiguration
                {
                    TeamId = team.Id,
                    BrandColor = "#10B981",
                    TimeZone = "Asia/Kolkata",
                    Language = "en",
                    Is24Hours = true,
                    MaxMessagesPerMinute = 60,
                    MaxMessagesPerDay = 1000,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TeamConfigurations.Add(teamConfig);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created team {TeamId} successfully", team.Id);

                var teamDto = new TeamDto
                {
                    Id = team.Id,
                    Name = team.Name,
                    Description = team.Description ?? "No description provided",
                    IsActive = team.IsActive,
                    CreatedAt = team.CreatedAt,
                    WhatsAppPhoneNumberId = team.WhatsAppPhoneNumberId,
                    WhatsAppAccessToken = team.WhatsAppAccessToken,
                    WhatsAppBusinessAccountId = team.WhatsAppBusinessAccountId,
                    WhatsAppPhoneNumber = team.WhatsAppPhoneNumber ?? "Not configured",
                    ApiVersion = team.ApiVersion ?? "18.0",
                    UserCount = 0,
                    ContactCount = 0,
                    ChatCount = 0,
                    GroupCount = 0
                };

                return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, teamDto);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ Database error creating team");
                return StatusCode(500, new
                {
                    message = "Database error while creating team",
                    error = dbEx.InnerException?.Message ?? dbEx.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating team");
                return StatusCode(500, new
                {
                    message = "Failed to create team",
                    error = ex.Message
                });
            }
        }

        // PUT: api/teams/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<ActionResult<TeamDto>> UpdateTeam(int id, [FromBody] UpdateTeamRequest request)
        {
            try
            {
                _logger.LogInformation("✏️ Updating team {TeamId} with data: {@RequestData}", id, request);

                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

                if (team == null)
                {
                    _logger.LogWarning("⚠️ Team {TeamId} not found for update", id);
                    return NotFound(new { message = "Team not found" });
                }

                // Enhanced validation for update
                if (request.Name != null)
                {
                    if (string.IsNullOrWhiteSpace(request.Name))
                    {
                        return BadRequest(new { message = "Team name cannot be empty" });
                    }

                    var newName = request.Name.Trim();
                    var normalizedNewName = newName.ToLower();

                    // Check for duplicate name (excluding current team)
                    var currentNormalizedName = team.Name?.ToLower() ?? "";
                    if (normalizedNewName != currentNormalizedName)
                    {
                        var duplicateName = await _context.Teams
                            .AnyAsync(t => t.Id != id &&
                                        t.Name != null &&
                                        t.Name.Trim().ToLower() == normalizedNewName &&
                                        t.IsActive);

                        if (duplicateName)
                        {
                            return BadRequest(new { message = "A team with this name already exists" });
                        }
                    }

                    team.Name = newName;
                }

                // Update fields only if provided - FIXED: Handle null properly
                if (request.Description != null)
                {
                    team.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                }

                // Update WhatsApp fields only if provided - FIXED: Don't set defaults on update
                if (request.WhatsAppPhoneNumberId != null)
                {
                    team.WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumberId) ?
                        null : request.WhatsAppPhoneNumberId.Trim();
                }

                if (request.WhatsAppAccessToken != null)
                {
                    team.WhatsAppAccessToken = string.IsNullOrWhiteSpace(request.WhatsAppAccessToken) ?
                        null : request.WhatsAppAccessToken.Trim();
                }

                if (request.WhatsAppBusinessAccountId != null)
                {
                    team.WhatsAppBusinessAccountId = string.IsNullOrWhiteSpace(request.WhatsAppBusinessAccountId) ?
                        null : request.WhatsAppBusinessAccountId.Trim();
                }

                if (request.WhatsAppPhoneNumber != null)
                {
                    team.WhatsAppPhoneNumber = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumber) ?
                        null : request.WhatsAppPhoneNumber.Trim();
                }

                if (request.ApiVersion != null)
                {
                    team.ApiVersion = string.IsNullOrWhiteSpace(request.ApiVersion) ? "18.0" : request.ApiVersion.Trim();
                }

                // Handle IsActive update if provided
                if (request.IsActive.HasValue)
                {
                    team.IsActive = request.IsActive.Value;
                }

                team.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Return the updated team
                var updatedTeamDto = new TeamDto
                {
                    Id = team.Id,
                    Name = team.Name ?? "Unnamed Team",
                    Description = team.Description ?? "No description",
                    IsActive = team.IsActive,
                    CreatedAt = team.CreatedAt,
                    WhatsAppPhoneNumberId = team.WhatsAppPhoneNumberId,
                    WhatsAppAccessToken = team.WhatsAppAccessToken,
                    WhatsAppBusinessAccountId = team.WhatsAppBusinessAccountId,
                    WhatsAppPhoneNumber = team.WhatsAppPhoneNumber ?? "Not set",
                    ApiVersion = team.ApiVersion ?? "18.0",
                    UserCount = _context.Users.Count(u => u.TeamId == team.Id && u.IsActive),
                    ContactCount = _context.Drivers.Count(d => d.TeamId == team.Id && d.IsActive),
                    ChatCount = _context.Conversations.Count(c => c.TeamId == team.Id),
                    GroupCount = _context.Groups.Count(g => g.TeamId == team.Id && g.IsActive)
                };

                _logger.LogInformation("✅ Updated team {TeamId} successfully", id);
                return Ok(updatedTeamDto);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ Database error updating team {TeamId}", id);
                return StatusCode(500, new
                {
                    message = "Database error while updating team",
                    error = dbEx.InnerException?.Message ?? dbEx.Message,
                    details = "Check team name uniqueness and required fields"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating team {TeamId}", id);
                return StatusCode(500, new
                {
                    message = "Failed to update team",
                    error = ex.Message
                });
            }
        }

        // DELETE: api/teams/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            try
            {
                _logger.LogInformation("🗑️ Deleting team {TeamId}", id);

                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

                if (team == null)
                {
                    _logger.LogWarning("⚠️ Team {TeamId} not found for deletion", id);
                    return NotFound(new { message = "Team not found" });
                }

                // Check if team can be deleted - only allow if no users are assigned
                var userCount = await _context.Users.CountAsync(u => u.TeamId == id && u.IsActive);

                if (userCount > 0)
                {
                    _logger.LogWarning("⚠️ Cannot delete team {TeamId} with {UserCount} users assigned", id, userCount);
                    return BadRequest(new
                    {
                        message = $"Cannot delete team because it has {userCount} user(s) assigned. " +
                                 "Please reassign or remove all users before deleting the team."
                    });
                }

                // For teams with data but no users, we can proceed with deletion
                var contactCount = await _context.Drivers.CountAsync(d => d.TeamId == id && d.IsActive);
                var conversationCount = await _context.Conversations.CountAsync(c => c.TeamId == id);
                var groupCount = await _context.Groups.CountAsync(g => g.TeamId == id && g.IsActive);

                if (contactCount > 0 || conversationCount > 0 || groupCount > 0)
                {
                    _logger.LogWarning("⚠️ Team {TeamId} has data but no users - proceeding with deletion: Contacts={ContactCount}, Conversations={ConversationCount}, Groups={GroupCount}",
                        id, contactCount, conversationCount, groupCount);
                }

                // Use hard delete to prevent reappearance
                _context.Teams.Remove(team);

                // Also delete team configuration
                var teamConfig = await _context.TeamConfigurations
                    .FirstOrDefaultAsync(tc => tc.TeamId == id);
                if (teamConfig != null)
                {
                    _context.TeamConfigurations.Remove(teamConfig);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Deleted team {TeamId} successfully", id);
                return Ok(new { message = "Team deleted successfully" });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ Database error deleting team {TeamId}", id);
                return StatusCode(500, new
                {
                    message = "Database error while deleting team",
                    error = dbEx.InnerException?.Message ?? dbEx.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting team {TeamId}", id);
                return StatusCode(500, new
                {
                    message = "Failed to delete team",
                    error = ex.Message
                });
            }
        }

        // GET: api/teams/{id}/users
        [HttpGet("{id}/users")]
        public async Task<ActionResult<IEnumerable<TeamUserDto>>> GetTeamUsers(int id)
        {
            try
            {
                _logger.LogInformation("👥 Loading users for team {TeamId}", id);

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var isAdmin = User.IsInRole("Admin");

                // Verify team exists first
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

                if (team == null)
                {
                    _logger.LogWarning("⚠️ Team {TeamId} not found or inactive", id);
                    return NotFound(new { message = $"Team with ID {id} not found" });
                }

                // Check if current user has access to this team's users
                if (!isSuperAdmin && !isAdmin)
                {
                    var currentUser = await _context.Users
                        .Where(u => u.Id == currentUserId)
                        .Select(u => new { u.TeamId })
                        .FirstOrDefaultAsync();

                    if (currentUser?.TeamId != id)
                    {
                        _logger.LogWarning("⛔ User {UserId} attempted to access team {TeamId} users without permission", currentUserId, id);
                        return Forbid("You do not have permission to view users for this team");
                    }
                }

                var users = await _context.Users
                    .Include(u => u.Department)
                    .Include(u => u.Depot)
                    .Include(u => u.Team)
                    .Where(u => u.TeamId == id && u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new TeamUserDto
                    {
                        Id = u.Id,
                        FullName = u.FullName ?? "Unknown User",
                        Email = u.Email ?? "No email",
                        PhoneNumber = u.PhoneNumber,
                        TeamRole = u.TeamRole ?? "TeamMember",
                        IsActive = u.IsActive,
                        LastLoginAt = u.LastLoginAt,
                        CreatedAt = u.CreatedAt,
                        DepartmentId = u.DepartmentId,
                        DepotId = u.DepotId,
                        DepartmentName = u.Department != null ? u.Department.Name : null,
                        DepotName = u.Depot != null ? u.Depot.Name : null,
                        TeamName = team.Name ?? "Team " + team.Id
                    })
                    .ToListAsync();

                _logger.LogInformation("✅ Found {UserCount} users for team {TeamId}", users.Count, id);

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading users for team {TeamId}", id);
                return StatusCode(500, new
                {
                    message = "Failed to load team users",
                    error = ex.Message
                });
            }
        }

        // POST: api/teams/{id}/assign-user
        [HttpPost("{id}/assign-user")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> AssignUserToTeam(int id, [FromBody] AssignUserToTeamRequest request)
        {
            try
            {
                _logger.LogInformation("👤 Assigning user {UserId} to team {TeamId}", request.UserId, id);

                if (string.IsNullOrWhiteSpace(request.UserId))
                {
                    return BadRequest(new { message = "User ID is required" });
                }

                // Verify team exists
                var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
                if (team == null)
                {
                    _logger.LogWarning("⚠️ Team {TeamId} not found for user assignment", id);
                    return NotFound(new { message = "Team not found" });
                }

                // Find the user
                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                {
                    _logger.LogWarning("⚠️ User {UserId} not found for team assignment", request.UserId);
                    return NotFound(new { message = "User not found" });
                }

                // Update user's team assignment
                user.TeamId = id;
                user.TeamRole = request.TeamRole ?? "TeamMember";

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("❌ Failed to assign user to team: {Errors}", errors);
                    return BadRequest(new { message = "Failed to assign user to team", errors = errors });
                }

                _logger.LogInformation("✅ Successfully assigned user {UserId} to team {TeamId}", request.UserId, id);
                return Ok(new { message = "User assigned to team successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error assigning user to team {TeamId}", id);
                return StatusCode(500, new { message = "Failed to assign user to team", error = ex.Message });
            }
        }

        // GET: api/teams/{id}/stats
        [HttpGet("{id}/stats")]
        public async Task<ActionResult<object>> GetTeamStats(int id)
        {
            try
            {
                _logger.LogInformation("📊 Loading stats for team {TeamId}", id);

                // Check if current user has access to this team
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var isAdmin = User.IsInRole("Admin");

                if (!isSuperAdmin && !isAdmin)
                {
                    var currentUser = await _context.Users
                        .Where(u => u.Id == currentUserId)
                        .Select(u => new { u.TeamId })
                        .FirstOrDefaultAsync();

                    if (currentUser?.TeamId != id)
                    {
                        _logger.LogWarning("⛔ User {UserId} attempted to access team {TeamId} stats without permission", currentUserId, id);
                        return Forbid();
                    }
                }

                var userCount = await _context.Users
                    .CountAsync(u => u.TeamId == id && u.IsActive);

                var contactCount = await _context.Drivers
                    .CountAsync(d => d.TeamId == id && d.IsActive);

                var conversationCount = await _context.Conversations
                    .CountAsync(c => c.TeamId == id);

                var groupCount = await _context.Groups
                    .CountAsync(g => g.TeamId == id && g.IsActive);

                var stats = new
                {
                    Users = userCount,
                    Contacts = contactCount,
                    Chats = conversationCount,
                    Groups = groupCount
                };

                _logger.LogInformation("📈 Team {TeamId} stats: {Stats}", id, stats);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading stats for team {TeamId}", id);
                return StatusCode(500, new { message = "Failed to load team stats", error = ex.Message });
            }
        }
    }

    // Request models
    public class CreateTeamRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? WhatsAppPhoneNumberId { get; set; }
        public string? WhatsAppAccessToken { get; set; }
        public string? WhatsAppBusinessAccountId { get; set; }
        public string? WhatsAppPhoneNumber { get; set; }
        public string? ApiVersion { get; set; }
    }

    public class UpdateTeamRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? WhatsAppPhoneNumberId { get; set; }
        public string? WhatsAppAccessToken { get; set; }
        public string? WhatsAppBusinessAccountId { get; set; }
        public string? WhatsAppPhoneNumber { get; set; }
        public string? ApiVersion { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AssignUserToTeamRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string? TeamRole { get; set; }
    }

    public class TeamUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? TeamRole { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? DepartmentId { get; set; }
        public int? DepotId { get; set; }
        public string? DepartmentName { get; set; }
        public string? DepotName { get; set; }
        public string TeamName { get; set; } = string.Empty;
    }

    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? WhatsAppPhoneNumberId { get; set; }
        public string? WhatsAppAccessToken { get; set; }
        public string? WhatsAppBusinessAccountId { get; set; }
        public string? WhatsAppPhoneNumber { get; set; }
        public string? ApiVersion { get; set; }
        public int UserCount { get; set; }
        public int ContactCount { get; set; }
        public int ChatCount { get; set; }
        public int GroupCount { get; set; }
    }
}