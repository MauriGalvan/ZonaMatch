using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Perfil
{
    // Criterio base del usuario, o null si todavia no lo armo. No tenerlo es lo normal en el primer ingreso,
    // asi que no es una excepcion: el controlador responde 404 y el front manda al usuario a armarlo.
    public class ObtenerMiPerfil
    {
        private readonly IPerfilRepository _perfiles;

        public ObtenerMiPerfil(IPerfilRepository perfiles)
        {
            _perfiles = perfiles;
        }

        public async Task<PerfilDto?> EjecutarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var perfil = await _perfiles.ObtenerDeUsuarioAsync(usuarioId, cancellationToken);

            return perfil is null ? null : PerfilMapeo.ADto(perfil);
        }
    }
}
