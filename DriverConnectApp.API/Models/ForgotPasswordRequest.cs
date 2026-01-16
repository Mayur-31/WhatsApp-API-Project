using System.ComponentModel.DataAnnotations;

namespace DriverConnectApp.API.Models
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}