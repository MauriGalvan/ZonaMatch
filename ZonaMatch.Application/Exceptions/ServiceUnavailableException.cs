namespace ZonaMatch.Application.Exceptions;

public sealed class ServiceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
