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
            var resetLink = $"https://onestopvan.work.gd/reset-password?token={WebUtility.UrlEncode(resetToken)}&email={WebUtility.UrlEncode(toEmail)}";

            var subject = "Password Reset Request - DriverConnect";
            var body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .container {{ background: #f9f9f9; border: 1px solid #ddd; border-radius: 5px; padding: 30px; }}
                        .header {{ background: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .button {{ display: inline-block; background: #4CAF50; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; font-weight: bold; margin: 20px 0; }}
                        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Password Reset</h1>
                        </div>
                        <h2>Hello,</h2>
                        <p>You have requested to reset your password for your DriverConnect account.</p>
                        <p><strong>Click the button below to reset your password:</strong></p>
                        <p>
                            <a href='{resetLink}' class='button'>Reset Password</a>
                        </p>
                        <p>Or copy and paste this link into your browser:</p>
                        <p><code style='background: #f4f4f4; padding: 10px; display: block; word-break: break-all;'>{resetLink}</code></p>
                        <p><strong>This link will expire in 24 hours.</strong></p>
                        <p>If you did not request a password reset, please ignore this email.</p>
                        <div class='footer'>
                            <p>Best regards,<br/><strong>DriverConnect Team</strong></p>
                        </div>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpHost = _configuration["SmtpSettings:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "587");
                var smtpUsername = _configuration["SmtpSettings:Username"];
                var smtpPassword = _configuration["SmtpSettings:Password"];
                var smtpFrom = _configuration["SmtpSettings:From"] ?? smtpUsername;
                var enableSsl = bool.Parse(_configuration["SmtpSettings:EnableSsl"] ?? "true");

                // Clean password (remove spaces if any)
                if (!string.IsNullOrEmpty(smtpPassword))
                {
                    smtpPassword = smtpPassword.Replace(" ", "");
                }

                _logger.LogInformation("📧 Sending email to {ToEmail} via {Host}:{Port}", toEmail, smtpHost, smtpPort);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpFrom!),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    Priority = MailPriority.High
                };

                mailMessage.To.Add(toEmail);

                // Add headers for better deliverability
                mailMessage.Headers.Add("X-Priority", "1");
                mailMessage.Headers.Add("X-MSMail-Priority", "High");

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("✅ Email sent successfully to {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send email to {ToEmail}", toEmail);
                throw new Exception($"Failed to send email: {ex.Message}", ex);
            }
        }
    }
}