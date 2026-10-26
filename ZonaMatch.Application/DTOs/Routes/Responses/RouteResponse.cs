namespace ZonaMatch.Application.DTOs.Routes.Responses
{
    // The fastest trip found, leg by leg, to draw it on the map. Mode is the one used: a-pie for five blocks or
    // less, whatever was asked.
    public sealed record RouteResponse(string Mode, int DurationMinutes, IReadOnlyList<RouteLegResponse> Legs);
}
