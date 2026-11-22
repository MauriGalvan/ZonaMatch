namespace ZonaMatch.Domain.Entities
{
    // A PDF report of an analysis downloaded from the result page (app.report_downloads table), counted for the admin
    // statistics. The PDF is built in the browser; only the fact that it was downloaded is stored.
    public class ReportDownload
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Who downloaded it; null for a visitor without an account (or an account deleted later).
        public Guid? UserId { get; set; }

        public DateTimeOffset DownloadedAt { get; set; }
    }
}
