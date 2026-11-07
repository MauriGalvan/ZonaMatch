using ZonaMatch.Application.DTOs.Shared.Responses;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zone;

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
            BuildPopulation(zone, cityReference),
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

    private static ZonePopulationResponse BuildPopulation(ZoneIndicators zone, ZoneIndicators? cityReference)
    {
        var isComparingCity = cityReference is not null && !IsBuenosAiresCity(zone);

        var averageAgeBadge = isComparingCity
            ? ToResponse(ZoneDemographicBenchmark.EvaluateAverageAge(zone.AverageAge, cityReference!.AverageAge))
            : null;

        var averageHouseholdSizeBadge = isComparingCity
            ? ToResponse(ZoneDemographicBenchmark.EvaluateAverageHouseholdSize(zone.AverageHouseholdSize, cityReference!.AverageHouseholdSize))
            : null;

        var singlePersonHouseholdsBadge = isComparingCity
            ? ToResponse(ZoneDemographicBenchmark.EvaluateSinglePersonHouseholds(zone.SinglePersonHouseholdsPercentage, cityReference!.SinglePersonHouseholdsPercentage))
            : null;

        var childrenUnder15Badge = isComparingCity
            ? ToResponse(ZoneDemographicBenchmark.EvaluateChildrenUnder15(zone.ResidentsUnder15Percentage, cityReference!.ResidentsUnder15Percentage))
            : null;

        var seniorsOver65Badge = isComparingCity
            ? ToResponse(ZoneDemographicBenchmark.EvaluateSeniorsOver65(zone.ResidentsAged65PlusPercentage, cityReference!.ResidentsAged65PlusPercentage))
            : null;

        var ageDistribution = new ZoneAgeDistributionResponse(
            zone.ResidentsAged0To4Percentage,
            zone.ResidentsAged5To14Percentage,
            zone.ResidentsAged15To29Percentage,
            zone.ResidentsAged30To44Percentage,
            zone.ResidentsAged45To64Percentage,
            zone.ResidentsOver65Percentage);

        return new ZonePopulationResponse(
            zone.Population,
            zone.Households,
            zone.Dwellings,
            zone.AverageAge,
            averageAgeBadge,
            zone.AverageHouseholdSize,
            averageHouseholdSizeBadge,
            zone.SinglePersonHouseholdsPercentage,
            singlePersonHouseholdsBadge,
            childrenUnder15Badge,
            seniorsOver65Badge,
            ageDistribution,
            zone.PopulationDensityPerKm2);
    }

    private static DemographicBadgeResponse? ToResponse(DemographicBadge? badge) =>
        badge is null ? null : new DemographicBadgeResponse(badge.Label, badge.Variant);

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
