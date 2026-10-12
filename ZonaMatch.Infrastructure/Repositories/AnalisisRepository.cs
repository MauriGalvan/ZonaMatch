using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class AnalisisRepository : IAnalisisRepository
    {
        private readonly ZonaMatchDbContext _context;

        public AnalisisRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AnalisisDetallado>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.AnalisisDetallado
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.FechaActualizacion)
                .ToListAsync(cancellationToken);
        }

        public async Task<AnalisisDetallado?> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.AnalisisDetallado
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId, cancellationToken);
        }

        public async Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.AnalisisDetallado
                .CountAsync(a => a.UsuarioId == usuarioId, cancellationToken);
        }

        public async Task AddAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default)
        {
            await _context.AnalisisDetallado.AddAsync(analisis, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default)
        {
            analisis.FechaActualizacion = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default)
        {
            _context.AnalisisDetallado.Remove(analisis);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
