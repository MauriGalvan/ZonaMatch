namespace ZonaMatch.Application.Exceptions
{
    // La zona, resena, pregunta o aporte del pedido no existe (404)
    public class NoEncontradoException : Exception
    {
        public NoEncontradoException(string mensaje)
            : base(mensaje)
        {
        }
    }
}
