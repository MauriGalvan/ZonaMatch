using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Common;

namespace ZonaMatch.Api.Infrastructure
{
    // traduce las excepciones de los casos de uso a respuestas ProblemDetails
    public class ApiExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetails;

        public ApiExceptionHandler(IProblemDetailsService problemDetails)
        {
            _problemDetails = problemDetails;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title) = exception switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Recurso inexistente"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
                _ => (0, string.Empty),
            };

            if (status == 0)
            {
                return false;
            }

            httpContext.Response.StatusCode = status;

            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = exception.Message,
                },
            });
        }
    }

    public class AdminOptions
    {
        public const string SectionName = "Admin";

        // la ingesta reescribe datos: solo se habilita en entornos de carga
        public bool IngestionEndpointsEnabled { get; set; }
    }

    public class CorsSettings
    {
        public const string SectionName = "Cors";
        public const string PolicyName = "frontend";

        public string[] AllowedOrigins { get; set; } = [];
    }

    public static class CorsSetup
    {
        // el frontend (Vite en desarrollo) consume la API desde otro origen
        public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = new CorsSettings();
            configuration.GetSection(CorsSettings.SectionName).Bind(settings);

            var policy = new CorsPolicyBuilder()
                .WithOrigins(settings.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .Build();

            return services.AddCors(options => options.AddPolicy(CorsSettings.PolicyName, policy));
        }
    }
}
