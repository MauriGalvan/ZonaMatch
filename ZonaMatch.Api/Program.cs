using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// se configura PostgreSQL y PostGIS
builder.Services.AddDbContext<ZonaMatchDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        x => x.UseNetTopologySuite() // Habilita el manejo de puntos/poligonos
    )
);

// Repositorio generico
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Repositorios geograficos especificos
builder.Services.AddScoped<IEscuelaRepository, EscuelaRepository>();
builder.Services.AddScoped<IEspacioVerdeRepository, EspacioVerdeRepository>();
builder.Services.AddScoped<IBarrioRepository, BarrioRepository>();
builder.Services.AddScoped<IZonaRepository, ZonaRepository>();

// Servicios de aplicacion
builder.Services.AddScoped<IGeolocationService, GeolocationService>();

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

app.UseAuthorization();
app.MapControllers();
app.Run();
