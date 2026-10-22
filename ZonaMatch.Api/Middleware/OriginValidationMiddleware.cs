using ZonaMatch.Application.Exceptions;

namespace ZonaMatch.Api.Middleware;

public sealed class OriginValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _allowedOrigins;

    public OriginValidationMiddleware(RequestDelegate next, string[] allowedOrigins)
    {
        _next = next;
        _allowedOrigins = allowedOrigins;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isStateChangingMethod = HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method);
        var origin = context.Request.Headers["Origin"].ToString();
        var requestOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        var isAllowedOrigin = _allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            || string.Equals(origin, requestOrigin, StringComparison.OrdinalIgnoreCase);

        if (isStateChangingMethod && !string.IsNullOrWhiteSpace(origin) && !isAllowedOrigin)
            throw new ForbiddenException("El origen de la solicitud no esta permitido.");

        await _next(context);
    }
}
