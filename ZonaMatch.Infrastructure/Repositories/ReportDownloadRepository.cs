using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public sealed class ReportDownloadRepository(ZonaMatchDbContext context) : IReportDownloadRepository
{
    public async Task AddAsync(ReportDownload download, CancellationToken cancellationToken = default)
    {
        context.ReportDownloads.Add(download);
        await context.SaveChangesAsync(cancellationToken);
    }
}
