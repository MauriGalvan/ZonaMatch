using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Question;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Tests.UseCases.Community;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;

namespace ZonaMatch.Tests.UseCases.Question;

public class AnswerQuestionTests
{
    private const string Zone = FakeZoneRepository.ZoneSlug;
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeQuestionRepository _questions = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly RecordingEmailService _emailService = new();
    private readonly AnswerQuestion _useCase;
    private readonly AuthenticatedUserContext _luis = new(Guid.NewGuid(), "luis.gomez@example.com");
    private readonly DomainQuestion _question = new()
    {
        ZoneSlug = Zone,
        UserId = Guid.NewGuid(),
        Text = "¿Hay verdulerias?"
    };

    public AnswerQuestionTests()
    {
        _useCase = new AnswerQuestion(
            _questions,
            _notifications,
            _emailService,
            NullLogger<AnswerQuestion>.Instance,
            new FixedTimeProvider(CurrentTime),
            new FrontendLinks("http://localhost:5173/"));
        _questions.Add(_question);
    }

    [Fact]
    public async Task AnswerQuestion_WhenQuestionExists_AddsTrimmedAnswerAndReturnsResponse()
    {
        var response = await _useCase.ExecuteAsync(
            Zone,
            _question.Id,
            _luis,
            new CreateAnswerRequest("  Si, sobre Rivadavia. "));

        var savedAnswer = _question.Answers.Should().ContainSingle().Which;
        savedAnswer.Text.Should().Be("Si, sobre Rivadavia.");
        savedAnswer.UserId.Should().Be(_luis.Id);
        savedAnswer.CreatedAt.Should().Be(CurrentTime);
        response.Id.Should().Be(savedAnswer.Id);
        response.Initials.Should().Be("LG");
        response.IsMine.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerQuestion_WhenQuestionDoesNotExist_ThrowsNotFoundException()
    {
        Func<Task> act = () => _useCase.ExecuteAsync(
            Zone,
            Guid.NewGuid(),
            _luis,
            new CreateAnswerRequest("Hola"));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AnswerQuestion_WhenQuestionBelongsToAnotherZone_ThrowsNotFoundException()
    {
        Func<Task> act = () => _useCase.ExecuteAsync(
            "otra-zona",
            _question.Id,
            _luis,
            new CreateAnswerRequest("Hola"));

        await act.Should().ThrowAsync<NotFoundException>();
        _question.Answers.Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerQuestion_WhenAnotherUserAnswers_NotifiesAndEmailsQuestionAuthor()
    {
        _question.User = new User { Id = _question.UserId, Email = "question-author@example.com" };

        await _useCase.ExecuteAsync(Zone, _question.Id, _luis, new CreateAnswerRequest("Sí, hay varias."));

        var notification = _notifications.Notifications.Should().ContainSingle().Which;
        notification.UserId.Should().Be(_question.UserId);
        notification.Type.Should().Be(NotificationType.CommunityReply);
        _emailService.Recipients.Should().ContainSingle().Which.Should().Be("question-author@example.com");
        _question.Answers.Should().ContainSingle();
    }

    [Fact]
    public async Task AnswerQuestion_WhenAuthorAnswersOwnQuestion_DoesNotNotifyOrEmail()
    {
        _question.UserId = _luis.Id;
        _question.User = new User { Id = _luis.Id, Email = _luis.Email };

        await _useCase.ExecuteAsync(Zone, _question.Id, _luis, new CreateAnswerRequest("Agrego un detalle."));

        _notifications.Notifications.Should().BeEmpty();
        _emailService.Recipients.Should().BeEmpty();
        _question.Answers.Should().ContainSingle();
    }

    [Fact]
    public async Task AnswerQuestion_WhenEmailDeliveryFails_KeepsTheAnswerAndNotification()
    {
        _question.User = new User { Id = _question.UserId, Email = "question-author@example.com" };
        _emailService.ExceptionToThrow = new InvalidOperationException("Email provider unavailable.");

        Func<Task> act = () => _useCase.ExecuteAsync(
            Zone, _question.Id, _luis, new CreateAnswerRequest("Sí, hay varias."));

        await act.Should().NotThrowAsync();
        _question.Answers.Should().ContainSingle();
        _notifications.Notifications.Should().ContainSingle();
    }

    [Fact]
    public async Task AnswerQuestion_WhenRequestIsCanceledDuringEmailSend_PropagatesCancellationAfterSavingTheAnswer()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _question.User = new User { Id = _question.UserId, Email = "question-author@example.com" };
        _emailService.ExceptionToThrow = new OperationCanceledException(cancellation.Token);

        Func<Task> act = () => _useCase.ExecuteAsync(
            Zone,
            _question.Id,
            _luis,
            new CreateAnswerRequest("Sí, hay varias."),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _question.Answers.Should().ContainSingle();
        _notifications.Notifications.Should().ContainSingle();
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<string> Recipients { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            Recipients.Add(to);
            return ExceptionToThrow is { } exception
                ? Task.FromException(exception)
                : Task.CompletedTask;
        }

        public Task SendEmailAsync(
            string to,
            string subject,
            string body,
            IReadOnlyList<EmailAttachment> attachments,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
