namespace EmailVerificationAPI.Models
{
    public class RegisterDto
    {

        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;


        public RegisterDto() { }

        public RegisterDto(string email, string password)
        {
            Email = email;
            Password = password;
        }
    }
}


