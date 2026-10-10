using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IGroupInvitationRepository
{
    Task AddAsync(GroupInvitation invitation, CancellationToken cancellationToken);

    // Busca la invitación privada usando el token del mail
    Task<GroupInvitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken);

    Task UpdateAsync(GroupInvitation invitation, CancellationToken cancellationToken);
}
