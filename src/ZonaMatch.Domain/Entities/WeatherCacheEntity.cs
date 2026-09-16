using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities;

public class WeatherCacheEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal GridLat { get; set; }
    public decimal GridLon { get; set; }
    public Point Geom { get; set; } = default!;
    public decimal? AvgTemperature { get; set; }
    public decimal? MaxTemperature { get; set; }
    public decimal? MinTemperature { get; set; }
    public decimal? AnnualPrecipitationMm { get; set; }
    public int? RainyDaysCount { get; set; }
    public string? RawDataJson { get; set; }
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(30);
}
