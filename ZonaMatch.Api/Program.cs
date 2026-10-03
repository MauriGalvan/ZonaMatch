using System.Text.Json.Serialization;
using NetTopologySuite.IO.Converters;
using ZonaMatch.Api.Infrastructure;
using ZonaMatch.Application;
using ZonaMatch.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// capas: Application (casos de uso) e Infrastructure (PostgreSQL + PostGIS, lectores de fuentes)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // geometrías como GeoJSON y enums como texto ("High", "PublicTransport")
    options.JsonSerializerOptions.Converters.Add(new GeoJsonConverterFactory());
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddFrontendCors(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsSettings.PolicyName);
app.UseAuthorization();
app.MapControllers();
app.Run();

// expuesto para los tests de integración (WebApplicationFactory)
public partial class Program;
