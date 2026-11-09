using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Review.Requests;

public sealed record SaveReviewRequest(
    [Required(ErrorMessage = "Indicá el puntaje de cada aspecto.")]
    ReviewAspectScoresRequest? Aspects,
    [Required(ErrorMessage = "El texto de la reseña es obligatorio.")]
    [StringLength(TextLimits.ReviewText, MinimumLength = TextLimits.ReviewTextMinimum,
        ErrorMessage = "La reseña debe tener entre 20 y 1000 caracteres.")]
    string Text,
    [Range(0, 100, ErrorMessage = "Los años en la zona deben estar entre 0 y 100.")]
    int? YearsInZone,
    IReadOnlyList<string>? Topics,
    // Names returned by POST /api/v1/images; the list replaces the review's current images.
    IReadOnlyList<string>? ImageNames = null);
