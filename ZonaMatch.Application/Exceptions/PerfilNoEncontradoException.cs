namespace ZonaMatch.Application.Exceptions
{
    // El usuario todavia no armo su perfil (404): primero tiene que guardarlo con PUT /Perfil
    public class PerfilNoEncontradoException : Exception
    {
        public PerfilNoEncontradoException()
            : base("Todavía no armaste tu perfil.")
        {
        }
    }
}
