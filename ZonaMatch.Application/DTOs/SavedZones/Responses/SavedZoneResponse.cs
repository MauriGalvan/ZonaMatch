namespace ZonaMatch.Application.DTOs.SavedZones.Responses
{
    public record SavedZoneResponse(string Id, string Name, string Subtitle, DateTimeOffset SavedAt);
}
