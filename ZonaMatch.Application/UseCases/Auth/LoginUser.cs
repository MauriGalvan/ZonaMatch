using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Auth;

public sealed class LoginUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    public async Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedException("Email o contrasena incorrectos.");

        var (accessToken, expiresAt) = tokenService.Generate(user);
        var userResponse = new UserResponse(user.Id, user.Email, user.FechaNacimiento);

        return new LoginResult(accessToken, expiresAt, userResponse);
    }
}
