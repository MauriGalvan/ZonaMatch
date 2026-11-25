using FluentAssertions;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Admin;

public class GetAdminStatsTests
{
    // Wednesday 2026-11-11, 15:00 in Argentina (UTC-3). The current week starts on Monday 2026-11-09.
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 18, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Argentina = TimeSpan.FromHours(-3);

    private static GetAdminStats CreateUseCase(FakeAdminStatsRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    [Fact]
    public async Task GetStats_ReturnsTwelveWeeksEndingInTheCurrentWeek()
    {
        var stats = await CreateUseCase(new FakeAdminStatsRepository()).ExecuteAsync();

        stats.WeeklyRegistrations.Should().HaveCount(GetAdminStats.WeekCount);
        stats.WeeklyRegistrations[0].WeekStart.Should().Be(new DateOnly(2026, 8, 24));
        stats.WeeklyRegistrations[^1].WeekStart.Should().Be(new DateOnly(2026, 11, 9));
        stats.WeeklyRegistrations.Should().OnlyContain(week => week.WeekStart.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task GetStats_WhenNoRegistrations_ReturnsZeroForEveryWeek()
    {
        var stats = await CreateUseCase(new FakeAdminStatsRepository()).ExecuteAsync();

        stats.WeeklyRegistrations.Should().OnlyContain(week => week.Count == 0);
    }

    [Fact]
    public async Task GetStats_CountsRegistrationsInTheirWeek()
    {
        var repository = new FakeAdminStatsRepository(
            new DateTimeOffset(2026, 11, 9, 10, 0, 0, Argentina),
            new DateTimeOffset(2026, 11, 11, 9, 0, 0, Argentina),
            new DateTimeOffset(2026, 11, 3, 12, 0, 0, Argentina));

        var stats = await CreateUseCase(repository).ExecuteAsync();

        stats.WeeklyRegistrations[^1].Count.Should().Be(2);
        stats.WeeklyRegistrations[^2].Count.Should().Be(1);
    }

    [Fact]
    public async Task GetStats_WhenRegisteredSundayNightInArgentina_CountsItInThatWeek()
    {
        // Sunday 23:30 in Argentina is already Monday in UTC; the week is the one that ends that Sunday.
        var repository = new FakeAdminStatsRepository(new DateTimeOffset(2026, 11, 8, 23, 30, 0, Argentina));

        var stats = await CreateUseCase(repository).ExecuteAsync();

        stats.WeeklyRegistrations[^1].Count.Should().Be(0);
        stats.WeeklyRegistrations[^2].Count.Should().Be(1);
    }

    [Fact]
    public async Task GetStats_RequestsTheLastDay7And30DaysAndTheFirstWeek()
    {
        var repository = new FakeAdminStatsRepository();

        await CreateUseCase(repository).ExecuteAsync();

        repository.RequestedPeriod.Should().NotBeNull();
        repository.RequestedPeriod!.SinceLastDay.Should().Be(Now.AddDays(-1));
        repository.RequestedPeriod.Since7Days.Should().Be(Now.AddDays(-7));
        repository.RequestedPeriod.Since30Days.Should().Be(Now.AddDays(-30));
        repository.RequestedPeriod.RegistrationsSince.Should().Be(new DateTimeOffset(2026, 8, 24, 0, 0, 0, Argentina));
        // Npgsql only accepts UTC values.
        repository.RequestedPeriod.RegistrationsSince.Offset.Should().Be(TimeSpan.Zero);
        repository.RequestedPeriod.TopZoneCount.Should().Be(GetAdminStats.TopZoneCount);
    }

    [Fact]
    public async Task GetStats_ReturnsTheRepositoryTotalsAndTheGenerationTime()
    {
        var stats = await CreateUseCase(new FakeAdminStatsRepository()).ExecuteAsync();

        stats.Users.Should().Be(FakeAdminStatsRepository.Users);
        stats.TopAnalyzedZones.Should().ContainSingle().Which.ZoneSlug.Should().Be("palermo");
        stats.CriteriaUsage.Should().ContainSingle().Which.HighPriorityCount.Should().Be(2);
        stats.Operations.Purchase.Should().Be(1);
        stats.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task GetStats_SeparatesCustomPlaceTagsFromThePredefinedOnes()
    {
        var repository = new FakeAdminStatsRepository();
        repository.PlaceTags.AddRange([
            new AdminPlaceTagCount("workshop", Users: 1, SavedAnalyses: 3),
            new AdminPlaceTagCount("Trabajo", Users: 4, SavedAnalyses: 6),
            // Same predefined tag whatever its case or spaces.
            new AdminPlaceTagCount(" casa de amigo ", Users: 2, SavedAnalyses: 1)]);

        var tags = (await CreateUseCase(repository).ExecuteAsync()).PlaceTags;

        tags.Custom.Select(tag => tag.Tag).Should().Equal("workshop");
        tags.Predefined.Select(tag => tag.Tag).Should().Equal("Trabajo", " casa de amigo ");
    }

    [Fact]
    public async Task GetStats_SortsCustomPlaceTagsByUsersThenBySavedAnalyses()
    {
        var repository = new FakeAdminStatsRepository();
        repository.PlaceTags.AddRange([
            new AdminPlaceTagCount("Gimnasio", Users: 1, SavedAnalyses: 1),
            new AdminPlaceTagCount("Taller", Users: 3, SavedAnalyses: 0),
            new AdminPlaceTagCount("Iglesia", Users: 1, SavedAnalyses: 5)]);

        var tags = (await CreateUseCase(repository).ExecuteAsync()).PlaceTags;

        tags.Custom.Select(tag => tag.Tag).Should().Equal("Taller", "Iglesia", "Gimnasio");
    }

    [Fact]
    public async Task GetStats_WhenNoPlaceHasATag_ReturnsEmptyTagLists()
    {
        var tags = (await CreateUseCase(new FakeAdminStatsRepository()).ExecuteAsync()).PlaceTags;

        tags.Custom.Should().BeEmpty();
        tags.Predefined.Should().BeEmpty();
    }
}
