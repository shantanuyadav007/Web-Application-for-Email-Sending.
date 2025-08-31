using Microsoft.AspNetCore.Mvc;
using EmailVerificationAPI.Services;
using EmailVerificationAPI.Models;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EmailVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(EmailService emailService, ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("send")]
        [Authorize]
        public async Task<IActionResult> SendEmail([FromForm] EmailRequest request)
        {
            try
            {
                var toList = string.Join(",", request?.Emails ?? Enumerable.Empty<string>());
                _logger.LogInformation("Sending email to {ToList}", toList);

                if (request == null)
                {
                    _logger.LogWarning("Email request is null.");
                    return BadRequest("Email request cannot be null.");
                }

                await _emailService.SendEmailAsync(request);
                _logger.LogInformation("Email sent successfully to {ToList}", toList);
                return Ok("Email sent successfully.");
            }
            catch (Exception ex)
            {
                var toList = string.Join(",", request?.Emails ?? Enumerable.Empty<string>());
                _logger.LogError("Failed to send email to {ToList}: {ErrorMessage}", toList, ex.Message);
                return BadRequest($"Email is not sent: {ex.Message}");
            }
        }
    }
}