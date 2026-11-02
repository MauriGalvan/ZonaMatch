using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Profile;

// In-memory repositories used by profile and saved analysis tests.
internal sealed class FakeCriterionRepository : ICriterionRepository
{
    public Task<IReadOnlyList<Criterion>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Criterion>>(
        [
            new Criterion { Id = "seguridad", Nombre = "Seguridad", Activo = true },
            new Criterion { Id = "salud", Nombre = "Salud", Activo = true },
            new Criterion { Id = "transporte", Nombre = "Transporte", Activo = false },
        ]);
}

internal sealed class FakeProfileRepository : IProfileRepository
{
    public List<Context> Profiles { get; } = [];
    public int SaveCount { get; private set; }

    public Task<Context?> GetByUserAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Profiles.FirstOrDefault(profile => profile.UsuarioId == usuarioId));

    public Task<IReadOnlyList<Context>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> usuarioIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Context>>(
            Profiles.Where(profile => usuarioIds.Contains(profile.UsuarioId)).ToList());

    public void Add(Context perfil) => Profiles.Add(perfil);

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

    public Task<IReadOnlyList<Analysis>> GetForUserAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Analysis>>(
            Analyses.Where(analysis => analysis.UsuarioId == usuarioId).OrderByDescending(analysis => analysis.FechaActualizacion).ToList());

    public Task<Analysis?> GetAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Analyses.FirstOrDefault(analysis => analysis.Id == id && analysis.UsuarioId == usuarioId));

    public Task<IReadOnlyList<Analysis>> ListVersionsAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Analysis>>(
            Analyses.Where(analysis => analysis.AnalisisOrigenId == analisisOrigenId).OrderBy(analysis => analysis.FechaCreacion).ToList());

    public void Add(Analysis analisis) => Analyses.Add(analisis);

    public void Delete(Analysis analisis) => Analyses.Remove(analisis);

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

    public static AnalysisZoneRequest AnalysisZone(string slug = FakeZonaRepository.Zona, bool isInitial = false) =>
        new() { Slug = slug, Name = "Villa Luro, CABA", IsInitial = isInitial };
}
