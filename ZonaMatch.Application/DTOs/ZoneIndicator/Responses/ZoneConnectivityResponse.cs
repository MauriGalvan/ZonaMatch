namespace ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

// When the zone is in CABA, the reference contains the city-level value for comparison.
public sealed record ZoneConnectivityResponse(
    double? HouseholdsWithInternetPercentage,
    double? HouseholdsWithComputerPercentage,
    double? HouseholdsWithMobileInternetPercentage,
    IndicatorReferenceResponse? Reference);
