using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Contribution.Requests;

public sealed record VoteOnContributionRequest(
    [Required(ErrorMessage = "Indica si el aporte es correcto.")]
    bool? Confirm);
