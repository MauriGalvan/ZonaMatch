using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Exceptions;

namespace ZonaMatch.Api.ExceptionHandling
{
    // Single place where exceptions become HTTP responses (RFC 9457 ProblemDetails).
    // Controllers and services just throw; they do not catch to build responses.
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetails;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(
            IProblemDetailsService problemDetails,
            IHostEnvironment environment,
            ILogger<GlobalExceptionHandler> logger)
        {
            _problemDetails = problemDetails;
            _environment = environment;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // The client closed the connection: nothing to report and nobody to answer
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                httpContext.Response.StatusCode = 499;
                return true;
            }

            var (status, title) = exception switch
            {
                ConflictException => (StatusCodes.Status409Conflict, exception.Message),
                UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message),
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                BadRequestException => (StatusCodes.Status400BadRequest, exception.Message),
                BadGatewayException => (StatusCodes.Status502BadGateway, exception.Message),
                // External services are unavailable, such as the email provider.
                ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, exception.Message),
                // A unique index rejected a repeated row, for example two requests adding the same participant at once
                _ when IsUniqueViolation(exception) => (StatusCodes.Status409Conflict, "Eso ya estaba registrado. Actualizá la página."),
                // Database unreachable, connection refused, timeouts...
                _ when IsTransientDbFailure(exception) => (StatusCodes.Status503ServiceUnavailable, "El servicio no está disponible por el momento. Intentá de nuevo en unos instantes."),
                _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.")
            };

            // Do not log the external exception: reverse-geocoding requests can contain the user's coordinates.
            if (exception is BadGatewayException)
                _logger.LogWarning("External service unavailable on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            // Expected business errors are not worth an Error log entry
            else if (status >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            else
                _logger.LogInformation("Request rejected ({Status}): {Message}", status, exception.Message);

            httpContext.Response.StatusCode = status;

            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    // Internal details only while developing: never leak them in production
                    Detail = _environment.IsDevelopment()
                        && status >= StatusCodes.Status500InternalServerError
                        && exception is not BadGatewayException
                        ? exception.ToString()
                        : null
                }
            });
        }

        // EF wraps provider exceptions (e.g. in InvalidOperationException), so look through the whole chain
        private static bool IsTransientDbFailure(Exception exception)
        {
            for (var e = exception; e is not null; e = e.InnerException)
            {
                if (e is DbException { IsTransient: true })
                    return true;
            }

            return false;
        }

        // PostgreSQL unique_violation (23505), also wrapped by EF in DbUpdateException
        private static bool IsUniqueViolation(Exception exception)
        {
            for (var e = exception; e is not null; e = e.InnerException)
            {
                if (e is DbException { SqlState: "23505" })
                    return true;
            }

            return false;
        }
    }
}
