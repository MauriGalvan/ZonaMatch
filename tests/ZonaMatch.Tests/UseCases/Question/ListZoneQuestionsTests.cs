using ZonaMatch.Application.UseCases.Question;
using ZonaMatch.Domain.Entities;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;

namespace ZonaMatch.Tests.UseCases.Question
{
    public class ListZoneQuestionsTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeQuestionRepository _questions = new();
        private readonly ListZoneQuestions _useCase;
        private readonly User _ana = new() { Email = "ana@example.com" };
        private readonly User _luis = new() { Email = "luis.gomez@example.com" };

        public ListZoneQuestionsTests()
        {
            _useCase = new ListZoneQuestions(_questions);
        }

        private Answer Answer(DomainQuestion question, string text, DateTimeOffset createdAt) =>
            new() { QuestionId = question.Id, UserId = _luis.Id, User = _luis, Text = text, CreatedAt = createdAt };

        [Fact]
        public async Task List_IndicatesOwnershipAndOrdersAnswersFromOldest()
        {
            var question = new DomainQuestion { ZoneSlug = Zone, UserId = _ana.Id, User = _ana, Text = "¿Hay verdulerias?", CreatedAt = CurrentTime };
            _questions.Add(question);
            _questions.Add(Answer(question, "Segunda", CurrentTime.AddHours(2)));
            _questions.Add(Answer(question, "Primera", CurrentTime.AddHours(1)));
            _questions.Add(new DomainQuestion { ZoneSlug = "otra-zona", UserId = _ana.Id, Text = "¿Y aca?" });

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
            _questions.Add(new DomainQuestion { ZoneSlug = Zone, UserId = _ana.Id, User = _ana, Text = "¿Hay verdulerias?" });

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, currentUserId: null));

            Assert.False(item.IsMine);
        }
    }
}
