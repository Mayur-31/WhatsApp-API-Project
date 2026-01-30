using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DriverConnectApp.Infrastructure.Persistence;
using DriverConnectApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DriverConnectApp.Domain.Enums;

namespace DriverConnectApp.API.Services
{
    public class WhatsAppBackgroundService : BackgroundService
    {
        private readonly ILogger<WhatsAppBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageQueueService _messageQueue;

        public WhatsAppBackgroundService(
            ILogger<WhatsAppBackgroundService> logger,
            IServiceProvider serviceProvider,
            IMessageQueueService messageQueue)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _messageQueue = messageQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 WhatsApp Background Service started");

            // ✅ Process queued messages
            var processTask = ProcessQueuedMessagesAsync(stoppingToken);
            
            // ✅ Bonus: Check retry messages
            var retryTask = CheckRetryMessagesAsync(stoppingToken);
            
            await Task.WhenAll(processTask, retryTask);

            _logger.LogInformation("🛑 WhatsApp Background Service stopped");
        }

        private async Task ProcessQueuedMessagesAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // ✅ Dequeue message
                    var queueItem = await _messageQueue.DequeueMessageAsync(stoppingToken);

                    if (queueItem == null)
                        continue;

                    var (messageId, teamId) = queueItem.Value;

                    // ✅ Process in scoped context
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var whatsAppService = scope.ServiceProvider
                            .GetRequiredService<IMultiTenantWhatsAppService>();

                        await whatsAppService.ProcessQueuedMessageAsync(messageId, teamId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing message {MsgId}", messageId);
                        // Message status updated in service, will retry if needed
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in background processing loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        // ✅ BONUS: Periodic retry checker for failed messages
        private async Task CheckRetryMessagesAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider
                        .GetRequiredService<AppDbContext>();

                    // FIXED: Use the Message.TeamId property directly since we added it to the Message entity
                    var retryMessages = await context.Messages
                        .Where(m => m.Status == MessageStatus.Queued &&
                                   m.NextRetryAt <= DateTime.UtcNow &&
                                   m.RetryCount < 3)
                        .Select(m => new { 
                            m.Id, 
                            m.TeamId // Use the TeamId from Message entity
                        })
                        .ToListAsync(stoppingToken);

                    foreach (var msg in retryMessages)
                    {
                        // FIXED: TeamId is now non-nullable int from Message entity
                        await _messageQueue.EnqueueMessageAsync(msg.Id, msg.TeamId);
                    }

                    if (retryMessages.Any())
                    {
                        _logger.LogInformation("🔄 Re-queued {Count} messages for retry",
                            retryMessages.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in retry checker");
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("⏸️ WhatsApp Background Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}