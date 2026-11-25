namespace ZonaMatch.Infrastructure.Caching;

public sealed class CacheLifetimeOptions
{
    public const string SectionName = "Cache";
    public const string PublicResponseProfile = "PublicData";

    public TimeSpan ZoneProfiles { get; set; }
    public TimeSpan NominatimAddresses { get; set; }
    public TimeSpan MotisTravelTimes { get; set; }
    public TimeSpan MotisRoutePlans { get; set; }
    public int PublicResponseSeconds { get; set; }
}
