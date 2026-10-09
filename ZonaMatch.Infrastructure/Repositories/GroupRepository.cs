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
                p.UsuarioId == userCreatorId &&
                p.Role == "Propietario")) // <-- Opcional: si solo quieres los que él creó
            .ToListAsync(cancellationToken);
    }
}
