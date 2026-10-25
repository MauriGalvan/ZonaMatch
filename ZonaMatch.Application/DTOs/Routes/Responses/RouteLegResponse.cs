namespace ZonaMatch.Application.DTOs.Routes.Responses
{
    // Mode: walk, bike, car, bus, train, subway, tram or ferry. Line, headsign, agency and color are set for
    // public transport legs ("Sarmiento", "a Once"); color is a hex RGB without "#" when the operator publishes it.
    // Path: [latitude, longitude] pairs.
    public sealed record RouteLegResponse(
        string Mode,
        int DurationMinutes,
        int? DistanceMeters,
        string? From,
        string? To,
        string? Line,
        string? Headsign,
        string? Agency,
        string? Color,
        IReadOnlyList<double[]> Path);
}
