using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly ZonaMatchDbContext _context;

        public ProfileRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<Context?> ObtenerDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            await _context.Contextos
                .Include(c => c.Integrantes)
                .Include(c => c.Puntos)
                .Include(c => c.Criterios)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Tipo == ContextType.Perfil, cancellationToken);

        public async Task<IReadOnlyList<Context>> ObtenerDeUsuariosAsync(
            IReadOnlyCollection<Guid> usuarioIds,
            CancellationToken cancellationToken = default)
        {
            if (usuarioIds.Count == 0)
                return [];

            return await _context.Contextos
                .AsNoTracking()
                .Where(c => usuarioIds.Contains(c.UsuarioId) && c.Tipo == ContextType.Perfil)
                .Include(c => c.Integrantes)
                .Include(c => c.Puntos)
                .Include(c => c.Criterios)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
        }

        public void Agregar(Context perfil) => _context.Contextos.Add(perfil);

        public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two requests created the user's profile at the same time (unique index per user).
                throw new ConflictException("Tu perfil ya se había guardado. Actualizá la página.");
            }
        }
    }
}
