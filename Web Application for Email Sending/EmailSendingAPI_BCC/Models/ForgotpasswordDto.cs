namespace EmailVerificationAPI.Models
{
  public class ForgotPasswordDto
  {
    public string Email { get; set; } = string.Empty;
  }

  public class VerifyForgotPasswordOtpDto
  {
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
  }

  public class ResetPasswordDto
  {
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
  }
}
