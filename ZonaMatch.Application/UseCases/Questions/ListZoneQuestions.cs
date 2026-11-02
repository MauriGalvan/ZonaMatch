using ZonaMatch.Application.DTOs.Questions.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Comunidad;

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
            var questions = await _questions.ListarPorZonaAsync(zoneSlug, cancellationToken);

            return questions
                .Select(question => new QuestionResponse(
                    question.Id,
                    question.Texto,
                    Autor.Iniciales(question.User?.Email),
                    question.FechaCreacion,
                    question.UsuarioId == currentUserId,
                    question.Respuestas
                        .OrderBy(answer => answer.FechaCreacion)
                        .Select(answer => QuestionMapper.ToResponse(answer, answer.User?.Email, currentUserId))
                        .ToList()))
                .ToList();
        }
    }
}
