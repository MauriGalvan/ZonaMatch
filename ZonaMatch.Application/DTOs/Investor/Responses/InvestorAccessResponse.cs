namespace ZonaMatch.Application.DTOs.Investor.Responses;

// Investor profile access of the current user. Status: none, requested or granted (InvestorAccessStatus).
public sealed record InvestorAccessResponse(string Status, DateTimeOffset? RequestedAt, DateTimeOffset? GrantedAt);
