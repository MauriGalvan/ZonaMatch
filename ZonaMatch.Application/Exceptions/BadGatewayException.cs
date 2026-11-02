namespace ZonaMatch.Application.Exceptions;

public sealed class BadGatewayException(string message, Exception innerException)
    : Exception(message, innerException);
