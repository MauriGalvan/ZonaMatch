namespace ZonaMatch.Application.Models;

// How many times something happened in a zone, read by IAdminStatsRepository; the use case adds the zone name.
public sealed record ZoneActivityCount(string ZoneSlug, int Count);

// Zone rankings for the admin Zones page. LeastSaved only has zones saved at least once, the fewest first.
public sealed record AdminZoneActivitySnapshot(
    IReadOnlyList<ZoneActivityCount> MostSaved,
    IReadOnlyList<ZoneActivityCount> LeastSaved,
    IReadOnlyList<ZoneActivityCount> MostReviewed,
    IReadOnlyList<ZoneActivityCount> MostAsked,
    IReadOnlyList<ZoneActivityCount> MostContributed);
