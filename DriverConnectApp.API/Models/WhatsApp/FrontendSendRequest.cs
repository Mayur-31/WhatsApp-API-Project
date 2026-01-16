namespace DriverConnectApp.API.Models.WhatsApp
{
    public class FrontendSendRequest
    {
        public required string PhoneNumber { get; set; }
        public required string Message { get; set; }
    }
}