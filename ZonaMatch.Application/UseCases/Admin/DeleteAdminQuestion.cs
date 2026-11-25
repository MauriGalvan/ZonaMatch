using Microsoft.Extensions.Logging;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Admin;

// An administrator deletes an inappropriate question; its answers are deleted with it (database cascade). A copy of the
// question stays in the history of deleted questions, with who deleted it and when.
public sealed class DeleteAdminQuestion(
    IQuestionRepository questions, IUserRepository users, TimeProvider clock, ILogger<DeleteAdminQuestion> logger)
{
    public async Task ExecuteAsync(Guid questionId, Guid administratorId, CancellationToken cancellationToken = default)
    {
        var question = await questions.GetAsync(questionId, cancellationToken)
            ?? throw new NotFoundException("La pregunta no existe o ya se eliminó.");
        var administrator = await users.GetByIdAsync(administratorId, cancellationToken);

        questions.Add(new DeletedQuestion
        {
            QuestionId = question.Id,
            ZoneSlug = question.ZoneSlug,
            Text = question.Text,
            AuthorEmail = question.User?.Email ?? string.Empty,
            AskedAt = question.CreatedAt,
            AnswerCount = question.Answers.Count,
            DeletedAt = clock.GetUtcNow(),
            DeletedByEmail = administrator?.Email ?? string.Empty,
        });
        questions.Remove(question);
        // One save: the copy is stored only if the question is really deleted.
        await questions.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Administrator {AdministratorId} deleted question {QuestionId}", administratorId, questionId);
    }
}
