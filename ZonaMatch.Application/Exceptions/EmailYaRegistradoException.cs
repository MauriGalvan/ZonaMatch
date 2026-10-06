namespace ZonaMatch.Application.Exceptions
{
    public class EmailYaRegistradoException : Exception
    {
        public EmailYaRegistradoException()
            : base("El email ya esta registrado.")
        {
        }
    }
}
