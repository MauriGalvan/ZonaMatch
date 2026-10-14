namespace ZonaMatch.Application.Exceptions
{
    // El usuario todavia no armo su perfil (404): primero tiene que guardarlo con PUT /Perfil.
    // GET /Perfil no la lanza: no tener perfil es lo normal antes del primer guardado (responde 404 con Mensaje).
    public class PerfilNoEncontradoException : Exception
    {
        public const string Mensaje = "Todavía no armaste tu perfil.";

        public PerfilNoEncontradoException()
            : base(Mensaje)
        {
        }
    }
}
