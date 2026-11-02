using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IGroupInvitationRepository
{
    Task AddAsync(GroupInvitation invitation, CancellationToken cancellationToken);

    // Finds a private invitation by its email token.
    Task<GroupInvitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken);

    Task UpdateAsync(GroupInvitation invitation, CancellationToken cancellationToken);
}
