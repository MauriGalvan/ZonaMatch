using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

internal class GroupRepository : IGroupRepository
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
}
