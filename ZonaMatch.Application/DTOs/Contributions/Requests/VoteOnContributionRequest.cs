using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Contributions.Requests;

public sealed record VoteOnContributionRequest(
    [Required(ErrorMessage = "Indica si el aporte es correcto.")]
    bool? Confirm);
