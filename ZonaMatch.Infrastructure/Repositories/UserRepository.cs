using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public sealed class UserRepository(ZonaMatchDbContext dbContext) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Usuarios.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Usuarios.AsNoTracking().FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public async Task AddAsync(Usuario user, CancellationToken cancellationToken = default)
    {
        dbContext.Usuarios.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("El email ya esta registrado.");
        }
    }
}
