using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.GuestReport;

// The guest_report_sends table, in memory.
internal sealed class InMemoryGuestReportSendRepository : IGuestReportSendRepository
{
    public List<GuestReportSend> Sends { get; } = [];

    // The last instants the quota asked for, to check what reaches the database.
    public DateTimeOffset? LastSince { get; private set; }
    public DateTimeOffset? LastDeleteBefore { get; private set; }

    public Task<int> CountSinceAsync(string recipientHash, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        LastSince = since;
        return Task.FromResult(Sends.Count(send => send.RecipientHash == recipientHash && send.SentAt >= since));
    }

    public Task AddAsync(GuestReportSend send, DateTimeOffset deleteBefore, CancellationToken cancellationToken = default)
    {
        LastDeleteBefore = deleteBefore;
        Sends.Add(send);
        Sends.RemoveAll(old => old.SentAt < deleteBefore);
        return Task.CompletedTask;
    }
}

// A clock the test can move.
internal sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
