namespace ZonaMatch.Domain.Entities;

// Departures for a line in one time window, measured at the zone stop with the most service.
public sealed class TransitLineDepartures
{
    public string Mode { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public string TimeWindow { get; set; } = string.Empty;
    public int DepartureCount { get; set; }
    public int StopCount { get; set; }
}
