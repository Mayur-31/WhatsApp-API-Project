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
    public class DepartmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DepartmentsController> _logger;

        public DepartmentsController(AppDbContext context, ILogger<DepartmentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetDepartments()
        {
            try
            {
                var departments = await _context.Departments
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var departmentDtos = departments.Select(d => new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt
                }).ToList();

                _logger.LogInformation("Loaded {DepartmentCount} departments", departmentDtos.Count);
                return Ok(departmentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments");
                return StatusCode(500, new { message = "Failed to load departments" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<DepartmentDto>> CreateDepartment([FromBody] CreateDepartmentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "Department name is required" });

                // Check if department with same name already exists
                var existingDepartment = await _context.Departments
                    .FirstOrDefaultAsync(d => d.Name.ToLower() == request.Name.Trim().ToLower());

                if (existingDepartment != null)
                    return BadRequest(new { message = "Department with this name already exists" });

                var department = new Department
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created department {DepartmentId} with name {DepartmentName}", department.Id, department.Name);

                // Return the complete list of departments after creation
                var departments = await _context.Departments
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var departmentDtos = departments.Select(d => new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt
                }).ToList();

                return Ok(departmentDtos);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating department");
                return StatusCode(500, new { message = "Database error while creating department" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department");
                return StatusCode(500, new { message = "Failed to create department" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DepartmentDto>> UpdateDepartment(int id, [FromBody] UpdateDepartmentRequest request)
        {
            try
            {
                var department = await _context.Departments.FindAsync(id);
                if (department == null)
                    return NotFound(new { message = "Department not found" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "Department name is required" });

                // Check if another department with same name already exists
                var existingDepartment = await _context.Departments
                    .FirstOrDefaultAsync(d => d.Name.ToLower() == request.Name.Trim().ToLower() && d.Id != id);

                if (existingDepartment != null)
                    return BadRequest(new { message = "Another department with this name already exists" });

                department.Name = request.Name.Trim();
                department.Description = request.Description?.Trim();
                department.IsActive = request.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated department {DepartmentId}", id);

                // Return the complete list of departments after update
                var departments = await _context.Departments
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var departmentDtos = departments.Select(d => new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt
                }).ToList();

                return Ok(departmentDtos);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating department {DepartmentId}", id);
                return StatusCode(500, new { message = "Database error while updating department" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating department {DepartmentId}", id);
                return StatusCode(500, new { message = "Failed to update department" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            try
            {
                var department = await _context.Departments.FindAsync(id);
                if (department == null)
                    return NotFound(new { message = "Department not found" });

                // Check if department is being used by any users
                var usersInDepartment = await _context.Users
                    .AnyAsync(u => u.DepartmentId == id);

                if (usersInDepartment)
                {
                    return BadRequest(new { message = "Cannot delete department because it is assigned to users" });
                }

                // Check if department is being used by any conversations
                var conversationsInDepartment = await _context.Conversations
                    .AnyAsync(c => c.DepartmentId == id);

                if (conversationsInDepartment)
                {
                    return BadRequest(new { message = "Cannot delete department because it is assigned to conversations" });
                }

                // If no dependencies, delete the department
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted department {DepartmentId}", id);

                // Return success message
                return Ok(new { message = "Department deleted successfully" });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting department {DepartmentId}", id);
                return StatusCode(500, new { message = "Database error while deleting department" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department {DepartmentId}", id);
                return StatusCode(500, new { message = "Failed to delete department" });
            }
        }
    }

    public class CreateDepartmentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateDepartmentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}