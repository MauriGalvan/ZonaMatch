using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public class GroupInvitationRepository : IGroupInvitationRepository
{
    private readonly ZonaMatchDbContext _context;

    public GroupInvitationRepository(ZonaMatchDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GroupInvitation invitation, CancellationToken cancellationToken)
    {
        await _context.GroupInvitations.AddAsync(invitation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<GroupInvitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken)
    {
        // Include the group so the frontend can display its name.
        return await _context.GroupInvitations
            .Include(i => i.Group)
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
    }

    public async Task UpdateAsync(GroupInvitation invitation, CancellationToken cancellationToken)
    {
        _context.GroupInvitations.Update(invitation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GroupInvitation>> ListPendingAsync(Guid groupId, DateTime now, CancellationToken cancellationToken)
    {
        return await _context.GroupInvitations
            .AsNoTracking()
            .Where(i => i.GroupId == groupId && i.Status == InvitationStatus.Pending && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }
}
