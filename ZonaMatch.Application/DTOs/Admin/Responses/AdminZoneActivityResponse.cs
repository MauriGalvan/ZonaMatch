namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/zones/activity: the zones users save the most and the least, and the ones with the most reviews,
// questions and contributions. LeastSaved only has zones saved at least once. Reviews and contributions count the
// approved and pending ones, not the rejected; questions an administrator deleted no longer count.
public sealed record AdminZoneActivityResponse(
    IReadOnlyList<AdminZoneCount> MostSaved,
    IReadOnlyList<AdminZoneCount> LeastSaved,
    IReadOnlyList<AdminZoneCount> MostReviewed,
    IReadOnlyList<AdminZoneCount> MostAsked,
    IReadOnlyList<AdminZoneCount> MostContributed);
