using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class AporteRepository : IAporteRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public AporteRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(int Aprobados, int Pendientes)> ContarAsync(string zonaSlug, CancellationToken cancellationToken = default)
        {
            var cantidades = await _dbContext.Aportes
                .Where(a => a.ZonaSlug == zonaSlug && a.Estado != EstadoAporte.Rechazado)
                .GroupBy(a => a.Estado)
                .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                .ToListAsync(cancellationToken);

            int Cantidad(EstadoAporte estado) => cantidades.FirstOrDefault(c => c.Estado == estado)?.Cantidad ?? 0;

            return (Cantidad(EstadoAporte.Aprobado), Cantidad(EstadoAporte.Pendiente));
        }

        public async Task<IReadOnlyList<Aporte>> ListarPendientesAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Aportes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Usuario)
                .Include(a => a.Validaciones)
                .Where(a => a.ZonaSlug == zonaSlug && a.Estado == EstadoAporte.Pendiente)
                .OrderBy(a => a.FechaCreacion)
                .ToListAsync(cancellationToken);

        public Task<Aporte?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Aportes
                .Include(a => a.Usuario)
                .Include(a => a.Validaciones)
                .FirstOrDefaultAsync(a => a.ZonaSlug == zonaSlug && a.Id == id, cancellationToken);

        public void Agregar(Aporte aporte) => _dbContext.Aportes.Add(aporte);

        public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // The same user voted twice at the same time
                throw new OperacionNoPermitidaException("Tu voto ya se habia registrado. Actualiza la pagina.");
            }
        }
    }
}
