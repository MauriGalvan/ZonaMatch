namespace ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

public sealed record ZoneSocialConditionsResponse(
    double? HealthCoveragePercentage,
    double? SecondaryEducationPercentage,
    double? UniversityEducationPercentage,
    int? HealthCoverageResidents = null,
    int? SecondaryEducationResidents = null,
    int? UniversityEducationResidents = null);
