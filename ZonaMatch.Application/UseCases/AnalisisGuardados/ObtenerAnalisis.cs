using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados
{
    // Returns one of the user's analyses and, for originals, its versions.
    public class ObtenerAnalisis
    {
        private readonly IAnalisisRepository _analisis;

        public ObtenerAnalisis(IAnalisisRepository analisis)
        {
            _analisis = analisis;
        }

        public async Task<AnalysisResponse> EjecutarAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var analisis = await _analisis.ObtenerAsync(id, usuarioId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var versiones = analisis.AnalisisOrigenId is null
                ? await _analisis.ListarVersionesAsync(analisis.Id, cancellationToken)
                : [];

            return AnalisisMapeo.ToResponse(analisis, versiones);
        }
    }
}
