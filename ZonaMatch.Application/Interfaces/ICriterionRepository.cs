using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ICriterionRepository
    {
        // Todos los criterios del catalogo, en orden de aparicion en el wizard.
        // Incluye los inactivos: la validacion los distingue de los desconocidos.
        Task<IReadOnlyList<Criterion>> ListAsync(CancellationToken cancellationToken = default);
    }
}
