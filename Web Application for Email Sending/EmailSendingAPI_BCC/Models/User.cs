namespace EmailVerificationAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string OTP { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
}
