using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Admin.Requests;

public sealed class AdminSurveyQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Outcome { get; set; }
    [Range(1, 5)]
    public int? Helpfulness { get; set; }
    [Range(1, 1_000_000)]
    public int Page { get; set; } = 1;
}
