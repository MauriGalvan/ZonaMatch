namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record AnalysisContextResponse(
    int PeopleCount,
    IReadOnlyList<string> PersonNames,
    IReadOnlyList<string?> AgeRanges,
    IReadOnlyList<string> Mobility,
    IReadOnlyList<string> Pets);
