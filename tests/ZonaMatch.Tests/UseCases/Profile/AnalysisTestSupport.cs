using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Profile;

// In-memory repositories used by profile and saved analysis tests.
internal sealed class FakeCriterionRepository : ICriterioRepository
{
    public Task<IReadOnlyList<Criterio>> ListarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Criterio>>(
        [
            new Criterio { Id = "seguridad", Nombre = "Seguridad", Activo = true },
            new Criterio { Id = "salud", Nombre = "Salud", Activo = true },
            new Criterio { Id = "transporte", Nombre = "Transporte", Activo = false },
        ]);
}

internal sealed class FakeProfileRepository : IPerfilRepository
{
    public List<Contexto> Profiles { get; } = [];
    public int SaveCount { get; private set; }

    public Task<Contexto?> ObtenerDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Profiles.FirstOrDefault(profile => profile.UsuarioId == usuarioId));

    public Task<IReadOnlyList<Contexto>> ObtenerDeUsuariosAsync(
        IReadOnlyCollection<Guid> usuarioIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Contexto>>(
            Profiles.Where(profile => usuarioIds.Contains(profile.UsuarioId)).ToList());

    public void Agregar(Contexto perfil) => Profiles.Add(perfil);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAnalysisRepository : IAnalisisRepository
{
    public List<Analisis> Analyses { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Analisis>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Analisis>>(
            Analyses.Where(analysis => analysis.UsuarioId == usuarioId).OrderByDescending(analysis => analysis.FechaActualizacion).ToList());

    public Task<Analisis?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Analyses.FirstOrDefault(analysis => analysis.Id == id && analysis.UsuarioId == usuarioId));

    public Task<IReadOnlyList<Analisis>> ListarVersionesAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Analisis>>(
            Analyses.Where(analysis => analysis.AnalisisOrigenId == analisisOrigenId).OrderBy(analysis => analysis.FechaCreacion).ToList());

    public void Agregar(Analisis analisis) => Analyses.Add(analisis);

    public void Eliminar(Analisis analisis) => Analyses.Remove(analisis);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
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
