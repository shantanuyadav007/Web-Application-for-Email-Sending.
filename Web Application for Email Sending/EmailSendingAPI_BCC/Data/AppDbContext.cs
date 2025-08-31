using Microsoft.EntityFrameworkCore;
using EmailVerificationAPI.Models;

namespace EmailVerificationAPI.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<EmailLog> EmailLogs { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    }
}