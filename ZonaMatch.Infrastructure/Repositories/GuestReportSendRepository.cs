using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public sealed class GuestReportSendRepository(ZonaMatchDbContext context) : IGuestReportSendRepository
{
    public Task<int> CountSinceAsync(string recipientHash, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        context.GuestReportSends.CountAsync(send => send.RecipientHash == recipientHash && send.SentAt >= since, cancellationToken);

    public async Task AddAsync(GuestReportSend send, DateTimeOffset deleteBefore, CancellationToken cancellationToken = default)
    {
        context.GuestReportSends.Add(send);
        await context.SaveChangesAsync(cancellationToken);
        await context.GuestReportSends.Where(old => old.SentAt < deleteBefore).ExecuteDeleteAsync(cancellationToken);
    }
}
