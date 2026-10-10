namespace ZonaMatch.Application.Exceptions
{
    // Reglas de entrada que las data annotations no pueden expresar, p. ej. campos obligatorios solo para un tipo de correccion (400)
    public class DatosInvalidosException : Exception
    {
        public DatosInvalidosException(string mensaje)
            : base(mensaje)
        {
        }
    }
}
