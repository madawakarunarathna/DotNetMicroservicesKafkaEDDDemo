using Microsoft.EntityFrameworkCore;

namespace UserService.Persistence
{
    public class UserDbContext : DbContext
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
        {
        }
        public DbSet<Domain.User> Users { get; set; } = default!;
    }
}
