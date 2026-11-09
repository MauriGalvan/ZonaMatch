using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Tests.UseCases.Community;

// Stores entities by reference, as tracked entities: SaveChangesAsync only counts calls.
internal sealed class FakeContributionRepository : IContributionRepository
{
    public List<DomainContribution> Contributions { get; } = [];
    public int SaveCount { get; private set; }

    public Task<(int ApprovedCount, int PendingCount)> CountAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult((
            Contributions.Count(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Approved),
            Contributions.Count(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Pending)));

    public Task<IReadOnlyList<DomainContribution>> ListPendingAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainContribution>>(
            Contributions.Where(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Pending).ToList());

    public Task<DomainContribution?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Contributions.FirstOrDefault(a => a.ZoneSlug == zoneSlug && a.Id == id));

    public void Add(DomainContribution contribution) => Contributions.Add(contribution);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
