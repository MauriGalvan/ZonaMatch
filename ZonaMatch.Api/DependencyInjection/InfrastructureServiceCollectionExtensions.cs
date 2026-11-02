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
        services.AddScoped<IEscuelaRepository, EscuelaRepository>();
        services.AddScoped<IPartidoRepository, PartidoRepository>();
        services.AddScoped<IComunaRepository, ComunaRepository>();
        services.AddScoped<IMapaRepository, MapaRepository>();
        services.AddScoped<IPuntoInteresRepository, PuntoInteresRepository>();
        services.AddScoped<IZonaRepository, ZonaRepository>();
        services.AddScoped<IIndicadoresZonaRepository, IndicadoresZonaRepository>();
        services.AddScoped<IZoneMarketRepository, ZoneMarketRepository>();
        services.AddScoped<ITransporteZonaRepository, TransporteZonaRepository>();

        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IGroupInvitationRepository, GroupInvitationRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAnalisisRepository, AnalisisRepository>();
        services.AddScoped<ISavedZoneRepository, SavedZoneRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<ICriterioRepository, CriterioRepository>();
        services.AddScoped<IResenaRepository, ResenaRepository>();
        services.AddScoped<IPreguntaRepository, PreguntaRepository>();
        services.AddScoped<IAporteRepository, AporteRepository>();
        services.AddScoped<ICommunityPoiRepository, CommunityPoiRepository>();
        services.AddScoped<IPerfilRepository, PerfilRepository>();
        services.AddScoped<IPerfilesZonaRepository, PerfilesZonaRepository>();

        if (string.Equals(configuration["Geocoding:Provider"], "Nominatim", StringComparison.OrdinalIgnoreCase))
        {
            services.AddMemoryCache();
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
        services.AddMemoryCache();
        services.AddSingleton<PerfilesZonaCache>();
        services.AddHttpClient<IRuteoClient, MotisRuteoClient>(client =>
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
