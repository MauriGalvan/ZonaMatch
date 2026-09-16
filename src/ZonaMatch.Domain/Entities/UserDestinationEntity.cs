using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities;

public class UserDestinationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public UserProfileEntity Profile { get; set; } = default!;

    public string Name { get; set; } = string.Empty; // 'Trabajo', 'Facultad', etc.
    public string? Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Point Geom { get; set; } = default!;
    public string TransportMode { get; set; } = "driving"; // 'driving', 'walking', 'cycling'

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
