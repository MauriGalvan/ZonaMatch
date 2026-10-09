using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Api.ExceptionHandling;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;
using ZonaMatch.Infrastructure.Repositories;
using ZonaMatch.Infrastructure.Security;
using ZonaMatch.Application.UseCases;

var builder = WebApplication.CreateBuilder(args);

// se configura PostgreSQL y PostGIS
builder.Services.AddDbContext<ZonaMatchDbContext>(options =>
    options.UseZonaMatchNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositorio generico
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Repositorios geograficos especificos
builder.Services.AddScoped<IEscuelaRepository, EscuelaRepository>();
builder.Services.AddScoped<IPartidoRepository, PartidoRepository>();
builder.Services.AddScoped<IComunaRepository, ComunaRepository>();
builder.Services.AddScoped<IMapaRepository, MapaRepository>();
builder.Services.AddScoped<IPuntoInteresRepository, PuntoInteresRepository>();
builder.Services.AddScoped<IZonaRepository, ZonaRepository>();

// Reverse geocoding. "Osm" (default) queries the local osm schema; "Nominatim" calls the public OpenStreetMap API.
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

// Servicios de aplicacion
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
builder.Services.AddScoped<IMapaService, MapaService>();
builder.Services.AddScoped<IZonaService, ZonaService>();
builder.Services.AddScoped<CreateGroup>();

// Registro de usuarios
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

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
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
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
        .AllowAnyMethod()));

// Centralized error handling: every unhandled exception becomes a ProblemDetails response
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
