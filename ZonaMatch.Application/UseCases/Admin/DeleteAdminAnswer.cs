using Microsoft.Extensions.Logging;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// An administrator deletes an inappropriate answer; the question and its other answers stay.
public sealed class DeleteAdminAnswer(IQuestionRepository questions, ILogger<DeleteAdminAnswer> logger)
{
    public async Task ExecuteAsync(Guid questionId, Guid answerId, Guid administratorId, CancellationToken cancellationToken = default)
    {
        var answer = await questions.GetAnswerAsync(questionId, answerId, cancellationToken)
            ?? throw new NotFoundException("La respuesta no existe o ya se eliminó.");

        questions.Remove(answer);
        await questions.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Administrator {AdministratorId} deleted answer {AnswerId} of question {QuestionId}", administratorId, answerId, questionId);
    }
}
