using ZonaMatch.Application.DTOs.Questions.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Comunidad;

namespace ZonaMatch.Application.UseCases.Questions
{
    // Lists a zone's questions from newest to oldest, with answers ordered from oldest to newest.
    public sealed class ListZoneQuestions
    {
        private readonly IPreguntaRepository _questions;

        public ListZoneQuestions(IPreguntaRepository questions)
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
                    Autor.Iniciales(question.Usuario?.Email),
                    question.FechaCreacion,
                    question.UsuarioId == currentUserId,
                    question.Respuestas
                        .OrderBy(answer => answer.FechaCreacion)
                        .Select(answer => QuestionMapper.ToResponse(answer, answer.Usuario?.Email, currentUserId))
                        .ToList()))
                .ToList();
        }
    }
}
