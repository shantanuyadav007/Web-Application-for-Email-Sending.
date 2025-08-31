using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmailVerificationAPI.Models;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using BCrypt.Net;
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace EmailSendingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly SmtpSettings _smtpSettings;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration configuration, IOptions<SmtpSettings> smtpOptions, ILogger<AuthController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _smtpSettings = smtpOptions.Value;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password) || string.IsNullOrWhiteSpace(model.ConfirmPassword))
                return BadRequest(new { message = "All fields are required." });

            if (model.Password != model.ConfirmPassword)
                return BadRequest(new { message = "Passwords do not match." });

            var otp = new Random().Next(100000, 999999).ToString();

            SaveOtp(model.Email, otp, "registration");

            _logger.LogInformation("Sending registration OTP to {Email}", model.Email);
            if (!await SendOtpEmail(model.Email, otp, "Your OTP for registration"))
            {
                _logger.LogError("Failed to send registration OTP to {Email}", model.Email);
                return StatusCode(500, new { message = "Failed to send OTP email." });
            }

            _logger.LogInformation("Registration OTP sent to {Email}", model.Email);
            return Ok(new { message = "OTP sent to your email." });
        }

        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] OtpVerificationDto model)
        {
            _logger.LogInformation("Verifying OTP for {Email}", model.Email);
            if (!ValidateOtp(model.Email, model.OTP, "registration"))
            {
                _logger.LogWarning("Invalid or expired OTP for {Email}", model.Email);
                return BadRequest(new { message = "Invalid or expired OTP." });
            }

            if (model.Password != model.ConfirmPassword)
            {
                _logger.LogWarning("Passwords do not match for {Email}", model.Email);
                return BadRequest(new { message = "Passwords do not match." });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", conn))
            {
                checkCmd.Parameters.AddWithValue("@Email", model.Email);
                int count = (int)checkCmd.ExecuteScalar();
                if (count > 0)
                {
                    _logger.LogWarning("User already exists: {Email}", model.Email);
                    return BadRequest(new { message = "User already exists." });
                }
            }

            var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);

            using var cmd = new SqlCommand(
                "INSERT INTO Users (Email, PasswordHash, IsVerified, CreatedAt, Name) VALUES (@Email, @PasswordHash, @IsVerified, @CreatedAt, @Name)", conn);
            cmd.Parameters.AddWithValue("@Email", model.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
            cmd.Parameters.AddWithValue("@IsVerified", true);
            cmd.Parameters.AddWithValue("@CreatedAt", istNow);
            cmd.Parameters.AddWithValue("@Name", (object)model.Name ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            
            _logger.LogInformation("Registration successful for {Email}", model.Email);
            return Ok(new { message = "Registration successful." });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                _logger.LogWarning("Login failed: Email or password missing");
                return BadRequest(new { message = "Email and password are required." });
            }

            _logger.LogInformation("Login attempt for {Email}", model.Email);
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT PasswordHash FROM Users WHERE Email = @Email AND IsVerified = 1", conn);
            cmd.Parameters.AddWithValue("@Email", model.Email);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                _logger.LogWarning("Login failed for {Email}: Invalid credentials or user not verified", model.Email);
                return BadRequest(new { message = "Invalid credentials or user not verified." });
            }

            var storedHash = reader.GetString(0);
            if (!BCrypt.Net.BCrypt.Verify(model.Password, storedHash))
            {
                _logger.LogWarning("Login failed for {Email}: Invalid credentials", model.Email);
                return BadRequest(new { message = "Invalid credentials." });
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new Exception("JWT Key missing."));
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, model.Email) }),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            _logger.LogInformation("Login successful for {Email}", model.Email);
            return Ok(new { token = tokenHandler.WriteToken(token) });
        }

        [HttpPost("forgot-password/request-otp")]
        public async Task<IActionResult> RequestForgotPasswordOtp([FromBody] ForgotPasswordDto model)
        {
            var otp = new Random().Next(100000, 999999).ToString();
            SaveOtp(model.Email, otp, "forgot-password");

            _logger.LogInformation("Sending forgot-password OTP to {Email}", model.Email);
            if (!await SendOtpEmail(model.Email, otp, "Your OTP to reset password"))
            {
                _logger.LogError("Failed to send forgot-password OTP to {Email}", model.Email);
                return StatusCode(500, new { message = "Failed to send OTP." });
            }

            _logger.LogInformation("Forgot-password OTP sent to {Email}", model.Email);
            return Ok(new { message = "OTP sent to your registered email." });
        }

        [HttpPost("forgot-password/verify-otp")]
        public IActionResult VerifyForgotPasswordOtp([FromBody] VerifyForgotPasswordOtpDto model)
        {
            _logger.LogInformation("Verifying forgot-password OTP for {Email}", model.Email);
            if (!ValidateOtp(model.Email, model.Otp, "forgot-password"))
            {
                _logger.LogWarning("Invalid or expired forgot-password OTP for {Email}", model.Email);
                return BadRequest(new { message = "Invalid or expired OTP." });
            }

            _logger.LogInformation("Forgot-password OTP verified for {Email}", model.Email);
            return Ok(new { message = "OTP verified. Proceed to reset password." });
        }

        [HttpPost("forgot-password/reset")]
        public IActionResult ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (model.NewPassword != model.ConfirmPassword)
            {
                _logger.LogWarning("Password reset failed for {Email}: Passwords do not match", model.Email);
                return BadRequest(new { message = "Passwords do not match." });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("UPDATE Users SET PasswordHash = @Hash WHERE Email = @Email AND IsVerified = 1", conn);
            cmd.Parameters.AddWithValue("@Hash", hashedPassword);
            cmd.Parameters.AddWithValue("@Email", model.Email);
            int rows = cmd.ExecuteNonQuery();

            if (rows == 0)
            {
                _logger.LogWarning("Password reset failed for {Email}: User not found or not verified", model.Email);
                return NotFound(new { message = "User not found or not verified." });
            }

            
            _logger.LogInformation("Password reset successful for {Email}", model.Email);
            return Ok(new { message = "Password reset successfully." });
        }

        [HttpGet("validate-token")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            _logger.LogInformation("Validating token");
            return Ok(new { message = "Token is valid." });
        }

        

        private void SaveOtp(string email, string otp, string purpose)
        {
            var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
            var istExpiry = istNow.AddMinutes(15);

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO EmailOtp (Email, OtpCode, ExpiryTime, IsUsed, CreatedAt, Purpose) 
                VALUES (@Email, @OtpCode, @ExpiryTime, @IsUsed, @CreatedAt, @Purpose)", conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@OtpCode", otp);
            cmd.Parameters.AddWithValue("@ExpiryTime", istExpiry);
            cmd.Parameters.AddWithValue("@IsUsed", false);
            cmd.Parameters.AddWithValue("@CreatedAt", istNow);
            cmd.Parameters.AddWithValue("@Purpose", purpose);
            cmd.ExecuteNonQuery();
        }

        private bool ValidateOtp(string email, string otp, string purpose)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM EmailOtp 
                WHERE Email = @Email AND OtpCode = @OtpCode AND Purpose = @Purpose 
                AND IsUsed = 0 AND ExpiryTime > DATEADD(MINUTE, 330, GETUTCDATE())", conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@OtpCode", otp);
            cmd.Parameters.AddWithValue("@Purpose", purpose);

            return (int)cmd.ExecuteScalar() > 0;
        }

        private void DeleteOtp(string email, string purpose)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("DELETE FROM EmailOtp WHERE Email = @Email AND Purpose = @Purpose", conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Purpose", purpose);
            cmd.ExecuteNonQuery();
        }

        private async Task<bool> SendOtpEmail(string toEmail, string otp, string subject)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Datanova", _smtpSettings.SMTPFrom));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = $"Your OTP is: {otp}" };

            try
            {
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
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send OTP email to {Email}: {ErrorMessage}", toEmail, ex.Message);
                return false;
            }
        }
    }
}