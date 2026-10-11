using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ICriterioRepository
    {
        // Todos los criterios del catalogo, en orden de aparicion en el wizard.
        // Incluye los inactivos: la validacion los distingue de los desconocidos.
        Task<IReadOnlyList<Criterio>> ListarAsync(CancellationToken cancellationToken = default);
    }
}
