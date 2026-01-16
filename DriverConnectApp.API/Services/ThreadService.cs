using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EntitiesThread = DriverConnectApp.Domain.Entities.Thread;

namespace DriverConnectApp.API.Services
{
    public class ThreadService : IThreadService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ThreadService> _logger;

        public ThreadService(AppDbContext context, ILogger<ThreadService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<EntitiesThread>> GetAllThreadsAsync()
        {
            return await _context.Threads
                .Include(t => t.Conversations)
                .Include(t => t.Messages)
                .ToListAsync();
        }

        public async Task<EntitiesThread?> GetThreadByIdAsync(int id)
        {
            return await _context.Threads
                .Include(t => t.Conversations)
                .Include(t => t.Messages)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<EntitiesThread> CreateThreadAsync(int driverId, string topic)
        {
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                throw new InvalidOperationException($"Driver with ID {driverId} not found.");
            }

            var thread = new EntitiesThread
            {
                Topic = topic,
                CreatedAt = DateTime.UtcNow
            };

            _context.Threads.Add(thread);
            await _context.SaveChangesAsync();
            return thread;
        }
    }
}