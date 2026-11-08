using ZonaMatch.Application.DTOs.Shared.Responses;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Zone;

// Population, connectivity, living conditions, and safety data for a zone, with sources.
public sealed class GetZoneIndicators(
    IZoneIndicatorsRepository indicatorsRepository,
    IZoneProfilesRepository? profilesRepository = null)
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

        int? safetyPercentile = null;
        if (profilesRepository is not null && zone.CrimesPerThousandResidents is not null)
        {
            var zoneUniverse = await profilesRepository.ListAsync(cancellationToken);
            if (zoneUniverse.Count > 0)
            {
                var scoringEngine = new ScoringEngine(zoneUniverse);
                safetyPercentile = scoringEngine.CalculateSafetyPercentile(zone.CrimesPerThousandResidents, zone.Level <= 5);
            }
        }

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
                zone.UniversityEducationPercentage,
                new ZoneInfrastructureConditionsResponse(
                    zone.HouseholdsWithPipedWaterPercentage,
                    zone.HouseholdsWithSewerPercentage,
                    zone.HouseholdsWithGasNetworkPercentage,
                    zone.HouseholdsWithPipedWater,
                    zone.HouseholdsWithSewer,
                    zone.HouseholdsWithGasNetwork),
                new ZoneHabitabilityConditionsResponse(
                    zone.HouseholdsWithGoodHousingQualityPercentage,
                    zone.OvercrowdedHouseholdsPercentage,
                    zone.RentingHouseholdsPercentage,
                    zone.HouseholdsWithUnmetBasicNeedsPercentage,
                    zone.HouseholdsWithGoodHousingQuality,
                    zone.OvercrowdedHouseholds,
                    zone.RentingHouseholds,
                    zone.HouseholdsWithUnmetBasicNeeds),
                new ZoneSocialConditionsResponse(
                    zone.HealthCoveragePercentage,
                    zone.SecondaryEducationPercentage,
                    zone.UniversityEducationPercentage,
                    zone.ResidentsWithHealthCoverage,
                    zone.ResidentsWithSecondaryEducation,
                    zone.ResidentsWithUniversityEducation)),
            BuildSafety(zone, cityReference, safetyPercentile),
            BuildSources(zone));
    }

    private static ZonePopulationResponse BuildPopulation(ZoneIndicators zone, ZoneIndicators? cityReference)
    {
        var isComparingCity = cityReference is not null && !IsBuenosAiresCity(zone);
        var effectiveAverageAge = zone.CalculatedAverageAge;
        var cityEffectiveAverageAge = cityReference?.CalculatedAverageAge;

        var averageAgeBadge = isComparingCity
            ? ToResponse(ZoneDemographicBenchmark.EvaluateAverageAge(effectiveAverageAge, cityEffectiveAverageAge))
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
            effectiveAverageAge,
            averageAgeBadge,
            zone.AverageHouseholdSize,
            averageHouseholdSizeBadge,
            zone.SinglePersonHouseholdsPercentage,
            singlePersonHouseholdsBadge,
            childrenUnder15Badge,
            seniorsOver65Badge,
            ageDistribution,
            zone.PopulationDensityPerKm2,
            zone.ResidentsUnder15Percentage,
            zone.ResidentsAged15To64Percentage,
            zone.ResidentsAged65PlusPercentage);
    }

    private static DemographicBadgeResponse? ToResponse(DemographicBadge? badge) =>
        badge is null ? null : new DemographicBadgeResponse(badge.Label, badge.Variant);

    private static ZoneSafetyResponse? BuildSafety(
        ZoneIndicators zone,
        ZoneIndicators? cityReference,
        int? percentile)
    {
        if (zone.CrimeCount is not { } crimes || zone.CrimeDataYear is not { } year)
            return null;

        var isCaba = zone.HasGeolocatedCrimes;
        var cityAverageRate = isCaba
            ? (cityReference?.CrimesPerThousandResidents ?? ZoneSafetyBenchmark.DefaultCityAverageRate)
            : (double?)null;

        var robberies = zone.Robberies ?? 0;
        var thefts = zone.Thefts ?? 0;
        var injuries = zone.Injuries ?? 0;
        var threats = zone.Threats ?? 0;
        var homicides = zone.Homicides ?? 0;
        var roadIncidents = zone.TrafficIncidents ?? 0;

        var evaluation = ZoneSafetyBenchmark.Evaluate(
            zone.CrimesPerThousandResidents,
            percentile,
            isCaba,
            cityAverageRate,
            robberies,
            thefts,
            injuries,
            threats,
            homicides,
            roadIncidents);

        if (evaluation is null)
            return null;

        var badgeResponse = new SafetyBadgeResponse(evaluation.Badge.Label, evaluation.Badge.Variant);
        var breakdownResponse = evaluation.Breakdown
            .Select(b => new SafetyBreakdownItemResponse(b.Category, b.Count, b.Percentage))
            .ToList();
        var roadIncidentsResponse = new SafetyRoadIncidentsResponse(roadIncidents);

        var referenceResponse = isCaba && !IsBuenosAiresCity(zone) && cityAverageRate is not null
            ? new IndicatorReferenceResponse(BuenosAiresCity, cityAverageRate)
            : null;

        var totalCrimes = evaluation.TotalCrimes > 0 ? evaluation.TotalCrimes : crimes;

        return new ZoneSafetyResponse(
            evaluation.Score,
            evaluation.MaxScore,
            badgeResponse,
            evaluation.Summary,
            evaluation.CrimesPerThousandResidents,
            cityAverageRate,
            totalCrimes,
            evaluation.DominantCategoryText,
            breakdownResponse,
            roadIncidentsResponse,
            evaluation.Percentile,
            year,
            zone.CrimeDataSource ?? ZoneIndicators.CrimeMapSource,
            zone.CrimeDataCoverage,
            totalCrimes,
            zone.CrimesPerThousandResidents,
            robberies,
            thefts,
            injuries,
            homicides,
            threats,
            referenceResponse);
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
