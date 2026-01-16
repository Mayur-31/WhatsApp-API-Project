using DriverConnectApp.API.Models;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverConnectApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AppDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AppDbContext context,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context; // Add this line
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for: {Email}", request.Email);

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found for email {Email}", request.Email);
                    return BadRequest(new { success = false, message = "Invalid credentials" });
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login failed: User {Email} is inactive", request.Email);
                    return BadRequest(new { success = false, message = "Account is deactivated" });
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName ?? user.Email!,
                    request.Password,
                    request.RememberMe,
                    lockoutOnFailure: false
                );

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} logged in successfully", request.Email);

                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    var roles = await _userManager.GetRolesAsync(user);
                    var rolesArray = roles?.ToArray() ?? Array.Empty<string>();

                    // Get team information
                    TeamDto? teamDto = null;
                    if (user.TeamId.HasValue)
                    {
                        var team = await _context.Teams
                            .FirstOrDefaultAsync(t => t.Id == user.TeamId.Value);

                        if (team != null)
                        {
                            teamDto = new TeamDto
                            {
                                Id = team.Id,
                                Name = team.Name,
                                Description = team.Description,
                                IsActive = team.IsActive
                            };
                        }
                    }

                    var userResponse = new
                    {
                        user.Id,
                        user.FullName,
                        user.Email,
                        user.PhoneNumber,
                        user.IsActive,
                        user.CreatedAt,
                        user.LastLoginAt,
                        user.DepartmentId,
                        user.DepotId,
                        user.TeamId,        // CRITICAL: Include TeamId
                        user.TeamRole,      // CRITICAL: Include TeamRole
                        Team = teamDto,     // Include full team object
                        Roles = rolesArray,
                        IsSuperAdmin = rolesArray.Contains("SuperAdmin"),
                        IsAdmin = rolesArray.Contains("Admin")
                    };

                    _logger.LogInformation("Login successful for {Email}: TeamId={TeamId}, Team={TeamName}, Roles={Roles}",
                        request.Email, user.TeamId, teamDto?.Name ?? "No Team", string.Join(",", rolesArray));

                    return Ok(new
                    {
                        success = true,
                        message = "Login successful",
                        user = userResponse,
                        token = "authenticated"
                    });
                }

                _logger.LogWarning("Login failed for {Email}: Invalid password", request.Email);
                return BadRequest(new { success = false, message = "Invalid credentials" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", request.Email);
                return StatusCode(500, new { success = false, message = "An error occurred during login" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registration attempt for: {Email}", request.Email);

                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new { success = false, message = "User with this email already exists" });
                }

                var user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (result.Succeeded)
                {
                    // Assign default role
                    await _userManager.AddToRoleAsync(user, "User");

                    _logger.LogInformation("User {Email} registered successfully", request.Email);

                    return Ok(new
                    {
                        success = true,
                        message = "Registration successful. Please login."
                    });
                }

                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { success = false, message = string.Join(", ", errors) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", request.Email);
                return StatusCode(500, new { success = false, message = "An error occurred during registration" });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User logged out");
                return Ok(new { success = true, message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "An error occurred during logout" });
            }
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var roles = await _userManager.GetRolesAsync(user);
                var rolesArray = roles?.ToArray() ?? Array.Empty<string>();

                // Get team information
                TeamDto? teamDto = null;
                if (user.TeamId.HasValue)
                {
                    var team = await _context.Teams
                        .FirstOrDefaultAsync(t => t.Id == user.TeamId.Value);

                    if (team != null)
                    {
                        teamDto = new TeamDto
                        {
                            Id = team.Id,
                            Name = team.Name,
                            Description = team.Description,
                            IsActive = team.IsActive
                        };
                    }
                }

                var userResponse = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.IsActive,
                    user.CreatedAt,
                    user.LastLoginAt,
                    user.DepartmentId,
                    user.DepotId,
                    user.TeamId,
                    user.TeamRole,
                    Team = teamDto,
                    Roles = rolesArray,
                    IsSuperAdmin = rolesArray.Contains("SuperAdmin"),
                    IsAdmin = rolesArray.Contains("Admin")
                };

                return Ok(userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { success = false, message = "Error getting user information" });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    // Don't reveal that the user doesn't exist
                    return Ok(new { success = true, message = "If your email is registered, you will receive a password reset link." });
                }

                // TODO: Implement password reset logic here
                // For now, just return success
                return Ok(new { success = true, message = "If your email is registered, you will receive a password reset link." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in forgot password for {Email}", request.Email);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }
    }
}