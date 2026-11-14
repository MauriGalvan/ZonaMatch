using FluentAssertions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Tests.UseCases.Community;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Admin;

public class ListAdminReviewsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    private readonly User _author = new() { Email = "ana@example.com" };
    private readonly User _administrator = new() { Email = "admin@example.com", IsAdministrator = true };
    private readonly FakeReviewRepository _reviews = new();
    private readonly ListAdminReviews _useCase;

    public ListAdminReviewsTests()
    {
        _useCase = new ListAdminReviews(
            _reviews,
            new FakeAdminUserRepository(_author, _administrator),
            new FakeAdminZoneProfilesRepository(new ZoneProfile { Slug = "palermo", Name = "Palermo" }));
    }

    private DomainReview AddReview(string zoneSlug, string text, DateTimeOffset updatedAt)
    {
        var review = new DomainReview
        {
            ZoneSlug = zoneSlug,
            UserId = _author.Id,
            User = _author,
            Text = text,
            SafetyScore = 4,
            TransitScore = 5,
            ConnectivityScore = 4,
            CommerceScore = 3,
            GreenSpaceScore = 4,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        };
        _reviews.Add(review);
        return review;
    }

    [Fact]
    public async Task ListReviews_SplitsPendingReviewsFromTheHistory()
    {
        AddReview("palermo", "Pendiente", Now.AddHours(-3));
        AddReview("palermo", "Aprobada", Now.AddHours(-5)).Resolve(_administrator.Id, approve: true, null, Now.AddHours(-1));

        var result = await _useCase.ExecuteAsync();

        result.Pending.Should().ContainSingle().Which.Text.Should().Be("Pendiente");
        result.History.Should().ContainSingle().Which.Text.Should().Be("Aprobada");
    }

    [Fact]
    public async Task ListReviews_ReturnsTheDecisionTheReasonAndWhoResolvedIt()
    {
        AddReview("palermo", "Spam", Now.AddHours(-2)).Resolve(_administrator.Id, approve: false, "Publicidad", Now);

        var review = (await _useCase.ExecuteAsync()).History.Single();

        review.Status.Should().Be("rejected");
        review.RejectionReason.Should().Be("Publicidad");
        review.ResolvedAt.Should().Be(Now);
        review.ResolvedByEmail.Should().Be("admin@example.com");
        review.AuthorEmail.Should().Be("ana@example.com");
        review.OverallScore.Should().Be(4);
    }

    [Fact]
    public async Task ListReviews_ListsTheMostRecentlyResolvedFirst()
    {
        AddReview("palermo", "Primera", Now.AddDays(-3)).Resolve(_administrator.Id, approve: true, null, Now.AddDays(-2));
        AddReview("palermo", "Segunda", Now.AddDays(-3)).Resolve(_administrator.Id, approve: true, null, Now.AddDays(-1));

        var history = (await _useCase.ExecuteAsync()).History;

        history.Select(review => review.Text).Should().Equal("Segunda", "Primera");
    }

    [Fact]
    public async Task ListReviews_UsesTheZoneNameOrTheSlugWhenTheZoneHasNoData()
    {
        AddReview("palermo", "Con nombre", Now);
        AddReview("zona-sin-datos", "Sin nombre", Now);

        var pending = (await _useCase.ExecuteAsync()).Pending;

        pending.Single(review => review.Text == "Con nombre").ZoneName.Should().Be("Palermo");
        pending.Single(review => review.Text == "Sin nombre").ZoneName.Should().Be("zona-sin-datos");
    }

    [Fact]
    public async Task ListReviews_WhenPending_HasNoResolution()
    {
        AddReview("palermo", "Pendiente", Now);

        var review = (await _useCase.ExecuteAsync()).Pending.Single();

        review.Status.Should().Be("pending");
        review.ResolvedAt.Should().BeNull();
        review.ResolvedByEmail.Should().BeNull();
    }
}
