using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public QuestionRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<Question>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Preguntas
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.User)
                .Include(p => p.Respuestas).ThenInclude(r => r.User)
                .Where(p => p.ZonaSlug == zonaSlug)
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync(cancellationToken);

        public Task<bool> ExisteAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Preguntas.AnyAsync(p => p.ZonaSlug == zonaSlug && p.Id == id, cancellationToken);

        public Task<Question?> GetByIdAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Preguntas
                .Include(question => question.User)
                .FirstOrDefaultAsync(question => question.ZonaSlug == zonaSlug && question.Id == id, cancellationToken);

        public void Agregar(Question pregunta) => _dbContext.Preguntas.Add(pregunta);

        public void Agregar(Answer respuesta) => _dbContext.Respuestas.Add(respuesta);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
            _dbContext.SaveChangesAsync(cancellationToken);
    }
}
