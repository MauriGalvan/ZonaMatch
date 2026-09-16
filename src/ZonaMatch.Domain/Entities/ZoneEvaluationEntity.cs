using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities;

public class ZoneEvaluationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProfileId { get; set; }
    public UserProfileEntity? Profile { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Point EvaluatedPoint { get; set; } = default!;
    public string? AddressQuery { get; set; }

    public decimal TotalScore { get; set; }       // 0.00 a 100.00
    public decimal DataConfidence { get; set; }   // 0.00 a 100.00

    public string SubscoresJson { get; set; } = "{}";
    public string? MobilityDetailsJson { get; set; }
    public string? ProsJson { get; set; }
    public string? ConsJson { get; set; }
    public string? Explanation { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
