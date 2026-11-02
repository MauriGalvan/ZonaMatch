using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zones;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases;

namespace ZonaMatch.Tests.UseCases.Zones;

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

    private sealed class FakeTransitZoneRepository : IZoneTransitRepository
    {
        public List<ZoneTransitModeStops> Stops { get; } = [];
        public List<TransitLineDepartures> Departures { get; } = [];

        public Task<IReadOnlyList<ZoneTransitModeStops>> ListStopsAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ZoneTransitModeStops>>(slug == ZoneSlug ? Stops : []);

        public Task<IReadOnlyList<TransitLineDepartures>> ListDeparturesAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TransitLineDepartures>>(slug == ZoneSlug ? Departures : []);
    }
}
