using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// Lists every registered user for the admin panel, ordered by email.
public sealed class ListUsers(IUserRepository users)
{
    public async Task<IReadOnlyList<AdminUserResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var registeredUsers = await users.ListAsync(cancellationToken);

        return registeredUsers
            .Select(user => new AdminUserResponse(user.Id, user.Email, user.DateOfBirth, user.IsAdministrator))
            .ToList();
    }
}
