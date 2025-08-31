namespace EmailVerificationAPI.Models
{
    public class EmailLog
    {
        public int Id { get; set; }
        public string? ToListCsv { get; set; }
        public string? CcListCsv { get; set; }
        public string? BccListCsv { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
        public string? Status { get; set; }
        public string? AttachmentLink { get; set; }
    }
}