using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Questions
{
    internal static class QuestionMapper
    {
        public static AnswerResponse ToResponse(Answer answer, string? authorEmail, Guid? currentUserId) =>
            new(answer.Id, answer.Text, AuthorInitials.GetInitials(authorEmail), answer.CreatedAt, answer.UserId == currentUserId);
    }
}
