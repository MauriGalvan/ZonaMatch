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
                EmailYaRegistradoException => (StatusCodes.Status409Conflict, exception.Message),
                CredencialesInvalidasException => (StatusCodes.Status401Unauthorized, exception.Message),
                // Database unreachable, connection refused, timeouts...
                _ when IsTransientDbFailure(exception) => (StatusCodes.Status503ServiceUnavailable, "El servicio no esta disponible temporalmente. Intenta nuevamente en unos instantes."),
                _ => (StatusCodes.Status500InternalServerError, "Ocurrio un error inesperado.")
            };

            // Expected business errors are not worth an Error log entry
            if (status >= StatusCodes.Status500InternalServerError)
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
                    Detail = _environment.IsDevelopment() && status >= StatusCodes.Status500InternalServerError
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
    }
}
