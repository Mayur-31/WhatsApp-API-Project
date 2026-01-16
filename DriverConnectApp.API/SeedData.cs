// DriverConnectApp.API/SeedData.cs
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverConnectApp.API
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var context = new AppDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>());

            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("🚀 Starting database seeding...");

                // 1. Create roles (always ensure roles exist)
                await EnsureRolesExist(roleManager, logger);

                // 2. ONLY seed teams if database is empty
                var existingTeamsCount = await context.Teams.CountAsync();
                if (existingTeamsCount == 0)
                {
                    logger.LogInformation("📋 No teams found. Creating default teams...");
                    await CreateDefaultTeams(context, logger);
                }
                else
                {
                    logger.LogInformation("📋 Teams already exist ({Count}). Skipping team creation.", existingTeamsCount);
                }

                // 3. ONLY seed users if they don't exist (don't overwrite)
                await EnsureUsersExist(context, userManager, logger);

                // 4. Ensure team configurations exist
                await EnsureTeamConfigurationsExist(context, logger);

                logger.LogInformation("🎉 Database seeding completed successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ CRITICAL ERROR during database seeding");
                throw;
            }
        }

        private static async Task EnsureRolesExist(RoleManager<IdentityRole> roleManager, ILogger logger)
        {
            string[] roleNames = { "SuperAdmin", "Admin", "Manager", "User", "TeamAdmin" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.LogInformation("✅ Created role: {RoleName}", roleName);
                }
            }
        }

        private static async Task CreateDefaultTeams(AppDbContext context, ILogger logger)
        {
            var teamsData = new[]
            {
                new {
                    Name = "Sales Team",
                    Description = "Primary sales and customer support team",
                    PhoneNumberId = "sales_phone_123",
                    AccessToken = "sales_token_456",
                    BusinessAccountId = "sales_account_789",
                    PhoneNumber = "+1234567890"
                },
                new {
                    Name = "Support Team",
                    Description = "Technical support and assistance team",
                    PhoneNumberId = "support_phone_123",
                    AccessToken = "support_token_456",
                    BusinessAccountId = "support_account_789",
                    PhoneNumber = "+1234567891"
                },
                new {
                    Name = "Operations Team",
                    Description = "Operations and logistics team",
                    PhoneNumberId = "ops_phone_123",
                    AccessToken = "ops_token_456",
                    BusinessAccountId = "ops_account_789",
                    PhoneNumber = "+1234567892"
                },
                new {
                    Name = "Team Alpha",
                    Description = "Alpha division team",
                    PhoneNumberId = "alpha_phone_123",
                    AccessToken = "alpha_token_456",
                    BusinessAccountId = "alpha_account_789",
                    PhoneNumber = "+1234567893"
                },
                new {
                    Name = "Team Beta",
                    Description = "Beta division team",
                    PhoneNumberId = "beta_phone_123",
                    AccessToken = "beta_token_456",
                    BusinessAccountId = "beta_account_789",
                    PhoneNumber = "+1234567894"
                },
                new {
                    Name = "Team Gamma",
                    Description = "Gamma division team",
                    PhoneNumberId = "gamma_phone_123",
                    AccessToken = "gamma_token_456",
                    BusinessAccountId = "gamma_account_789",
                    PhoneNumber = "+1234567895"
                }
            };

            foreach (var teamData in teamsData)
            {
                var team = new Team
                {
                    Name = teamData.Name,
                    Description = teamData.Description,
                    WhatsAppPhoneNumberId = teamData.PhoneNumberId,
                    WhatsAppAccessToken = teamData.AccessToken,
                    WhatsAppBusinessAccountId = teamData.BusinessAccountId,
                    WhatsAppPhoneNumber = teamData.PhoneNumber,
                    ApiVersion = "18.0",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Teams.Add(team);
                logger.LogInformation("✅ Created team: {TeamName}", teamData.Name);
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureTeamConfigurationsExist(AppDbContext context, ILogger logger)
        {
            var teamsWithoutConfig = await context.Teams
                .Where(t => t.IsActive && !context.TeamConfigurations.Any(tc => tc.TeamId == t.Id))
                .ToListAsync();

            foreach (var team in teamsWithoutConfig)
            {
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
                context.TeamConfigurations.Add(teamConfig);
                logger.LogInformation("✅ Created config for team: {TeamName} (ID: {TeamId})", team.Name, team.Id);
            }

            if (teamsWithoutConfig.Any())
            {
                await context.SaveChangesAsync();
            }
        }

        private static async Task EnsureUsersExist(AppDbContext context, UserManager<ApplicationUser> userManager, ILogger logger)
        {
            // Get teams for assignments (use existing teams, not hardcoded IDs)
            var salesTeam = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Sales Team");
            var supportTeam = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Support Team");
            var operationsTeam = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Operations Team");
            var teamAlpha = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Team Alpha");
            var teamBeta = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Team Beta");
            var teamGamma = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Team Gamma");

            // SuperAdmin - NO team assignment (can access all)
            await EnsureUserExists(
                userManager,
                context,
                "superadmin@driverconnect.com",
                "SuperAdmin123!",
                "Super Administrator",
                null, // No team
                "SuperAdmin",
                new[] { "SuperAdmin", "Admin" },
                logger
            );

            // Admin - Sales Team (only if team exists)
            if (salesTeam != null)
            {
                await EnsureUserExists(
                    userManager,
                    context,
                    "admin@driverconnect.com",
                    "Admin123!",
                    "System Administrator",
                    salesTeam.Id,
                    "TeamAdmin",
                    new[] { "Admin" },
                    logger
                );
            }

            // Personal User - Operations Team (only if team exists)
            if (operationsTeam != null)
            {
                await EnsureUserExists(
                    userManager,
                    context,
                    "9850mayurshinde@gmail.com",
                    "Mayur123!",
                    "Mayur Shinde",
                    operationsTeam.Id,
                    "TeamMember",
                    new[] { "User", "Manager" },
                    logger
                );
            }

            // Regular User - Support Team (only if team exists)
            if (supportTeam != null)
            {
                await EnsureUserExists(
                    userManager,
                    context,
                    "user@driverconnect.com",
                    "User123!",
                    "Regular User",
                    supportTeam.Id,
                    "TeamMember",
                    new[] { "User" },
                    logger
                );
            }

            // Team Admins (only if teams exist)
            if (teamAlpha != null)
            {
                await EnsureUserExists(
                    userManager,
                    context,
                    "adminteamalpha@driverconnect.com",
                    "TeamAdmin123!",
                    "Team Alpha Administrator",
                    teamAlpha.Id,
                    "TeamAdmin",
                    new[] { "User", "TeamAdmin" },
                    logger
                );
            }

            if (teamBeta != null)
            {
                await EnsureUserExists(
                    userManager,
                    context,
                    "adminteambeta@driverconnect.com",
                    "TeamAdmin123!",
                    "Team Beta Administrator",
                    teamBeta.Id,
                    "TeamAdmin",
                    new[] { "User", "TeamAdmin" },
                    logger
                );
            }

            if (teamGamma != null)
            {
                await EnsureUserExists(
                    userManager,
                    context,
                    "adminteamgamma@driverconnect.com",
                    "TeamAdmin123!",
                    "Team Gamma Administrator",
                    teamGamma.Id,
                    "TeamAdmin",
                    new[] { "User", "TeamAdmin" },
                    logger
                );
            }
        }

        private static async Task EnsureUserExists(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            string email,
            string password,
            string fullName,
            int? teamId,
            string teamRole,
            string[] roles,
            ILogger logger)
        {
            var existingUser = await userManager.FindByEmailAsync(email);

            if (existingUser == null)
            {
                // Create new user
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    TeamId = teamId,
                    TeamRole = teamRole
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRolesAsync(user, roles);
                    logger.LogInformation("✅ Created user: {Email} with team {TeamId}", email, teamId);
                }
                else
                {
                    logger.LogError("❌ Failed to create user {Email}: {Errors}",
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                // User exists - DON'T overwrite their team assignment
                // Only ensure they have the correct roles
                var currentRoles = await userManager.GetRolesAsync(existingUser);
                var missingRoles = roles.Except(currentRoles).ToList();

                if (missingRoles.Any())
                {
                    await userManager.AddToRolesAsync(existingUser, missingRoles);
                    logger.LogInformation("✅ Added missing roles to {Email}: {Roles}", email, string.Join(", ", missingRoles));
                }

                // Fix null FullName if needed
                if (string.IsNullOrEmpty(existingUser.FullName) || existingUser.FullName == "Unknown User")
                {
                    existingUser.FullName = fullName;
                    await userManager.UpdateAsync(existingUser);
                    logger.LogInformation("✅ Fixed FullName for {Email}", email);
                }

                logger.LogInformation("ℹ️ User {Email} already exists - preserved existing team assignment", email);
            }
        }
    }
}