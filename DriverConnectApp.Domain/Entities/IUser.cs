namespace DriverConnectApp.Domain.Entities
{
    public interface IUser
    {
        string Id { get; set; }
        string UserName { get; set; }
        string Email { get; set; }
        string FullName { get; set; }
        string? PhoneNumber { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? LastLoginAt { get; set; }
        bool IsActive { get; set; }
        int? DepartmentId { get; set; }
        int? DepotId { get; set; }
        int? DriverId { get; set; }

        Department? Department { get; set; }
        Depot? Depot { get; set; }
        Driver? Driver { get; set; }
    }
}