using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Perfil
{
    // Criterio base del usuario. Sin perfil responde 404: el front lo usa para mandarlo a armarlo.
    public class ObtenerMiPerfil
    {
        private readonly IPerfilRepository _perfiles;

        public ObtenerMiPerfil(IPerfilRepository perfiles)
        {
            _perfiles = perfiles;
        }

        public async Task<PerfilDto> EjecutarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var perfil = await _perfiles.ObtenerDeUsuarioAsync(usuarioId, cancellationToken)
                ?? throw new PerfilNoEncontradoException();

            return PerfilMapeo.ADto(perfil);
        }
    }
}
