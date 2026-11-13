using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Investor;

// Investor profile access of the current user, read from the database so an approval applies at once.
public sealed class GetInvestorAccess(IUserRepository users)
{
    public async Task<InvestorAccessResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        return InvestorAccessStatus.ToResponse(user);
    }
}
