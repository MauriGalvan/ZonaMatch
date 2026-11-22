using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Caching;
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
        services.AddScoped<IAnalysisFollowUpRepository, AnalysisFollowUpRepository>();
        services.AddScoped<ISavedZoneRepository, SavedZoneRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAdminStatsRepository, AdminStatsRepository>();
        services.AddScoped<IAdminSurveyRepository, AdminSurveyRepository>();
        services.AddScoped<INpsRepository, NpsRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<ICriterionRepository, CriterionRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<ICommunityPoiRepository, CommunityPoiRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IGuestReportSendRepository, GuestReportSendRepository>();
        services.AddScoped<IReportDownloadRepository, ReportDownloadRepository>();
        services.AddScoped<IZoneProfilesRepository, ZoneProfilesRepository>();

        services.AddOptions<LocationServicesOptions>()
            .BindConfiguration(string.Empty)
            .Validate(options => options.Geocoding.IsValidGeocodingProvider, "Geocoding:Provider must be Osm, Nominatim or Photon.")
            .Validate(options => options.PlaceSearch.IsValidSearchProvider, "PlaceSearch:Provider must be Nominatim or Photon.")
            .Validate(options => !options.IsEnabled("nominatim") || LocationServicesOptions.IsHttpBaseUrl(options.Nominatim.BaseUrl),
                "Nominatim:BaseUrl must be an absolute HTTP or HTTPS URL when Nominatim is enabled.")
            .Validate(options => !options.IsEnabled("photon") || LocationServicesOptions.IsHttpBaseUrl(options.Photon.BaseUrl),
                "Photon:BaseUrl must be an absolute HTTP or HTTPS URL when Photon is enabled.")
            .Validate(options => !options.IsEnabled("nominatim") || options.Nominatim.Timeout > TimeSpan.Zero,
                "Nominatim:Timeout must be positive when Nominatim is enabled.")
            .Validate(options => !options.IsEnabled("photon") || options.Photon.Timeout > TimeSpan.Zero,
                "Photon:Timeout must be positive when Photon is enabled.")
            .Validate(options => !options.Geocoding.CanSelect("nominatim") || !string.IsNullOrWhiteSpace(options.Nominatim.UserAgent),
                "Nominatim:UserAgent is required when Nominatim reverse geocoding is enabled.")
            .ValidateOnStart();

        services.AddScoped<OsmGeocodingClient>();
        services.AddHttpClient<NominatimGeocodingClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<LocationServicesOptions>>().Value.Nominatim;
            client.BaseAddress = LocationServicesOptions.ToBaseAddress(options.BaseUrl!);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent!);
            client.Timeout = options.Timeout;
        });
        services.AddHttpClient<PhotonGeocodingClient>(ConfigurePhoton);
        services.AddScoped<IGeocodingClientSelector>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<LocationServicesOptions>>().Value.Geocoding;
            return new GeocodingClientSelector(provider, options.Provider.Trim(), options.AllowProviderOverride);
        });
        services.AddScoped<IGeocodingClient>(serviceProvider =>
            serviceProvider.GetRequiredService<IGeocodingClientSelector>().Resolve(null));

        services.AddSingleton(PlaceSearchBounds.LoadAmba());
        services.AddHttpClient<NominatimPlaceSearchClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<LocationServicesOptions>>().Value.Nominatim;
            client.BaseAddress = LocationServicesOptions.ToBaseAddress(options.BaseUrl!);
            client.Timeout = options.Timeout;
        });
        services.AddSingleton(provider =>
            new PhotonSearchOptions(provider.GetRequiredService<IOptions<LocationServicesOptions>>().Value.Photon.Language));
        services.AddHttpClient<PhotonPlaceSearchClient>(ConfigurePhoton);
        services.AddScoped<IPlaceSearchClientSelector>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<LocationServicesOptions>>().Value.PlaceSearch;
            return new PlaceSearchClientSelector(provider, options.Provider.Trim(), options.AllowProviderOverride);
        });
        services.AddScoped<IPlaceSearchClient>(serviceProvider =>
            serviceProvider.GetRequiredService<IPlaceSearchClientSelector>().Resolve(null));
        services.AddScoped<IEmailService, EmailService>();

        // Without a connection string it falls back to the local Azurite emulator: the API still starts
        // (reviews without images never reach storage) and only uploading fails until storage is configured.
        services.AddSingleton<IImageStorage>(_ => new AzureBlobImageStorage(
            new BlobContainerClient(
                configuration["Storage:ConnectionString"] ?? "UseDevelopmentStorage=true",
                configuration["Storage:ContainerName"] ?? "imagenes"),
            configuration.GetValue<bool>("Storage:PublicRead")));

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddOptions<CacheLifetimeOptions>()
            .BindConfiguration(CacheLifetimeOptions.SectionName)
            .Validate(options => options.ZoneProfiles > TimeSpan.Zero, "Cache:ZoneProfiles must be positive.")
            .Validate(options => options.NominatimAddresses > TimeSpan.Zero, "Cache:NominatimAddresses must be positive.")
            .Validate(options => options.MotisTravelTimes > TimeSpan.Zero, "Cache:MotisTravelTimes must be positive.")
            .Validate(options => options.MotisRoutePlans > TimeSpan.Zero, "Cache:MotisRoutePlans must be positive.")
            .Validate(options => options.PublicResponseSeconds > 0, "Cache:PublicResponseSeconds must be positive.")
            .ValidateOnStart();
        // MOTIS trips and plans and Nominatim addresses. Today it lives in memory; with several instances, a distributed
        // cache (Redis or PostgreSQL) can be added here as its second level.
        services.AddHybridCache();
        // The zone profiles are too large for HybridCache: they have their own in-memory cache.
        services.AddSingleton<ZoneProfilesCache>();
        services.AddOptions<MotisOptions>()
            .BindConfiguration(MotisOptions.SectionName)
            .Validate(options => LocationServicesOptions.IsHttpBaseUrl(options.Url),
                "Motis:Url must be an absolute HTTP or HTTPS URL.")
            .Validate(options => options.ReferenceTime is not null, "Motis:FechaReferencia is required.")
            .Validate(options => options.RoutingTimeout > TimeSpan.Zero, "Motis:RoutingTimeout must be positive.")
            .Validate(options => options.PlanningTimeout > TimeSpan.Zero, "Motis:PlanningTimeout must be positive.")
            .ValidateOnStart();
        services.AddHttpClient<IRoutingClient, MotisRoutingClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MotisOptions>>().Value;
            client.BaseAddress = LocationServicesOptions.ToBaseAddress(options.Url!);
            client.Timeout = options.RoutingTimeout;
        });
        services.AddHttpClient<IRoutePlanner, MotisRoutePlanner>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MotisOptions>>().Value;
            client.BaseAddress = LocationServicesOptions.ToBaseAddress(options.Url!);
            client.Timeout = options.PlanningTimeout;
        });

        return services;
    }

    private static void ConfigurePhoton(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<LocationServicesOptions>>().Value.Photon;
        client.BaseAddress = LocationServicesOptions.ToBaseAddress(options.BaseUrl!);
        client.Timeout = options.Timeout;
    }
}
