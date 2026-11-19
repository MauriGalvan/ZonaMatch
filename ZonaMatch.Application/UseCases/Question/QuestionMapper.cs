using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Entities;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;

namespace ZonaMatch.Application.UseCases.Question
{
    internal static class QuestionMapper
    {
        public static QuestionResponse ToResponse(
            DomainQuestion question, CommunityAuthor author, Guid? currentUserId) =>
            new(
                question.Id,
                question.Text,
                author.Initials,
                question.CreatedAt,
                question.UserId == currentUserId,
                question.Answers
                    .OrderBy(answer => answer.CreatedAt)
                    .Select(answer => ToResponse(answer, CommunityAuthor.FromUser(answer.User), currentUserId))
                    .ToList(),
                author.Name);

        public static AnswerResponse ToResponse(Answer answer, CommunityAuthor author, Guid? currentUserId) =>
            new(answer.Id, answer.Text, author.Initials, answer.CreatedAt, answer.UserId == currentUserId, author.Name);
    }
}
