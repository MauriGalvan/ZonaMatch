using Microsoft.Extensions.Logging;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// An administrator deletes an inappropriate question; its answers are deleted with it (database cascade).
public sealed class DeleteAdminQuestion(IQuestionRepository questions, ILogger<DeleteAdminQuestion> logger)
{
    public async Task ExecuteAsync(Guid questionId, Guid administratorId, CancellationToken cancellationToken = default)
    {
        var question = await questions.GetAsync(questionId, cancellationToken)
            ?? throw new NotFoundException("La pregunta no existe o ya se eliminó.");

        questions.Remove(question);
        await questions.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Administrator {AdministratorId} deleted question {QuestionId}", administratorId, questionId);
    }
}
