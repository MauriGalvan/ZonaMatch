using System.Security.Cryptography;
using System.Text;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.GuestReport;

// Reports emailed to each address today (Argentina time). Stored in the database, so the limit survives a restart
// and is shared by every API instance. Only a hash of the address is kept.
public sealed class GuestReportQuota(IGuestReportSendRepository sends, TimeProvider clock)
{
    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    public Task<int> SentTodayAsync(string recipient, CancellationToken cancellationToken = default) =>
        sends.CountSinceAsync(Hash(recipient), StartOfToday(), cancellationToken);

    public Task RegisterAsync(string recipient, CancellationToken cancellationToken = default) =>
        sends.AddAsync(
            new GuestReportSend { RecipientHash = Hash(recipient), SentAt = clock.GetUtcNow() },
            deleteBefore: StartOfToday(),
            cancellationToken);

    private static string Hash(string recipient) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(recipient.Trim().ToLowerInvariant())));

    // Midnight in Argentina, as an instant
    private DateTimeOffset StartOfToday()
    {
        var local = clock.GetUtcNow().ToOffset(ArgentinaOffset);
        return new DateTimeOffset(local.Date, ArgentinaOffset);
    }
}
