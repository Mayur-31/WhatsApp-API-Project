using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverConnectApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TestController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult TestApi()
        {
            return Ok(new { message = "API is working!", timestamp = DateTime.UtcNow });
        }

        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
                var pendingMigrations = _context.Database.GetPendingMigrations();

                var driverCount = 0;
                var messageCount = 0;
                var conversationCount = 0;

                try
                {
                    driverCount = await _context.Drivers.CountAsync();
                    messageCount = await _context.Messages.CountAsync();
                    conversationCount = await _context.Conversations.CountAsync();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        status = "Database Schema Issue",
                        database = canConnect ? "Connected" : "Disconnected",
                        appliedMigrations,
                        pendingMigrations,
                        schemaError = ex.Message,
                        version = "1.0.0"
                    });
                }

                return Ok(new
                {
                    status = "Healthy",
                    database = canConnect ? "Connected" : "Disconnected",
                    counts = new { drivers = driverCount, messages = messageCount, conversations = conversationCount },
                    migrations = new { applied = appliedMigrations, pending = pendingMigrations },
                    version = "1.0.0"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Unhealthy",
                    error = ex.Message,
                    version = "1.0.0"
                });
            }
        }

        [HttpGet("schema")]
        public async Task<IActionResult> CheckSchema()
        {
            try
            {
                var driversTableInfo = new List<object>();
                var messagesTableInfo = new List<object>();

                using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var driversCommand = connection.CreateCommand();
                driversCommand.CommandText = "PRAGMA table_info(Drivers);";
                using var driversReader = await driversCommand.ExecuteReaderAsync();
                while (await driversReader.ReadAsync())
                {
                    driversTableInfo.Add(new
                    {
                        cid = driversReader.GetInt32(0),
                        name = driversReader.GetString(1),
                        type = driversReader.GetString(2),
                        notnull = driversReader.GetBoolean(3),
                        dflt_value = driversReader.IsDBNull(4) ? null : driversReader.GetValue(4),
                        pk = driversReader.GetBoolean(5)
                    });
                }

                using var messagesCommand = connection.CreateCommand();
                messagesCommand.CommandText = "PRAGMA table_info(Messages);";
                using var messagesReader = await messagesCommand.ExecuteReaderAsync();
                while (await messagesReader.ReadAsync())
                {
                    messagesTableInfo.Add(new
                    {
                        cid = messagesReader.GetInt32(0),
                        name = messagesReader.GetString(1),
                        type = messagesReader.GetString(2),
                        notnull = messagesReader.GetBoolean(3),
                        dflt_value = messagesReader.IsDBNull(4) ? null : messagesReader.GetValue(4),
                        pk = messagesReader.GetBoolean(5)
                    });
                }

                return Ok(new
                {
                    drivers = new
                    {
                        hasCreatedAt = driversTableInfo.Any(c => ((dynamic)c).name == "CreatedAt"),
                        columns = driversTableInfo
                    },
                    messages = new
                    {
                        hasSentAt = messagesTableInfo.Any(c => ((dynamic)c).name == "SentAt"),
                        columns = messagesTableInfo
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
    }
}