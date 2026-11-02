namespace ZonaMatch.Application.DTOs.Analysis.Requests;

// Household information shared by the profile and analysis criteria workflows.
public sealed class AnalysisContextRequest
{
    public int PeopleCount { get; set; } = 1;

    // Names are optional for older clients; missing names receive stable defaults.
    public List<string?> PersonNames { get; set; } = [];

    // Age range for each person, in order; fewer values than people are allowed.
    public List<string?> AgeRanges { get; set; } = [];

    // Mobility options are stored as provided.
    public List<string> Mobility { get; set; } = [];

    public List<string> Pets { get; set; } = [];
}
