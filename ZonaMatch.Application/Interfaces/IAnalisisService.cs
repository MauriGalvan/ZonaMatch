using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IAnalisisService
    {
        // Throws LimiteAnalisisException when the user already has the maximum.
        Task<AnalisisDetalladoDto> CrearAsync(Guid usuarioId, CrearAnalisisRequest request, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AnalisisDetalladoDto>> ObtenerTodosAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Throws AnalisisNoEncontradoException when it does not exist or belongs to another user.
        Task<AnalisisDetalladoDto> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

        // Throws AnalisisNoEncontradoException when it does not exist or belongs to another user.
        Task<AnalisisDetalladoDto> ActualizarAsync(Guid id, Guid usuarioId, ActualizarAnalisisRequest request, CancellationToken cancellationToken = default);

        // Throws AnalisisNoEncontradoException when it does not exist or belongs to another user.
        Task EliminarAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);
    }
}
