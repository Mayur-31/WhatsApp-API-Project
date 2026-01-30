using System.Threading.Channels;

namespace DriverConnectApp.API.Services
{
    public class MessageQueueService : IMessageQueueService
    {
        private readonly Channel<(int MessageId, int TeamId)> _queue;
        private readonly ILogger<MessageQueueService> _logger;

        public MessageQueueService(ILogger<MessageQueueService> logger)
        {
            _logger = logger;

            // ✅ Bounded channel with backpressure
            var options = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false, // Multiple workers can read
                SingleWriter = false  // Multiple controllers can write
            };

            _queue = Channel.CreateBounded<(int, int)>(options);
        }

        public async Task EnqueueMessageAsync(int messageId, int teamId)
        {
            try
            {
                await _queue.Writer.WriteAsync((messageId, teamId));
                _logger.LogDebug("📥 Message {MsgId} queued", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to enqueue message {MsgId}", messageId);
                throw;
            }
        }

        public async Task<(int MessageId, int TeamId)?> DequeueMessageAsync(CancellationToken cancellationToken)
        {
            try
            {
                var item = await _queue.Reader.ReadAsync(cancellationToken);
                return item;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error dequeuing message");
                return null;
            }
        }
    }
}