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

        // El analisis se lee siempre con sus zonas y su criterio completo (integrantes, lugares y
        // criterios). Split query: varias colecciones en una sola consulta multiplicarian las filas.
        private IQueryable<Analisis> ConDetalle =>
            _context.Analisis
                .Include(a => a.Zonas)
                .Include(a => a.Contexto).ThenInclude(c => c.Integrantes)
                .Include(a => a.Contexto).ThenInclude(c => c.Puntos)
                .Include(a => a.Contexto).ThenInclude(c => c.Criterios)
                .AsSplitQuery();

        public async Task<IReadOnlyList<Analisis>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            await ConDetalle
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.FechaActualizacion)
                .ToListAsync(cancellationToken);

        public async Task<Analisis?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
            await ConDetalle.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId, cancellationToken);

        public async Task<IReadOnlyList<Analisis>> ListarVersionesAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default) =>
            await ConDetalle
                .Where(a => a.AnalisisOrigenId == analisisOrigenId)
                .OrderBy(a => a.FechaCreacion)
                .ToListAsync(cancellationToken);

        public void Agregar(Analisis analisis) => _context.Analisis.Add(analisis);

        public void Eliminar(Analisis analisis)
        {
            _context.Analisis.Remove(analisis);
            // La copia del criterio es solo de este analisis
            _context.Contextos.Remove(analisis.Contexto);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
