using ZonaMatch.Application.DTOs.Reviews.Responses;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Reviews
{
    // Maps domain reviews to API responses. The current user is null for anonymous requests.
    internal static class ReviewMapper
    {
        public static ReviewSummaryResponse Summarize(IReadOnlyList<Resena> reviews)
        {
            static double Average(IReadOnlyList<Resena> items, Func<Resena, double> score) =>
                items.Count == 0 ? 0 : Math.Round(items.Average(score), 1);

            return new ReviewSummaryResponse(
                Average(reviews, review => review.Puntaje),
                reviews.Count,
                reviews.Count(review => review.Verificada),
                [
                    new ReviewAspectRatingResponse(AspectoZona.Seguridad, Average(reviews, review => review.PuntajeSeguridad)),
                    new ReviewAspectRatingResponse(AspectoZona.Transporte, Average(reviews, review => review.PuntajeTransporte)),
                    new ReviewAspectRatingResponse(AspectoZona.Conectividad, Average(reviews, review => review.PuntajeConectividad)),
                    new ReviewAspectRatingResponse(AspectoZona.Comercios, Average(reviews, review => review.PuntajeComercios)),
                    new ReviewAspectRatingResponse(AspectoZona.EspaciosVerdes, Average(reviews, review => review.PuntajeEspaciosVerdes)),
                ]);
        }

        public static ReviewResponse ToResponse(Resena review, string? authorEmail, Guid? currentUserId) =>
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
