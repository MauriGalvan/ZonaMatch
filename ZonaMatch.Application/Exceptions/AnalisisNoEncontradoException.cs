namespace ZonaMatch.Application.Exceptions
{
    // The analysis id does not exist or does not belong to the current user.
    public class AnalisisNoEncontradoException : Exception
    {
        public AnalisisNoEncontradoException()
            : base("El análisis no existe.")
        {
        }
    }
}
