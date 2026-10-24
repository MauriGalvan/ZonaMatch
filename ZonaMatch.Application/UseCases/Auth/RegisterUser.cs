using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Auth;

public sealed class RegisterUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<UserResponse> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // The DTO already marks it as [Required]; this keeps a direct call from ending in a NullReferenceException (500)
        if (request.BirthDate is not { } birthDate)
            throw new BadRequestException("La fecha de nacimiento es obligatoria.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
            throw new ConflictException("El email ya esta registrado.");

        var user = new Usuario
        {
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            FechaNacimiento = birthDate,
            FechaCreacion = timeProvider.GetUtcNow()
        };

        await users.AddAsync(user, cancellationToken);

        return new UserResponse(user.Id, user.Email, user.FechaNacimiento);
    }
}
