namespace EmailVerificationAPI.Models
{
    public class EmailRequest
    {
        public IEnumerable<string>? Emails { get; set; }
        public IEnumerable<string>? Ccs { get; set; }
        public IEnumerable<string>? Bccs { get; set; }
        public string? Subject { get; set; }
        public string? Msg { get; set; }
        public IEnumerable<IFormFile>? Files { get; set; }
    }
}