namespace ZonaMatch.Application.DTOs.Investor.Requests;

// Ranking for an investment analysis. Without settings it uses the saved investor profile; with them (an analysis
// adjusted on its own) the profile stays unchanged. Added zones join the automatic top, up to the maximum.
public sealed class InvestmentRankingRequest
{
    public InvestorProfileRequest? Settings { get; set; }

    public IReadOnlyList<string>? AddedZones { get; set; }
}
