using ZonaMatch.Application.Interfaces;
using DomainReportDownload = ZonaMatch.Domain.Entities.ReportDownload;

namespace ZonaMatch.Application.UseCases.ReportDownload;

// Counts a PDF report downloaded from the analysis result, by a signed-in user or a visitor (userId null).
public sealed class RegisterReportDownload(IReportDownloadRepository downloads, TimeProvider clock)
{
    public Task ExecuteAsync(Guid? userId, CancellationToken cancellationToken = default) =>
        downloads.AddAsync(new DomainReportDownload { UserId = userId, DownloadedAt = clock.GetUtcNow() }, cancellationToken);
}
