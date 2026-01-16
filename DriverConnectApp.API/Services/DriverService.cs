using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Persistence;
using DriverConnectApp.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DriverConnectApp.API.Services
{
    public class DriverService : IDriverService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DriverService> _logger;

        public DriverService(AppDbContext context, ILogger<DriverService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Driver?> GetDriverByPhoneNumberAsync(string phoneNumber)
        {
            try
            {
                var normalized = NormalizePhoneNumber(phoneNumber);
                return await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting driver by phone number {PhoneNumber}", phoneNumber);
                return null;
            }
        }

        public async Task<Driver> CreateOrUpdateDriverAsync(DriverCreateRequest driverData)
        {
            try
            {
                var normalized = NormalizePhoneNumber(driverData.PhoneNumber);
                var existingDriver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalized);

                if (existingDriver != null)
                {
                    // Update existing driver
                    if (!string.IsNullOrEmpty(driverData.Name) && driverData.Name != "Unknown")
                    {
                        existingDriver.Name = driverData.Name;
                        await _context.SaveChangesAsync();
                    }
                    return existingDriver;
                }

                // Create new driver
                var newDriver = new Driver
                {
                    PhoneNumber = normalized,
                    Name = string.IsNullOrEmpty(driverData.Name) ? "Unknown" : driverData.Name,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Drivers.Add(newDriver);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new driver: {PhoneNumber} - {Name}", normalized, newDriver.Name);
                return newDriver;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating driver");
                throw;
            }
        }

        public async Task<bool> IsDriverActiveAsync(string phoneNumber)
        {
            try
            {
                var normalized = NormalizePhoneNumber(phoneNumber);
                var driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalized);

                return driver != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking driver active status for {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<List<Driver>> GetAllDriversAsync()
        {
            try
            {
                return await _context.Drivers
                    .Include(d => d.Conversations)
                    .ThenInclude(c => c.Messages)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all drivers");
                return new List<Driver>();
            }
        }

        public async Task<Driver?> GetDriverByIdAsync(int id)
        {
            try
            {
                return await _context.Drivers
                    .Include(d => d.Conversations)
                    .ThenInclude(c => c.Messages)
                    .FirstOrDefaultAsync(d => d.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting driver by ID {DriverId}", id);
                return null;
            }
        }

        public async Task<bool> DeleteDriverAsync(string phoneNumber)
        {
            try
            {
                var normalized = NormalizePhoneNumber(phoneNumber);
                var driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.PhoneNumber == normalized);

                if (driver == null)
                {
                    return false;
                }

                _context.Drivers.Remove(driver);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted driver: {PhoneNumber}", normalized);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting driver {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        private static string NormalizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return "unknown";

            var normalized = Regex.Replace(phoneNumber, @"[^\d]", "");

            if (normalized.Length == 10 && !normalized.StartsWith("91"))
            {
                normalized = "91" + normalized;
            }

            return normalized;
        }
    }
}