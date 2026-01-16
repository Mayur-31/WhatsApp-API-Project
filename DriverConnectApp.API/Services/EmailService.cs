using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace DriverConnectApp.API.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmail(string email, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendPasswordResetEmail(string email, string resetLink)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var host = smtpSettings["Host"] ?? "smtp.gmail.com"; // Default to Gmail if not set
                var port = int.Parse(smtpSettings["Port"] ?? "587");
                var username = smtpSettings["Username"];
                var password = smtpSettings["Password"];
                var from = smtpSettings["From"] ?? "no-reply@driverconnectapp.com";

                // Log loaded SMTP settings for debugging
                _logger.LogInformation("SMTP Settings - Host: {Host}, Port: {Port}, Username: {Username}, From: {From}", host, port, username, from);

                if (string.IsNullOrEmpty(host) || (host == "smtp.gmail.com" && (string.IsNullOrEmpty(username) || username == "your-email@gmail.com")))
                {
                    _logger.LogInformation("Password Reset Link for {Email}: {ResetLink}", email, resetLink);
                    Console.WriteLine($"Password Reset Link for {email}: {resetLink}");
                    return;
                }

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(from),
                    Subject = "Password Reset - DriverConnect",
                    Body = $@"
                        <h3>Password Reset Request</h3>
                        <p>You requested to reset your password for your DriverConnect account.</p>
                        <p>Click <a href='{resetLink}'>here</a> to reset your password.</p>
                        <p>This link allows you to set a new password. It is valid for 24 hours.</p>
                        <p>If you didn't request this, please ignore this email.</p>
                        <br><p>Best regards,<br>DriverConnect Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Password reset email sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}. Reset link: {ResetLink}", email, resetLink);
                Console.WriteLine($"Password Reset Link for {email}: {resetLink} (Failed to send: {ex.Message})");
            }
        }
    }
}