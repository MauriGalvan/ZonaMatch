namespace ZonaMatch.Application.Exceptions
{
    // The request is valid but a business rule forbids it, e.g. validating your own contribution (409)
    public class OperacionNoPermitidaException : Exception
    {
        public OperacionNoPermitidaException(string message)
            : base(message)
        {
        }
    }
}
