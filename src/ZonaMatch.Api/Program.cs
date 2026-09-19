using Microsoft.EntityFrameworkCore;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Servicio de Cobertura Metropolitana (CABA y AMBA)
builder.Services.AddSingleton<IGeoCoverageService, GeoCoverageService>();

// Configuración de PostgreSQL / PostGIS con NetTopologySuite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=zonamatch_db;Username=zonamatch_user;Password=zonamatch_pass";

builder.Services.AddDbContext<ZonaMatchDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseNetTopologySuite();
    });
});

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configuración de CORS para el frontend (React / Vite)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Healthcheck de la API y verificación de conexión DB
app.MapGet("/api/health", async (ZonaMatchDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new
        {
            status = "Healthy",
            databaseConnected = canConnect,
            timestamp = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "Degraded",
            databaseConnected = false,
            error = ex.Message,
            timestamp = DateTimeOffset.UtcNow
        }, statusCode: 503);
    }
});

// Configuración de límites y presets de cobertura geográfica (CABA y AMBA)
app.MapGet("/api/config/coverage", (IGeoCoverageService coverage) =>
{
    return Results.Ok(coverage.GetCoverageInfo());
});

// Validación de pertenencia geográfica a CABA / AMBA
app.MapPost("/api/geo/validate-coverage", (ValidateCoverageRequest req, IGeoCoverageService coverage) =>
{
    var isInside = coverage.IsInsideCoverage(req.Latitude, req.Longitude);
    return Results.Ok(new
    {
        isInside = isInside,
        region = "CABA y AMBA",
        latitude = req.Latitude,
        longitude = req.Longitude,
        message = isInside
            ? "Coordenada válida dentro del área metropolitana (CABA y AMBA)."
            : "La ubicación seleccionada está fuera de la cobertura exclusiva de ZonaMatch (CABA y AMBA)."
    });
});

app.Run();

public record ValidateCoverageRequest(double Latitude, double Longitude);
