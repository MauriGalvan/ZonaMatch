namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/zones/activity: the zones users save the most and the least, and the ones with the most reviews,
// questions and contributions. LeastSaved only has zones saved at least once. Reviews count the approved and pending
// ones (not the rejected); contributions count every status.
public sealed record AdminZoneActivityResponse(
    IReadOnlyList<AdminZoneCount> MostSaved,
    IReadOnlyList<AdminZoneCount> LeastSaved,
    IReadOnlyList<AdminZoneCount> MostReviewed,
    IReadOnlyList<AdminZoneCount> MostAsked,
    IReadOnlyList<AdminZoneCount> MostContributed);
