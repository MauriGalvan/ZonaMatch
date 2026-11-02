using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Question;
using ZonaMatch.Domain.Entities;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;
using Microsoft.Extensions.Logging.Abstractions;

namespace ZonaMatch.Tests.UseCases.Question
{
    public class AnswerQuestionTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeQuestionRepository _questions = new();
        private readonly AnswerQuestion _useCase;
        private readonly AuthenticatedUserContext _luis = new(Guid.NewGuid(), "luis.gomez@example.com");
        private readonly DomainQuestion _question = new() { ZoneSlug = Zone, UserId = Guid.NewGuid(), Text = "¿Hay verdulerias?" };

        public AnswerQuestionTests()
        {
            _useCase = new AnswerQuestion(
                _questions,
                new FakeNotificationRepository(),
                new FakeEmailService(),
                NullLogger<AnswerQuestion>.Instance,
                new FixedTimeProvider(CurrentTime),
                new FrontendLinks("http://localhost:5173/"));
            _questions.Add(_question);
        }

        [Fact]
        public async Task Answer_AddsAnswerToQuestion()
        {
            var created = await _useCase.ExecuteAsync(Zone, _question.Id, _luis, new CreateAnswerRequest("  Si, sobre Rivadavia. "));

            var saved = Assert.Single(_question.Answers);
            Assert.Equal("Si, sobre Rivadavia.", saved.Text);
            Assert.Equal(_luis.Id, saved.UserId);
            Assert.Equal(CurrentTime, saved.CreatedAt);
            Assert.Equal(saved.Id, created.Id);
            Assert.Equal("LG", created.Initials);
            Assert.True(created.IsMine);
        }

        [Fact]
        public async Task Answer_WhenQuestionDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), _luis, new CreateAnswerRequest("Hola")));
        }

        [Fact]
        public async Task Answer_WhenQuestionBelongsToAnotherZone_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync("otra-zona", _question.Id, _luis, new CreateAnswerRequest("Hola")));
            Assert.Empty(_question.Answers);
        }
    }
}
