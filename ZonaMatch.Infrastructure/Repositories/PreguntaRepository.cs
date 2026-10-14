using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class PreguntaRepository : IPreguntaRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public PreguntaRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<Pregunta>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Preguntas
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Usuario)
                .Include(p => p.Respuestas).ThenInclude(r => r.Usuario)
                .Where(p => p.ZonaSlug == zonaSlug)
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync(cancellationToken);

        public Task<bool> ExisteAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Preguntas.AnyAsync(p => p.ZonaSlug == zonaSlug && p.Id == id, cancellationToken);

        public void Agregar(Pregunta pregunta) => _dbContext.Preguntas.Add(pregunta);

        public void Agregar(Respuesta respuesta) => _dbContext.Respuestas.Add(respuesta);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
            _dbContext.SaveChangesAsync(cancellationToken);
    }
}
