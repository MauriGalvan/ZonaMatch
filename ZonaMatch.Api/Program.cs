using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Api.Background;
using ZonaMatch.Api.DependencyInjection;
using ZonaMatch.Api.ExceptionHandling;
using ZonaMatch.Api.Middleware;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Caching;
using ZonaMatch.Infrastructure.Data;

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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddHostedService<WarmZoneProfilesService>();
builder.Services.AddHostedService<PurgeExpiredAnalysesBackgroundService>();

// Keep the database health check in the API composition root because it backs the HTTP health endpoint.
builder.Services.AddHealthChecks().AddDbContextCheck<ZonaMatchDbContext>("postgres");

// JWT. The settings are validated at startup: without Jwt:SigningKey the app refuses to start.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

builder.Services.AddRequestRateLimiting();

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
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)))
    // Invalid requests answer with the same ProblemDetails shape as the global exception handler, in Spanish
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create);
builder.Services.AddOptions<MvcOptions>()
    .Configure<IOptions<CacheLifetimeOptions>>((mvc, cache) =>
        mvc.CacheProfiles.Add(CacheLifetimeOptions.PublicResponseProfile,
            new CacheProfile { Duration = cache.Value.PublicResponseSeconds }));
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
// Email invitation quotas are keyed by the authenticated user.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();

public partial class Program { }
