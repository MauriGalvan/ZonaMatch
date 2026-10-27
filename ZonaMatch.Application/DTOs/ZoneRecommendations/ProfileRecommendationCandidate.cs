namespace ZonaMatch.Application.DTOs.ZoneRecommendations;

// Internal repository result used to keep the per-category limit metadata out of the public response.
public sealed class ProfileRecommendationCandidate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceMeters { get; set; }
    public bool IsTruncated { get; set; }
}
