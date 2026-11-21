using FluentAssertions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.ReportDownload;
using ZonaMatch.Tests.UseCases.Community;
using DomainReportDownload = ZonaMatch.Domain.Entities.ReportDownload;

namespace ZonaMatch.Tests.UseCases.ReportDownload;

public class RegisterReportDownloadTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeReportDownloadRepository _downloads = new();
    private readonly RegisterReportDownload _useCase;

    public RegisterReportDownloadTests()
    {
        _useCase = new RegisterReportDownload(_downloads, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Register_WhenSignedIn_StoresTheUserAndTheTime()
    {
        var userId = Guid.NewGuid();

        await _useCase.ExecuteAsync(userId);

        var download = _downloads.Downloads.Should().ContainSingle().Subject;
        download.UserId.Should().Be(userId);
        download.DownloadedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Register_WhenVisitor_StoresItWithoutUser()
    {
        await _useCase.ExecuteAsync(userId: null);

        _downloads.Downloads.Should().ContainSingle().Which.UserId.Should().BeNull();
    }

    private sealed class FakeReportDownloadRepository : IReportDownloadRepository
    {
        public List<DomainReportDownload> Downloads { get; } = [];

        public Task AddAsync(DomainReportDownload download, CancellationToken cancellationToken = default)
        {
            Downloads.Add(download);
            return Task.CompletedTask;
        }
    }
}
