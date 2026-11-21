using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IReportDownloadRepository
{
    Task AddAsync(ReportDownload download, CancellationToken cancellationToken = default);
}
