using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Reviews.Requests;

public sealed record SaveReviewRequest(
    [Required(ErrorMessage = "Indica el puntaje de cada aspecto.")]
    ReviewAspectScoresRequest? Aspects,
    [Required(ErrorMessage = "El texto de la reseña es obligatorio.")]
    [StringLength(Longitudes.TextoResena, MinimumLength = Longitudes.TextoResenaMinimo,
        ErrorMessage = "La reseña debe tener entre 20 y 1000 caracteres.")]
    string Text,
    [Range(0, 100, ErrorMessage = "Los años en la zona deben estar entre 0 y 100.")]
    int? YearsInZone,
    IReadOnlyList<string>? Topics);
