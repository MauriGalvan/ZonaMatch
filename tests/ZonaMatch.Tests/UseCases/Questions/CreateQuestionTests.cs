using ZonaMatch.Application.DTOs.Questions.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Questions;

namespace ZonaMatch.Tests.UseCases.Questions
{
    public class CreateQuestionTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _questions = new();
        private readonly CreateQuestion _useCase;
        private readonly AuthenticatedUserContext _ana = new(Guid.NewGuid(), "ana@example.com");

        public CreateQuestionTests()
        {
            _useCase = new CreateQuestion(
                _questions,
                new FakeZonaRepository(),
                new FakeSavedZoneRepository(),
                new FakeNotificationRepository(),
                new FixedTimeProvider(CurrentTime));
        }

        [Fact]
        public async Task Create_TrimsAndStoresQuestionText()
        {
            var created = await _useCase.ExecuteAsync(Zone, _ana, new CreateQuestionRequest("  ¿Hay verdulerias cerca de la estacion?  "));

            var saved = Assert.Single(_questions.Preguntas);
            Assert.Equal("¿Hay verdulerias cerca de la estacion?", saved.Texto);
            Assert.Equal(_ana.Id, saved.UsuarioId);
            Assert.Equal(CurrentTime, saved.FechaCreacion);
            Assert.Equal(saved.Id, created.Id);
            Assert.Equal("AN", created.Initials);
            Assert.True(created.IsMine);
            Assert.Empty(created.Answers);
        }

        [Fact]
        public async Task Create_WhenZoneDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync("no-existe", _ana, new CreateQuestionRequest("¿Como es el barrio de noche?")));
            Assert.Empty(_questions.Preguntas);
        }
    }
}
