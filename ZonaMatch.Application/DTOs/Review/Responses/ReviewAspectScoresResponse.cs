namespace ZonaMatch.Application.DTOs.Review.Responses;

public sealed record ReviewAspectScoresResponse(
    int Safety,
    int Transport,
    int Connectivity,
    int Commerce,
    int GreenSpaces);
