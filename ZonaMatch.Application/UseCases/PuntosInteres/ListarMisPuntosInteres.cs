using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.PuntosInteres
{
    // Los puntos de interes personales de un usuario, el mas nuevo primero
    public class ListarMisPuntosInteres
    {
        private readonly IPuntoInteresRepository _puntos;

        public ListarMisPuntosInteres(IPuntoInteresRepository puntos)
        {
            _puntos = puntos;
        }

        public async Task<IReadOnlyList<PuntoInteresUsuarioDto>> EjecutarAsync(
            Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var puntos = await _puntos.ListarDeUsuarioAsync(usuarioId, cancellationToken);
            return puntos.Select(PuntoInteresUsuarioMapeo.ADto).ToList();
        }
    }
}
