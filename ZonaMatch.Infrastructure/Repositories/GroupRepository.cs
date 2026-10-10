using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IEnumerable<Group>> GetByUserCreatorIdAsync(Guid userCreatorId, CancellationToken cancellationToken)
    {
        // Vamos a la tabla Groups
        return await _context.Groups
            // Filtramos: "Traeme los grupos donde exista AL MENOS UN participante..."
            .Where(g => g.Participants.Any(p =>
                p.UserId == userCreatorId &&
                p.Role == "Propietario")) // <-- Opcional: si solo quieres los que él creó
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
            .FirstOrDefaultAsync(g => g.InviteToken == inviteToken, cancellationToken);
    }
}
