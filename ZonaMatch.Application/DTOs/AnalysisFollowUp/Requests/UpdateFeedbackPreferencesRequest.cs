using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.AnalysisFollowUp.Requests;

public sealed class UpdateFeedbackPreferencesRequest
{
    [Required]
    public bool? SurveysEnabled { get; set; }
}
