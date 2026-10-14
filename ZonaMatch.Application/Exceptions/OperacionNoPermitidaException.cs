namespace ZonaMatch.Application.Exceptions
{
    // El pedido es valido pero una regla de negocio lo prohibe, p. ej. validar tu propio aporte (409)
    public class OperacionNoPermitidaException : Exception
    {
        public OperacionNoPermitidaException(string mensaje)
            : base(mensaje)
        {
        }
    }
}
