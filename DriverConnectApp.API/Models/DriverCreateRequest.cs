namespace DriverConnectApp.API.Models
{
    public class DriverCreateRequest
    {
        public required string PhoneNumber { get; set; }
        public required string Name { get; set; }
        public bool IsActive { get; set; } = true;
    }
}