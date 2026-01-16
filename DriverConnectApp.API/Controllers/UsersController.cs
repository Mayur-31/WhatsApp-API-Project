using DriverConnectApp.API.Models;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DriverConnectApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            try
            {
                _logger.LogInformation("Loading all users with department, depot and team information");

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var currentUser = await _context.Users
                    .Where(u => u.Id == currentUserId)
                    .Select(u => new { u.TeamId, u.TeamRole })
                    .FirstOrDefaultAsync();

                _logger.LogInformation("👤 User {UserId} - TeamId: {TeamId}, TeamRole: {TeamRole}",
                    currentUserId, currentUser?.TeamId, currentUser?.TeamRole);

                IQueryable<ApplicationUser> usersQuery = _context.Users;

                // If user has a team, filter by team (unless they're admin/superadmin)
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                if (!isAdmin && currentUser?.TeamId.HasValue == true)
                {
                    _logger.LogInformation("👤 User {UserId} has TeamId: {TeamId}", currentUserId, currentUser.TeamId);
                    usersQuery = usersQuery.Where(u => u.TeamId == currentUser.TeamId.Value);
                }

                var users = await usersQuery
                    .Include(u => u.Department)
                    .Include(u => u.Depot)
                    .Include(u => u.Team)
                    .OrderBy(u => u.FullName)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        FullName = u.FullName ?? "Unknown User",
                        Email = u.Email ?? "No email provided",
                        PhoneNumber = u.PhoneNumber,
                        IsActive = u.IsActive,
                        CreatedAt = u.CreatedAt,
                        LastLoginAt = u.LastLoginAt,
                        DepartmentId = u.DepartmentId,
                        DepotId = u.DepotId,
                        TeamId = u.TeamId,
                        TeamRole = u.TeamRole,
                        DepartmentName = u.Department != null ? u.Department.Name : null,
                        DepotName = u.Depot != null ? u.Depot.Name : null,
                        TeamName = u.Team != null ? u.Team.Name : null,
                        Roles = _context.UserRoles
                            .Where(ur => ur.UserId == u.Id)
                            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                            .ToList()
                    })
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} users", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                return StatusCode(500, new { message = "An error occurred while loading users", error = ex.Message });
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
                    userId, user?.TeamId ?? 0, user?.TeamRole ?? "No Role");

                return user?.TeamId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting current user team ID");
                return null;
            }
        }

        [HttpPut("{id}/assignment")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> UpdateUserAssignment(string id, [FromBody] UpdateUserAssignmentRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 Updating assignment for user {UserId}", id);

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("⚠️ User {UserId} not found for assignment update", id);
                    return NotFound(new { message = "User not found" });
                }

                // FIXED: Handle TeamRole properly - use empty string instead of null when removing from team
                user.TeamId = request.TeamId;

                // Set TeamRole to empty string when no team, or to the provided role
                if (!request.TeamId.HasValue)
                {
                    user.TeamRole = ""; // Empty string instead of null
                }
                else
                {
                    user.TeamRole = string.IsNullOrWhiteSpace(request.TeamRole) ? "TeamMember" : request.TeamRole;
                }

                user.DepartmentId = request.DepartmentId;
                user.DepotId = request.DepotId;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("❌ Failed to update user assignment: {Errors}", errors);
                    return BadRequest(new { message = "Failed to update user assignment", errors = errors });
                }

                _logger.LogInformation("✅ Successfully updated assignment for user {UserId}", id);
                return Ok(new { message = "User assignment updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating assignment for user {UserId}", id);
                return StatusCode(500, new { message = "Failed to update user assignment", error = ex.Message });
            }
        }

        [HttpGet("my-team")]
        public async Task<ActionResult<object>> GetMyTeam()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (!currentUser.TeamId.HasValue)
                {
                    return Ok(new { message = "User is not assigned to any team" });
                }

                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.Id == currentUser.TeamId.Value);

                if (team == null)
                {
                    return NotFound(new { message = "Team not found" });
                }

                return Ok(new
                {
                    id = team.Id,
                    name = team.Name,
                    description = team.Description,
                    whatsAppPhoneNumber = team.WhatsAppPhoneNumber,
                    teamRole = currentUser.TeamRole
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user team");
                return StatusCode(500, new { message = "Failed to get team information" });
            }
        }
    }

    public class UpdateUserAssignmentRequest
    {
        public int? DepartmentId { get; set; }
        public int? DepotId { get; set; }
        public int? TeamId { get; set; }
        public string? TeamRole { get; set; }
    }
}