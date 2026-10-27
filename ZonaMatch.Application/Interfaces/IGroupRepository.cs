using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IGroupRepository
{
    // Persists a group.
    Task AddAsync(Group group, CancellationToken cancellationToken);
    Task DeleteAsync(Group group, CancellationToken cancellationToken);
    Task<Group?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken);
    // Loads the group details with its participants and related users.
    Task<Group?> GetDetailsByIdAsync(Guid groupId, CancellationToken cancellationToken);
    Task<IEnumerable<Group>> GetByParticipantUserIdAsync(Guid participantUserId, CancellationToken cancellationToken);
    Task UpdateAsync(Group group, CancellationToken cancellationToken);
    Task<Group?> GetByInviteTokenAsync(Guid inviteToken, CancellationToken cancellationToken);
    Task AddParticipantAsync(Participant participant, CancellationToken cancellationToken);
    Task RemoveParticipantAsync(Participant participantToRemove, CancellationToken cancellationToken);
}
