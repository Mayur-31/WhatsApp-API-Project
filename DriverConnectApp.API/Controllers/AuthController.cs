using DriverConnectApp.API.Models;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DriverConnectApp.API.Services;
using Microsoft.AspNetCore.Authorization;

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
        private readonly IEmailService _emailService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AppDbContext context,
            ILogger<AuthController> logger,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context; // Add this line
            _logger = logger;
            _emailService = emailService;
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
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Password reset requested for: {Email}", request.Email);

                var user = await _userManager.FindByEmailAsync(request.Email);

                // Security: Always return same message to prevent email enumeration
                if (user == null || !user.IsActive)
                {
                    _logger.LogInformation("Password reset requested for non-existent/inactive: {Email}", request.Email);
                    return Ok(new { success = true, message = "If your email is registered, you will receive a password reset link." });
                }

                // Generate password reset token (ASP.NET Identity handles expiration)
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                _logger.LogDebug("Generated reset token for {Email}", request.Email);

                try
                {
                    // Send email - EmailService handles URL encoding
                    await _emailService.SendPasswordResetEmailAsync(user.Email!, token);
                    _logger.LogInformation("✅ Password reset email sent to {Email}", request.Email);
                }
                catch (Exception emailEx)
                {
                    // Log error but don't reveal to user (security)
                    _logger.LogError(emailEx, "Failed to send password reset email to {Email}", request.Email);
                    // Continue - still return success to user
                }

                return Ok(new { success = true, message = "If your email is registered, you will receive a password reset link." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in forgot-password for {Email}", request.Email);
                return StatusCode(500, new { success = false, message = "An error occurred while processing your request" });
            }
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Password reset attempt for: {Email}", request.Email);

                // Validate request
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { success = false, message = "Invalid reset request" });
                }

                if (request.NewPassword != request.ConfirmPassword)
                {
                    return BadRequest(new { success = false, message = "Passwords do not match" });
                }

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    // Security: Return generic error
                    _logger.LogWarning("Password reset for non-existent user: {Email}", request.Email);
                    return BadRequest(new { success = false, message = "Invalid reset request" });
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Password reset for inactive user: {Email}", request.Email);
                    return BadRequest(new { success = false, message = "Account is deactivated" });
                }

                // IMPORTANT: Don't decode the token! ASP.NET Identity handles encoded tokens
                // The token from the URL is already URL-encoded, and Identity can handle it
                var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ Password reset successful for {Email}", request.Email);

                    // Update security stamp to invalidate any existing sessions
                    await _userManager.UpdateSecurityStampAsync(user);

                    // Send confirmation email
                    try
                    {
                        await _emailService.SendEmailAsync(
                            user.Email!,
                            "Password Changed Successfully - DriverConnect",
                            $@"
                            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #4F46E5;'>Password Changed Successfully</h2>
                                <p>Your DriverConnect password has been changed successfully.</p>
                                <div style='background: #f0f9ff; border-left: 4px solid #0ea5e9; padding: 12px; margin: 20px 0;'>
                                    <p style='margin: 0; color: #0369a1;'>
                                        <strong>Note:</strong> If you didn't make this change, please contact support immediately.
                                    </p>
                                </div>
                                <p>Best regards,<br/><strong>DriverConnect Team</strong></p>
                            </div>"
                        );
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send confirmation email to {Email}", request.Email);
                        // Don't fail the reset if email fails
                    }

                    return Ok(new
                    {
                        success = true,
                        message = "Password has been reset successfully. You can now login with your new password."
                    });
                }

                // Handle errors
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Password reset failed for {Email}: {Errors}", request.Email, errors);

                // User-friendly error messages
                if (result.Errors.Any(e => e.Code.Contains("InvalidToken")))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "The reset link is invalid or has expired. Please request a new password reset."
                    });
                }

                // Check for password validation errors
                var passwordErrors = result.Errors
                    .Where(e => e.Code.Contains("Password"))
                    .Select(e => e.Description);

                if (passwordErrors.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = string.Join(" ", passwordErrors)
                    });
                }

                return BadRequest(new
                {
                    success = false,
                    message = "Failed to reset password. Please try again or request a new reset link."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for {Email}", request.Email);
                return StatusCode(500, new { success = false, message = "An error occurred while resetting your password" });
            }
        }
    }
}