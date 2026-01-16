using DriverConnectApp.API.Models;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverConnectApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DepotsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DepotsController> _logger;

        public DepotsController(AppDbContext context, ILogger<DepotsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DepotDto>>> GetDepots()
        {
            try
            {
                var depots = await _context.Depots
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var depotDtos = depots.Select(d => new DepotDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Location = d.Location,
                    City = d.City,
                    Address = d.Address,
                    PostalCode = d.PostalCode,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt
                }).ToList();

                _logger.LogInformation("Loaded {DepotCount} depots", depotDtos.Count);
                return Ok(depotDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading depots");
                return StatusCode(500, new { message = "Failed to load depots" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<DepotDto>> CreateDepot([FromBody] CreateDepotRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "Depot name is required" });

                // Check if depot with same name already exists
                var existingDepot = await _context.Depots
                    .FirstOrDefaultAsync(d => d.Name.ToLower() == request.Name.Trim().ToLower());

                if (existingDepot != null)
                    return BadRequest(new { message = "Depot with this name already exists" });

                var depot = new Depot
                {
                    Name = request.Name.Trim(),
                    Location = request.Location?.Trim(),
                    City = request.City?.Trim(),
                    Address = request.Address?.Trim(),
                    PostalCode = request.PostalCode?.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Depots.Add(depot);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created depot {DepotId} with name {DepotName}", depot.Id, depot.Name);

                // Return the complete list of depots after creation
                var depots = await _context.Depots
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var depotDtos = depots.Select(d => new DepotDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Location = d.Location,
                    City = d.City,
                    Address = d.Address,
                    PostalCode = d.PostalCode,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt
                }).ToList();

                return Ok(depotDtos);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating depot");
                return StatusCode(500, new { message = "Database error while creating depot" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating depot");
                return StatusCode(500, new { message = "Failed to create depot" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DepotDto>> UpdateDepot(int id, [FromBody] UpdateDepotRequest request)
        {
            try
            {
                var depot = await _context.Depots.FindAsync(id);
                if (depot == null)
                    return NotFound(new { message = "Depot not found" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "Depot name is required" });

                // Check if another depot with same name already exists
                var existingDepot = await _context.Depots
                    .FirstOrDefaultAsync(d => d.Name.ToLower() == request.Name.Trim().ToLower() && d.Id != id);

                if (existingDepot != null)
                    return BadRequest(new { message = "Another depot with this name already exists" });

                depot.Name = request.Name.Trim();
                depot.Location = request.Location?.Trim();
                depot.City = request.City?.Trim();
                depot.Address = request.Address?.Trim();
                depot.PostalCode = request.PostalCode?.Trim();
                depot.IsActive = request.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated depot {DepotId}", id);

                // Return the complete list of depots after update
                var depots = await _context.Depots
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var depotDtos = depots.Select(d => new DepotDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Location = d.Location,
                    City = d.City,
                    Address = d.Address,
                    PostalCode = d.PostalCode,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt
                }).ToList();

                return Ok(depotDtos);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating depot {DepotId}", id);
                return StatusCode(500, new { message = "Database error while updating depot" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating depot {DepotId}", id);
                return StatusCode(500, new { message = "Failed to update depot" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDepot(int id)
        {
            try
            {
                var depot = await _context.Depots.FindAsync(id);
                if (depot == null)
                    return NotFound(new { message = "Depot not found" });

                // Check if depot is being used by any users
                var usersInDepot = await _context.Users
                    .AnyAsync(u => u.DepotId == id);

                if (usersInDepot)
                {
                    return BadRequest(new { message = "Cannot delete depot because it is assigned to users" });
                }

                // Check if depot is being used by any drivers
                var driversInDepot = await _context.Drivers
                    .AnyAsync(d => d.DepotId == id);

                if (driversInDepot)
                {
                    return BadRequest(new { message = "Cannot delete depot because it is assigned to drivers" });
                }

                // If no dependencies, delete the depot
                _context.Depots.Remove(depot);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted depot {DepotId}", id);
                return Ok(new { message = "Depot deleted successfully" });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting depot {DepotId}", id);
                return StatusCode(500, new { message = "Database error while deleting depot" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting depot {DepotId}", id);
                return StatusCode(500, new { message = "Failed to delete depot" });
            }
        }
    }

    public class CreateDepotRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
    }

    public class UpdateDepotRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
        public bool IsActive { get; set; }
    }
}