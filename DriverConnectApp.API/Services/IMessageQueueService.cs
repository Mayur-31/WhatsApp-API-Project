namespace DriverConnectApp.API.Services
{
    public interface IMessageQueueService
    {
        Task EnqueueMessageAsync(int messageId, int teamId);
        Task<(int MessageId, int TeamId)?> DequeueMessageAsync(CancellationToken cancellationToken);
    }
}