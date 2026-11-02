using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ICriterionRepository
    {
        // All catalog criteria, in the order shown in the wizard.
        // Inactive criteria are included so validation can distinguish them from unknown criteria.
        Task<IReadOnlyList<Criterion>> ListAsync(CancellationToken cancellationToken = default);
    }
}
