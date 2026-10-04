using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;
using ZonaMatch.Infrastructure.Repositories;

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

var app = builder.Build();

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
app.UseAuthorization();
app.MapControllers();
app.Run();
