namespace ZonaMatch.Application.DTOs.Reviews.Responses;

public sealed record ReviewAspectScoresResponse(
    int Safety,
    int Transport,
    int Connectivity,
    int Commerce,
    int GreenSpaces);
