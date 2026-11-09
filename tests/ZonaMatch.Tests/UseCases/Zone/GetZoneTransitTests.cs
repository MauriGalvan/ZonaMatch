using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZoneTransitTests
{
    private const string ZoneSlug = FakeZoneRepository.ZoneSlug;

    private readonly FakeTransitZoneRepository _transitRepository = new();
    private readonly GetZoneTransit _useCase;

    public GetZoneTransitTests()
    {
        _useCase = new GetZoneTransit(_transitRepository, new FakeZoneRepository());
    }

    private void AddDepartures(string mode, string line, string timeWindow, int departures, int stops = 3) =>
        _transitRepository.Departures.Add(new TransitLineDepartures
        {
            Mode = mode,
            Line = line,
            TimeWindow = timeWindow,
            DepartureCount = departures,
            StopCount = stops
        });

    [Fact]
    public async Task GetZoneTransit_ReturnsModesInDisplayOrder()
    {
        _transitRepository.Stops.AddRange([
            new ZoneTransitModeStops { Mode = "colectivo", StopCount = 90, LineCount = 25 },
            new ZoneTransitModeStops { Mode = "tren", StopCount = 1, LineCount = 1 },
        ]);

        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Equal(["tren", "colectivo"], result.Modes.Select(mode => mode.Mode));
        Assert.Equal(2, result.Sources.Count);
    }

    [Fact]
    public async Task GetZoneTransit_SumsEachTimeWindowAndSortsLinesByPeakDepartures()
    {
        _transitRepository.Stops.Add(new ZoneTransitModeStops { Mode = "colectivo", StopCount = 90, LineCount = 2 });
        AddDepartures("colectivo", "2", "manana", 30);
        AddDepartures("colectivo", "47", "manana", 45);
        AddDepartures("colectivo", "47", "noche", 0);

        var buses = Assert.Single((await _useCase.ExecuteAsync(ZoneSlug)).Modes);

        var morning = buses.TimeWindows.Single(window => window.TimeWindow == "manana");
        Assert.Equal(2, morning.ActiveLines);
        Assert.Equal(25.0, morning.DeparturesPerHour);
        Assert.Equal(0, buses.TimeWindows.Single(window => window.TimeWindow == "noche").ActiveLines);
        Assert.Equal(["47", "2"], buses.Lines.Select(line => line.Name));
        var line47Morning = buses.Lines[0].Frequencies.Single(window => window.TimeWindow == "manana");
        Assert.Equal(15.0, line47Morning.DeparturesPerHour);
        Assert.Equal(4, line47Morning.MinutesBetweenDepartures);
    }

    [Fact]
    public async Task GetZoneTransit_WhenZoneHasNoStops_ReturnsEmptyTransitData()
    {
        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Empty(result.Modes);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task GetZoneTransit_WhenZoneDoesNotExist_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe"));
    }

    [Fact]
    public async Task GetZoneTransit_WhenSlugIsInvalid_ThrowsBadRequestWithoutQueryingTheRepository()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync("Villa Luro"));

        Assert.Equal(0, _transitRepository.StopListCallCount);
    }

    [Fact]
    public async Task GetZoneTransit_WhenRepositoryReturnsAnUnknownMode_DoesNotIncludeAnUnknownDataSource()
    {
        _transitRepository.Stops.Add(new ZoneTransitModeStops { Mode = "ferry", StopCount = 2, LineCount = 1 });

        var result = await _useCase.ExecuteAsync(ZoneSlug);

        Assert.Equal("ferry", Assert.Single(result.Modes).Mode);
        Assert.Empty(result.Sources);
    }

    private sealed class FakeTransitZoneRepository : IZoneTransitRepository
    {
        public List<ZoneTransitModeStops> Stops { get; } = [];
        public List<TransitLineDepartures> Departures { get; } = [];
        public int StopListCallCount { get; private set; }

        public Task<IReadOnlyList<ZoneTransitModeStops>> ListStopsAsync(string slug, CancellationToken cancellationToken = default)
        {
            StopListCallCount++;
            return Task.FromResult<IReadOnlyList<ZoneTransitModeStops>>(slug == ZoneSlug ? Stops : []);
        }

        public Task<IReadOnlyList<TransitLineDepartures>> ListDeparturesAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TransitLineDepartures>>(slug == ZoneSlug ? Departures : []);

        public Task<IReadOnlyList<TransitRouteGeometry>> ListRoutesAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TransitRouteGeometry>>([]);
    }
}
