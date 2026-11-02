using ZonaMatch.Application.DTOs.Questions.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Questions;
using ZonaMatch.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ZonaMatch.Tests.UseCases.Questions
{
    public class AnswerQuestionTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _questions = new();
        private readonly AnswerQuestion _useCase;
        private readonly AuthenticatedUserContext _luis = new(Guid.NewGuid(), "luis.gomez@example.com");
        private readonly Pregunta _question = new() { ZonaSlug = Zone, UsuarioId = Guid.NewGuid(), Texto = "¿Hay verdulerias?" };

        public AnswerQuestionTests()
        {
            _useCase = new AnswerQuestion(
                _questions,
                new FakeNotificationRepository(),
                new FakeEmailService(),
                NullLogger<AnswerQuestion>.Instance,
                new FixedTimeProvider(CurrentTime),
                new FrontendLinks("http://localhost:5173/"));
            _questions.Agregar(_question);
        }

        [Fact]
        public async Task Answer_AddsAnswerToQuestion()
        {
            var created = await _useCase.ExecuteAsync(Zone, _question.Id, _luis, new CreateAnswerRequest("  Si, sobre Rivadavia. "));

            var saved = Assert.Single(_question.Respuestas);
            Assert.Equal("Si, sobre Rivadavia.", saved.Texto);
            Assert.Equal(_luis.Id, saved.UsuarioId);
            Assert.Equal(CurrentTime, saved.FechaCreacion);
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
            Assert.Empty(_question.Respuestas);
        }
    }
}
