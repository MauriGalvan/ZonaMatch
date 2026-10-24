namespace ZonaMatch.Domain.Entities
{
    public class SavedZone
    {
        public Guid UserId { get; set; }
        public string ZoneSlug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public DateTimeOffset SavedAt { get; set; }
    }
}
