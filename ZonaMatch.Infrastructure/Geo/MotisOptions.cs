using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ZonaMatch.Infrastructure.Geo;

public sealed class MotisOptions
{
    public const string SectionName = "Motis";

    public string? Url { get; set; }

    [ConfigurationKeyName("FechaReferencia")]
    public DateTimeOffset? ReferenceTime { get; set; }

    public TimeSpan RoutingTimeout { get; set; }
    public TimeSpan PlanningTimeout { get; set; }

    internal string ReferenceTimestamp => ReferenceTime!.Value.UtcDateTime
        .ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
}
