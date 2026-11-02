using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Community;

namespace ZonaMatch.Application.UseCases.Questions
{
    // Lists a zone's questions from newest to oldest, with answers ordered from oldest to newest.
    public sealed class ListZoneQuestions
    {
        private readonly IQuestionRepository _questions;

        public ListZoneQuestions(IQuestionRepository questions)
        {
            _questions = questions;
        }

        public async Task<IReadOnlyList<QuestionResponse>> ExecuteAsync(
            string zoneSlug, Guid? currentUserId, CancellationToken cancellationToken = default)
        {
            var questions = await _questions.ListByZoneAsync(zoneSlug, cancellationToken);

            return questions
                .Select(question => new QuestionResponse(
                    question.Id,
                    question.Text,
                    AuthorInitials.GetInitials(question.User?.Email),
                    question.CreatedAt,
                    question.UserId == currentUserId,
                    question.Answers
                        .OrderBy(answer => answer.CreatedAt)
                        .Select(answer => QuestionMapper.ToResponse(answer, answer.User?.Email, currentUserId))
                        .ToList()))
                .ToList();
        }
    }
}
