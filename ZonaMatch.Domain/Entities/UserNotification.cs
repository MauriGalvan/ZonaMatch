using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Domain.Entities
{
    public class UserNotification
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid UserId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        // The review a ReviewPendingModeration notification asks to moderate; null for every other type.
        public Guid? ReviewId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
    }
}
