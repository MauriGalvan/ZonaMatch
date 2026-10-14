namespace ZonaMatch.Application.DTOs.Group;

public sealed record GroupDetailsResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<GroupParticipantResponse> Participants,
    IReadOnlyList<GroupCriterionResponse> CriteriaList,
    GroupRestrictionsResponse Restrictions);

public sealed record GroupParticipantResponse(
    Guid Id,
    Guid UserId,
    string Name,
    bool IsMe,
    string Initials,
    string Role,
    GroupParticipantStatsResponse Stats,
    IReadOnlyDictionary<string, string?> Preferences);

public sealed record GroupParticipantStatsResponse(
    int Criteria,
    int Poi,
    int Restrictions);

public sealed record GroupCriterionResponse(string Key, string Label);

public sealed record GroupRestrictionsResponse(
    decimal SuggestedMaxRent,
    string TravelTimesText,
    string PoiText);
