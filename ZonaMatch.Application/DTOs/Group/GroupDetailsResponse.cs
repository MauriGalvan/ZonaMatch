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
    IReadOnlyDictionary<string, string?> Preferences,
    IReadOnlyList<GroupParticipantPointResponse> Points);

public sealed record GroupParticipantPointResponse(
    Guid Id,
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? Type,
    string? Priority,
    string? MaximumTime);

public sealed record GroupParticipantStatsResponse(
    int Criteria,
    int Poi,
    int Restrictions);

public sealed record GroupCriterionResponse(string Key, string Label);

// SuggestedMaxRent is the monthly budget: rent plus expenses, or only expenses when the suggested operation is
// buying. The suggestion follows the members' profiles (see GroupHousingSuggestion).
public sealed record GroupRestrictionsResponse(
    decimal SuggestedMaxRent,
    string TravelTimesText,
    string PoiText,
    string SuggestedOperation = "alquiler",
    decimal? SuggestedMaxPurchaseUsd = null,
    string? SuggestedPropertyType = null,
    int? SuggestedSurfaceM2 = null,
    int? SuggestedRooms = null);
