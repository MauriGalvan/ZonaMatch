using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities;

public class PoiEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long? OsmId { get; set; }
    public string? Name { get; set; }
    public string Category { get; set; } = string.Empty;       // salud, educacion, abastecimiento, espacios_verdes, transporte
    public string Subcategory { get; set; } = string.Empty;    // hospital, farmacia, escuela, supermercado, parque, etc.
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Point Geom { get; set; } = default!;
    public string? TagsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
