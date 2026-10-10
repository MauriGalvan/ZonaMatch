using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IGroupRepository
{
    // Agrega el grupo a la base de datos
    Task AddAsync(Group group, CancellationToken cancellationToken);
    Task DeleteAsync(Group group, CancellationToken cancellationToken);
    // Necesitamos un método para buscar un solo grupo por ID
    Task<Group?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken);
    Task<IEnumerable<Group>> GetByUserCreatorIdAsync(Guid userCreatorId, CancellationToken cancellationToken);
    Task UpdateAsync(Group group, CancellationToken cancellationToken);
}
