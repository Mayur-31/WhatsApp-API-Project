using DriverConnectApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DriverConnectApp.Infrastructure.Persistence
{
    public class Repository<T> where T : class
    {
        private readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T?> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<Driver?> GetDriverByPhoneNumberAsync(string phoneNumber)
        {
            return await _context.Drivers
                .Include(d => d.Conversations)
                .FirstOrDefaultAsync(d => d.PhoneNumber == phoneNumber);
        }

        public async Task<List<Message>> GetRecentMessagesAsync(int limit = 50)
        {
            return await _context.Messages
                .OrderByDescending(m => m.SentAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Conversation>> GetActiveConversationsAsync()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);

            // Get conversation IDs that have recent messages
            var activeConversationIds = await _context.Messages
                .Where(m => m.SentAt > cutoffTime)
                .Select(m => m.ConversationId)
                .Distinct()
                .ToListAsync();

            // Get the conversations with their drivers
            return await _context.Conversations
                .Where(c => activeConversationIds.Contains(c.Id))
                .ToListAsync();
        }

        public async Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId)
        {
            return await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<Driver?> GetDriverByIdAsync(int driverId)
        {
            return await _context.Drivers
                .Include(d => d.Conversations)
                .FirstOrDefaultAsync(d => d.Id == driverId);
        }

        public async Task<Conversation?> GetConversationByIdAsync(int conversationId)
        {
            return await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);
        }

        public async Task<List<DriverConnectApp.Domain.Entities.Thread>> GetAllThreadsAsync()
        {
            return await _context.Threads.ToListAsync();
        }

        public async Task<DriverConnectApp.Domain.Entities.Thread?> GetThreadByIdAsync(int threadId)
        {
            return await _context.Threads
                .FirstOrDefaultAsync(t => t.Id == threadId);
        }
    }
}