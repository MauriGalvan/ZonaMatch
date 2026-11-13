using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Investor;

// Investor access as the API shows it: none, requested or granted.
public static class InvestorAccessStatus
{
    public const string None = "none";
    public const string Requested = "requested";
    public const string Granted = "granted";

    public static string Of(User user) => user.InvestorAccess switch
    {
        InvestorAccess.Granted => Granted,
        InvestorAccess.Requested => Requested,
        _ => None,
    };

    public static InvestorAccessResponse ToResponse(User user) =>
        new(Of(user), user.InvestorAccessRequestedAt, user.InvestorAccessGrantedAt);
}
