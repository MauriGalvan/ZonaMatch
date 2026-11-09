using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ZonaMatch.Infrastructure.Data
{
    public class ZonaMatchDbContextFactory : IDesignTimeDbContextFactory<ZonaMatchDbContext>
    {
        private const string ApiUserSecretsId = "d1e65f38-f0f9-4687-9a7f-2e2518016583";

        public ZonaMatchDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "ZonaMatch.Api"))
                .AddJsonFile("appsettings.json", optional: true)
                // Same UserSecretsId as ZonaMatch.Api.csproj: the design-time factory
                // bypasses the API host, so secrets must be added explicitly.
                .AddUserSecrets(ApiUserSecretsId)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured. " +
                    "Set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\" --project ZonaMatch.Api");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ZonaMatchDbContext>();

            optionsBuilder.UseZonaMatchNpgsql(connectionString);

            return new ZonaMatchDbContext(optionsBuilder.Options);
        }
    }
}
