using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.AnalysisFollowUp.Requests;

public sealed class SubmitAnalysisFollowUpRequest
{
    [Required, StringLength(32)]
    public string Outcome { get; set; } = string.Empty;

    [Required, Range(1, 5)]
    public int? Helpfulness { get; set; }

    [StringLength(AnalysisFollowUpRules.MaximumCommentLength)]
    public string? Comment { get; set; }
}
