using DriverConnectApp.API.Models;
using DriverConnectApp.API.Models.WhatsApp;
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;

namespace DriverConnectApp.API.Services
{
    public interface IMultiTenantWhatsAppService
    {
        Task ProcessIncomingMessage(IncomingMessage incomingMessage, int teamId);
        Task<object> SendMessageAsync(SendMessageRequest request, int teamId);
        Task<bool> SendWhatsAppTextMessageAsync(string phoneNumber, string message, int teamId, bool isTemplate = false); // ✅ ADDED
        Task<bool> SendMediaMessageAsync(string to, string mediaUrl, MessageType messageType, int teamId, string? caption = null);
        Task<bool> SendLocationMessageAsync(string to, decimal latitude, decimal longitude, int teamId, string? name = null, string? address = null);
        Task ProcessWebhookAsync(string webhookData);

        // Team management
        Task<Team?> GetTeamByPhoneNumberId(string phoneNumberId);
        Task<Team?> GetTeamById(int teamId); // ✅ Make this public
        Task<List<Team>> GetAllTeams();
        Task<Team> CreateTeamAsync(CreateTeamRequest request);
        Task<Team> UpdateTeamAsync(int teamId, UpdateTeamRequest request);

        Task<bool> SendTemplateMessageAsync(string to, string templateName,
            Dictionary<string, string> templateParameters, int teamId, string? languageCode = "en_US");

        // ✅ ADDED: Group message method
        Task<bool> SendGroupMessageAsync(string groupId, string messageText, string? mediaUrl, MessageType messageType, Team team);
    }
}