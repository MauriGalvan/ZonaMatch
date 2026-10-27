using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Api.Background;
using ZonaMatch.Api.ExceptionHandling;
using ZonaMatch.Api.Middleware;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.AnalisisGuardados;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Application.UseCases.Contacto;
using ZonaMatch.Application.UseCases.Criterios;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Application.UseCases.GroupLinks;
using ZonaMatch.Application.UseCases.Groups;
using ZonaMatch.Application.UseCases.Notifications;
using ZonaMatch.Application.UseCases.Perfil;
using ZonaMatch.Application.UseCases.Preguntas;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Application.UseCases.Resenas;
using ZonaMatch.Application.UseCases.Zonas;
using ZonaMatch.Application.UseCases.Routes;
using ZonaMatch.Application.UseCases.SavedZones;
using ZonaMatch.Application.UseCases.ZoneRecommendations;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;
using ZonaMatch.Infrastructure.Repositories;
using ZonaMatch.Infrastructure.Security;
using ZonaMatch.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Telemetry to Azure Monitor (Application Insights): logs, requests, dependencies and exceptions.
// Only enabled when the connection string is configured (APPLICATIONINSIGHTS_CONNECTION_STRING app setting in Azure),
// so local runs and CI keep working without it.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
    {
        // Rate-limited trace sampling keeps the ingested volume (and the Azure bill) bounded
        options.TracesPerSecond = 2;
    });

    builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"));
}

// Configure PostgreSQL and PostGIS.
builder.Services.AddDbContext<ZonaMatchDbContext>(options =>
    options.UseZonaMatchNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// GET /health: used by the App Service "Health check" feature and by availability alerts. Verifies the database connection.
builder.Services.AddHealthChecks().AddDbContextCheck<ZonaMatchDbContext>("postgres");

// Generic repository.
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Specialized geographic repositories.
builder.Services.AddScoped<IEscuelaRepository, EscuelaRepository>();
builder.Services.AddScoped<IPartidoRepository, PartidoRepository>();
builder.Services.AddScoped<IComunaRepository, ComunaRepository>();
builder.Services.AddScoped<IMapaRepository, MapaRepository>();
builder.Services.AddScoped<IPuntoInteresRepository, PuntoInteresRepository>();
builder.Services.AddScoped<GetZoneProfileRecommendations>();
builder.Services.AddScoped<IZonaRepository, ZonaRepository>();
builder.Services.AddScoped<IIndicadoresZonaRepository, IndicadoresZonaRepository>();
// Real estate market per zone (app.zone_market, MERCADO-ZONAS-DOCKER.bat)
builder.Services.AddScoped<IZoneMarketRepository, ZoneMarketRepository>();
builder.Services.AddScoped<GetZoneMarket>();
builder.Services.AddScoped<ITransporteZonaRepository, TransporteZonaRepository>();

builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IGroupInvitationRepository, GroupInvitationRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IAnalisisRepository, AnalisisRepository>();
builder.Services.AddScoped<ISavedZoneRepository, SavedZoneRepository>();

// Reverse geocoding. "Osm" (default) queries the local osm schema; "Nominatim" uses the configured Nominatim instance.
if (string.Equals(builder.Configuration["Geocoding:Provider"], "Nominatim", StringComparison.OrdinalIgnoreCase))
{
    // Its usage policy requires an identifying User-Agent
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient<IGeocodingClient, NominatimGeocodingClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Nominatim:BaseUrl"] ?? "https://nominatim.openstreetmap.org/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.Configuration["Nominatim:UserAgent"] ?? "ZonaMatch/1.0");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}
else
{
    builder.Services.AddScoped<IGeocodingClient, OsmGeocodingClient>();
}

// Application services.
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
builder.Services.AddHttpClient<IPlaceSearchClient, NominatimPlaceSearchClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Nominatim:BaseUrl"] ?? "http://localhost:8080/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<SearchPlaces>();
builder.Services.AddScoped<IMapaService, MapaService>();
builder.Services.AddScoped<IZonaService, ZonaService>();
builder.Services.AddScoped<CreateGroup>();
builder.Services.AddScoped<GetGroupDetails>();
builder.Services.AddScoped<CalcularPuntajeEnGrupo>();
builder.Services.AddScoped<GuardarAnalisisEnGrupo>();
builder.Services.AddScoped<GetGroups>();
builder.Services.AddScoped<DeleteGroup>();
builder.Services.AddScoped<UpdateGroup>();
builder.Services.AddScoped<RemoveParticipant>();
builder.Services.AddScoped<GetNotifications>();
builder.Services.AddScoped<MarkNotificationAsRead>();
builder.Services.AddScoped<MarkAllNotificationsAsRead>();

builder.Services.AddScoped<CreateInvitation>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<GetInvitationByToken>();
builder.Services.AddScoped<AcceptInvitation>();
builder.Services.AddScoped<GetGroupByLink>();
builder.Services.AddScoped<GetGroupInviteLink>();
builder.Services.AddScoped<JoinGroupByLink>();
builder.Services.AddScoped<RevokeInviteLink>();

// Authentication use cases
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<LoginUser>();
builder.Services.AddScoped<RegisterUser>();
builder.Services.AddScoped<ICriterioRepository, CriterioRepository>();

// Community features for each zone: reviews, questions, and map contributions.
builder.Services.AddScoped<IResenaRepository, ResenaRepository>();
builder.Services.AddScoped<IPreguntaRepository, PreguntaRepository>();
builder.Services.AddScoped<IAporteRepository, AporteRepository>();

// Community use cases, one per operation.
builder.Services.AddScoped<ListarResenas>();
builder.Services.AddScoped<GuardarMiResena>();
builder.Services.AddScoped<EliminarMiResena>();
builder.Services.AddScoped<MarcarResenaUtil>();
builder.Services.AddScoped<ListarPreguntas>();
builder.Services.AddScoped<CrearPregunta>();
builder.Services.AddScoped<ResponderPregunta>();
builder.Services.AddScoped<ResumirAportes>();
builder.Services.AddScoped<ListarAportesPendientes>();
builder.Services.AddScoped<CrearPuntoNuevo>();
builder.Services.AddScoped<CrearCorreccion>();
builder.Services.AddScoped<ValidarAporte>();
builder.Services.AddScoped<ICommunityPoiRepository, CommunityPoiRepository>();
builder.Services.AddScoped<CommunityPoiUpdater>();
builder.Services.AddScoped<ResolveContribution>();

// Criteria catalog for the wizard (GET /Criterios).
builder.Services.AddScoped<ListarCriterios>();

// User profile and saved analyses with their versions.
builder.Services.AddScoped<IPerfilRepository, PerfilRepository>();
builder.Services.AddScoped<ObtenerMiPerfil>();
builder.Services.AddScoped<GuardarMiPerfil>();
builder.Services.AddScoped<ListarMisAnalisis>();
builder.Services.AddScoped<ObtenerAnalisis>();
builder.Services.AddScoped<CompararAnalisis>();
builder.Services.AddScoped<CrearAnalisis>();
builder.Services.AddScoped<CrearVersionAnalisis>();
builder.Services.AddScoped<IAnalysisResultCalculator, AnalysisResultCalculator>();
builder.Services.AddScoped<RecalculateAnalysis>();
builder.Services.AddScoped<RenombrarAnalisis>();
builder.Services.AddScoped<EliminarAnalisis>();
builder.Services.AddScoped<ListSavedZones>();
builder.Services.AddScoped<SaveZone>();
builder.Services.AddScoped<RemoveSavedZone>();

// Scoring engine: zone profiles are cached in memory; travel times come from MOTIS.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PerfilesZonaCache>();
builder.Services.AddScoped<IPerfilesZonaRepository, PerfilesZonaRepository>();
builder.Services.AddHttpClient<IRuteoClient, MotisRuteoClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Motis:Url"] ?? "http://localhost:18080/");
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddScoped<CalcularPuntajes>();

// Trips from a zone to the user's places, leg by leg, to draw them on the analysis map (MOTIS).
builder.Services.AddHttpClient<IRoutePlanner, MotisRoutePlanner>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Motis:Url"] ?? "http://localhost:18080/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<GetRoute>();
builder.Services.AddHostedService<PrecalcularPerfilesZona>();

// Open data for each zone (census and safety), loaded by DATOS-ZONAS-DOCKER.bat.
builder.Services.AddScoped<ObtenerIndicadoresZona>();
builder.Services.AddScoped<ObtenerTransporteZona>();

// Landing page contact form; each submission sends an email to the team.
builder.Services.AddSingleton(new ContactoOpciones(builder.Configuration["Contacto:Destinatario"] ?? "zonamatchtpi@gmail.com"));
builder.Services.AddScoped<EnviarFormularioContacto>();

// JWT. The settings are validated at startup: without Jwt:SigningKey the app refuses to start.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

// Slows down password guessing on /Auth/login: 10 attempts per minute per IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Rejections would otherwise be silent: repeated hits on /Auth/login are the signal of a brute-force attempt
    options.OnRejected = (context, _) =>
    {
        var http = context.HttpContext;
        http.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ZonaMatch.RateLimiting")
            .LogWarning("Rate limit exceeded on {Method} {Path} from {RemoteIp}",
                http.Request.Method, http.Request.Path, http.Connection.RemoteIpAddress);
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // The public contact form sends an email per submission: 5 requests per 10 minutes per IP.
    options.AddPolicy("contacto", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
});

// GeoJSON compresses very well; "application/geo+json" is not in the default MIME list
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/geo+json");
});

// CORS for the React frontend; origins come from configuration (Cors:AllowedOrigins)
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Centralized error handling: every unhandled exception becomes a ProblemDetails response
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Enums are serialized as kebab-case strings ("punto-nuevo", "cerro") in requests and responses.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)));
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ZonaMatch API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseResponseCompression();
app.UseCors(FrontendCorsPolicy);
app.UseMiddleware<OriginValidationMiddleware>(new object[] { allowedOrigins });
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
