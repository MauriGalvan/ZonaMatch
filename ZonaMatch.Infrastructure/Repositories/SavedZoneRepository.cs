using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class SavedZoneRepository : ISavedZoneRepository
    {
        private readonly ZonaMatchDbContext _context;

        public SavedZoneRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SavedZone>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            await _context.SavedZones
                .AsNoTracking()
                .Where(zone => zone.UserId == userId)
                .OrderByDescending(zone => zone.SavedAt)
                .ToListAsync(cancellationToken);

        public Task<SavedZone?> GetAsync(Guid userId, string zoneSlug, CancellationToken cancellationToken = default) =>
            _context.SavedZones.FirstOrDefaultAsync(
                zone => zone.UserId == userId && zone.ZoneSlug == zoneSlug, cancellationToken);

        public async Task<IReadOnlyList<SavedZone>> GetForZoneAsync(
            string zoneSlug, Guid excludedUserId, CancellationToken cancellationToken = default) =>
            await _context.SavedZones
                .AsNoTracking()
                .Where(zone => zone.ZoneSlug == zoneSlug && zone.UserId != excludedUserId)
                .ToListAsync(cancellationToken);

        public void Add(SavedZone savedZone) => _context.SavedZones.Add(savedZone);

        public void Remove(SavedZone savedZone) => _context.SavedZones.Remove(savedZone);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
