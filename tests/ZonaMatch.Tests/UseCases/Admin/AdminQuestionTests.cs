using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Tests.UseCases.Community;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;

namespace ZonaMatch.Tests.UseCases.Admin;

// ListAdminQuestions, DeleteAdminQuestion and DeleteAdminAnswer: following and deleting community questions and answers.
public class AdminQuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    private readonly User _author = new() { Email = "ana@example.com" };
    private readonly User _neighbor = new() { Email = "vecino@example.com" };
    private readonly FakeQuestionRepository _questions = new();

    private DomainQuestion AddQuestion(string text, DateTimeOffset createdAt, string zoneSlug = "palermo")
    {
        var question = new DomainQuestion { ZoneSlug = zoneSlug, UserId = _author.Id, User = _author, Text = text, CreatedAt = createdAt };
        _questions.Add(question);
        return question;
    }

    private ListAdminQuestions ListUseCase() =>
        new(_questions, new FakeAdminZoneProfilesRepository(new ZoneProfile { Slug = "palermo", Name = "Palermo" }));

    [Fact]
    public async Task ListQuestions_ReturnsTheNewestFirstWithTheirZoneAndAuthor()
    {
        AddQuestion("¿Es tranquila de noche?", Now.AddDays(-2));
        AddQuestion("¿Hay plazas?", Now.AddDays(-1), zoneSlug: "zona-sin-datos");

        var questions = await ListUseCase().ExecuteAsync();

        questions.Select(question => question.Text).Should().Equal("¿Hay plazas?", "¿Es tranquila de noche?");
        questions[0].ZoneName.Should().Be("zona-sin-datos");
        questions[1].ZoneName.Should().Be("Palermo");
        questions[1].AuthorEmail.Should().Be("ana@example.com");
    }

    [Fact]
    public async Task ListQuestions_ReturnsTheAnswersOldestFirst()
    {
        var question = AddQuestion("¿Hay plazas?", Now.AddDays(-3));
        question.Answers.Add(new Answer { QuestionId = question.Id, User = _neighbor, Text = "Segunda", CreatedAt = Now });
        question.Answers.Add(new Answer { QuestionId = question.Id, User = _neighbor, Text = "Primera", CreatedAt = Now.AddDays(-1) });

        var answers = (await ListUseCase().ExecuteAsync()).Single().Answers;

        answers.Select(answer => answer.Text).Should().Equal("Primera", "Segunda");
        answers[0].AuthorEmail.Should().Be("vecino@example.com");
    }

    [Fact]
    public async Task DeleteQuestion_RemovesIt()
    {
        var question = AddQuestion("Publicidad", Now);

        await new DeleteAdminQuestion(_questions, NullLogger<DeleteAdminQuestion>.Instance).ExecuteAsync(question.Id, Guid.NewGuid());

        _questions.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteQuestion_WhenItDoesNotExist_ThrowsNotFound()
    {
        var act = () => new DeleteAdminQuestion(_questions, NullLogger<DeleteAdminQuestion>.Instance).ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAnswer_RemovesOnlyThatAnswer()
    {
        var question = AddQuestion("¿Hay plazas?", Now);
        var spam = new Answer { QuestionId = question.Id, User = _neighbor, Text = "Publicidad", CreatedAt = Now };
        var useful = new Answer { QuestionId = question.Id, User = _neighbor, Text = "Sí, dos", CreatedAt = Now };
        question.Answers.AddRange([spam, useful]);

        await new DeleteAdminAnswer(_questions, NullLogger<DeleteAdminAnswer>.Instance).ExecuteAsync(question.Id, spam.Id, Guid.NewGuid());

        _questions.Questions.Should().ContainSingle();
        question.Answers.Should().ContainSingle().Which.Text.Should().Be("Sí, dos");
    }

    [Fact]
    public async Task DeleteAnswer_WhenItBelongsToAnotherQuestion_ThrowsNotFound()
    {
        var question = AddQuestion("¿Hay plazas?", Now);
        var other = AddQuestion("¿Hay colectivos?", Now);
        var answer = new Answer { QuestionId = other.Id, User = _neighbor, Text = "Varios", CreatedAt = Now };
        other.Answers.Add(answer);

        var act = () => new DeleteAdminAnswer(_questions, NullLogger<DeleteAdminAnswer>.Instance).ExecuteAsync(question.Id, answer.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
        other.Answers.Should().ContainSingle();
    }
}
