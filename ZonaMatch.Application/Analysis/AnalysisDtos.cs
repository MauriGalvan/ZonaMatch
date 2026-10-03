using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Analysis
{
    public sealed record CriterionRequest(string Code, Priority Priority);

    // Importance: 1 (baja) a 4 (muy importante); pondera junto con la frecuencia (PBI 30a)
    public sealed record ImportantPointRequest(
        string Name,
        double Latitude,
        double Longitude,
        TravelMode Mode,
        int TripsPerWeek,
        int? MaxMinutes,
        int Importance = 2);

    // un análisis individual es un grupo de un solo participante
    public sealed record ParticipantRequest(
        string Name,
        double Weight,
        IReadOnlyList<CriterionRequest> Criteria,
        IReadOnlyList<ImportantPointRequest> Points);

    public sealed record RankingRequest(
        IReadOnlyList<string> ZoneSlugs,
        IReadOnlyList<ParticipantRequest> Participants,
        int RadiusMeters,
        double? MaxRent,
        bool IncludeSuggestion);

    public sealed record CriterionResultDto(string Code, string Name, double? Score, string DataNote);

    public sealed record ParticipantResultDto(string Name, double Score, double WeeklyTravelMinutes);

    public sealed record RestrictionResultDto(
        string Kind,
        string Label,
        string? Participant,
        string Status,
        double? Actual,
        double Limit);

    public sealed record RankedZoneDto(
        int Position,
        string Slug,
        string Name,
        string ParentName,
        double Score,
        double Coverage,
        IReadOnlyList<CriterionResultDto> Breakdown,
        IReadOnlyList<ParticipantResultDto> Participants,
        IReadOnlyList<RestrictionResultDto> Restrictions);

    public sealed record RankingResultDto(
        int RadiusMeters,
        IReadOnlyList<RankedZoneDto> Zones,
        RankedZoneDto? Suggestion);

    public sealed record CriterionInfoDto(string Code, string Name);
}
