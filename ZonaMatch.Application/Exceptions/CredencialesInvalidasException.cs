namespace ZonaMatch.Application.Exceptions
{
    // Deliberately generic: it must not reveal whether the email exists or the password was wrong
    public class CredencialesInvalidasException : Exception
    {
        public CredencialesInvalidasException()
            : base("Email o contrasena incorrectos.")
        {
        }
    }
}
