using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.Domain.Enums;

namespace DriverConnectApp.API.Services
{
    public interface IWhatsAppService
    {
        Task ProcessIncomingMessage(IncomingMessage incomingMessage);
        Task<object> SendMessageAsync(SendMessageRequest request);
        Task SendWhatsAppMessageAsync(string phoneNumber, string message, bool isTemplate, MessageContext? context);
        Task<bool> SendMediaMessageAsync(string to, string mediaUrl, MessageType messageType, string? caption = null);
        Task<bool> SendLocationMessageAsync(string to, decimal latitude, decimal longitude, string? name = null, string? address = null);
        Task ProcessWebhookAsync(string webhookData);
    }
}