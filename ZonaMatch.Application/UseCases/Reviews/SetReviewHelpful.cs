using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Reviews
{
    // Marks or unmarks another user's review as useful.
    public class SetReviewHelpful
    {
        private readonly IResenaRepository _reviews;
        private readonly TimeProvider _clock;

        public SetReviewHelpful(IResenaRepository reviews, TimeProvider clock)
        {
            _reviews = reviews;
            _clock = clock;
        }

        public async Task ExecuteAsync(
            string zoneSlug, Guid reviewId, Guid userId, bool helpful, CancellationToken cancellationToken = default)
        {
            var review = await _reviews.ObtenerAsync(zoneSlug, reviewId, cancellationToken)
                ?? throw new NotFoundException("La reseña no existe.");

            if (review.UsuarioId == userId)
                throw new ConflictException("No podés marcar como útil tu propia reseña.");

            var vote = review.VotosUtil.FirstOrDefault(item => item.UsuarioId == userId);

            // Idempotent: repeating either operation does not change the result.
            if (helpful && vote is null)
                review.VotosUtil.Add(new ResenaVotoUtil { ResenaId = review.Id, UsuarioId = userId, Fecha = _clock.GetUtcNow() });
            else if (!helpful && vote is not null)
                review.VotosUtil.Remove(vote);
            else
                return;

            await _reviews.GuardarCambiosAsync(cancellationToken);
        }
    }
}
