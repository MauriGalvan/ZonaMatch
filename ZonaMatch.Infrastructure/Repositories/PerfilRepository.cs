using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class PerfilRepository : IPerfilRepository
    {
        private readonly ZonaMatchDbContext _context;

        public PerfilRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<Contexto?> ObtenerDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            await _context.Contextos
                .Include(c => c.Integrantes)
                .Include(c => c.Puntos)
                .Include(c => c.Criterios)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Tipo == TipoContexto.Perfil, cancellationToken);

        public void Agregar(Contexto perfil) => _context.Contextos.Add(perfil);

        public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Dos pedidos crearon al mismo tiempo el perfil del usuario (indice unico por usuario)
                throw new OperacionNoPermitidaException("Tu perfil ya se había guardado. Actualizá la página.");
            }
        }
    }
}
