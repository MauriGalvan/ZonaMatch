using ZonaMatch.Application.UseCases.Questions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Questions
{
    public class ListZoneQuestionsTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _questions = new();
        private readonly ListZoneQuestions _useCase;
        private readonly User _ana = new() { Email = "ana@example.com" };
        private readonly User _luis = new() { Email = "luis.gomez@example.com" };

        public ListZoneQuestionsTests()
        {
            _useCase = new ListZoneQuestions(_questions);
        }

        private Answer Answer(Question question, string text, DateTimeOffset createdAt) =>
            new() { PreguntaId = question.Id, UsuarioId = _luis.Id, User = _luis, Texto = text, FechaCreacion = createdAt };

        [Fact]
        public async Task List_IndicatesOwnershipAndOrdersAnswersFromOldest()
        {
            var question = new Question { ZonaSlug = Zone, UsuarioId = _ana.Id, User = _ana, Texto = "¿Hay verdulerias?", FechaCreacion = CurrentTime };
            _questions.Add(question);
            _questions.Add(Answer(question, "Segunda", CurrentTime.AddHours(2)));
            _questions.Add(Answer(question, "Primera", CurrentTime.AddHours(1)));
            _questions.Add(new Question { ZonaSlug = "otra-zona", UsuarioId = _ana.Id, Texto = "¿Y aca?" });

            var list = await _useCase.ExecuteAsync(Zone, _ana.Id);

            var item = Assert.Single(list);
            Assert.True(item.IsMine);
            Assert.Equal("AN", item.Initials);
            Assert.Equal(["Primera", "Segunda"], item.Answers.Select(answer => answer.Text));
            Assert.All(item.Answers, answer =>
            {
                Assert.False(answer.IsMine);
                Assert.Equal("LG", answer.Initials);
            });
        }

        [Fact]
        public async Task List_WithoutSession_NoItemsAreOwnedByCurrentUser()
        {
            _questions.Add(new Question { ZonaSlug = Zone, UsuarioId = _ana.Id, User = _ana, Texto = "¿Hay verdulerias?" });

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, currentUserId: null));

            Assert.False(item.IsMine);
        }
    }
}
