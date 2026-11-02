using ZonaMatch.Application.DTOs;
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

    public async Task<TransporteZonaDto> ExecuteAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var stops = await transitRepository.ListarParadasAsync(slug, cancellationToken);

        // No stops may mean the zone has no transit service or does not exist.
        if (stops.Count == 0 && !await zoneRepository.ExistsAsync(slug, cancellationToken))
            throw new NotFoundException("No se encontro una zona con ese identificador.");

        var departures = stops.Count == 0
            ? []
            : await transitRepository.ListarSalidasAsync(slug, cancellationToken);

        var modes = stops
            .OrderBy(stop => Array.IndexOf(ModeOrder, stop.Modo) is var order and >= 0 ? order : ModeOrder.Length)
            .Select(stop => BuildMode(stop, departures.Where(departure => departure.Modo == stop.Modo).ToList()))
            .ToList();

        return new TransporteZonaDto(slug, modes, BuildSources(modes));
    }

    private static ModoTransporteDto BuildMode(
        ZoneTransitModeStops stops,
        IReadOnlyList<TransitLineDepartures> departures)
    {
        var timeWindows = TimeWindow.All
            .Select(timeWindow =>
            {
                var windowDepartures = departures
                    .Where(departure => departure.Franja == timeWindow.Code && departure.Salidas > 0)
                    .ToList();
                return new FranjaTransporteDto(
                    timeWindow.Code,
                    timeWindow.StartHour,
                    timeWindow.EndHour,
                    windowDepartures.Select(departure => departure.Linea).Distinct().Count(),
                    timeWindow.DeparturesPerHour(windowDepartures.Sum(departure => departure.Salidas)));
            })
            .ToList();

        var lines = departures
            .GroupBy(departure => departure.Linea)
            .Select(line => new
            {
                Name = line.Key,
                Stops = line.Max(departure => departure.Paradas),
                MorningPeak = line
                    .Where(departure => departure.Franja == TimeWindow.Morning.Code)
                    .Sum(departure => departure.Salidas),
                TimeWindows = TimeWindow.All
                    .Select(timeWindow =>
                    {
                        var count = line
                            .Where(departure => departure.Franja == timeWindow.Code)
                            .Sum(departure => departure.Salidas);
                        return new FrecuenciaLineaDto(
                            timeWindow.Code,
                            timeWindow.DeparturesPerHour(count),
                            timeWindow.MinutesBetweenDepartures(count));
                    })
                    .ToList(),
            })
            .OrderByDescending(line => line.MorningPeak)
            .ThenBy(line => line.Name, StringComparer.Ordinal)
            .Select(line => new LineaTransporteDto(line.Name, line.Stops, line.TimeWindows))
            .ToList();

        return new ModoTransporteDto(stops.Modo, stops.Paradas, stops.Lineas, timeWindows, lines);
    }

    private static IReadOnlyList<FuenteDatoDto> BuildSources(IReadOnlyList<ModoTransporteDto> modes)
    {
        var sourcesByMode = new Dictionary<string, (string Source, int Year)>
        {
            ["colectivo"] = ("GTFS de colectivos (Buenos Aires Data)", 2019),
            ["tren"] = ("GTFS de trenes (Buenos Aires Data)", 2020),
            ["subte"] = ("GTFS de subte (Buenos Aires Data)", 2019),
        };

        return modes
            .Where(mode => sourcesByMode.ContainsKey(mode.Modo))
            .Select(mode => new FuenteDatoDto(
                "Transporte",
                sourcesByMode[mode.Modo].Source,
                "Parada",
                sourcesByMode[mode.Modo].Year))
            .ToList();
    }
}
