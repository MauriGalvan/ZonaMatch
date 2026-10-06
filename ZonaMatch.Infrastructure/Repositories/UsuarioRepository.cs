using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public UsuarioRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default) =>
            _dbContext.Usuarios.AnyAsync(u => u.Email == email, cancellationToken);

        public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default) =>
            _dbContext.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
        {
            _dbContext.Usuarios.Add(usuario);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two requests with the same email passed ExisteEmailAsync at the same time
                throw new EmailYaRegistradoException();
            }
        }
    }
}
