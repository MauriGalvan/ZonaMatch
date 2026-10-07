using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    // Community of a zone. "usuarioActual" is null for anonymous requests (only used to mark what is theirs).
    // Writes throw NoEncontradoException for unknown zones or items and OperacionNoPermitidaException
    // when a rule forbids the action.

    public interface IResenaService
    {
        Task<ResenasZonaDto> ListarAsync(string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default);

        // Creates the user's review of the zone or replaces it
        Task<ResenaDto> GuardarMiaAsync(string zonaSlug, SesionDto usuario, GuardarResenaDto request, CancellationToken cancellationToken = default);

        Task EliminarMiaAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default);

        // Marks or unmarks a review of another user as useful
        Task MarcarUtilAsync(string zonaSlug, Guid resenaId, Guid usuarioId, bool util, CancellationToken cancellationToken = default);
    }

    public interface IPreguntaService
    {
        Task<IReadOnlyList<PreguntaDto>> ListarAsync(string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default);

        Task<PreguntaDto> CrearAsync(string zonaSlug, SesionDto usuario, CrearPreguntaDto request, CancellationToken cancellationToken = default);

        Task<RespuestaDto> ResponderAsync(string zonaSlug, Guid preguntaId, SesionDto usuario, CrearRespuestaDto request, CancellationToken cancellationToken = default);
    }

    public interface IAporteService
    {
        Task<ResumenAportesDto> ResumirAsync(string zonaSlug, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AporteDto>> ListarPendientesAsync(string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default);

        Task<AporteDto> CrearPuntoAsync(string zonaSlug, SesionDto usuario, CrearPuntoNuevoDto request, CancellationToken cancellationToken = default);

        Task<AporteDto> CrearCorreccionAsync(string zonaSlug, SesionDto usuario, CrearCorreccionDto request, CancellationToken cancellationToken = default);

        // Confirms or rejects a pending contribution of another user; may approve or reject it
        Task<AporteDto> ValidarAsync(string zonaSlug, Guid aporteId, Guid usuarioId, bool confirma, CancellationToken cancellationToken = default);
    }
}
