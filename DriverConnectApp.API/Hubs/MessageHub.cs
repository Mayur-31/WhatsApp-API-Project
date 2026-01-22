using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using DriverConnectApp.Infrastructure.Persistence;
namespace DriverConnectApp.API.Hubs
{
    public class MessageHub : Hub
    {
        private readonly ILogger<MessageHub> _logger;
        private readonly AppDbContext _context;
        
        public MessageHub(ILogger<MessageHub> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            }
            await base.OnConnectedAsync();
        }
        
        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        }
        
        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        }
        
        // Send message to specific conversation
        public async Task SendToConversation(int conversationId, string method, object data)
        {
            await Clients.Group($"conversation-{conversationId}").SendAsync(method, data);
        }
        
        // Send to specific user
        public async Task SendToUser(string userId, string method, object data)
        {
            await Clients.Group($"user-{userId}").SendAsync(method, data);
        }
    }
}

// Add this for user mapping
public class NameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}