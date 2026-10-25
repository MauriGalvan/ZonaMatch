using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Contributions.Requests;

// PUT /api/v1/zones/{slug}/contributions/{id}/resolution
public sealed record ResolveContributionRequest(
    [Required(ErrorMessage = "Indicá si el aporte se aprueba o se rechaza.")]
    bool? Approve);
