using EntitiesThread = DriverConnectApp.Domain.Entities.Thread;

namespace DriverConnectApp.API.Services
{
    public interface IThreadService
    {
        Task<List<EntitiesThread>> GetAllThreadsAsync();
        Task<EntitiesThread?> GetThreadByIdAsync(int threadId);
        Task<EntitiesThread> CreateThreadAsync(int driverId, string topic);
    }
}