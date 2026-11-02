namespace ZonaMatch.Domain.Common;

/// <summary>Weekday windows used to summarize public-transport departures.</summary>
public sealed record TimeWindow(string Code, int StartHour, int EndHour)
{
    // Codes are persisted and returned by the API, so keep their existing values.
    public static readonly TimeWindow EarlyMorning = new("madrugada", 0, 6);
    public static readonly TimeWindow Morning = new("manana", 6, 9);
    public static readonly TimeWindow Midday = new("mediodia", 9, 16);
    public static readonly TimeWindow Afternoon = new("tarde", 16, 20);
    public static readonly TimeWindow Evening = new("noche", 20, 24);

    public static readonly IReadOnlyList<TimeWindow> All =
        [EarlyMorning, Morning, Midday, Afternoon, Evening];

    public int Hours => EndHour - StartHour;

    public double DeparturesPerHour(int departures) => Math.Round((double)departures / Hours, 1);

    public int? MinutesBetweenDepartures(int departures) =>
        departures > 0 ? (int)Math.Round(60.0 * Hours / departures) : null;

    public static TimeWindow? FromCode(string code) => All.FirstOrDefault(window => window.Code == code);
}
