namespace ZonaMatch.Application.Exceptions
{
    // The zone, review, question or contribution of the request does not exist (404)
    public class NoEncontradoException : Exception
    {
        public NoEncontradoException(string message)
            : base(message)
        {
        }
    }
}
