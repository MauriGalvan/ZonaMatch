namespace ZonaMatch.Application.DTOs.SavedZone.Responses
{
    public record SavedZoneResponse(string Id, string Name, string Subtitle, DateTimeOffset SavedAt);
}
