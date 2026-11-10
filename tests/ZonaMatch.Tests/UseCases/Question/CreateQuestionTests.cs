using FluentAssertions;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Question;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Tests.UseCases.Community;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.Question;

public class CreateQuestionTests
{
    private const string Zone = FakeZoneRepository.ZoneSlug;
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeQuestionRepository _questions = new();
    private readonly FakeSavedZoneRepository _savedZones = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly CreateQuestion _useCase;
    private readonly AuthenticatedUserContext _ana = new(Guid.NewGuid(), "ana@example.com");

    public CreateQuestionTests()
    {
        _useCase = new CreateQuestion(
            _questions,
            new FakeZoneRepository(),
            _savedZones,
            _notifications,
            new FixedTimeProvider(CurrentTime));
    }

    [Fact]
    public async Task CreateQuestion_WhenRequestIsValid_TrimsAndStoresQuestion()
    {
        var response = await _useCase.ExecuteAsync(
            Zone,
            _ana,
            new CreateQuestionRequest("  ¿Hay verdulerias cerca de la estacion?  "));

        var savedQuestion = _questions.Questions.Should().ContainSingle().Which;
        savedQuestion.Text.Should().Be("¿Hay verdulerias cerca de la estacion?");
        savedQuestion.UserId.Should().Be(_ana.Id);
        savedQuestion.CreatedAt.Should().Be(CurrentTime);
        response.Id.Should().Be(savedQuestion.Id);
        response.Initials.Should().Be("AN");
        response.IsMine.Should().BeTrue();
        response.Answers.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateQuestion_WhenZoneDoesNotExist_ThrowsNotFoundException()
    {
        Func<Task> act = () => _useCase.ExecuteAsync(
            "no-existe",
            _ana,
            new CreateQuestionRequest("¿Como es el barrio de noche?"));

        await act.Should().ThrowAsync<NotFoundException>();
        _questions.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateQuestion_WhenOtherUsersSavedTheZone_NotifiesThem()
    {
        var savedZoneUserId = Guid.NewGuid();
        _savedZones.SavedZones.Add(new DomainSavedZone { UserId = savedZoneUserId, ZoneSlug = Zone, Name = "Villa Luro" });
        _savedZones.SavedZones.Add(new DomainSavedZone { UserId = _ana.Id, ZoneSlug = Zone, Name = "Villa Luro" });
        _savedZones.SavedZones.Add(new DomainSavedZone { UserId = Guid.NewGuid(), ZoneSlug = "otra-zona", Name = "Otra zona" });

        await _useCase.ExecuteAsync(Zone, _ana, new CreateQuestionRequest("¿Hay escuelas cerca?"));

        var notification = _notifications.Notifications.Should().ContainSingle().Which;
        notification.UserId.Should().Be(savedZoneUserId);
        notification.Type.Should().Be(NotificationType.SavedZoneQuestion);
        notification.Link.Should().Be("/zonas/villa-luro?tab=comunidad");
    }

    [Fact]
    public async Task CreateQuestion_WhenNoOtherUsersSavedTheZone_CreatesQuestionWithoutNotifications()
    {
        var response = await _useCase.ExecuteAsync(
            Zone,
            _ana,
            new CreateQuestionRequest("¿Hay espacios verdes cerca?"));

        _questions.Questions.Should().ContainSingle().Which.Id.Should().Be(response.Id);
        _notifications.Notifications.Should().BeEmpty();
    }
}
