using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Perfil
{
    // Crea el perfil del usuario (al terminar el registro) o lo reemplaza. Los analisis ya guardados
    // no cambian: cada uno tiene su propia copia del criterio.
    public class GuardarMiPerfil
    {
        private readonly IPerfilRepository _perfiles;
        private readonly ICriterioRepository _criterios;
        private readonly TimeProvider _reloj;

        public GuardarMiPerfil(IPerfilRepository perfiles, ICriterioRepository criterios, TimeProvider reloj)
        {
            _perfiles = perfiles;
            _criterios = criterios;
            _reloj = reloj;
        }

        public async Task<PerfilDto> EjecutarAsync(
            Guid usuarioId, CriterioUsuarioRequest solicitud, CancellationToken cancellationToken = default)
        {
            var catalogo = await _criterios.ListarAsync(cancellationToken);
            var ahora = _reloj.GetUtcNow();
            var perfil = await _perfiles.ObtenerDeUsuarioAsync(usuarioId, cancellationToken);
            var nuevo = perfil is null;

            perfil ??= new Contexto { UsuarioId = usuarioId, Tipo = TipoContexto.Perfil, FechaCreacion = ahora };

            // Valida todo antes de agregarlo: un pedido invalido no deja un perfil a medio armar
            CriterioUsuario.Aplicar(perfil, solicitud, catalogo);
            perfil.FechaActualizacion = ahora;

            if (nuevo)
                _perfiles.Agregar(perfil);

            await _perfiles.GuardarCambiosAsync(cancellationToken);

            return PerfilMapeo.ADto(perfil);
        }
    }
}
