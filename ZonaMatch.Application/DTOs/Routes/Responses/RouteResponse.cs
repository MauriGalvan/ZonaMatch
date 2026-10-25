namespace ZonaMatch.Application.DTOs.Routes.Responses
{
    // The fastest trip found, leg by leg, to draw it on the map.
    public sealed record RouteResponse(string Mode, int DurationMinutes, IReadOnlyList<RouteLegResponse> Legs);
}
