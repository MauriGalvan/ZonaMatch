using Microsoft.EntityFrameworkCore;

namespace ZonaMatch.Infrastructure.Data
{
    public class ZonaMatchDbContext : DbContext
    {
        public ZonaMatchDbContext(DbContextOptions<ZonaMatchDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            
            modelBuilder.HasPostgresExtension("postgis");
        }
    }
}