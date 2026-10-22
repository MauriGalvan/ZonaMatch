namespace ZonaMatch.Application.Exceptions;

public sealed class ServiceUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
