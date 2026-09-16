using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities;

public class LocationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? GeorefId { get; set; }
    public string NormalizedName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Province { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Point Geom { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
