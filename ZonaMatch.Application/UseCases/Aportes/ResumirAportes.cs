using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Total de aportes de una zona, aprobados y pendientes
    public class ResumirAportes
    {
        private readonly IAporteRepository _aportes;

        public ResumirAportes(IAporteRepository aportes)
        {
            _aportes = aportes;
        }

        public async Task<ResumenAportesDto> EjecutarAsync(string zonaSlug, CancellationToken cancellationToken = default)
        {
            var (aprobados, pendientes) = await _aportes.ContarAsync(zonaSlug, cancellationToken);
            return new ResumenAportesDto(aprobados + pendientes, aprobados, pendientes);
        }
    }
}
