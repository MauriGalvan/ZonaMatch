namespace ZonaMatch.Domain.Entities
{
    // A guest report emailed to a visitor, to limit how many each address gets per day.
    // Only the SHA-256 hash of the email is stored: the visitor has no account, so the address is not kept.
    public class GuestReportSend
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public string RecipientHash { get; set; } = string.Empty;
        public DateTimeOffset SentAt { get; set; }
    }
}
