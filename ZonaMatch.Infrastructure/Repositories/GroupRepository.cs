using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly ZonaMatchDbContext _context;

    public GroupRepository(ZonaMatchDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Group group, CancellationToken cancellationToken)
    {
        // Usamos AddAsync de Entity Framework
        await _context.Groups.AddAsync(group, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<Group>> GetByParticipantUserIdAsync(Guid participantUserId, CancellationToken cancellationToken)
    {
        return await _context.Groups
            .AsNoTracking()
            .Where(group => group.Participants.Any(participant => participant.UserId == participantUserId))
            .Include(group => group.Participants)
            .ToListAsync(cancellationToken);
    }

    public async Task<Group?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken)
    {
        // Usamos Include(g => g.Participants) para traernos los participantes también
        // y así el Caso de Uso puede verificar si el usuario es el Propietario.
        return await _context.Groups
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
    }

    public async Task<Group?> GetDetailsByIdAsync(Guid groupId, CancellationToken cancellationToken)
    {
        return await _context.Groups
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
    }

    public async Task DeleteAsync(Group group, CancellationToken cancellationToken)
    {
        // Eliminamos el grupo del contexto
        _context.Groups.Remove(group);

        // Guardamos los cambios en PostgreSQL
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Group group, CancellationToken cancellationToken)
    {
        _context.Groups.Update(group);
        await _context.SaveChangesAsync(cancellationToken);
    }
    public async Task<Group?> GetByInviteTokenAsync(Guid inviteToken, CancellationToken cancellationToken)
    {
        return await _context.Groups
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.InviteToken == inviteToken, cancellationToken);
    }

    public async Task AddParticipantAsync(Participant participant, CancellationToken cancellationToken)
    {
        // Le decimos a EF Core explícitamente: "Esto es un INSERT, respeta su ID V7"
        await _context.Set<Participant>().AddAsync(participant, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveParticipantAsync(Participant participant, CancellationToken cancellationToken)
    {
        _context.Set<Participant>().Remove(participant);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
