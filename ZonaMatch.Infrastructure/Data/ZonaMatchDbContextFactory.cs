using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ZonaMatch.Infrastructure.Data
{
    public class ZonaMatchDbContextFactory : IDesignTimeDbContextFactory<ZonaMatchDbContext>
    {
        public ZonaMatchDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "ZonaMatch.Api"))
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ZonaMatchDbContext>();

            optionsBuilder.UseZonaMatchNpgsql(
                configuration.GetConnectionString("DefaultConnection"));

            return new ZonaMatchDbContext(optionsBuilder.Options);
        }
    }
}
