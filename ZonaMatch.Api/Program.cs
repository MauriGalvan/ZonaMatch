using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// se configura PostgreSQL y PostGIS
builder.Services.AddDbContext<ZonaMatchDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        x => x.UseNetTopologySuite() // Habilita el manejo de polígonos
    )
);

// 2. Inyectar el Repositorio Genérico
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();