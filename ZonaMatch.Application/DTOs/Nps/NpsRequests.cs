using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Nps;

public sealed class SubmitNpsRequest
{
    [Required, Range(0, 10)]
    public int? Score { get; set; }
    [StringLength(2000)]
    public string? Comment { get; set; }
}

public sealed class AdminNpsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(0, 10)]
    public int? Score { get; set; }
    [Range(1, 1_000_000)]
    public int Page { get; set; } = 1;
}
