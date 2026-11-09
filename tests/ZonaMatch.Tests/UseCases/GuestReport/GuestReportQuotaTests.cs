using FluentAssertions;
using ZonaMatch.Application.UseCases.GuestReport;

namespace ZonaMatch.Tests.UseCases.GuestReport;

public class GuestReportQuotaTests
{
    private readonly InMemoryGuestReportSendRepository _sends = new();

    // 23:30 of November 6 in Buenos Aires (UTC-3)
    private readonly SettableTimeProvider _clock = new(new DateTimeOffset(2026, 11, 7, 2, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task SentTodayAsync_WhenTheSameAddressIsWrittenDifferently_CountsItOnce()
    {
        var quota = new GuestReportQuota(_sends, _clock);

        await quota.RegisterAsync("Ana@Example.com ");
        var sent = await quota.SentTodayAsync("ana@example.com");

        sent.Should().Be(1);
    }

    [Fact]
    public async Task SentTodayAsync_WhenADayHasPassedInArgentina_StartsAgainFromZero()
    {
        var quota = new GuestReportQuota(_sends, _clock);
        await quota.RegisterAsync("ana@example.com");

        // 00:30 of November 7 in Buenos Aires
        _clock.Now = _clock.Now.AddHours(1);
        var sent = await quota.SentTodayAsync("ana@example.com");

        sent.Should().Be(0);
    }

    [Fact]
    public async Task RegisterAsync_WhenCalled_StoresOnlyAHashOfTheAddress()
    {
        var quota = new GuestReportQuota(_sends, _clock);

        await quota.RegisterAsync("ana@example.com");

        _sends.Sends.Should().ContainSingle().Which.RecipientHash.Should().HaveLength(64).And.NotContain("ana");
    }

    [Fact]
    public async Task RegisterAsync_WhenSendsAreFromPreviousDays_DeletesThem()
    {
        var quota = new GuestReportQuota(_sends, _clock);
        await quota.RegisterAsync("ana@example.com");

        _clock.Now = _clock.Now.AddDays(1);
        await quota.RegisterAsync("beto@example.com");

        _sends.Sends.Should().ContainSingle();
    }
}
