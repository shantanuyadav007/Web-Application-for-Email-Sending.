namespace EmailVerificationAPI.Models
{
    public class SmtpSettings
    {
        public string SMTPDomain { get; set; } = string.Empty;
        public string SMTPFrom { get; set; } = string.Empty;
        public string SMTPHost { get; set; } = string.Empty;
        public string SMTPUser { get; set; } = string.Empty;
        public string SMTPPwd { get; set; } = string.Empty;
        public int SMTPPort { get; set; }
        public string UseSSL { get; set; } = string.Empty; // "StartTls", "SslOnConnect", etc.
    }
}
