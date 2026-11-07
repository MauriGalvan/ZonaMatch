using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IGuestReportSendRepository
{
    Task<int> CountSinceAsync(string recipientHash, DateTimeOffset since, CancellationToken cancellationToken = default);

    // Saves the send and drops the ones older than `deleteBefore`, which no longer count for any limit.
    Task AddAsync(GuestReportSend send, DateTimeOffset deleteBefore, CancellationToken cancellationToken = default);
}
