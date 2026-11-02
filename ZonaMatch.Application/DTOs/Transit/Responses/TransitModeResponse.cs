namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Mode values use the existing transit mode codes.
public sealed record TransitModeResponse(
    string Mode,
    int Stops,
    int LineCount,
    IReadOnlyList<TransitTimeWindowResponse> TimeWindows,
    // Lines with the most service during the morning peak come first.
    IReadOnlyList<TransitLineResponse> Lines);
