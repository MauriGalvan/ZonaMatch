using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ContributionRepository : IContributionRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public ContributionRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(int Aprobados, int Pendientes)> CountAsync(string zonaSlug, CancellationToken cancellationToken = default)
        {
            var cantidades = await _dbContext.Aportes
                .Where(a => a.ZonaSlug == zonaSlug && a.Estado != ContributionStatus.Rechazado)
                .GroupBy(a => a.Estado)
                .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                .ToListAsync(cancellationToken);

            int Cantidad(ContributionStatus estado) => cantidades.FirstOrDefault(c => c.Estado == estado)?.Cantidad ?? 0;

            return (Cantidad(ContributionStatus.Aprobado), Cantidad(ContributionStatus.Pendiente));
        }

        public async Task<IReadOnlyList<Contribution>> ListPendingAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Aportes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.User)
                .Include(a => a.Validaciones)
                .Where(a => a.ZonaSlug == zonaSlug && a.Estado == ContributionStatus.Pendiente)
                .OrderBy(a => a.FechaCreacion)
                .ToListAsync(cancellationToken);

        public Task<Contribution?> GetAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Aportes
                .Include(a => a.User)
                .Include(a => a.Validaciones)
                .FirstOrDefaultAsync(a => a.ZonaSlug == zonaSlug && a.Id == id, cancellationToken);

        public void Add(Contribution aporte) => _dbContext.Aportes.Add(aporte);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // The same user submitted two votes at the same time.
                throw new ConflictException("Tu voto ya se habia registrado. Actualiza la pagina.");
            }
        }
    }
}
