using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class AnalysisRepository : IAnalysisRepository
    {
        private readonly ZonaMatchDbContext _context;

        public AnalysisRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        // Always load an analysis with its zones and complete criteria (members, places, and
        // preferences). Split queries avoid multiplying rows when loading several collections at once.
        private IQueryable<Analysis> ConDetalle =>
            _context.Analysis
                .Include(a => a.Zonas)
                .Include(a => a.Context).ThenInclude(c => c.Integrantes)
                .Include(a => a.Context).ThenInclude(c => c.Puntos)
                .Include(a => a.Context).ThenInclude(c => c.Criterios)
                .AsSplitQuery();

        public async Task<IReadOnlyList<Analysis>> GetForUserAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            await ConDetalle
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.FechaActualizacion)
                .ToListAsync(cancellationToken);

        public async Task<Analysis?> GetAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
            await ConDetalle.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId, cancellationToken);

        public async Task<IReadOnlyList<Analysis>> ListVersionsAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default) =>
            await ConDetalle
                .Where(a => a.AnalisisOrigenId == analisisOrigenId)
                .OrderBy(a => a.FechaCreacion)
                .ToListAsync(cancellationToken);

        public void Add(Analysis analisis) => _context.Analysis.Add(analisis);

        public void Delete(Analysis analisis)
        {
            _context.Analysis.Remove(analisis);
            // The criteria snapshot belongs only to this analysis.
            _context.Contextos.Remove(analisis.Context);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
