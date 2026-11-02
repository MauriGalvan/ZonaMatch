using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using CriterionRepository = ZonaMatch.Application.Interfaces.ICriterionRepository;
using DomainCriterion = ZonaMatch.Domain.Entities.Criterion;

namespace ZonaMatch.Tests.UseCases.Profile;

// In-memory repositories used by profile and saved analysis tests.
internal sealed class FakeCriterionRepository : CriterionRepository
{
    public Task<IReadOnlyList<DomainCriterion>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainCriterion>>(
        [
            new DomainCriterion { Id = "seguridad", Name = "Seguridad", IsActive = true },
            new DomainCriterion { Id = "salud", Name = "Salud", IsActive = true },
            new DomainCriterion { Id = "transporte", Name = "Transporte", IsActive = false },
        ]);
}

internal sealed class FakeProfileRepository : IProfileRepository
{
    public List<Context> Profiles { get; } = [];
    public int SaveCount { get; private set; }

    public Task<Context?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId));

    public Task<IReadOnlyList<Context>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Context>>(
            Profiles.Where(profile => userIds.Contains(profile.UserId)).ToList());

    public void Add(Context profile) => Profiles.Add(profile);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAnalysisRepository : IAnalysisRepository
{
    public List<Analysis> Analyses { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Analysis>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Analysis>>(
            Analyses.Where(analysis => analysis.UserId == userId).OrderByDescending(analysis => analysis.UpdatedAt).ToList());

    public Task<Analysis?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Analyses.FirstOrDefault(analysis => analysis.Id == id && analysis.UserId == userId));

    public Task<IReadOnlyList<Analysis>> ListVersionsAsync(Guid originalAnalysisId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Analysis>>(
            Analyses.Where(analysis => analysis.OriginalAnalysisId == originalAnalysisId).OrderBy(analysis => analysis.CreatedAt).ToList());

    public void Add(Analysis analysis) => Analyses.Add(analysis);

    public void Delete(Analysis analysis) => Analyses.Remove(analysis);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal static class TestRequests
{
    public static AnalysisCriteriaRequest AnalysisCriteria(
        Dictionary<string, string?>? criteria = null,
        int peopleCount = 1,
        List<string?>? ageRanges = null,
        List<AnalysisPlaceRequest>? places = null,
        string? rentLimit = "500000") => new()
    {
        Context = new AnalysisContextRequest
        {
            PeopleCount = peopleCount,
            AgeRanges = ageRanges ?? [],
            Mobility = ["bici"],
            Pets = ["gato"],
        },
        Criteria = criteria ?? new() { ["seguridad"] = "alta" },
        Points = new AnalysisPointsRequest { RentLimit = rentLimit, Places = places ?? [] },
    };

    public static AnalysisZoneRequest AnalysisZone(string slug = FakeZoneRepository.ZoneSlug, bool isInitial = false) =>
        new() { Slug = slug, Name = "Villa Luro, CABA", IsInitial = isInitial };
}
