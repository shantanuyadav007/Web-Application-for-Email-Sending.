using Microsoft.EntityFrameworkCore;
using EmailVerificationAPI.Data;
using EmailVerificationAPI.Models;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EmailVerificationAPI.Services
{
    public class EmailService
    {
        private readonly AppDbContext _context;
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly List<string> _restrictedDomains = new List<string> { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com" };
        private const long MaxAttachmentSize = 10 * 1024 * 1024; // 10 MB in bytes

        public EmailService(AppDbContext context, IOptions<SmtpSettings> smtpOptions, ILogger<EmailService> logger)
        {
            _context = context;
            _smtpSettings = smtpOptions.Value;
            _logger = logger;
        }

        public async Task<string> SendEmailAsync(EmailRequest request)
        {
            string attachmentLink = "";
            string status = "Sent";
            try
            {
                if (request == null)
                {
                    _logger.LogError("Email request is null.");
                    throw new ArgumentNullException(nameof(request));
                }

                var toList = string.Join(",", request.Emails ?? Enumerable.Empty<string>());
                _logger.LogInformation("Sending email to {ToList}", toList);

                // Validate restricted domains
                var allEmails = (request.Emails ?? new List<string>())
                    .Concat(request.Ccs ?? new List<string>())
                    .Concat(request.Bccs ?? new List<string>())
                    .ToList();
                foreach (var email in allEmails)
                {
                    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    {
                        _logger.LogWarning("Invalid email address: {Email}", email);
                        throw new ArgumentException("Invalid email address provided.");
                    }
                    var domain = email.Split('@').Last().ToLower();
                    if (_restrictedDomains.Contains(domain))
                    {
                        _logger.LogWarning("Restricted domain detected: {Domain} for email {Email}", domain, email);
                        throw new ArgumentException("Not allowed to send emails to restricted email ID. Please recheck and try later.");
                    }
                }

                // Validate attachment size
                long totalSize = 0;
                foreach (var file in request.Files ?? new List<IFormFile>())
                {
                    totalSize += file.Length;
                    if (totalSize > MaxAttachmentSize)
                    {
                        _logger.LogWarning("Attachment size exceeds 10 MB limit");
                        throw new ArgumentException("Attachment size exceeds 10 MB limit. Please recheck and try again.");
                    }
                }

                // Upload attachments and get link
                if (request.Files != null && request.Files.Any())
                {
                    _logger.LogInformation("Uploading attachments");
                    attachmentLink = await UploadAttachmentsToStorage(request.Files);
                    _logger.LogInformation("Attachments uploaded successfully");
                }

                // Build email message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Data Nova ", _smtpSettings.SMTPFrom));

                if (request.Emails != null)
                {
                    foreach (var email in request.Emails)
                    {
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            message.To.Add(MailboxAddress.Parse(email));
                        }
                    }
                }

                if (request.Ccs != null)
                {
                    foreach (var cc in request.Ccs)
                    {
                        if (!string.IsNullOrWhiteSpace(cc))
                        {
                            message.Cc.Add(MailboxAddress.Parse(cc));
                        }
                    }
                }

                if (request.Bccs != null)
                {
                    foreach (var bcc in request.Bccs)
                    {
                        if (!string.IsNullOrWhiteSpace(bcc))
                        {
                            message.Bcc.Add(MailboxAddress.Parse(bcc));
                        }
                    }
                }

                message.Subject = request.Subject;

                var builder = new BodyBuilder { TextBody = request.Msg };

                if (request.Files != null)
                {
                    foreach (var file in request.Files)
                    {
                        using var stream = new MemoryStream();
                        await file.CopyToAsync(stream);
                        builder.Attachments.Add(file.FileName, stream.ToArray(), ContentType.Parse(file.ContentType));
                    }
                }

                message.Body = builder.ToMessageBody();

                // Send email
                using var client = new SmtpClient();
                var secureSocket = _smtpSettings.UseSSL.ToLower() switch
                {
                    "ssl" => SecureSocketOptions.SslOnConnect,
                    "starttls" => SecureSocketOptions.StartTls,
                    _ => SecureSocketOptions.Auto
                };

                await client.ConnectAsync(_smtpSettings.SMTPHost, _smtpSettings.SMTPPort, secureSocket);
                await client.AuthenticateAsync(_smtpSettings.SMTPUser, _smtpSettings.SMTPPwd);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {ToList}", toList);
            }
            catch (Exception ex)
            {
                status = $"Failed: {ex.Message}";
                var toList = string.Join(",", request?.Emails ?? Enumerable.Empty<string>());
                _logger.LogError("Failed to send email to {ToList}: {ErrorMessage}", toList, ex.Message);
                throw; // Re-throw to let controller handle response
            }
            finally
            {
                // Log email to database
                try
                {
                    if (request == null)
                    {
                        _logger.LogError("Email request is null.");
                        throw new ArgumentNullException(nameof(request));
                    }

                    var toList = string.Join(",", request.Emails ?? Enumerable.Empty<string>());
                    _logger.LogInformation("Logging email to database for {ToList}", toList);
                    var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
                    var log = new EmailLog
                    {
                        ToListCsv = string.Join(",", request.Emails ?? Enumerable.Empty<string>()),
                        CcListCsv = string.Join(",", request.Ccs ?? Enumerable.Empty<string>()),
                        BccListCsv = string.Join(",", request.Bccs ?? Enumerable.Empty<string>()),
                        Subject = request.Subject ?? string.Empty,
                        Message = request.Msg ?? string.Empty,
                        SentAt = istNow,
                        Status = status,
                        AttachmentLink = string.IsNullOrEmpty(attachmentLink) ? null : attachmentLink
                    };
                    _context.EmailLogs.Add(log);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Email logged successfully to database for {ToList}", toList);
                }
                catch (Exception ex)
                {
                    var toList = string.Join(",", request?.Emails ?? Enumerable.Empty<string>());
                    _logger.LogError("Failed to log email to database for {ToList}: {ErrorMessage}", toList, ex.Message);
                }
            }

            return attachmentLink;
        }

        private async Task<string> UploadAttachmentsToStorage(IEnumerable<IFormFile> files)
        {
            try
            {
                // Ensure the uploads directory exists
                var uploadDir = Path.Combine("wwwroot", "Uploads");
                Directory.CreateDirectory(uploadDir); // Creates directory if it doesn't exist

                var attachmentLinks = new List<string>();
                foreach (var file in files)
                {
                    if (file == null || file.Length == 0)
                    {
                        _logger.LogWarning("Skipping invalid file during upload");
                        continue; 
                    }

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadDir, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    // Use lowercase 'uploads' in the URL to match the directory
                    attachmentLinks.Add($"/uploads/{fileName}");
                }

                return string.Join(",", attachmentLinks);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to upload attachments: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Failed to upload attachments: {ex.Message}", ex);
            }
        }
    }
}