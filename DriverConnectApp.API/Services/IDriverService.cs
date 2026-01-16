// IDriverService.cs
using DriverConnectApp.Domain.Entities;
using DriverConnectApp.API.Models;

namespace DriverConnectApp.API.Services
{
    public interface IDriverService
    {
        Task<Driver?> GetDriverByPhoneNumberAsync(string phoneNumber);
        Task<Driver> CreateOrUpdateDriverAsync(DriverCreateRequest driverData);
        Task<bool> IsDriverActiveAsync(string phoneNumber);
        Task<List<Driver>> GetAllDriversAsync();
        Task<Driver?> GetDriverByIdAsync(int id);
        Task<bool> DeleteDriverAsync(string phoneNumber);
    }
}