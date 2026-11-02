using ZonaMatch.Application.DTOs.Shared.Responses;
using ZonaMatch.Application.DTOs.Transit.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zones;

// Public transport stops, routes, and frequencies by time window for a zone.
public sealed class GetZoneTransit(
    IZoneTransitRepository transitRepository,
    IZoneRepository zoneRepository)
{
    // Display order for transport modes.
    private static readonly string[] ModeOrder = ["subte", "tren", "colectivo"];

    public async Task<ZoneTransitResponse> ExecuteAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var stops = await transitRepository.ListStopsAsync(slug, cancellationToken);

        // No stops may mean the zone has no transit service or does not exist.
        if (stops.Count == 0 && !await zoneRepository.ExistsAsync(slug, cancellationToken))
            throw new NotFoundException("No se encontro una zona con ese identificador.");

        var departures = stops.Count == 0
            ? []
            : await transitRepository.ListDeparturesAsync(slug, cancellationToken);

        var modes = stops
            .OrderBy(stop => Array.IndexOf(ModeOrder, stop.Mode) is var order and >= 0 ? order : ModeOrder.Length)
            .Select(stop => BuildMode(stop, departures.Where(departure => departure.Mode == stop.Mode).ToList()))
            .ToList();

        return new ZoneTransitResponse(slug, modes, BuildSources(modes));
    }

    private static TransitModeResponse BuildMode(
        ZoneTransitModeStops stops,
        IReadOnlyList<TransitLineDepartures> departures)
    {
        var timeWindows = TimeWindow.All
            .Select(timeWindow =>
            {
                var windowDepartures = departures
                    .Where(departure => departure.TimeWindow == timeWindow.Code && departure.DepartureCount > 0)
                    .ToList();
                return new TransitTimeWindowResponse(
                    timeWindow.Code,
                    timeWindow.StartHour,
                    timeWindow.EndHour,
                    windowDepartures.Select(departure => departure.Line).Distinct().Count(),
                    timeWindow.DeparturesPerHour(windowDepartures.Sum(departure => departure.DepartureCount)));
            })
            .ToList();

        var lines = departures
            .GroupBy(departure => departure.Line)
            .Select(line => new
            {
                Name = line.Key,
                Stops = line.Max(departure => departure.StopCount),
                MorningPeak = line
                    .Where(departure => departure.TimeWindow == TimeWindow.Morning.Code)
                    .Sum(departure => departure.DepartureCount),
                TimeWindows = TimeWindow.All
                    .Select(timeWindow =>
                    {
                        var count = line
                            .Where(departure => departure.TimeWindow == timeWindow.Code)
                            .Sum(departure => departure.DepartureCount);
                        return new TransitLineFrequencyResponse(
                            timeWindow.Code,
                            timeWindow.DeparturesPerHour(count),
                            timeWindow.MinutesBetweenDepartures(count));
                    })
                    .ToList(),
            })
            .OrderByDescending(line => line.MorningPeak)
            .ThenBy(line => line.Name, StringComparer.Ordinal)
            .Select(line => new TransitLineResponse(line.Name, line.Stops, line.TimeWindows))
            .ToList();

        return new TransitModeResponse(stops.Mode, stops.StopCount, stops.LineCount, timeWindows, lines);
    }

    private static IReadOnlyList<DataSourceResponse> BuildSources(IReadOnlyList<TransitModeResponse> modes)
    {
        var sourcesByMode = new Dictionary<string, (string Source, int Year)>
        {
            ["colectivo"] = ("GTFS de colectivos (Buenos Aires Data)", 2019),
            ["tren"] = ("GTFS de trenes (Buenos Aires Data)", 2020),
            ["subte"] = ("GTFS de subte (Buenos Aires Data)", 2019),
        };

        return modes
            .Where(mode => sourcesByMode.ContainsKey(mode.Mode))
            .Select(mode => new DataSourceResponse(
                "Transporte",
                sourcesByMode[mode.Mode].Source,
                "Parada",
                sourcesByMode[mode.Mode].Year))
            .ToList();
    }
}
