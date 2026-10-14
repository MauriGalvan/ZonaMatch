using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ResenaRepository : IResenaRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public ResenaRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<Resena>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Resenas
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.Usuario)
                .Include(r => r.VotosUtil)
                .Where(r => r.ZonaSlug == zonaSlug)
                .OrderByDescending(r => r.FechaCreacion)
                .ToListAsync(cancellationToken);

        public Task<Resena?> ObtenerDeUsuarioAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default) =>
            _dbContext.Resenas
                .Include(r => r.VotosUtil)
                .FirstOrDefaultAsync(r => r.ZonaSlug == zonaSlug && r.UsuarioId == usuarioId, cancellationToken);

        public Task<Resena?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Resenas
                .Include(r => r.VotosUtil)
                .FirstOrDefaultAsync(r => r.ZonaSlug == zonaSlug && r.Id == id, cancellationToken);

        public void Agregar(Resena resena) => _dbContext.Resenas.Add(resena);

        public void Eliminar(Resena resena) => _dbContext.Resenas.Remove(resena);

        public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Dos pedidos crearon al mismo tiempo la resena del usuario en la zona, o marcaron dos veces
                // la misma resena como util
                throw new OperacionNoPermitidaException("La operacion ya se habia registrado. Actualiza la pagina.");
            }
        }
    }
}
