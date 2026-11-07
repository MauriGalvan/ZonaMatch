using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;
using ZonaMatch.Infrastructure.Repositories;
using ZonaMatch.Infrastructure.Security;
using ZonaMatch.Infrastructure.Services;

namespace ZonaMatch.Api.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ZonaMatchDbContext>(options =>
            options.UseZonaMatchNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ISchoolRepository, SchoolRepository>();
        services.AddScoped<IDistrictRepository, DistrictRepository>();
        services.AddScoped<ICommuneRepository, CommuneRepository>();
        services.AddScoped<IMapRepository, MapRepository>();
        services.AddScoped<IPointOfInterestRepository, PointOfInterestRepository>();
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IZoneIndicatorsRepository, ZoneIndicatorsRepository>();
        services.AddScoped<IZoneMarketRepository, ZoneMarketRepository>();
        services.AddScoped<IZoneTransitRepository, ZoneTransitRepository>();

        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IGroupInvitationRepository, GroupInvitationRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<ISavedZoneRepository, SavedZoneRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<ICriterionRepository, CriterionRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<ICommunityPoiRepository, CommunityPoiRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IGuestReportSendRepository, GuestReportSendRepository>();
        services.AddScoped<IZoneProfilesRepository, ZoneProfilesRepository>();

        if (string.Equals(configuration["Geocoding:Provider"], "Nominatim", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IGeocodingClient, NominatimGeocodingClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Nominatim:BaseUrl"] ?? "https://nominatim.openstreetmap.org/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(configuration["Nominatim:UserAgent"] ?? "ZonaMatch/1.0");
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }
        else
        {
            services.AddScoped<IGeocodingClient, OsmGeocodingClient>();
        }

        services.AddHttpClient<IPlaceSearchClient, NominatimPlaceSearchClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Nominatim:BaseUrl"] ?? "http://localhost:8080/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IEmailService, EmailService>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        // MOTIS trips and plans and Nominatim addresses. Today it lives in memory; with several instances, a distributed
        // cache (Redis or PostgreSQL) can be added here as its second level.
        services.AddHybridCache();
        // The zone profiles are too large for HybridCache: they have their own in-memory cache.
        services.AddSingleton<ZoneProfilesCache>();
        services.AddHttpClient<IRoutingClient, MotisRoutingClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Motis:Url"] ?? "http://localhost:18080/");
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddHttpClient<IRoutePlanner, MotisRoutePlanner>(client =>
        {
            client.BaseAddress = new Uri(configuration["Motis:Url"] ?? "http://localhost:18080/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
