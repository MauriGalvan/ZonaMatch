using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Review.Requests;

// PUT /api/v1/zones/{slug}/reviews/{id}/resolution. The reason is required when the review is rejected.
public sealed record ResolveReviewRequest(
    [Required(ErrorMessage = "Indicá si la reseña se aprueba o se rechaza.")]
    bool? Approve,
    [StringLength(TextLimits.ReviewRejectionReason, ErrorMessage = "El motivo no puede superar los 300 caracteres.")]
    string? Reason = null);
