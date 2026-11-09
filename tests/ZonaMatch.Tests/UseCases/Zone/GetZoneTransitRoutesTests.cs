using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZoneTransitRoutesTests
{
    private const string ZoneSlug = FakeZoneRepository.ZoneSlug;

    private readonly FakeRoutesRepository _transitRepository = new();
    private readonly GetZoneTransitRoutes _useCase;

    public GetZoneTransitRoutesTests()
    {
        _useCase = new GetZoneTransitRoutes(_transitRepository, new FakeZoneRepository());
    }

    private void AddRoute(string mode, string line, string geometry) =>
        _transitRepository.Routes.Add(new TransitRouteGeometry { Mode = mode, Line = line, GeometryGeoJson = geometry });

    [Fact]
    public async Task ExecuteAsync_WhenZoneHasLines_ReturnsPathsAsLatitudeLongitude()
    {
        AddRoute("colectivo", "110", """{"type":"LineString","coordinates":[[-58.42,-34.58],[-58.41,-34.57]]}""");

        var result = await _useCase.ExecuteAsync(ZoneSlug);

        var route = Assert.Single(result.Routes);
        Assert.Equal(ZoneSlug, result.Slug);
        Assert.Equal("110", route.Line);
        var path = Assert.Single(route.Paths);
        Assert.Equal([-34.58, -58.42], path[0]);
        Assert.Equal([-34.57, -58.41], path[1]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLineIsSplit_ReturnsOnePathPerPart()
    {
        AddRoute("tren", "Mitre", """{"type":"MultiLineString","coordinates":[[[-58.4,-34.5],[-58.3,-34.4]],[[-58.2,-34.3],[-58.1,-34.2]]]}""");

        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Equal(2, Assert.Single(result.Routes).Paths.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenModesAndLinesAreMixed_SortsRailFirstAndLinesByNumber()
    {
        const string Line = """{"type":"LineString","coordinates":[[-58.4,-34.5],[-58.3,-34.4]]}""";
        AddRoute("colectivo", "152", Line);
        AddRoute("colectivo", "29", Line);
        AddRoute("tren", "Mitre", Line);
        AddRoute("subte", "D", Line);

        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Equal(["D", "Mitre", "29", "152"], result.Routes.Select(route => route.Line));
    }

    [Fact]
    public async Task ExecuteAsync_WhenGeometryIsNotALine_SkipsIt()
    {
        AddRoute("colectivo", "60", """{"type":"Point","coordinates":[-58.4,-34.5]}""");

        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Empty(result.Routes);
    }

    [Fact]
    public async Task ExecuteAsync_WhenZoneHasNoLines_ReturnsEmptyList()
    {
        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Empty(result.Routes);
    }

    [Fact]
    public async Task ExecuteAsync_WhenZoneDoesNotExist_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe"));
    }

    private sealed class FakeRoutesRepository : IZoneTransitRepository
    {
        public List<TransitRouteGeometry> Routes { get; } = [];

        public Task<IReadOnlyList<ZoneTransitModeStops>> ListStopsAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ZoneTransitModeStops>>([]);

        public Task<IReadOnlyList<TransitLineDepartures>> ListDeparturesAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TransitLineDepartures>>([]);

        public Task<IReadOnlyList<TransitRouteGeometry>> ListRoutesAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TransitRouteGeometry>>(slug == ZoneSlug ? Routes : []);
    }
}
