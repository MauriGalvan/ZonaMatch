using ZonaMatch.Application.DTOs.Shared.Responses;
using ZonaMatch.Application.DTOs.ZoneIndicators.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zones;

// Population, connectivity, living conditions, and safety data for a zone, with sources.
public sealed class GetZoneIndicators(IZoneIndicatorsRepository indicatorsRepository)
{
    // Year of the data loaded by etl/descargar_datos.py.
    public const int CensusYear = 2022;

    private const string BuenosAiresCity = "CABA";
    private const int BuenosAiresCityAdminLevel = 4;

    public async Task<ZoneIndicatorsResponse> ExecuteAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);

        var zone = await indicatorsRepository.GetBySlugAsync(slug, cancellationToken)
            ?? throw new NotFoundException("Todavia no hay datos de censo ni de seguridad para esta zona.");

        // Zones with geolocated crimes are compared against the whole city.
        var cityReference = zone.DelitosGeorreferenciados
            ? await indicatorsRepository.GetCapitalCityAsync(cancellationToken)
            : null;

        return new ZoneIndicatorsResponse(
            zone.Slug,
            zone.Nombre,
            zone.Nivel,
            new ZonePopulationResponse(
                zone.Poblacion,
                zone.DensidadHabitantesKm2,
                zone.Hogares,
                zone.Viviendas,
                zone.PorcentajeMenores15,
                zone.Porcentaje15a64,
                zone.Porcentaje65yMas),
            new ZoneConnectivityResponse(
                zone.PorcentajeHogaresInternet,
                zone.PorcentajeHogaresComputadora,
                zone.PorcentajeHogaresCelularInternet,
                cityReference is null || IsBuenosAiresCity(zone)
                    ? null
                    : new IndicatorReferenceResponse(BuenosAiresCity, cityReference.PorcentajeHogaresInternet)),
            new ZoneConditionsResponse(
                zone.PorcentajeHogaresNbi,
                zone.PorcentajeHogaresHacinamiento,
                zone.PorcentajeHogaresAlquilan,
                zone.PorcentajeCoberturaSalud,
                zone.PorcentajeUniversitario),
            BuildSafety(zone, cityReference),
            BuildSources(zone));
    }

    private static ZoneSafetyResponse? BuildSafety(ZoneIndicators zone, ZoneIndicators? cityReference)
    {
        if (zone.Delitos is not { } crimes || zone.DelitosPeriodo is not { } year)
            return null;

        return new ZoneSafetyResponse(
            year,
            zone.DelitosFuente ?? ZoneIndicators.FuenteMapaDelito,
            zone.DelitosAmbito,
            crimes,
            zone.DelitosCada1000Habitantes,
            zone.Robos ?? 0,
            zone.Hurtos ?? 0,
            zone.Lesiones ?? 0,
            zone.Homicidios ?? 0,
            zone.Amenazas ?? 0,
            zone.SiniestrosViales ?? 0,
            cityReference is null || IsBuenosAiresCity(zone)
                ? null
                : new IndicatorReferenceResponse(BuenosAiresCity, cityReference.DelitosCada1000Habitantes));
    }

    private static IReadOnlyList<DataSourceResponse> BuildSources(ZoneIndicators zone)
    {
        var sources = new List<DataSourceResponse>
        {
            new("Poblacion, hogares y conectividad", "Censo 2022 (INDEC)", "Radio censal", CensusYear),
        };

        if (zone.TieneDelitos && zone.DelitosPeriodo is { } year)
        {
            sources.Add(zone.DelitosGeorreferenciados
                ? new("Seguridad", "Mapa del Delito (Buenos Aires Data)", "Hecho georreferenciado", year)
                : new("Seguridad", "SNIC (Ministerio de Seguridad de la Nación)", "Partido", year));
        }

        return sources;
    }

    private static bool IsBuenosAiresCity(ZoneIndicators zone) =>
        zone.Nivel == BuenosAiresCityAdminLevel;
}
