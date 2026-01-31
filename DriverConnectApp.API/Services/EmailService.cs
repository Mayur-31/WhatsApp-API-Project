using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DriverConnectApp.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken)
        {
            // Get base URL from configuration or use default
            var baseUrl = _configuration["BaseUrl"]?.TrimEnd('/') ?? "https://onestopvan.work.gd";

            // IMPORTANT: Use Uri.EscapeDataString to properly encode the token
            // This ensures special characters in the token are URL-safe
            var encodedToken = Uri.EscapeDataString(resetToken);
            var encodedEmail = Uri.EscapeDataString(toEmail);

            // Create reset link
            var resetLink = $"{baseUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

            var subject = "Reset Your Password - DriverConnect";

            var body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <style>
                        body {{ margin: 0; padding: 20px; font-family: Arial, sans-serif; background: #f9fafb; }}
                        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
                        .header {{ background: linear-gradient(to right, #4F46E5, #7C3AED); padding: 30px; text-align: center; color: white; }}
                        .content {{ padding: 30px; }}
                        .button {{ display: inline-block; background: #4F46E5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; }}
                        .warning {{ background: #fef3c7; border-left: 4px solid #f59e0b; padding: 12px; margin: 20px 0; color: #92400e; }}
                        .link-box {{ background: #f9fafb; padding: 12px; border-radius: 6px; border-left: 4px solid #4F46E5; word-break: break-all; font-size: 14px; margin: 20px 0; }}
                        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #e5e7eb; color: #6b7280; font-size: 14px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1 style='margin: 0; font-size: 24px;'>Reset Your Password</h1>
                        </div>
                        <div class='content'>
                            <p>Hello,</p>
                            <p>You requested to reset your password for your DriverConnect account.</p>
                            
                            <p>Click the button below to reset your password:</p>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{resetLink}' class='button'>Reset Password</a>
                            </div>
                            
                            <p>Or copy and paste this link into your browser:</p>
                            <div class='link-box'>{resetLink}</div>
                            
                            <div class='warning'>
                                <strong>⚠️ Important:</strong> This link will expire in 24 hours.
                            </div>
                            
                            <p>If you didn't request this password reset, please ignore this email.</p>
                            
                            <div class='footer'>
                                <p>Best regards,<br/><strong>DriverConnect Team</strong></p>
                                <p style='font-size: 12px; margin-top: 20px;'>
                                    © {DateTime.UtcNow.Year} DriverConnect. This is an automated email, please do not reply.
                                </p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpHost = _configuration["SmtpSettings:Host"];
                var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "587");
                var smtpUsername = _configuration["SmtpSettings:Username"];
                var smtpPassword = _configuration["SmtpSettings:Password"];
                var smtpFrom = _configuration["SmtpSettings:From"] ?? smtpUsername;
                var enableSsl = bool.Parse(_configuration["SmtpSettings:EnableSsl"] ?? "true");

                // Log configuration (hide password)
                _logger.LogInformation("SMTP Config: Host={Host}, Port={Port}, From={From}",
                    smtpHost, smtpPort, smtpFrom);

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    throw new InvalidOperationException("SMTP configuration is incomplete. Check appsettings.json.");
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    Timeout = 30000 // 30 seconds
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpFrom, "DriverConnect"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                _logger.LogInformation("Sending email to {ToEmail}", toEmail);

                await client.SendMailAsync(mailMessage);

                _logger.LogInformation("✅ Email sent successfully to {ToEmail}", toEmail);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error sending to {ToEmail}: {Message}", toEmail, ex.Message);
                throw new Exception($"Failed to send email: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {ToEmail}", toEmail);
                throw;
            }
        }
    }
}