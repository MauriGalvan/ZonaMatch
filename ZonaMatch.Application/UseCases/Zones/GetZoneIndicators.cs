using ZonaMatch.Application.DTOs.Shared.Responses;
using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zones;

// Population, connectivity, living conditions, and safety data for a zone, with sources.
public sealed class GetZoneIndicators(IZoneIndicatorsRepository indicatorsRepository)
{
    // Year of the data loaded by etl/download_open_data.py.
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
        var cityReference = zone.HasGeolocatedCrimes
            ? await indicatorsRepository.GetCapitalCityAsync(cancellationToken)
            : null;

        return new ZoneIndicatorsResponse(
            zone.Slug,
            zone.Name,
            zone.Level,
            new ZonePopulationResponse(
                zone.Population,
                zone.PopulationDensityPerKm2,
                zone.Households,
                zone.Dwellings,
                zone.ResidentsUnder15Percentage,
                zone.ResidentsAged15To64Percentage,
                zone.ResidentsAged65PlusPercentage),
            new ZoneConnectivityResponse(
                zone.HouseholdsWithInternetPercentage,
                zone.HouseholdsWithComputerPercentage,
                zone.HouseholdsWithMobileInternetPercentage,
                cityReference is null || IsBuenosAiresCity(zone)
                    ? null
                    : new IndicatorReferenceResponse(BuenosAiresCity, cityReference.HouseholdsWithInternetPercentage)),
            new ZoneConditionsResponse(
                zone.HouseholdsWithUnmetBasicNeedsPercentage,
                zone.OvercrowdedHouseholdsPercentage,
                zone.RentingHouseholdsPercentage,
                zone.HealthCoveragePercentage,
                zone.UniversityEducationPercentage),
            BuildSafety(zone, cityReference),
            BuildSources(zone));
    }

    private static ZoneSafetyResponse? BuildSafety(ZoneIndicators zone, ZoneIndicators? cityReference)
    {
        if (zone.CrimeCount is not { } crimes || zone.CrimeDataYear is not { } year)
            return null;

        return new ZoneSafetyResponse(
            year,
            zone.CrimeDataSource ?? ZoneIndicators.CrimeMapSource,
            zone.CrimeDataCoverage,
            crimes,
            zone.CrimesPerThousandResidents,
            zone.Robberies ?? 0,
            zone.Thefts ?? 0,
            zone.Injuries ?? 0,
            zone.Homicides ?? 0,
            zone.Threats ?? 0,
            zone.TrafficIncidents ?? 0,
            cityReference is null || IsBuenosAiresCity(zone)
                ? null
                : new IndicatorReferenceResponse(BuenosAiresCity, cityReference.CrimesPerThousandResidents));
    }

    private static IReadOnlyList<DataSourceResponse> BuildSources(ZoneIndicators zone)
    {
        var sources = new List<DataSourceResponse>
        {
            new("Poblacion, hogares y conectividad", "Censo 2022 (INDEC)", "Radio censal", CensusYear),
        };

        if (zone.HasCrimeData && zone.CrimeDataYear is { } year)
        {
            sources.Add(zone.HasGeolocatedCrimes
                ? new("Seguridad", "Mapa del Delito (Buenos Aires Data)", "Hecho georreferenciado", year)
                : new("Seguridad", "SNIC (Ministerio de Seguridad de la Nación)", "Partido", year));
        }

        return sources;
    }

    private static bool IsBuenosAiresCity(ZoneIndicators zone) =>
        zone.Level == BuenosAiresCityAdminLevel;
}
