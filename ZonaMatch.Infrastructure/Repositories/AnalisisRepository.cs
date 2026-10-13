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

        // El analisis se lee siempre con su detalle: integrantes, lugares y criterios
        // elegidos. Split query: dos colecciones en una sola consulta multiplicarian
        // las filas del cross join.
        private IQueryable<Contexto> ConDetalle =>
            _context.Contextos
                .Include(c => c.Integrantes)
                .Include(c => c.Puntos)
                .Include(c => c.Criterios)
                .AsSplitQuery();

        public async Task<IReadOnlyList<Contexto>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            return await ConDetalle
                .Where(c => c.UsuarioId == usuarioId)
                .OrderByDescending(c => c.FechaActualizacion)
                .ToListAsync(cancellationToken);
        }

        public async Task<Contexto?> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            return await ConDetalle
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId, cancellationToken);
        }

        public async Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Contextos
                .CountAsync(c => c.UsuarioId == usuarioId, cancellationToken);
        }

        public async Task AddAsync(Contexto contexto, CancellationToken cancellationToken = default)
        {
            await _context.Contextos.AddAsync(contexto, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Contexto contexto, CancellationToken cancellationToken = default)
        {
            contexto.FechaActualizacion = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Contexto contexto, CancellationToken cancellationToken = default)
        {
            _context.Contextos.Remove(contexto);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
