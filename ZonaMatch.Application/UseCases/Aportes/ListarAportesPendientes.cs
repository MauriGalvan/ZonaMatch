using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Aportes que esperan votos. Con un usuario, cada uno indica si es suyo y su voto.
    public class ListarAportesPendientes
    {
        private readonly IAporteRepository _aportes;

        public ListarAportesPendientes(IAporteRepository aportes)
        {
            _aportes = aportes;
        }

        public async Task<IReadOnlyList<AporteDto>> EjecutarAsync(
            string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default)
        {
            var aportes = await _aportes.ListarPendientesAsync(zonaSlug, cancellationToken);
            return aportes.Select(a => AporteMapeo.ADto(a, a.Usuario?.Email, usuarioActual)).ToList();
        }
    }
}
