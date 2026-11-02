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
        private IQueryable<Analysis> ConDetalle =>
            _context.Analysis
                .Include(a => a.Zonas)
                .Include(a => a.Context).ThenInclude(c => c.Integrantes)
                .Include(a => a.Context).ThenInclude(c => c.Puntos)
                .Include(a => a.Context).ThenInclude(c => c.Criterios)
                .AsSplitQuery();

        public async Task<IReadOnlyList<Analysis>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            await ConDetalle
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.FechaActualizacion)
                .ToListAsync(cancellationToken);

        public async Task<Analysis?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
            await ConDetalle.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId, cancellationToken);

        public async Task<IReadOnlyList<Analysis>> ListarVersionesAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default) =>
            await ConDetalle
                .Where(a => a.AnalisisOrigenId == analisisOrigenId)
                .OrderBy(a => a.FechaCreacion)
                .ToListAsync(cancellationToken);

        public void Agregar(Analysis analisis) => _context.Analysis.Add(analisis);

        public void Eliminar(Analysis analisis)
        {
            _context.Analysis.Remove(analisis);
            // La copia del criterio es solo de este analisis
            _context.Contextos.Remove(analisis.Context);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
