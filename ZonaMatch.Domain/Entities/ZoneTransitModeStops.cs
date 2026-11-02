namespace ZonaMatch.Domain.Entities;

// Stops and lines for one transit mode in a zone.
public sealed class ZoneTransitModeStops
{
    public string Mode { get; set; } = string.Empty;
    public int StopCount { get; set; }
    public int LineCount { get; set; }
}
