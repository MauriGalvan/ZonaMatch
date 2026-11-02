using ZonaMatch.Application.DTOs.Reviews.Responses;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Reviews
{
    // Maps domain reviews to API responses. The current user is null for anonymous requests.
    internal static class ReviewMapper
    {
        public static ReviewSummaryResponse Summarize(IReadOnlyList<Review> reviews)
        {
            static double Average(IReadOnlyList<Review> items, Func<Review, double> score) =>
                items.Count == 0 ? 0 : Math.Round(items.Average(score), 1);

            return new ReviewSummaryResponse(
                Average(reviews, review => review.Puntaje),
                reviews.Count,
                reviews.Count(review => review.Verificada),
                [
                    new ReviewAspectRatingResponse(ReviewAspect.Safety, Average(reviews, review => review.PuntajeSeguridad)),
                    new ReviewAspectRatingResponse(ReviewAspect.PublicTransport, Average(reviews, review => review.PuntajeTransporte)),
                    new ReviewAspectRatingResponse(ReviewAspect.Connectivity, Average(reviews, review => review.PuntajeConectividad)),
                    new ReviewAspectRatingResponse(ReviewAspect.Shopping, Average(reviews, review => review.PuntajeComercios)),
                    new ReviewAspectRatingResponse(ReviewAspect.GreenSpaces, Average(reviews, review => review.PuntajeEspaciosVerdes)),
                ]);
        }

        public static ReviewResponse ToResponse(Review review, string? authorEmail, Guid? currentUserId) =>
            new(
                review.Id,
                Autor.Iniciales(authorEmail),
                review.AniosEnZona,
                review.Verificada,
                review.Puntaje,
                new ReviewAspectScoresResponse(
                    review.PuntajeSeguridad,
                    review.PuntajeTransporte,
                    review.PuntajeConectividad,
                    review.PuntajeComercios,
                    review.PuntajeEspaciosVerdes),
                review.Temas,
                review.Texto,
                review.FechaCreacion,
                review.VotosUtil.Count,
                currentUserId is not null && review.VotosUtil.Any(vote => vote.UsuarioId == currentUserId),
                review.UsuarioId == currentUserId);
    }
}
