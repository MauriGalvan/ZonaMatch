using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Confirms or rejects another user's pending contribution; the vote can approve or reject it.
    public class ValidarAporte
    {
        private readonly IAporteRepository _aportes;
        private readonly TimeProvider _reloj;

        public ValidarAporte(IAporteRepository aportes, TimeProvider reloj)
        {
            _aportes = aportes;
            _reloj = reloj;
        }

        public async Task<AporteDto> EjecutarAsync(
            string zonaSlug, Guid aporteId, Guid usuarioId, bool confirma, CancellationToken cancellationToken = default)
        {
            var aporte = await _aportes.ObtenerAsync(zonaSlug, aporteId, cancellationToken)
                ?? throw new NotFoundException("El aporte no existe.");

            if (aporte.Estado != EstadoAporte.Pendiente)
                throw new ConflictException("El aporte ya fue resuelto por otros vecinos.");

            if (aporte.UsuarioId == usuarioId)
                throw new ConflictException("No podes validar tu propio aporte.");

            aporte.Validar(usuarioId, confirma, _reloj.GetUtcNow());
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return AporteMapeo.ADto(aporte, aporte.Usuario?.Email, usuarioId);
        }
    }
}
