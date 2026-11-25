using FluentAssertions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Admin;

public class GetAdminZoneActivityTests
{
    private readonly FakeAdminStatsRepository _stats = new();

    private GetAdminZoneActivity UseCase() =>
        new(_stats, new FakeAdminZoneProfilesRepository(new ZoneProfile { Slug = "palermo", Name = "Palermo" }));

    [Fact]
    public async Task GetActivity_AsksForTheRankingSize()
    {
        await UseCase().ExecuteAsync();

        _stats.RequestedRankingSize.Should().Be(GetAdminZoneActivity.RankingSize);
    }

    [Fact]
    public async Task GetActivity_KeepsEachRankingInItsOrderAndAddsTheZoneNames()
    {
        _stats.ZoneActivity = new AdminZoneActivitySnapshot(
            MostSaved: [new("palermo", 8), new("zona-sin-datos", 3)],
            LeastSaved: [new("zona-sin-datos", 1)],
            MostReviewed: [new("palermo", 4)],
            MostAsked: [new("palermo", 2)],
            MostContributed: [new("zona-sin-datos", 5)]);

        var activity = await UseCase().ExecuteAsync();

        activity.MostSaved.Select(zone => (zone.Name, zone.Count)).Should().Equal(("Palermo", 8), ("zona-sin-datos", 3));
        activity.LeastSaved.Should().ContainSingle().Which.Count.Should().Be(1);
        activity.MostReviewed.Should().ContainSingle().Which.Name.Should().Be("Palermo");
        activity.MostAsked.Should().ContainSingle().Which.ZoneSlug.Should().Be("palermo");
        activity.MostContributed.Should().ContainSingle().Which.Name.Should().Be("zona-sin-datos", "a zone without data shows its slug");
    }

    [Fact]
    public async Task GetActivity_WhenThereIsNoActivity_ReturnsEmptyRankings()
    {
        var activity = await UseCase().ExecuteAsync();

        activity.MostSaved.Should().BeEmpty();
        activity.LeastSaved.Should().BeEmpty();
        activity.MostReviewed.Should().BeEmpty();
        activity.MostAsked.Should().BeEmpty();
        activity.MostContributed.Should().BeEmpty();
    }
}
