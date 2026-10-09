using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IGroupRepository
{
    // Agrega el grupo a la base de datos
    Task AddAsync(Group group, CancellationToken cancellationToken);
    Task<IEnumerable<Group>> GetByUserCreatorIdAsync(Guid userCreatorId, CancellationToken cancellationToken);
}
