using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Review
{
    // Marks or unmarks another user's review as useful.
    public class SetReviewHelpful
    {
        private readonly IReviewRepository _reviews;
        private readonly TimeProvider _clock;

        public SetReviewHelpful(IReviewRepository reviews, TimeProvider clock)
        {
            _reviews = reviews;
            _clock = clock;
        }

        public async Task ExecuteAsync(
            string zoneSlug, Guid reviewId, Guid userId, bool helpful, CancellationToken cancellationToken = default)
        {
            // Other users only see approved reviews, so for them any other review does not exist.
            var review = await _reviews.GetAsync(zoneSlug, reviewId, cancellationToken);
            if (review is not { Status: ReviewStatus.Approved })
                throw new NotFoundException("La reseña no existe.");

            if (review.UserId == userId)
                throw new ConflictException("No podés marcar como útil tu propia reseña.");

            var vote = review.HelpfulVotes.FirstOrDefault(item => item.UserId == userId);

            // Idempotent: repeating either operation does not change the result.
            if (helpful && vote is null)
                review.HelpfulVotes.Add(new ReviewHelpfulVote { ReviewId = review.Id, UserId = userId, CreatedAt = _clock.GetUtcNow() });
            else if (!helpful && vote is not null)
                review.HelpfulVotes.Remove(vote);
            else
                return;

            await _reviews.SaveChangesAsync(cancellationToken);
        }
    }
}
