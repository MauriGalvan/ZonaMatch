using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ZonaMatch.Infrastructure.Data
{
    // Used by dotnet ef (migrations add, database update).
    public class ZonaMatchDbContextFactory : IDesignTimeDbContextFactory<ZonaMatchDbContext>
    {
        // Same UserSecretsId as ZonaMatch.Api.csproj: the connection string is not in appsettings.json
        private const string ApiUserSecretsId = "d1e65f38-f0f9-4687-9a7f-2e2518016583";

        public ZonaMatchDbContext CreateDbContext(string[] args)
        {
            // Same sources as the API: User Secrets locally, environment variables (ConnectionStrings__DefaultConnection) elsewhere
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "ZonaMatch.Api"))
                .AddJsonFile("appsettings.json")
                .AddUserSecrets(ApiUserSecretsId)
                .AddEnvironmentVariables()
                .Build();

            // Without a connection string, commands that do not touch the database (migrations add, script) still work
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<ZonaMatchDbContext>();

            optionsBuilder.UseZonaMatchNpgsql(connectionString);

            return new ZonaMatchDbContext(optionsBuilder.Options);
        }
    }
}
